using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using MediaVacuum.Core.Interfaces;
using MediaVacuum.Core.Models;
using MediaVacuum.Core.Utilities;

namespace MediaVacuum.Core.Services;

public class UpdateService : IUpdateService
{
    private const string LatestApiUrl = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
    private const string LatestDownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

    private readonly string _targetPath;
    private readonly HttpClient _httpClient;
    private readonly HttpClient _redirectClient;

    public UpdateService(string targetPath, HttpClient? httpClient = null)
    {
        _targetPath = targetPath;

        if (httpClient is not null)
        {
            _httpClient = httpClient;
        }
        else
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MediaVacuum", "0.2"));
        }

        _redirectClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _redirectClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MediaVacuum", "0.2"));
    }

    public string CurrentVersion { get; private set; } = string.Empty;

    public string? LatestVersion { get; private set; }

    public bool Exists()
    {
        try
        {
            return File.Exists(_targetPath) && new FileInfo(_targetPath).Length > 1_000_000;
        }
        catch
        {
            return false;
        }
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        CurrentVersion = await GetCurrentVersionAsync(ct).ConfigureAwait(false);

        UpdateInfo? info = null;
        try
        {
            info = await CheckWithApiAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLog.Exception("GitHub API update check failed, falling back to redirect check", ex);
        }

        info ??= await CheckWithRedirectAsync(ct).ConfigureAwait(false);

        LatestVersion = info?.Version;
        return info;
    }

    public async Task UpdateAsync(
        UpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_targetPath)!);

        var tempPath = _targetPath + ".download";
        try
        {
            using var response = await _httpClient
                .GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            await using var content = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using (var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await content.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    totalRead += read;

                    if (totalBytes > 0)
                    {
                        progress?.Report((double)totalRead / totalBytes * 100);
                    }
                }

                await file.FlushAsync(ct).ConfigureAwait(false);
            }

            var length = new FileInfo(tempPath).Length;
            if (length < 1_000_000)
            {
                throw new InvalidOperationException("Downloaded yt-dlp.exe is unexpectedly small.");
            }

            if (!string.IsNullOrWhiteSpace(update.Sha256))
            {
                var actual = ComputeSha256(tempPath);
                if (!string.Equals(actual, NormalizeDigest(update.Sha256), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Checksum mismatch for yt-dlp.exe.");
                }
            }

            try
            {
                File.Move(tempPath, _targetPath, true);
            }
            catch (IOException)
            {
                // The old binary may be locked by a running yt-dlp process.
                if (File.Exists(_targetPath))
                {
                    File.Delete(_targetPath);
                }

                File.Move(tempPath, _targetPath);
            }

            CurrentVersion = update.Version;
            LatestVersion = update.Version;
            progress?.Report(100);
            AppLog.Info($"yt-dlp updated to {update.Version}.");
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best effort.
            }
        }
    }

    public async Task EnsureDownloadedAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (Exists())
        {
            return;
        }

        AppLog.Info("yt-dlp.exe not found, downloading it.");
        var info = await CheckForUpdateAsync(ct).ConfigureAwait(false);
        info ??= new UpdateInfo("latest", LatestDownloadUrl, null, null);

        if (string.Equals(info.Version, "latest", StringComparison.OrdinalIgnoreCase))
        {
            info = info with { Version = await ResolveLatestVersionFromRedirectAsync(ct).ConfigureAwait(false) ?? "latest" };
        }

        await UpdateAsync(info, progress, ct).ConfigureAwait(false);
    }

    public async Task<string> GetCurrentVersionAsync(CancellationToken ct = default)
    {
        if (!Exists())
        {
            return "0.0.0";
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _targetPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("--version");

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(output) ? "0.0.0" : output.Trim();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to read current yt-dlp version", ex);
            return "0.0.0";
        }
    }

    private async Task<UpdateInfo?> CheckWithApiAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestApiUrl);
        request.Headers.UserAgent.ParseAdd("MediaVacuum/0.2");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"GitHub API returned {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = document.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
        var releaseUrl = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() : null;

        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        string? downloadUrl = null;
        string? digest = null;

        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                if (!string.Equals(name, "yt-dlp.exe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                downloadUrl = asset.TryGetProperty("browser_download_url", out var downloadElement)
                    ? downloadElement.GetString()
                    : null;
                digest = asset.TryGetProperty("digest", out var digestElement)
                    ? digestElement.GetString()
                    : null;
                break;
            }
        }

        downloadUrl ??= LatestDownloadUrl;

        if (!VersionComparer.IsNewer(tag, CurrentVersion))
        {
            return null;
        }

        return new UpdateInfo(tag, downloadUrl, digest, releaseUrl, "yt-dlp.exe");
    }

    private async Task<UpdateInfo?> CheckWithRedirectAsync(CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, LatestDownloadUrl);
            using var response = await _redirectClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            var location = response.Headers.Location;
            if (location is null && response.StatusCode is not (HttpStatusCode.MovedPermanently
                or HttpStatusCode.Redirect
                or HttpStatusCode.RedirectMethod
                or HttpStatusCode.TemporaryRedirect
                or HttpStatusCode.PermanentRedirect))
            {
                return null;
            }

            var version = ExtractVersionFromUrl(location?.ToString());
            if (string.IsNullOrWhiteSpace(version))
            {
                return null;
            }

            if (!VersionComparer.IsNewer(version, CurrentVersion))
            {
                return null;
            }

            return new UpdateInfo(version, LatestDownloadUrl, null, null, "yt-dlp.exe");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLog.Exception("Redirect based update check failed", ex);
            return null;
        }
    }

    private async Task<string?> ResolveLatestVersionFromRedirectAsync(CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, LatestDownloadUrl);
            using var response = await _redirectClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            return ExtractVersionFromUrl(response.Headers.Location?.ToString());
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractVersionFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var segments = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (string.Equals(segments[i], "download", StringComparison.OrdinalIgnoreCase))
            {
                return segments[i + 1];
            }
        }

        return null;
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string NormalizeDigest(string digest)
    {
        var value = digest.Trim();
        return value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? value["sha256:".Length..]
            : value;
    }
}
