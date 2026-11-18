using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MediaVacuum.Core.Models;

namespace MediaVacuum.Core;

public class AppConfig
{
    public const int CurrentConfigVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public int ConfigVersion { get; set; } = CurrentConfigVersion;

    public string OutputDirectory { get; set; } = GetDefaultOutputDirectory();

    public string SelectedLanguage { get; set; } = "en";

    public AppTheme Theme { get; set; } = AppTheme.System;

    public DownloadMode DownloadMode { get; set; } = DownloadMode.Video;

    public string VideoPreset { get; set; } = "best";

    public string? CustomFormat { get; set; }

    public string VideoContainer { get; set; } = "auto";

    public string AudioFormat { get; set; } = "mp3";

    public bool EmbedMetadata { get; set; } = true;

    public bool EmbedThumbnail { get; set; } = true;

    public bool WriteThumbnail { get; set; }

    public bool WriteSubtitles { get; set; }

    public bool EmbedSubtitles { get; set; }

    public string SubtitleLanguages { get; set; } = "en";

    public bool IncludePlaylist { get; set; } = true;

    public bool CreatePlaylistFolder { get; set; } = true;

    public bool IncludeVideoId { get; set; }

    public int ConcurrentDownloads { get; set; } = 1;

    public int ConcurrentFragments { get; set; } = 1;

    public int LimitRateKb { get; set; }

    public bool ProbeSizes { get; set; } = true;

    public bool AutoCheckUpdates { get; set; } = true;

    public bool AutoUpdateApp { get; set; } = true;

    public bool OverwriteExisting { get; set; }

    public string? FfmpegPath { get; set; }

    public string? LastOutputDirectory { get; set; }

    public static AppConfig Load() => LoadFrom(AppPaths.ConfigPath);

    public static AppConfig LoadFrom(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return CreateDefault();
            }

            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? CreateDefault();
            ApplyLegacySettings(json, config);
            config.Normalize();
            return config;
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to load configuration", ex);
            return CreateDefault();
        }
    }

    public void Save() => SaveTo(AppPaths.ConfigPath);

    public void SaveTo(string path)
    {
        try
        {
            Normalize();
            AppPaths.EnsureDataDir();

            var json = JsonSerializer.Serialize(this, JsonOptions);
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);

            if (File.Exists(path))
            {
                File.Move(tempPath, path, true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to save configuration", ex);
        }
    }

    public string GetEffectiveOutputDirectory() =>
        Environment.ExpandEnvironmentVariables(
            string.IsNullOrWhiteSpace(OutputDirectory) ? GetDefaultOutputDirectory() : OutputDirectory);

    public void Normalize()
    {
        ConfigVersion = CurrentConfigVersion;

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            OutputDirectory = GetDefaultOutputDirectory();
        }

        if (string.IsNullOrWhiteSpace(SelectedLanguage))
        {
            SelectedLanguage = "en";
        }

        if (string.IsNullOrWhiteSpace(VideoPreset))
        {
            VideoPreset = "best";
        }

        if (string.IsNullOrWhiteSpace(VideoContainer))
        {
            VideoContainer = "auto";
        }

        if (string.IsNullOrWhiteSpace(AudioFormat))
        {
            AudioFormat = "mp3";
        }

        if (string.IsNullOrWhiteSpace(SubtitleLanguages))
        {
            SubtitleLanguages = "en";
        }

        ConcurrentDownloads = Math.Clamp(ConcurrentDownloads, 1, 4);
        ConcurrentFragments = Math.Clamp(ConcurrentFragments, 1, 16);
        LimitRateKb = Math.Clamp(LimitRateKb, 0, 10_000_000);
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static AppConfig? FromJson(string json)
    {
        try
        {
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (config is not null)
            {
                ApplyLegacySettings(json, config);
                config.Normalize();
            }

            return config;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Maps settings from v0.1.x (SelectedFormat, ExtractAudio, ...) to the v0.2 model.</summary>
    private static void ApplyLegacySettings(string json, AppConfig config)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("DownloadMode", out _))
            {
                return;
            }

            var isAudio = root.TryGetProperty("ExtractAudio", out var extractAudio)
                && extractAudio.ValueKind == JsonValueKind.True;

            if (isAudio)
            {
                config.DownloadMode = DownloadMode.Audio;

                if (root.TryGetProperty("SelectedAudioFormat", out var audioFormat)
                    && audioFormat.GetString() is { Length: > 0 } format)
                {
                    config.AudioFormat = format.ToLowerInvariant();
                }
            }

            if (root.TryGetProperty("SelectedFormat", out var selectedFormat)
                && selectedFormat.GetString() is { Length: > 0 } legacyFormat)
            {
                switch (legacyFormat)
                {
                    case "bestaudio":
                        config.DownloadMode = DownloadMode.Audio;
                        break;
                    case "worst":
                        config.CustomFormat = "worst";
                        break;
                }
            }
        }
        catch
        {
            // The main loader already handles invalid JSON.
        }
    }

    private static AppConfig CreateDefault()
    {
        var config = new AppConfig
        {
            SelectedLanguage = GetDefaultLanguage()
        };

        config.Normalize();
        return config;
    }

    private static string GetDefaultOutputDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "MediaVacuum");

    private static string GetDefaultLanguage()
    {
        var twoLetters = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Utilities.LanguageCatalog.All.Any(l => l.Code == twoLetters) ? twoLetters : "en";
    }
}
