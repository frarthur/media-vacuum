using MediaVacuum.Core.Models;

namespace MediaVacuum.Core.Interfaces;

public interface IYtDlpService
{
    string YtDlpPath { get; }

    bool Exists { get; }

    Task<MediaInfo?> GetMediaInfoAsync(
        string url,
        bool flatPlaylist,
        bool noPlaylist,
        CancellationToken ct = default);

    Task<DownloadResult> DownloadAsync(
        DownloadItem item,
        DownloadOptions options,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default);

    Task<string> GetVersionAsync(CancellationToken ct = default);

    /// <summary>Kills any yt-dlp processes started by this service (used when closing the app).</summary>
    void KillActiveProcesses();
}
