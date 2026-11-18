using MediaVacuum.Core.Interfaces;
using MediaVacuum.Core.Models;
using MediaVacuum.Core.Utilities;

namespace MediaVacuum.Core.Services;

/// <summary>
/// Analyses URLs (single video or playlist/channel), probes sizes and downloads
/// queue items. Item updates are marshalled to the UI synchronization context.
/// </summary>
public class DownloadQueueService
{
    private readonly IYtDlpService _ytDlp;
    private readonly SynchronizationContext? _sync;
    private readonly Dictionary<DownloadItem, CancellationTokenSource> _itemTokens = new();
    private CancellationTokenSource? _queueToken;

    public DownloadQueueService(IYtDlpService ytDlp)
    {
        _ytDlp = ytDlp;
        _sync = SynchronizationContext.Current;
    }

    public bool IsRunning { get; private set; }

    public event EventHandler<bool>? RunningChanged;

    public async Task<AnalysisResult> AnalyzeAsync(
        string url,
        DownloadOptions options,
        bool includePlaylist,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("A URL is required.", nameof(url));
        }

        AppLog.Info($"Analysing URL: {url}");
        var info = await _ytDlp
            .GetMediaInfoAsync(url, flatPlaylist: includePlaylist, noPlaylist: !includePlaylist, ct)
            .ConfigureAwait(false);

        if (info is null)
        {
            throw new InvalidOperationException("Unable to analyse this URL.");
        }

        if (info.IsPlaylist && (info.Entries is null || info.Entries.Count == 0))
        {
            throw new InvalidOperationException("This playlist is empty or unavailable.");
        }

        var items = new List<DownloadItem>();
        long estimatedTotal = 0;
        var hasEstimates = false;

        if (info.IsPlaylist && info.Entries is { Count: > 0 })
        {
            var playlistTitle = string.IsNullOrWhiteSpace(info.Title) ? "Playlist" : info.Title!;
            var index = 0;

            foreach (var entry in info.Entries)
            {
                if (entry is null)
                {
                    continue;
                }

                index++;
                var entryUrl = ResolveEntryUrl(entry, info, url);
                if (string.IsNullOrWhiteSpace(entryUrl))
                {
                    continue;
                }

                var item = new DownloadItem
                {
                    Id = entry.Id ?? string.Empty,
                    Title = string.IsNullOrWhiteSpace(entry.Title) ? $"Video {index}" : entry.Title!,
                    Url = entryUrl,
                    ThumbnailUrl = entry.GetBestThumbnailUrl() ?? info.GetBestThumbnailUrl(),
                    DurationSeconds = entry.Duration,
                    PlaylistIndex = index,
                    PlaylistTitle = playlistTitle,
                    PlaylistUrl = info.WebpageUrl,
                    Uploader = entry.Uploader ?? entry.Channel ?? info.Uploader ?? info.Channel
                };

                var estimate = SizeEstimator.EstimateFromDuration(entry.Duration, options);
                if (estimate.Bytes is > 0)
                {
                    item.TotalBytesEstimate = estimate.Bytes;
                    item.IsSizeEstimated = true;
                    estimatedTotal += estimate.Bytes.Value;
                    hasEstimates = true;
                }

                items.Add(item);
            }
        }
        else
        {
            var item = new DownloadItem
            {
                Id = info.Id ?? string.Empty,
                Title = string.IsNullOrWhiteSpace(info.Title) ? "Video" : info.Title!,
                Url = info.WebpageUrl ?? info.Url ?? url,
                ThumbnailUrl = info.GetBestThumbnailUrl(),
                DurationSeconds = info.Duration,
                Uploader = info.Uploader ?? info.Channel
            };

            var estimate = SizeEstimator.Estimate(info, options);
            if (estimate.Bytes is > 0)
            {
                if (estimate.IsApproximate)
                {
                    item.TotalBytesEstimate = estimate.Bytes;
                    item.IsSizeEstimated = true;
                    hasEstimates = true;
                }
                else
                {
                    item.TotalBytes = estimate.Bytes;
                }

                estimatedTotal += estimate.Bytes.Value;
            }

            items.Add(item);
        }

        AppLog.Info($"Analysis complete: {items.Count} item(s).");

