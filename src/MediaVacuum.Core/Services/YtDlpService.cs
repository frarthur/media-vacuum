using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MediaVacuum.Core.Interfaces;
using MediaVacuum.Core.Models;

namespace MediaVacuum.Core.Services;

public class YtDlpService : IYtDlpService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _ytDlpPath;
    private readonly ConcurrentDictionary<int, Process> _activeProcesses = new();

    public YtDlpService(string? ytDlpPath = null)
    {
        _ytDlpPath = string.IsNullOrWhiteSpace(ytDlpPath) ? AppPaths.YtDlpPath : ytDlpPath;
    }

    public string YtDlpPath => _ytDlpPath;

    public bool Exists
    {
        get
        {
            try
            {
                return File.Exists(_ytDlpPath) && new FileInfo(_ytDlpPath).Length > 1_000_000;
            }
            catch
            {
                return false;
            }
        }
    }

    public void KillActiveProcesses()
    {
        foreach (var process in _activeProcesses.Values.ToArray())
        {
            TryKill(process);
        }
    }

    public async Task<MediaInfo?> GetMediaInfoAsync(
        string url,
        bool flatPlaylist,
        bool noPlaylist,
        CancellationToken ct = default)
    {
        EnsureExecutable();

        var args = new List<string>
        {
            "--dump-single-json",
            "--no-download",
            "--no-warnings",
            "--no-color"
        };

        if (flatPlaylist)
        {
            args.Add("--flat-playlist");
        }

        if (noPlaylist)
        {
            args.Add("--no-playlist");
        }

        args.Add(url);

        using var process = CreateProcess(args);
        try
        {
            process.Start();
            _activeProcesses[process.Id] = process;
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to start yt-dlp for media info", ex);
            return null;
        }

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            try
            {
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }

            var output = await stdoutTask.ConfigureAwait(false);
            var error = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                AppLog.Warn($"yt-dlp media info failed (exit {process.ExitCode}): {Trim(error)}");
                return null;
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                AppLog.Warn("yt-dlp returned empty media info.");
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<MediaInfo>(output, JsonOptions);
            }
            catch (JsonException ex)
            {
                AppLog.Exception("Unable to parse yt-dlp JSON", ex);
                return null;
            }
        }
        finally
        {
            _activeProcesses.TryRemove(process.Id, out _);
        }
    }

    public async Task<DownloadResult> DownloadAsync(
        DownloadItem item,
        DownloadOptions options,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        EnsureExecutable();

        Directory.CreateDirectory(options.OutputDirectory);

        var args = YtDlpArgumentBuilder.Build(item, options, options.FfmpegPath);
        AppLog.Info($"Starting download: {item.Url}");
        AppLog.Info("yt-dlp " + string.Join(' ', args));

        using var process = CreateProcess(args);

        var lastLines = new StringBuilder();
        var lastLinesLock = new object();
        string? finalPath = null;
        string? expectedPath = null;
        string? lastDestination = null;
        long? fileSize = null;
        var alreadyDownloaded = false;

        void Remember(string line)
        {
            lock (lastLinesLock)
            {
                if (lastLines.Length > 16_000)
                {
                    lastLines.Remove(0, lastLines.Length - 12_000);
                }

                lastLines.AppendLine(line);
            }
        }

        void HandleLine(string line)
        {
            Remember(line);

            if (line.Contains("Destination:", StringComparison.Ordinal))
            {
                lastDestination = line[(line.IndexOf("Destination:", StringComparison.Ordinal) + "Destination:".Length)..].Trim();
            }

            var parsed = ProgressParser.Parse(line);
            if (parsed is null)
            {
                return;
            }

            switch (parsed.Kind)
            {
                case ProgressLineKind.Download:
                    progress?.Report(parsed.Progress!);
                    break;

                case ProgressLineKind.PostProcessing:
                    progress?.Report(new DownloadProgress { Status = "postprocessing" });
                    break;

                case ProgressLineKind.FileName:
                    expectedPath = parsed.FilePath;
                    break;

                case ProgressLineKind.FileCompleted:
                    finalPath = parsed.FilePath;
                    fileSize = parsed.FileSize;
                    break;

                case ProgressLineKind.AlreadyDownloaded:
                    alreadyDownloaded = true;
                    break;
            }
        }

        try
        {
            process.Start();
            _activeProcesses[process.Id] = process;
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to start yt-dlp", ex);
            return DownloadResult.FromError(ex.Message);
        }

        var stdoutTask = Task.Run(async () =>
        {
            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                HandleLine(line);
            }
        }, CancellationToken.None);

        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            AppLog.Info($"Download cancelled: {item.Url}");
            throw;
        }
        finally
        {
            try
            {
                await stdoutTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLog.Exception("Error while reading yt-dlp output", ex);
            }
            finally
            {
                _activeProcesses.TryRemove(process.Id, out _);
            }
        }

        string error;
        try
        {
            error = await stderrTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            error = string.Empty;
        }

        if (process.ExitCode != 0)
        {
            var rawOutput = string.Join(Environment.NewLine, error, lastLines.ToString());
            var message = YtDlpErrorParser.Parse(error, lastLines.ToString());
            AppLog.Warn($"Download failed (exit {process.ExitCode}): {Trim(rawOutput)}");
            return DownloadResult.FromError(message);
        }

        var path = finalPath ?? expectedPath ?? lastDestination;
        if (alreadyDownloaded && string.IsNullOrWhiteSpace(path))
        {
            AppLog.Info($"Already downloaded: {item.Url}");
        }

        if (fileSize is null && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                fileSize = new FileInfo(path).Length;
            }
            catch
            {
                // The file may have been moved by the user while downloading.
            }
        }

        AppLog.Info($"Download completed: {item.Url} -> {path}");
        return DownloadResult.FromSuccess(path, item.Title, fileSize);
    }

    public async Task<string> GetVersionAsync(CancellationToken ct = default)
    {
        if (!Exists)
        {
            return "0.0.0";
        }

        var args = new List<string> { "--version", "--no-color" };
        using var process = CreateProcess(args);
        int? processId = null;

        try
        {
            process.Start();
            processId = process.Id;
            _activeProcesses[processId.Value] = process;
            var output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return output.Trim();
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to read yt-dlp version", ex);
            return "0.0.0";
        }
        finally
        {
            if (processId.HasValue)
            {
                _activeProcesses.TryRemove(processId.Value, out _);
            }
        }
    }

    private Process CreateProcess(IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ytDlpPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = AppContext.BaseDirectory
        };

        startInfo.Environment["PYTHONUTF8"] = "1";
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo };
        process.EnableRaisingEvents = true;
        return process;
    }

    private void EnsureExecutable()
    {
        if (!Exists)
        {
            throw new FileNotFoundException("yt-dlp.exe was not found or is incomplete.", _ytDlpPath);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort.
        }
    }

    private static string Trim(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        value = value.Trim();
        return value.Length <= 4000 ? value : value[^4000..];
    }
}
