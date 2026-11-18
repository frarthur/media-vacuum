using System.Text.Json.Serialization;

namespace MediaVacuum.Core.Models;

/// <summary>
/// Subset of the JSON returned by <c>yt-dlp --dump-single-json</c>. The same type
/// represents a single video or a playlist/channel.
/// </summary>
public class MediaInfo
{
    [JsonPropertyName("_type")]
    public string? Type { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("uploader")]
    public string? Uploader { get; set; }

    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    [JsonPropertyName("view_count")]
    public long? ViewCount { get; set; }

    [JsonPropertyName("like_count")]
    public long? LikeCount { get; set; }

    [JsonPropertyName("ext")]
    public string? Extension { get; set; }

    [JsonPropertyName("webpage_url")]
    public string? WebpageUrl { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonPropertyName("thumbnails")]
    public List<ThumbnailInfo>? Thumbnails { get; set; }

    [JsonPropertyName("filesize")]
    public long? FileSize { get; set; }

    [JsonPropertyName("filesize_approx")]
    public long? FileSizeApprox { get; set; }

    [JsonPropertyName("formats")]
    public List<FormatInfo>? Formats { get; set; }

    [JsonPropertyName("entries")]
    public List<MediaInfo>? Entries { get; set; }

    [JsonPropertyName("playlist_count")]
    public int? PlaylistCount { get; set; }

    [JsonPropertyName("is_live")]
    public bool IsLive { get; set; }

    [JsonIgnore]
    public bool IsPlaylist =>
        string.Equals(Type, "playlist", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Type, "multi_video", StringComparison.OrdinalIgnoreCase)
        || Entries is { Count: > 0 };

    /// <summary>Best thumbnail URL, preferring a reasonably sized image.</summary>
    public string? GetBestThumbnailUrl()
    {
        if (Thumbnails is { Count: > 0 })
        {
            var best = Thumbnails
                .Where(t => !string.IsNullOrWhiteSpace(t.Url))
                .OrderByDescending(t => t.Width ?? 0)
                .FirstOrDefault(t => (t.Width ?? 0) >= 160)
                ?? Thumbnails.LastOrDefault(t => !string.IsNullOrWhiteSpace(t.Url));

            if (best?.Url is { Length: > 0 } url)
            {
                return url;
            }
        }

        return Thumbnail;
    }
}

public class ThumbnailInfo
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("preference")]
    public int? Preference { get; set; }
}

public class FormatInfo
{
    [JsonPropertyName("format_id")]
    public string FormatId { get; set; } = string.Empty;

    [JsonPropertyName("ext")]
    public string Extension { get; set; } = string.Empty;

    [JsonPropertyName("resolution")]
    public string? Resolution { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("filesize")]
    public long? FileSize { get; set; }

    [JsonPropertyName("filesize_approx")]
    public long? FileSizeApprox { get; set; }

    [JsonPropertyName("format_note")]
    public string? FormatNote { get; set; }

    [JsonPropertyName("vcodec")]
    public string? VideoCodec { get; set; }

    [JsonPropertyName("acodec")]
    public string? AudioCodec { get; set; }

    [JsonPropertyName("tbr")]
    public double? TotalBitrate { get; set; }

    [JsonPropertyName("abr")]
    public double? AudioBitrate { get; set; }

    [JsonIgnore]
    public bool IsAudioOnly =>
        (string.IsNullOrEmpty(VideoCodec) || VideoCodec == "none")
        && !string.IsNullOrEmpty(AudioCodec)
        && AudioCodec != "none";

    [JsonIgnore]
    public bool IsVideoOnly =>
        !string.IsNullOrEmpty(VideoCodec)
        && VideoCodec != "none"
        && (string.IsNullOrEmpty(AudioCodec) || AudioCodec == "none");
}
