using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using MediaVacuum.Core.Interfaces;

namespace MediaVacuum.Core.Services;

public class FfmpegService : IFfmpegService
{
    private static readonly string[] DownloadUrls =
    [
        "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
        "https://github.com/BtbN/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-win64-gpl.zip"
    ];

    private readonly HttpClient _httpClient;
    private readonly string? _preferredPath;

    public FfmpegService(string? preferredPath = null, HttpClient? httpClient = null)
    {
        _preferredPath = preferredPath;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MediaVacuum", "0.2"));
    }

    public string? FfmpegPath { get; private set; }

    public string? FfprobePath { get; private set; }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(FfmpegPath) && File.Exists(FfmpegPath);

    public string? Version { get; private set; }

    public async Task DetectAsync(CancellationToken ct = default)
    {
        FfmpegPath = FindFfmpeg(_preferredPath);
        FfprobePath = FfmpegPath is null
            ? null
            : Path.Combine(Path.GetDirectoryName(FfmpegPath)!, "ffprobe.exe");

        if (FfprobePath is not null && !File.Exists(FfprobePath))
        {
            FfprobePath = null;
        }

        Version = IsAvailable ? await GetVersionAsync(ct).ConfigureAwait(false) : null;

        if (IsAvailable)
        {
            AppLog.Info($"ffmpeg found: {FfmpegPath} ({Version})");
        }
        else
        {
            AppLog.Info("ffmpeg was not found.");
        }
    }

    public async Task<string?> GetVersionAsync(CancellationToken ct = default)
    {
        if (!IsAvailable || FfmpegPath is null)
        {
            return null;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = FfmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-version");

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            var firstLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return firstLine?.Trim();
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to read ffmpeg version", ex);
            return null;
        }
    }

    public async Task DownloadAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        AppPaths.EnsureAll();
        var tempRoot = Path.Combine(AppPaths.TempDir, "ffmpeg-" + Guid.NewGuid().ToString("N"));
        var zipPath = Path.Combine(tempRoot, "ffmpeg.zip");
        var extractPath = Path.Combine(tempRoot, "extract");

        Directory.CreateDirectory(tempRoot);

        try
        {
            Exception? lastError = null;
            foreach (var url in DownloadUrls)
            {
                try
                {
                    AppLog.Info($"Downloading ffmpeg from {url}");
                    await DownloadFileAsync(url, zipPath, progress, ct).ConfigureAwait(false);

                    progress?.Report(92);
                    Directory.CreateDirectory(extractPath);
                    ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

                    var ffmpeg = Directory
                        .EnumerateFiles(extractPath, "ffmpeg.exe", SearchOption.AllDirectories)
                        .FirstOrDefault();

                    if (ffmpeg is null)
                    {
                        throw new InvalidOperationException("ffmpeg.exe was not found in the downloaded archive.");
                    }

                    var ffprobe = Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe");

                    Directory.CreateDirectory(AppPaths.FfmpegDir);
                    File.Copy(ffmpeg, AppPaths.FfmpegPath, true);

                    if (File.Exists(ffprobe))
                    {
                        File.Copy(ffprobe, AppPaths.FfprobePath, true);
                    }

                    progress?.Report(100);
                    await DetectAsync(ct).ConfigureAwait(false);

                    if (!IsAvailable)
                    {
                        throw new InvalidOperationException("ffmpeg could not be started after installation.");
                    }

                    AppLog.Info($"ffmpeg installed: {FfmpegPath}");
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    AppLog.Exception($"ffmpeg download failed from {url}", ex);
                }
            }

            throw new InvalidOperationException("Unable to download ffmpeg.", lastError);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    private async Task DownloadFileAsync(
        string url,
        string destination,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        using var response = await _httpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            readTotal += read;

            if (total > 0)
            {
                // Reserve the last 10% of the bar for extraction.
                progress?.Report((double)readTotal / total * 90);
            }
        }
    }

    private static string? FindFfmpeg(string? preferredPath)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(preferredPath))
        {
            candidates.Add(Directory.Exists(preferredPath)
                ? Path.Combine(preferredPath, "ffmpeg.exe")
                : preferredPath);
        }

        candidates.Add(AppPaths.FfmpegPath);
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"));
        candidates.Add(Path.Combine(Environment.CurrentDirectory, "ffmpeg.exe"));

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = "ffmpeg",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process is not null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(3000);
                var first = output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(first) && File.Exists(first))
                {
                    return first;
                }
            }
        }
        catch
        {
            // ffmpeg is optional.
        }

        return null;
    }
}
