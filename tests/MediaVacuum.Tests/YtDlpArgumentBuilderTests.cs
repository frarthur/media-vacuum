using MediaVacuum.Core.Models;
using MediaVacuum.Core.Services;

namespace MediaVacuum.Tests;

public class YtDlpArgumentBuilderTests
{
    private static DownloadItem CreateItem() => new()
    {
        Id = "abc",
        Title = "My video",
        Url = "https://example.com/watch?v=abc",
        PlaylistIndex = 2,
        PlaylistTitle = "My playlist"
    };

    [Fact]
    public void Build_ContainsProgressAndFileTemplates()
    {
        var args = YtDlpArgumentBuilder.Build(CreateItem(), new DownloadOptions());

        Assert.Contains("--progress-template", args);
        Assert.Contains(YtDlpArgumentBuilder.ProgressTemplate, args);
        Assert.Contains("before_dl:MVNAME|%(filepath)s", args);
        Assert.Contains("after_move:MVFILE|%(filepath)s|%(filesize)s", args);
        Assert.Contains("--no-playlist", args);
        Assert.Contains("--windows-filenames", args);
    }

    [Fact]
    public void Build_UsesValidFormatSelector()
    {
        var options = new DownloadOptions { VideoPreset = "720p" };
        var args = YtDlpArgumentBuilder.Build(CreateItem(), options);

        var formatIndex = args.IndexOf("-f");
        Assert.True(formatIndex >= 0);
        Assert.Equal("bv*[height<=720]+ba/b[height<=720]", args[formatIndex + 1]);
    }

    [Fact]
    public void Build_AudioModeAddsExtraction()
    {
        var options = new DownloadOptions
        {
            Mode = DownloadMode.Audio,
            AudioFormat = "mp3",
            EmbedMetadata = false,
            EmbedThumbnail = false
        };

        var args = YtDlpArgumentBuilder.Build(CreateItem(), options);

        Assert.Contains("-x", args);
        Assert.Contains("--audio-format", args);
        Assert.Contains("mp3", args);
    }

    [Fact]
    public void Build_AddsFfmpegLocationWhenAvailable()
    {
        var args = YtDlpArgumentBuilder.Build(
            CreateItem(),
            new DownloadOptions { EmbedThumbnail = false, EmbedMetadata = false },
            @"C:\MediaVacuum\ffmpeg");

        var index = args.IndexOf("--ffmpeg-location");
        Assert.True(index >= 0);
        Assert.Equal(@"C:\MediaVacuum\ffmpeg", args[index + 1]);
    }

    [Fact]
    public void Build_ThumbnailEmbeddingUsesJpgAndSidecarOption()
    {
        var options = new DownloadOptions
        {
            EmbedThumbnail = true,
            WriteThumbnail = true,
            EmbedMetadata = true
        };

        var args = YtDlpArgumentBuilder.Build(CreateItem(), options);

        Assert.Contains("--embed-thumbnail", args);
        Assert.Contains("--write-thumbnail", args);
        Assert.Contains("--embed-metadata", args);
        var index = args.IndexOf("--convert-thumbnails");
        Assert.True(index >= 0);
        Assert.Equal("jpg", args[index + 1]);
    }

    [Fact]
    public void Build_ContainerUsesMergeAndRemux()
    {
        var options = new DownloadOptions
        {
            VideoContainer = "mkv",
            EmbedMetadata = false,
            EmbedThumbnail = false
        };

        var args = YtDlpArgumentBuilder.Build(CreateItem(), options);

        Assert.Equal(2, args.Count(a => a == "mkv"));
        Assert.Contains("--merge-output-format", args);
        Assert.Contains("--remux-video", args);
    }

    [Fact]
    public void Build_SubtitleOptions()
    {
        var options = new DownloadOptions
        {
            WriteSubtitles = true,
            EmbedSubtitles = true,
            SubtitleLanguages = "fr,en",
            EmbedMetadata = false,
            EmbedThumbnail = false
        };

        var args = YtDlpArgumentBuilder.Build(CreateItem(), options);

        Assert.Contains("--write-subs", args);
        Assert.Contains("--embed-subs", args);
        var index = args.IndexOf("--sub-langs");
        Assert.True(index >= 0);
        Assert.Equal("fr,en", args[index + 1]);
    }

    [Fact]
    public void Build_OutputTemplateContainsPlaylistFolder()
    {
        var options = new DownloadOptions { CreatePlaylistFolder = true };
        var args = YtDlpArgumentBuilder.Build(CreateItem(), options);

        var index = args.IndexOf("-o");
        Assert.True(index >= 0);
        Assert.Equal(Path.Combine("My playlist", "02 - My video") + ".%(ext)s", args[index + 1]);
    }
}
