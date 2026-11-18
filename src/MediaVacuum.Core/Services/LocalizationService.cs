using System.Reflection;
using System.Text.Json;
using MediaVacuum.Core.Models;
using MediaVacuum.Core.Utilities;

namespace MediaVacuum.Core.Services;

/// <summary>
/// Loads the translation catalogue embedded in the Core assembly. A user file in
/// %LOCALAPPDATA%\MediaVacuum\Translations\&lt;lang&gt;.json can override individual keys.
/// </summary>
public sealed class LocalizationService
{
    private const string DefaultLanguage = "en";
    private const string ResourcePrefix = "MediaVacuum.Core.Translations.";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly Dictionary<string, Dictionary<string, string>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Assembly _assembly = typeof(LocalizationService).Assembly;
    private Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
    private string _currentLanguage = DefaultLanguage;

    public LocalizationService()
    {
        _currentLanguage = DefaultLanguage;
        LoadLanguage(DefaultLanguage, raiseEvent: false);
    }

    public IReadOnlyList<LanguageOption> AvailableLanguages => LanguageCatalog.All;

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set => SetLanguage(value);
    }

    public string this[string key] =>
        _strings.TryGetValue(key, out var value)
            ? value
            : TryGetDefault(key) ?? key;

    public event EventHandler? LanguageChanged;

    public void SetLanguage(string? language, bool raiseEvent = true)
    {
        var normalized = LanguageCatalog.Normalize(language);
        if (string.Equals(_currentLanguage, normalized, StringComparison.OrdinalIgnoreCase)
            && _strings.Count > 0)
        {
            return;
        }

        _currentLanguage = normalized;
        LoadLanguage(normalized, raiseEvent);
    }

    public string Format(string key, params object?[] args)
    {
        var template = this[key];
        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    /// <summary>Used by tests to verify that every language has the same keys.</summary>
    public IReadOnlyDictionary<string, string> GetStrings(string language)
    {
        var normalized = LanguageCatalog.Normalize(language);
        if (_cache.TryGetValue(normalized, out var cached))
        {
            return cached;
        }

        var dictionary = LoadEmbedded(normalized) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _cache[normalized] = dictionary;
        return dictionary;
    }

    private void LoadLanguage(string language, bool raiseEvent)
    {
        var normalized = LanguageCatalog.Normalize(language);
        var strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var english = GetStrings(DefaultLanguage);
        foreach (var pair in english)
        {
            strings[pair.Key] = pair.Value;
        }

        if (!string.Equals(normalized, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
        {
            var localized = GetStrings(normalized);
            foreach (var pair in localized)
            {
                strings[pair.Key] = pair.Value;
            }
        }

        var overridePath = Path.Combine(AppPaths.TranslationsDir, $"{normalized}.json");
        if (File.Exists(overridePath))
        {
            try
            {
                var overrideStrings = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(overridePath), JsonOptions);

                if (overrideStrings is not null)
                {
                    // Legacy versions copied the whole catalogue to LocalAppData.
                    // Only merge files explicitly marked as user overrides, otherwise
                    // they would permanently shadow updated embedded translations.
                    var isOptIn = overrideStrings.TryGetValue("_overrides", out var marker)
                        && bool.TryParse(marker, out var enabled)
                        && enabled;

                    if (!isOptIn)
                    {
                        AppLog.Info($"Ignoring legacy translation file (missing \"_overrides\": true): {overridePath}");
                    }
                    else
                    {
                        foreach (var pair in overrideStrings)
                        {
                            if (!string.IsNullOrWhiteSpace(pair.Key)
                                && !pair.Key.Equals("_overrides", StringComparison.OrdinalIgnoreCase)
                                && pair.Value is not null)
                            {
                                strings[pair.Key] = pair.Value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Exception($"Invalid translation override: {overridePath}", ex);
            }
        }

        _strings = strings;

        if (raiseEvent)
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private Dictionary<string, string>? LoadEmbedded(string language)
    {
        var resourceName = $"{ResourcePrefix}{language}.json";
        try
        {
            using var stream = _assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                AppLog.Warn($"Translation resource not found: {resourceName}");
                return null;
            }

            using var reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd(), JsonOptions)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            AppLog.Exception($"Unable to load translation resource: {resourceName}", ex);
            return null;
        }
    }

    private string? TryGetDefault(string key)
    {
        if (string.Equals(_currentLanguage, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return GetStrings(DefaultLanguage).TryGetValue(key, out var value) ? value : null;
    }
}
