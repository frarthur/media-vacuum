using System.Globalization;
using MediaVacuum.Core.Models;

namespace MediaVacuum.Core.Services;

public static class ProgressParser
{
    public static ParsedProgressLine? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        if (line.StartsWith("MVPROG|", StringComparison.Ordinal))
        {
            var parts = line.Split('|');
            if (parts.Length < 7)
            {
                return null;
            }

            return new ParsedProgressLine
            {
                Kind = ProgressLineKind.Download,
                Progress = new DownloadProgress
                {
                    Status = "downloading",
                    DownloadedBytes = ParseLong(parts[1]),
                    TotalBytes = ParseLong(parts[2]),
                    TotalBytesEstimate = ParseLong(parts[3]),
                    Percentage = ParseDouble(parts[4]),
                    SpeedBytesPerSecond = ParseDouble(parts[5]),
                    EtaSeconds = ParseDouble(parts[6])
                }
            };
        }

        if (line.StartsWith("MVPOST|", StringComparison.Ordinal))
        {
            var status = line["MVPOST|".Length..].Trim();
            return new ParsedProgressLine
            {
                Kind = ProgressLineKind.PostProcessing,
                Progress = new DownloadProgress { Status = "postprocessing" },
                PostProcessor = status
            };
        }

        if (line.StartsWith("MVNAME|", StringComparison.Ordinal))
        {
            return new ParsedProgressLine
            {
                Kind = ProgressLineKind.FileName,
                FilePath = ParsePath(line["MVNAME|".Length..])
            };
        }

        if (line.StartsWith("MVFILE|", StringComparison.Ordinal))
        {
            var parts = line.Split('|');
            return new ParsedProgressLine
            {
                Kind = ProgressLineKind.FileCompleted,
                FilePath = parts.Length > 1 ? ParsePath(parts[1]) : null,
                FileSize = parts.Length > 2 ? ParseLong(parts[2]) : null
            };
        }

        if (line.Contains("has already been downloaded", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedProgressLine { Kind = ProgressLineKind.AlreadyDownloaded };
        }

        return null;
    }

    public static long? ParseLong(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Equals("NA", StringComparison.OrdinalIgnoreCase)
            || value.Equals("None", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    public static string? ParsePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Equals("NA", StringComparison.OrdinalIgnoreCase)
            || value.Equals("None", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.Trim();
    }

    public static double? ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Equals("NA", StringComparison.OrdinalIgnoreCase)
            || value.Equals("None", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }
}

public enum ProgressLineKind
{
    Download,
    PostProcessing,
    FileName,
    FileCompleted,
    AlreadyDownloaded
}

public class ParsedProgressLine
{
    public ProgressLineKind Kind { get; set; }

    public DownloadProgress? Progress { get; set; }

    public string? FilePath { get; set; }

    public long? FileSize { get; set; }

    public string? PostProcessor { get; set; }
}
