using MediaVacuum.Core.Models;

namespace MediaVacuum.Core.Interfaces;

public interface IJsRuntimeService
{
    JsRuntimeInfo? Runtime { get; }

    bool IsAvailable { get; }

    Task DetectAsync(CancellationToken ct = default);

    /// <summary>Value for yt-dlp --js-runtimes, or null when no runtime is available.</summary>
    string? BuildYtDlpArgument();

    Task DownloadDenoAsync(IProgress<double>? progress = null, CancellationToken ct = default);
}
