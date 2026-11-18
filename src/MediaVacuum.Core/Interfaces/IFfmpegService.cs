namespace MediaVacuum.Core.Interfaces;

public interface IFfmpegService
{
    string? FfmpegPath { get; }

    string? FfprobePath { get; }

    bool IsAvailable { get; }

    string? Version { get; }

    Task DetectAsync(CancellationToken ct = default);

    Task<string?> GetVersionAsync(CancellationToken ct = default);

    Task DownloadAsync(IProgress<double>? progress = null, CancellationToken ct = default);
}
