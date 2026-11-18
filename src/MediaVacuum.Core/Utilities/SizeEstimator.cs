using MediaVacuum.Core.Models;

namespace MediaVacuum.Core.Utilities;

public static class SizeEstimator
{
    private const double DefaultVideoBitsPerSecond = 2_500_000;
    private const double DefaultAudioBitsPerSecond = 192_000;
    private const long MinimumPlausibleBytes = 1024;

    public static SizeEstimate Estimate(MediaInfo? info, DownloadOptions options)
    {
        if (info is null)
        {
            return new SizeEstimate(null, true);
        }

        // The root filesize is the size of the default/best video. It must not
        // be used for audio extraction or for a capped video preset.
        var useRootSize = options.Mode == DownloadMode.Video
            && string.IsNullOrWhiteSpace(options.CustomFormat)
            && (string.IsNullOrWhiteSpace(options.VideoPreset)
                || options.VideoPreset == "best");

        if (useRootSize)
        {
            var exact = info.FileSize ?? info.FileSizeApprox;
            if (exact is > MinimumPlausibleBytes)
            {
                return new SizeEstimate(exact, false);
            }
        }

        if (info.Formats is { Count: > 0 })
        {
            var formats = info.Formats;
            if (options.Mode == DownloadMode.Audio)
            {
                var audio = formats
                    .Where(f => f.IsAudioOnly)
                    .OrderByDescending(f => f.FileSize ?? f.FileSizeApprox ?? 0)
                    .FirstOrDefault();

                if (audio is not null)
                {
                    var size = audio.FileSize ?? audio.FileSizeApprox;
                    if (size is > MinimumPlausibleBytes)
                    {
                        return new SizeEstimate(size, false);
                    }

                    if (audio.AudioBitrate is > 0 && info.Duration is > 0)
                    {
                        return new SizeEstimate((long)(info.Duration.Value * audio.AudioBitrate.Value * 1000 / 8), true);
                    }
                }
            }
            else
            {
                var maxHeight = options.VideoPreset switch
                {
                    "2160p" => 2160,
                    "1440p" => 1440,
                    "1080p" => 1080,
                    "720p" => 720,
                    "480p" => 480,
                    "360p" => 360,
                    _ => int.MaxValue
                };

                var video = formats
                    .Where(f => !f.IsAudioOnly && (f.Height is null || f.Height <= maxHeight))
                    .OrderByDescending(f => f.Height ?? 0)
                    .ThenByDescending(f => f.FileSize ?? f.FileSizeApprox ?? 0)
                    .FirstOrDefault();

                if (video is not null)
                {
                    var size = video.FileSize ?? video.FileSizeApprox;
                    if (size is > MinimumPlausibleBytes)
                    {
                        var audioSize = formats
                            .Where(f => f.IsAudioOnly)
                            .Select(f => f.FileSize ?? f.FileSizeApprox ?? 0)
                            .DefaultIfEmpty(0)
                            .Max();

                        return new SizeEstimate(size + audioSize, false);
                    }

                    if (video.TotalBitrate is > 0 && info.Duration is > 0)
                    {
                        return new SizeEstimate((long)(info.Duration.Value * video.TotalBitrate.Value * 1000 / 8), true);
                    }
                }
            }
        }

        if (info.Duration is > 0)
        {
            var bps = options.Mode == DownloadMode.Audio
                ? DefaultAudioBitsPerSecond
                : DefaultVideoBitsPerSecond;

            return new SizeEstimate((long)(info.Duration.Value * bps / 8), true);
        }

        return new SizeEstimate(null, true);
    }

    /// <summary>Very rough estimate used in the queue before the real size is known.</summary>
    public static SizeEstimate EstimateFromDuration(double? durationSeconds, DownloadOptions options)
    {
        if (durationSeconds is null or <= 0)
        {
            return new SizeEstimate(null, true);
        }

        var bps = options.Mode == DownloadMode.Audio
            ? DefaultAudioBitsPerSecond
            : DefaultVideoBitsPerSecond;

        return new SizeEstimate((long)(durationSeconds.Value * bps / 8), true);
    }
}
