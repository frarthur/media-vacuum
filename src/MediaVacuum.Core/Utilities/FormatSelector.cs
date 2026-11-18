using MediaVacuum.Core.Models;

namespace MediaVacuum.Core.Utilities;

public static class FormatSelector
{
    public static readonly string[] VideoPresets = ["best", "2160p", "1440p", "1080p", "720p", "480p", "360p"];

    public static readonly string[] AudioFormats =
        ["mp3", "m4a", "aac", "flac", "opus", "vorbis", "wav", "alac"];

    public static readonly string[] VideoContainers = ["auto", "mp4", "mkv", "webm", "mp4-recode"];

    /// <summary>Builds a valid yt-dlp format selector. The old "720p" presets were invalid.</summary>
    public static string Build(DownloadOptions options)
    {
        if (options.Mode == DownloadMode.Audio)
        {
            return "ba/b";
        }

        var custom = options.CustomFormat?.Trim();
        if (!string.IsNullOrEmpty(custom))
        {
            return custom;
        }

        var height = options.VideoPreset switch
        {
            "2160p" => 2160,
            "1440p" => 1440,
            "1080p" => 1080,
            "720p" => 720,
            "480p" => 480,
            "360p" => 360,
            _ => 0
        };

        return height > 0
            ? $"bv*[height<={height}]+ba/b[height<={height}]"
            : "bv*+ba/b";
    }

    /// <summary>True when the selected options require ffmpeg for merge/convert/embed.</summary>
    public static bool RequiresFfmpeg(DownloadOptions options)
    {
        if (options.Mode == DownloadMode.Audio)
        {
            return true;
        }

        if (options.EmbedMetadata || options.EmbedThumbnail || options.EmbedSubtitles)
        {
            return true;
        }

        if (!string.Equals(options.VideoContainer, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Build(options).Contains('+');
    }

    public static string NormalizeContainer(string? container) =>
        string.IsNullOrWhiteSpace(container) ? "auto" : container.Trim().ToLowerInvariant();

    public static string NormalizeAudioFormat(string? format) =>
        string.IsNullOrWhiteSpace(format) ? "mp3" : format.Trim().ToLowerInvariant();
}