        return new AnalysisResult(
            info.IsPlaylist,
            string.IsNullOrWhiteSpace(info.Title) ? url : info.Title!,
            items,
            estimatedTotal > 0 ? estimatedTotal : null,
            hasEstimates);
    }

    public async Task ProbeSizesAsync(
        IReadOnlyList<DownloadItem> items,
        DownloadOptions options,
        int maxConcurrency = 2,
        IProgress<DownloadItem>? progress = null,
        CancellationToken ct = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        using var semaphore = new SemaphoreSlim(Math.Clamp(maxConcurrency, 1, 4));

        var tasks = items.Select(async item =>
        {
            if (ct.IsCancellationRequested || string.IsNullOrWhiteSpace(item.Url))
            {
                return;
            }

            try
            {
                await semaphore.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                if (item.Status != DownloadItemStatus.Pending || item.IsFinished)
                {
                    return;
                }

                _ = PostAsync(() => item.Status = DownloadItemStatus.Analyzing);
                var info = await _ytDlp
                    .GetMediaInfoAsync(item.Url, flatPlaylist: false, noPlaylist: true, ct)
                    .ConfigureAwait(false);

                if (info is not null)
                {
                    var estimate = SizeEstimator.Estimate(info, options);
                    if (estimate.Bytes is > 0)
                    {
                        ApplyEstimate(item, estimate);
                        progress?.Report(item);
                    }

                    EstimateMissingDuration(item, info);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore, cancellation is expected when the queue changes.
            }
            catch (Exception ex)
            {
                AppLog.Exception($"Unable to probe size for {item.Url}", ex);
            }
            finally
            {
                if (item.Status == DownloadItemStatus.Analyzing)
                {
                    _ = PostAsync(() => item.Status = DownloadItemStatus.Pending);
                }

                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task StartAsync(
        IReadOnlyList<DownloadItem> items,
        DownloadOptions options,
        int concurrency,
        CancellationToken ct = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        _queueToken = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IsRunning = true;
        RunningChanged?.Invoke(this, true);
        AppLog.Info($"Starting queue: {items.Count} item(s), concurrency {concurrency}.");

        using var semaphore = new SemaphoreSlim(Math.Clamp(concurrency, 1, 8));

        try
        {
            var tasks = items.Select(async item =>
            {
                try
                {
                    await semaphore.WaitAsync(_queueToken.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    await ProcessItemAsync(item, options, _queueToken.Token).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        finally
        {
            IsRunning = false;
            RunningChanged?.Invoke(this, false);
            _queueToken?.Dispose();
            _queueToken = null;
            AppLog.Info("Queue finished.");
        }
    }

    public void CancelItem(DownloadItem item)
    {
        if (_itemTokens.TryGetValue(item, out var token))
        {
            try
            {
                token.Cancel();
            }
            catch
            {
                // Best effort.
            }
        }
    }

    public void CancelAll()
    {
        try
        {
            _queueToken?.Cancel();
        }
        catch
        {
            // Best effort.
        }

        lock (_itemTokens)
        {
            foreach (var token in _itemTokens.Values)
            {
                try
                {
                    token.Cancel();
                }
                catch
                {
                    // Best effort.
                }
            }
        }

        // Kill the process tree synchronously so closing the app cannot leave
        // a yt-dlp download running in the background.
        _ytDlp.KillActiveProcesses();
    }

    private async Task ProcessItemAsync(DownloadItem item, DownloadOptions options, CancellationToken queueToken)
    {
        if (!item.IsSelected
            || item.Status is DownloadItemStatus.Completed
                or DownloadItemStatus.Cancelled
                or DownloadItemStatus.Skipped)
        {
            return;
        }

        using var itemToken = CancellationTokenSource.CreateLinkedTokenSource(queueToken);
        lock (_itemTokens)
        {
            _itemTokens[item] = itemToken;
        }

        try
        {
            Post(() =>
            {
                item.Status = DownloadItemStatus.Downloading;
                item.ErrorMessage = null;
                item.Progress = 0;
                item.DownloadedBytes = 0;
            });

            var progress = new CallbackProgress<DownloadProgress>(p => ApplyProgress(item, p));
            var result = await _ytDlp
                .DownloadAsync(item, options, progress, itemToken.Token)
                .ConfigureAwait(false);

            Post(() =>
            {
                if (result.Success)
                {
                    item.Status = DownloadItemStatus.Completed;
                    item.Progress = 100;
                    item.FilePath = result.FilePath ?? item.FilePath;
                    item.FileSize = result.FileSize ?? item.FileSize;
                    item.ErrorMessage = null;

                    if (item.FileSize is > 0)
                    {
                        item.IsSizeEstimated = false;
                    }
                }
                else
                {
                    item.Status = DownloadItemStatus.Failed;
                    item.ErrorMessage = result.ErrorMessage;
                }
            });
        }
        catch (OperationCanceledException)
        {
            Post(() =>
            {
                item.Status = DownloadItemStatus.Cancelled;
                item.ErrorMessage = null;
            });
            AppLog.Info($"Download cancelled: {item.Url}");
        }
        catch (Exception ex)
        {
            Post(() =>
            {
                item.Status = DownloadItemStatus.Failed;
                item.ErrorMessage = ex.Message;
            });
            AppLog.Exception($"Download failed: {item.Url}", ex);
        }
        finally
        {
            lock (_itemTokens)
            {
                _itemTokens.Remove(item);
            }
        }
    }

    private static string? ResolveEntryUrl(MediaInfo entry, MediaInfo playlist, string originalUrl)
    {
        var candidate = entry.WebpageUrl ?? entry.Url;
        var baseUrl = playlist.WebpageUrl ?? originalUrl;

        if (!string.IsNullOrWhiteSpace(candidate))
        {
            if (Uri.TryCreate(candidate, UriKind.Absolute, out _))
            {
                return candidate;
            }

            // yt-dlp flat playlists sometimes expose only the video id.
            if (!candidate.Contains("://", StringComparison.Ordinal)
                && !candidate.StartsWith('/')
                && IsYouTubeUrl(baseUrl))
            {
                return $"https://www.youtube.com/watch?v={entry.Id ?? candidate}";
            }

            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
                && Uri.TryCreate(baseUri, candidate, out var resolved))
            {
                return resolved.ToString();
            }

            return candidate;
        }

        if (!string.IsNullOrWhiteSpace(entry.Id) && IsYouTubeUrl(baseUrl))
        {
            return $"https://www.youtube.com/watch?v={entry.Id}";
        }

        return null;
    }

    private static bool IsYouTubeUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && (url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
            || url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase));

    private void ApplyProgress(DownloadItem item, DownloadProgress progress)
    {
        Post(() =>
        {
            if (string.Equals(progress.Status, "postprocessing", StringComparison.OrdinalIgnoreCase))
            {
                item.Status = DownloadItemStatus.PostProcessing;
                item.Progress = 100;
                item.SpeedBytesPerSecond = null;
                item.EtaSeconds = null;
                return;
            }

            item.Status = DownloadItemStatus.Downloading;

            if (progress.DownloadedBytes is not null)
            {
                item.DownloadedBytes = progress.DownloadedBytes;
            }

            if (progress.TotalBytes is > 0)
            {
                item.TotalBytes = progress.TotalBytes;
                item.IsSizeEstimated = false;
            }
            else if (progress.TotalBytesEstimate is > 0)
            {
                item.TotalBytesEstimate = progress.TotalBytesEstimate;
            }

            if (progress.SpeedBytesPerSecond is > 0)
            {
                item.SpeedBytesPerSecond = progress.SpeedBytesPerSecond;
            }

            if (progress.EtaSeconds is >= 0)
            {
                item.EtaSeconds = progress.EtaSeconds;
            }

            if (progress.Percentage is >= 0)
            {
                item.Progress = Math.Clamp(progress.Percentage.Value, 0, 100);
            }
            else
            {
                var total = item.TotalBytes ?? item.TotalBytesEstimate;
                if (total is > 0 && item.DownloadedBytes is not null)
                {
                    item.Progress = Math.Clamp((double)item.DownloadedBytes.Value / total.Value * 100, 0, 100);
                }
            }
        });
    }

    private void ApplyEstimate(DownloadItem item, SizeEstimate estimate)
    {
        Post(() =>
        {
            if (estimate.Bytes is not > 0)
            {
                return;
            }

            if (estimate.IsApproximate)
            {
                item.TotalBytesEstimate = estimate.Bytes;
                item.IsSizeEstimated = true;
            }
            else
            {
                item.TotalBytes = estimate.Bytes;
                item.IsSizeEstimated = false;
            }
        });
    }

    private void EstimateMissingDuration(DownloadItem item, MediaInfo info)
    {
        if (item.DurationSeconds is null && info.Duration is > 0)
        {
            Post(() => item.DurationSeconds = info.Duration);
        }
    }

    private void Post(Action action)
    {
        if (_sync is null || SynchronizationContext.Current == _sync)
        {
            action();
            return;
        }

        _sync.Post(_ => action(), null);
    }

    private Task PostAsync(Action action)
    {
        if (_sync is null || SynchronizationContext.Current == _sync)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _sync.Post(_ =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }, null);

        return tcs.Task;
    }

    private sealed class CallbackProgress<T> : IProgress<T>
    {
        private readonly Action<T> _callback;

        public CallbackProgress(Action<T> callback) => _callback = callback;

        public void Report(T value) => _callback(value);
    }
}
