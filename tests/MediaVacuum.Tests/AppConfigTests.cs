using MediaVacuum.Core;
using MediaVacuum.Core.Models;

namespace MediaVacuum.Tests;

public class AppConfigTests
{
    [Fact]
    public void JsonRoundTrip_PreservesValues()
    {
        var config = new AppConfig
        {
            OutputDirectory = @"C:\Media",
            SelectedLanguage = "fr",
            Theme = AppTheme.Dark,
            DownloadMode = DownloadMode.Audio,
            VideoPreset = "1080p",
            AudioFormat = "flac",
            EmbedThumbnail = false,
            ConcurrentDownloads = 3,
            LimitRateKb = 1500
        };

        var json = config.ToJson();
        var restored = AppConfig.FromJson(json);

        Assert.NotNull(restored);
        Assert.Equal(@"C:\Media", restored!.OutputDirectory);
        Assert.Equal("fr", restored.SelectedLanguage);
        Assert.Equal(AppTheme.Dark, restored.Theme);
        Assert.Equal(DownloadMode.Audio, restored.DownloadMode);
        Assert.Equal("1080p", restored.VideoPreset);
        Assert.Equal("flac", restored.AudioFormat);
        Assert.False(restored.EmbedThumbnail);
        Assert.Equal(3, restored.ConcurrentDownloads);
        Assert.Equal(1500, restored.LimitRateKb);
    }

    [Fact]
    public void Normalize_ClampsOutOfRangeValues()
    {
        var config = new AppConfig
        {
            ConcurrentDownloads = 99,
            ConcurrentFragments = 0,
            LimitRateKb = -5
        };

        config.Normalize();

        Assert.Equal(4, config.ConcurrentDownloads);
        Assert.Equal(1, config.ConcurrentFragments);
        Assert.Equal(0, config.LimitRateKb);
    }

    [Fact]
    public void FromJson_ReturnsNullForInvalidJson()
    {
        Assert.Null(AppConfig.FromJson("{ not valid json"));
    }

    [Fact]
    public void FromJson_MigratesLegacyAudioSettings()
    {
        const string json = """
        {
          "OutputDirectory": "C:\\Media",
          "SelectedFormat": "bestaudio",
          "ExtractAudio": true,
          "SelectedAudioFormat": "flac",
          "SelectedLanguage": "fr"
        }
        """;

        var config = AppConfig.FromJson(json);

        Assert.NotNull(config);
        Assert.Equal(DownloadMode.Audio, config!.DownloadMode);
        Assert.Equal("flac", config.AudioFormat);
        Assert.Equal(@"C:\Media", config.OutputDirectory);
        Assert.Equal("fr", config.SelectedLanguage);
        Assert.Equal(AppConfig.CurrentConfigVersion, config.ConfigVersion);
    }
}
