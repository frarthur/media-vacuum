using MediaVacuum.Core.Models;

namespace MediaVacuum.Core.Interfaces;

public interface IAppUpdateService
{
    Version CurrentVersion { get; }

    Task<AppUpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>Downloads the release asset and returns the path to the new MediaVacuum.exe.</summary>
    Task<string> DownloadAsync(
        AppUpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken ct = default);

    /// <summary>Starts a helper process that replaces the current executable and relaunches it.</summary>
    void LaunchUpdater(string downloadedExePath);
}
