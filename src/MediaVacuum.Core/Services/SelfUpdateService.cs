using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaVacuum.Core.Interfaces;
using MediaVacuum.Core.Models;
using MediaVacuum.Core.Utilities;

namespace MediaVacuum.Core.Services;

/// <summary>
/// Self-update for MediaVacuum itself. The latest GitHub release is checked,
/// its win-x64 zip (or exe) is downloaded, and a small cmd helper replaces the
/// running executable after the application exits.
/// </summary>
public class SelfUpdateService : IAppUpdateService
{
    private const string LatestApiUrl =
        "https://api.github.com/repos/frarthur/MediaVacuum/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly string? _updatesDirectory;

    public SelfUpdateService(
        Version? currentVersion = null,
        HttpClient? httpClient = null,
        string? updatesDirectory = null)
    {
        CurrentVersion = currentVersion ?? new Version(0, 0, 0);
        _updatesDirectory = updatesDirectory;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MediaVacuum", CurrentVersion.ToString(3)));
    }

    public Version CurrentVersion { get; }

    public async Task<AppUpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestApiUrl);
        request.Headers.UserAgent.ParseAdd($"MediaVacuum/{CurrentVersion.ToString(3)}");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = document.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
        var releaseUrl = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() : null;

        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        // Compare only Major.Minor.Build: AssemblyVersion is 0.2.0.0 while tags are v0.2.0.
        if (!VersionComparer.IsNewer(tag, CurrentVersion.ToString(3)))
        {
            return null;
        }

        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        AssetCandidate? zip = null;
        AssetCandidate? exe = null;
        AssetCandidate? anyZip = null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            var downloadUrl = asset.TryGetProperty("browser_download_url", out var downloadElement)
                ? downloadElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(downloadUrl))
            {
                continue;
            }

            var digest = asset.TryGetProperty("digest", out var digestElement)
                ? digestElement.GetString()
                : null;

            var candidate = new AssetCandidate(name, downloadUrl, digest);
            var isMediaVacuum = name.Contains("MediaVacuum", StringComparison.OrdinalIgnoreCase);
            var isZip = name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

            if (isMediaVacuum && isZip && name.Contains("win-x64", StringComparison.OrdinalIgnoreCase))
            {
                zip ??= candidate;
            }
            else if (isMediaVacuum && name.Equals("MediaVacuum.exe", StringComparison.OrdinalIgnoreCase))
            {
                exe ??= candidate;
            }
            else if (isMediaVacuum && isZip)
            {
                anyZip ??= candidate;
            }
        }

        var selected = zip ?? exe ?? anyZip;
        if (selected is null)
        {
            return null;
        }

        var version = tag.TrimStart('v', 'V');
        var isZipAsset = selected.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        return new AppUpdateInfo(
            version,
            tag,
            selected.Url,
            selected.Name,
            isZipAsset,
            selected.Digest,
            releaseUrl);
    }

    public async Task<string> DownloadAsync(
        AppUpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var updatesDir = _updatesDirectory ?? Path.Combine(AppPaths.DataDir, "updates");
        Directory.CreateDirectory(updatesDir);

        var extension = update.IsZip ? ".zip" : ".exe";
        var tempPath = Path.Combine(updatesDir, $"download-{Guid.NewGuid():N}{extension}");
        var extractDir = Path.Combine(updatesDir, $"extract-{Guid.NewGuid():N}");
        var targetName = "MediaVacuum-" + FilenameService.Sanitize(update.Version, 40) + ".exe";
        var targetPath = Path.Combine(updatesDir, targetName);

        try
        {
            await DownloadFileAsync(update.DownloadUrl, tempPath, progress, ct).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(update.Sha256))
            {
                var actual = ComputeSha256(tempPath);
                if (!string.Equals(actual, NormalizeDigest(update.Sha256), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Checksum mismatch for the MediaVacuum update.");
                }
            }

            progress?.Report(90);

            if (update.IsZip)
            {
                Directory.CreateDirectory(extractDir);
                ZipFile.ExtractToDirectory(tempPath, extractDir, overwriteFiles: true);

                var executable = Directory
                    .EnumerateFiles(extractDir, "MediaVacuum.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (executable is null)
                {
                    throw new InvalidOperationException("MediaVacuum.exe was not found in the update archive.");
                }

                File.Copy(executable, targetPath, overwrite: true);
            }
            else
            {
                File.Move(tempPath, targetPath, overwrite: true);
            }

            var length = new FileInfo(targetPath).Length;
            if (length < 10_000_000)
            {
                throw new InvalidOperationException("The downloaded MediaVacuum.exe is unexpectedly small.");
            }

            CleanupOldUpdates(updatesDir, targetPath);
            progress?.Report(100);
            AppLog.Info($"MediaVacuum update downloaded: {targetPath}");
            return targetPath;
        }
        finally
        {
            TryDeleteFile(tempPath);

            if (Directory.Exists(extractDir))
            {
                try
                {
                    Directory.Delete(extractDir, recursive: true);
                }
                catch
                {
                    // Best effort.
                }
            }
        }
    }

    public void LaunchUpdater(string downloadedExePath)
    {
        if (!File.Exists(downloadedExePath))
        {
            throw new FileNotFoundException("The downloaded update was not found.", downloadedExePath);
        }

        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe))
        {
            throw new InvalidOperationException("Unable to locate the current MediaVacuum executable.");
        }

        Directory.CreateDirectory(AppPaths.TempDir);
        var scriptPath = Path.Combine(AppPaths.TempDir, $"update-{Guid.NewGuid():N}.cmd");

        var script = string.Join(
            Environment.NewLine,
            "@echo off",
            "chcp 65001 >nul",
            "setlocal EnableExtensions",
            $"set \"TARGET={currentExe}\"",
            $"set \"SOURCE={downloadedExePath}\"",
            "set /a COUNT=0",
            ":wait",
            "timeout /t 1 /nobreak >nul",
            "copy /y \"%SOURCE%\" \"%TARGET%\" >nul 2>&1",
            "if not errorlevel 1 goto copied",
            "set /a COUNT+=1",
            "if %COUNT% GEQ 60 goto failed",
            "goto wait",
            ":copied",
            "del /f /q \"%SOURCE%\" >nul 2>&1",
            "start \"\" \"%TARGET%\"",
            "goto cleanup",
            ":failed",
            "start \"\" \"%TARGET%\"",
            ":cleanup",
            "timeout /t 2 /nobreak >nul",
            "del /f /q \"%~f0\" >nul 2>&1");

        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        Process.Start(startInfo);
        AppLog.Info($"Self-update helper started. Target: {currentExe}; source: {downloadedExePath}");
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
                // Reserve the last 10% for extraction.
                progress?.Report((double)readTotal / total * 90);
            }
        }
    }

    private static void CleanupOldUpdates(string updatesDir, string keepPath)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(updatesDir, "MediaVacuum-*.exe"))
            {
                if (!string.Equals(file, keepPath, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteFile(file);
                }
            }

            foreach (var file in Directory.EnumerateFiles(updatesDir, "download-*"))
            {
                TryDeleteFile(file);
            }

            foreach (var file in Directory.EnumerateFiles(updatesDir, "*.zip"))
            {
                TryDeleteFile(file);
            }

            foreach (var directory in Directory.EnumerateDirectories(updatesDir, "extract-*"))
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch
                {
                    // Best effort.
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to clean old update files", ex);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort.
        }
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

    private sealed record AssetCandidate(string Name, string Url, string? Digest);
}
