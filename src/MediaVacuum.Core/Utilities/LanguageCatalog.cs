using System.Globalization;
using MediaVacuum.Core.Models;

namespace MediaVacuum.Core.Utilities;

public static class LanguageCatalog
{
    public static readonly LanguageOption[] All =
    [
        new("en", "English"),
        new("fr", "Français"),
        new("es", "Español"),
        new("de", "Deutsch"),
        new("ru", "Русский")
    ];

    public static bool IsSupported(string? code) =>
        !string.IsNullOrWhiteSpace(code) && All.Any(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "en";
        }

        var match = All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));
        return match?.Code ?? "en";
    }

    public static LanguageOption Get(string? code) =>
        All.FirstOrDefault(l => string.Equals(l.Code, Normalize(code), StringComparison.OrdinalIgnoreCase)) ?? All[0];

    public static string GetOperatingSystemDefault()
    {
        var twoLetters = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return IsSupported(twoLetters) ? twoLetters : "en";
    }
}
