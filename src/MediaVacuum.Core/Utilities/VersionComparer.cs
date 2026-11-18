namespace MediaVacuum.Core.Utilities;

public static class VersionComparer
{
    /// <summary>Returns true when <paramref name="candidate"/> is newer than <paramref name="current"/>.</summary>
    public static bool IsNewer(string? candidate, string? current)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(current) || current == "0.0.0")
        {
            return true;
        }

        var normalizedCandidate = Normalize(candidate);
        var normalizedCurrent = Normalize(current);

        if (string.Equals(normalizedCandidate, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Compare(normalizedCandidate, normalizedCurrent) > 0;
    }

    public static int Compare(string? left, string? right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);

        if (Version.TryParse(normalizedLeft, out var a) && Version.TryParse(normalizedRight, out var b))
        {
            return a.CompareTo(b);
        }

        return string.Compare(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "0.0.0";
        }

        return value.Trim().TrimStart('v', 'V');
    }
}
