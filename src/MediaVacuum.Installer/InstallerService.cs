using System.Diagnostics;
using MediaVacuum.Core;
using MediaVacuum.Core.Interfaces;

namespace MediaVacuum.Installer;

/// <summary>
/// Removes only known MediaVacuum data. It never deletes an arbitrary parent
/// directory (the previous implementation could delete the user's Downloads folder).
/// </summary>
public class InstallerService : IInstallerService
{
    private readonly string _appExePath;

    public InstallerService(string? appExePath = null)
    {
        _appExePath = appExePath ?? Environment.ProcessPath ?? string.Empty;
    }

    public long GetAppDataSize()
    {
        try
        {
            if (!Directory.Exists(AppPaths.DataDir))
            {
                return 0;
            }

            return Directory
                .EnumerateFiles(AppPaths.DataDir, "*", SearchOption.AllDirectories)
                .Select(path =>
                {
                    try
                    {
                        return new FileInfo(path).Length;
                    }
                    catch
                    {
                        return 0L;
                    }
                })
                .Sum();
        }
        catch
        {
            return 0;
        }
    }

    public void RemoveAppData(bool includeYtDlp, bool includeFfmpeg, bool includeLogs)
    {
        TryDeleteFile(AppPaths.ConfigPath);
        TryDeleteDirectory(AppPaths.TranslationsDir);
        TryDeleteDirectory(AppPaths.ThumbsDir);
        TryDeleteDirectory(AppPaths.TempDir);

        if (includeYtDlp)
        {
            TryDeleteFile(AppPaths.YtDlpPath);
        }

        if (includeFfmpeg)
        {
            TryDeleteDirectory(AppPaths.FfmpegDir);
        }

        if (includeLogs)
        {
            TryDeleteDirectory(AppPaths.LogsDir);
        }

        try
        {
            if (Directory.Exists(AppPaths.DataDir)
                && !Directory.EnumerateFileSystemEntries(AppPaths.DataDir).Any())
            {
                Directory.Delete(AppPaths.DataDir);
            }
        }
        catch
        {
            // Best effort.
        }

        AppLog.Info("MediaVacuum app data removed.");
    }

    public void ScheduleSelfDelete()
    {
        if (string.IsNullOrWhiteSpace(_appExePath) || !File.Exists(_appExePath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(AppPaths.TempDir);
            var scriptPath = Path.Combine(AppPaths.TempDir, $"uninstall-{Guid.NewGuid():N}.cmd");
            var script = string.Join(
                Environment.NewLine,
                "@echo off",
                "ping 127.0.0.1 -n 3 >nul",
                $"del /f /q \"{_appExePath}\"",
                "del /f /q \"%~f0\"");

            File.WriteAllText(scriptPath, script);

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);
            AppLog.Info("Self-delete scheduled.");
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to schedule self-delete", ex);
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
        catch (Exception ex)
        {
            AppLog.Exception($"Unable to delete {path}", ex);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex)
        {
            AppLog.Exception($"Unable to delete {path}", ex);
        }
    }
}
