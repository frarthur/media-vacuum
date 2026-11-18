using System.Text;
using System.Text.RegularExpressions;
using MediaVacuum.Core.Models;

namespace MediaVacuum.Core.Utilities;

/// <summary>
/// Produces clean, Windows-safe filenames. Invalid characters, control characters,
/// duplicate whitespace and trailing dots/spaces are removed.
/// </summary>
public static partial class FilenameService
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string Sanitize(string? name, int maxLength = 150)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "video";
        }

        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsControl(c))
            {
                continue;
            }

            builder.Append(c is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*'
                ? ' '
                : c);
        }

        var cleaned = WhitespaceRegex().Replace(builder.ToString(), " ").Trim();
        cleaned = cleaned.Trim('.', ' ');

        if (cleaned.Length == 0)
        {
            cleaned = "video";
        }

        if (ReservedNames.Contains(cleaned))
        {
            cleaned = "_" + cleaned;
        }

        if (cleaned.Length > maxLength)
        {
            cleaned = cleaned[..maxLength].TrimEnd('.', ' ');
        }

        return cleaned;
    }

    /// <summary>Builds the base filename (without extension) for a queue item.</summary>
    public static string BuildBaseName(DownloadItem item, DownloadOptions options)
    {
        var title = Sanitize(item.Title, 150);

        if (item.PlaylistIndex is > 0)
        {
            title = $"{item.PlaylistIndex.Value:D2} - {title}";
        }

        if (options.IncludeVideoId && !string.IsNullOrWhiteSpace(item.Id))
        {
            title += $" [{Sanitize(item.Id, 24)}]";
        }

        return title;
    }

    /// <summary>
    /// Builds the yt-dlp output template (relative to the output directory).
    /// Literal percent signs must be escaped because yt-dlp interprets the template.
    /// </summary>
    public static string BuildOutputTemplate(DownloadItem item, DownloadOptions options)
    {
        var baseName = BuildBaseName(item, options).Replace("%", "%%");

        if (options.CreatePlaylistFolder && !string.IsNullOrWhiteSpace(item.PlaylistTitle))
        {
            var folder = Sanitize(item.PlaylistTitle, 100).Replace("%", "%%");
            return Path.Combine(folder, baseName) + ".%(ext)s";
        }

        return baseName + ".%(ext)s";
    }

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
