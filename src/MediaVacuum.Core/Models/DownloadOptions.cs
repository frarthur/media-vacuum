namespace MediaVacuum.Core.Models;

public enum DownloadMode
{
    Video,
    Audio
}

public class DownloadOptions
{
    public string OutputDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "MediaVacuum");

    public DownloadMode Mode { get; set; } = DownloadMode.Video;

    /// <summary>Preset key: best, 2160p, 1440p, 1080p, 720p, 480p, 360p or custom.</summary>
    public string VideoPreset { get; set; } = "best";

    /// <summary>Advanced yt-dlp format selector. Takes precedence over the preset when set.</summary>
    public string? CustomFormat { get; set; }

    /// <summary>auto, mp4, mkv, webm, mp4-recode.</summary>
    public string VideoContainer { get; set; } = "auto";

    public string AudioFormat { get; set; } = "mp3";

    public bool EmbedMetadata { get; set; } = true;

    public bool EmbedThumbnail { get; set; } = true;

    /// <summary>Also keep a .jpg/.webp thumbnail next to the media file.</summary>
    public bool WriteThumbnail { get; set; }

    public bool WriteSubtitles { get; set; }

    public bool EmbedSubtitles { get; set; }

    public string SubtitleLanguages { get; set; } = "en";

    public bool CreatePlaylistFolder { get; set; } = true;

    /// <summary>Append [videoId] to the filename to avoid collisions.</summary>
    public bool IncludeVideoId { get; set; }

    public int ConcurrentFragments { get; set; } = 1;

    /// <summary>Download speed limit in KiB/s. 0 means unlimited.</summary>
    public int LimitRateKb { get; set; }

    public bool OverwriteExisting { get; set; }

    /// <summary>Directory containing ffmpeg.exe/ffprobe.exe, when available.</summary>
    public string? FfmpegPath { get; set; }

    /// <summary>yt-dlp --js-runtimes value, for example "node" or "deno:C:\...\deno.exe".</summary>
    public string? JsRuntime { get; set; }
}
