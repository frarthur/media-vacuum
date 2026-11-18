namespace MediaVacuum.Core.Models;

public class DownloadProgress
{
    public double? Percentage { get; set; }

    public long? DownloadedBytes { get; set; }

    public long? TotalBytes { get; set; }

    public long? TotalBytesEstimate { get; set; }

    /// <summary>Bytes per second.</summary>
    public double? SpeedBytesPerSecond { get; set; }

    public double? EtaSeconds { get; set; }

    /// <summary>downloading or postprocessing.</summary>
    public string? Status { get; set; }

    public string? Title { get; set; }
}
