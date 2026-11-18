using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using MediaVacuum.Core.Interfaces;
using MediaVacuum.Core.Models;

namespace MediaVacuum.Core.Services;

/// <summary>
/// Detects a JavaScript runtime for yt-dlp's YouTube extractor and can install
/// Deno when no runtime is available.
/// </summary>
public class JsRuntimeService : IJsRuntimeService
{
    private const string DenoDownloadUrl =
        "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";

    private static readonly (string Name, string Executable)[] Candidates =
    [
        ("deno", "deno.exe"),
        ("node", "node.exe"),
        ("bun", "bun.exe"),
        ("qjs", "qjs.exe")
    ];

    private readonly HttpClient _httpClient;

    public JsRuntimeService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MediaVacuum", "0.2"));
    }

    public JsRuntimeInfo? Runtime { get; private set; }

    public bool IsAvailable => Runtime is not null;

    public Task DetectAsync(CancellationToken ct = default)
    {
        foreach (var (name, executable) in Candidates)
        {
            var bundled = Path.Combine(AppPaths.JsDir, executable);
            if (File.Exists(bundled))
            {
                Runtime = new JsRuntimeInfo(name, bundled, IsBundled: true);
                AppLog.Info($"JavaScript runtime found: {bundled}");
                return Task.CompletedTask;
            }
        }

        foreach (var (name, executable) in Candidates)
        {
            var fromPath = FindOnPath(executable);
            if (!string.IsNullOrWhiteSpace(fromPath))
            {
                Runtime = new JsRuntimeInfo(name, fromPath, IsBundled: false);
                AppLog.Info($"JavaScript runtime found on PATH: {fromPath}");
                return Task.CompletedTask;
            }
        }

        Runtime = null;
        AppLog.Info("No JavaScript runtime found for yt-dlp.");
        return Task.CompletedTask;
    }

    public string? BuildYtDlpArgument()
    {
        if (Runtime is null)
        {
            return null;
        }

        if (Runtime.IsBundled && !string.IsNullOrWhiteSpace(Runtime.Path))
        {
            return $"{Runtime.Name}:{Runtime.Path}";
        }

        return Runtime.Name;
    }

    public async Task DownloadDenoAsync(
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var tempRoot = Path.Combine(AppPaths.TempDir, "deno-" + Guid.NewGuid().ToString("N"));
        var zipPath = Path.Combine(tempRoot, "deno.zip");
        var extractPath = Path.Combine(tempRoot, "extract");

        Directory.CreateDirectory(tempRoot);

        try
        {
            AppLog.Info("Downloading Deno from GitHub.");
            await DownloadFileAsync(DenoDownloadUrl, zipPath, progress, ct).ConfigureAwait(false);

            progress?.Report(92);
            Directory.CreateDirectory(extractPath);
            ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

            var deno = Directory
                .EnumerateFiles(extractPath, "deno.exe", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (deno is null)
            {
                throw new InvalidOperationException("deno.exe was not found in the downloaded archive.");
            }

            Directory.CreateDirectory(AppPaths.JsDir);
            File.Copy(deno, AppPaths.DenoPath, overwrite: true);

            progress?.Report(100);
            await DetectAsync(ct).ConfigureAwait(false);

            if (!IsAvailable)
            {
                throw new InvalidOperationException("Deno could not be started after installation.");
            }

            AppLog.Info($"Deno installed: {AppPaths.DenoPath}");
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
                // Best effort.
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
                progress?.Report((double)readTotal / total * 90);
            }
        }
    }

    private static string? FindOnPath(string executable)
    {
        try
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var cleaned = directory.Trim().Trim('"');
                if (cleaned.Length == 0)
                {
                    continue;
                }

                var candidate = Path.Combine(cleaned, executable);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        catch
        {
            // Ignore malformed PATH entries.
        }

        return null;
    }
}
