namespace MediaVacuum.Core.Services;

/// <summary>
/// Extracts actionable yt-dlp errors and drops the warning noise (old version,
/// JavaScript runtime warnings, thumbnail container warnings, ...).
/// </summary>
public static class YtDlpErrorParser
{
    private const int MaxLength = 2000;

    public static string Parse(string? stderr, string? fallback = null)
    {
        var combined = string.Join(
            "\n",
            new[] { stderr, fallback }.Where(value => !string.IsNullOrWhiteSpace(value)));

        var lines = combined
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        if (lines.Count == 0)
        {
            return "yt-dlp failed.";
        }

        var errors = lines
            .Where(line => line.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
            .Select(StripErrorPrefix)
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (errors.Count == 0)
        {
            errors = lines
                .Where(line => !line.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase))
                .Where(line => !line.StartsWith("[download]", StringComparison.OrdinalIgnoreCase))
                .TakeLast(5)
                .ToList();
        }

        if (errors.Count == 0)
        {
            errors = lines.TakeLast(5).ToList();
        }

        var message = string.Join(Environment.NewLine, errors);
        return message.Length <= MaxLength ? message : message[^MaxLength..];
    }

    private static string StripErrorPrefix(string line)
    {
        const string prefix = "ERROR:";
        return line.Length > prefix.Length
            ? line[prefix.Length..].Trim()
            : line;
    }
}
