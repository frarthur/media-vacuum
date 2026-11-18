namespace MediaVacuum.Core;

public static class AppPaths
{
    public static string DataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MediaVacuum");

    public static string ConfigPath => Path.Combine(DataDir, "config.json");

    public static string TranslationsDir => Path.Combine(DataDir, "Translations");

    public static string YtDlpPath => Path.Combine(DataDir, "yt-dlp.exe");

    public static string FfmpegDir => Path.Combine(DataDir, "ffmpeg");

    public static string FfmpegPath => Path.Combine(FfmpegDir, "ffmpeg.exe");

    public static string FfprobePath => Path.Combine(FfmpegDir, "ffprobe.exe");

    public static string JsDir => Path.Combine(DataDir, "js");

    public static string DenoPath => Path.Combine(JsDir, "deno.exe");

    public static string ThumbsDir => Path.Combine(DataDir, "thumbnails");

    public static string LogsDir => Path.Combine(DataDir, "logs");

    public static string LogPath => Path.Combine(LogsDir, "app.log");

    public static string TempDir => Path.Combine(Path.GetTempPath(), "MediaVacuum");

    public static string LogoPath => Path.Combine(DataDir, "logo_app.png");

    public static void EnsureDataDir()
    {
        Directory.CreateDirectory(DataDir);
    }

    public static void EnsureAll()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(ThumbsDir);
        Directory.CreateDirectory(JsDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(TempDir);
    }
}
