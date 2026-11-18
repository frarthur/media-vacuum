using MediaVacuum.Core.Models;

namespace MediaVacuum.Core.Interfaces;

public interface IUpdateService
{
    string CurrentVersion { get; }

    string? LatestVersion { get; }

    bool Exists();

    Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default);

    Task UpdateAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken ct = default);

    Task EnsureDownloadedAsync(IProgress<double>? progress = null, CancellationToken ct = default);

    Task<string> GetCurrentVersionAsync(CancellationToken ct = default);
}
