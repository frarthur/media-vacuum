using MediaVacuum.Core.Models;
using MediaVacuum.Core.Utilities;

namespace MediaVacuum.Tests;

public class FormatSelectorTests
{
    [Theory]
    [InlineData("best", "bv*+ba/b")]
    [InlineData("1080p", "bv*[height<=1080]+ba/b[height<=1080]")]
    [InlineData("720p", "bv*[height<=720]+ba/b[height<=720]")]
    [InlineData("360p", "bv*[height<=360]+ba/b[height<=360]")]
    public void Build_UsesValidYtDlpSelectors(string preset, string expected)
    {
        var options = new DownloadOptions { VideoPreset = preset };
        Assert.Equal(expected, FormatSelector.Build(options));
    }

    [Fact]
    public void Build_AudioModeUsesBestAudio()
    {
        var options = new DownloadOptions { Mode = DownloadMode.Audio };
        Assert.Equal("ba/b", FormatSelector.Build(options));
    }

    [Fact]
    public void Build_CustomFormatWins()
    {
        var options = new DownloadOptions
        {
            VideoPreset = "720p",
            CustomFormat = "bv*[height<=480]+ba"
        };

        Assert.Equal("bv*[height<=480]+ba", FormatSelector.Build(options));
    }

    [Fact]
    public void RequiresFfmpeg_ForAudioAndEmbedding()
    {
        Assert.True(FormatSelector.RequiresFfmpeg(new DownloadOptions { Mode = DownloadMode.Audio }));
        Assert.True(FormatSelector.RequiresFfmpeg(new DownloadOptions { EmbedThumbnail = true }));
        Assert.True(FormatSelector.RequiresFfmpeg(new DownloadOptions { VideoContainer = "mp4" }));
        Assert.True(FormatSelector.RequiresFfmpeg(new DownloadOptions { VideoPreset = "best" }));
        Assert.True(FormatSelector.RequiresFfmpeg(new DownloadOptions
        {
            VideoPreset = "720p",
            EmbedThumbnail = false,
            EmbedMetadata = false,
            VideoContainer = "auto"
        }));
        Assert.False(FormatSelector.RequiresFfmpeg(new DownloadOptions
        {
            CustomFormat = "best",
            EmbedThumbnail = false,
            EmbedMetadata = false,
            VideoContainer = "auto"
        }));
    }
}
