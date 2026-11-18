using MediaVacuum.Core.Models;
using MediaVacuum.Core.Utilities;

namespace MediaVacuum.Tests;

public class SizeEstimatorTests
{
    [Fact]
    public void Estimate_AudioModeUsesAudioFormatNotRootVideoSize()
    {
        var info = new MediaInfo
        {
            FileSizeApprox = 100_000_000,
            Duration = 60,
            Formats =
            [
                new FormatInfo { FileSize = 99_000_000, VideoCodec = "avc1", AudioCodec = "none" },
                new FormatInfo { FileSize = 1_500_000, VideoCodec = "none", AudioCodec = "mp4a" }
            ]
        };

        var estimate = SizeEstimator.Estimate(info, new DownloadOptions { Mode = DownloadMode.Audio });

        Assert.Equal(1_500_000, estimate.Bytes);
        Assert.False(estimate.IsApproximate);
    }

    [Fact]
    public void Estimate_CappedPresetIgnoresHigherFormat()
    {
        var info = new MediaInfo
        {
            FileSizeApprox = 100_000_000,
            Formats =
            [
                new FormatInfo { Height = 2160, FileSize = 90_000_000, VideoCodec = "avc1" },
                new FormatInfo { Height = 720, FileSize = 8_000_000, VideoCodec = "avc1" }
            ]
        };

        var estimate = SizeEstimator.Estimate(info, new DownloadOptions { VideoPreset = "720p" });

        Assert.Equal(8_000_000, estimate.Bytes);
        Assert.False(estimate.IsApproximate);
    }

    [Fact]
    public void EstimateFromDuration_ReturnsApproximateSize()
    {
        var estimate = SizeEstimator.EstimateFromDuration(60, new DownloadOptions { Mode = DownloadMode.Audio });

        Assert.True(estimate.IsApproximate);
        Assert.True(estimate.Bytes > 0);
    }
}
