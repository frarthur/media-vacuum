namespace MediaVacuum.Core.Models;

public record LanguageOption(string Code, string NativeName);

public enum AppTheme
{
    System,
    Light,
    Dark
}

public record AnalysisResult(
    bool IsPlaylist,
    string Title,
    IReadOnlyList<DownloadItem> Items,
    long? EstimatedTotalBytes,
    bool HasEstimatedSizes);

public record SizeEstimate(long? Bytes, bool IsApproximate);
