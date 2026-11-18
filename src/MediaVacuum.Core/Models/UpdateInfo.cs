namespace MediaVacuum.Core.Models;

public record UpdateInfo(
    string Version,
    string DownloadUrl,
    string? Sha256,
    string? ReleaseNotesUrl,
    string? AssetName = null);
