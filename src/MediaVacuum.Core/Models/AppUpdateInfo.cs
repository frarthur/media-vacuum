namespace MediaVacuum.Core.Models;

public record AppUpdateInfo(
    string Version,
    string Tag,
    string DownloadUrl,
    string AssetName,
    bool IsZip,
    string? Sha256,
    string? ReleaseNotesUrl);
