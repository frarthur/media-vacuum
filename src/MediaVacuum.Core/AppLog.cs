using System.Text;

namespace MediaVacuum.Core;

/// <summary>Small thread-safe file logger (no external dependency).</summary>
public static class AppLog
{
    private const long MaxLogSize = 2 * 1024 * 1024;
    private static readonly object SyncRoot = new();
    private static bool _initialized;

    public static string LogPath { get; private set; } = AppPaths.LogPath;

    public static void Initialize(string? path = null)
    {
        lock (SyncRoot)
        {
            LogPath = path ?? AppPaths.LogPath;
            _initialized = true;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Info($"Application started (v{typeof(AppLog).Assembly.GetName().Version}).");
        }
        catch
        {
            // Logging must never break the application.
        }
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message) => Write("WARN", message, null);

    public static void Error(string message) => Write("ERROR", message, null);

    public static void Exception(string context, Exception exception) =>
        Write("ERROR", context, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (SyncRoot)
            {
                if (!_initialized)
                {
                    LogPath = AppPaths.LogPath;
                    _initialized = true;
                }

                var directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                RotateIfNeeded();

                var builder = new StringBuilder();
                builder.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
                builder.Append(" [").Append(level).Append("] ").Append(message);
                if (exception is not null)
                {
                    builder.AppendLine();
                    builder.Append(exception);
                }

                builder.AppendLine();
                File.AppendAllText(LogPath, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never break the application.
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            var info = new FileInfo(LogPath);
            if (!info.Exists || info.Length <= MaxLogSize)
            {
                return;
            }

            var oldPath = LogPath + ".old";
            if (File.Exists(oldPath))
            {
                File.Delete(oldPath);
            }

            File.Move(LogPath, oldPath);
        }
        catch
        {
            // Ignore rotation failures.
        }
    }
}
