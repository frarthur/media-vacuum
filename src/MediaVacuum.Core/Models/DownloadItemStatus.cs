namespace MediaVacuum.Core.Models;

public enum DownloadItemStatus
{
    Pending,
    Analyzing,
    Ready,
    Downloading,
    PostProcessing,
    Completed,
    Failed,
    Cancelled,
    Skipped
}
