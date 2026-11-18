using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MediaVacuum.Core.Models;

/// <summary>
/// One downloadable media entry (a single video, or one entry of a playlist/channel).
/// The UI wraps this model to add formatting, localization and thumbnails.
/// </summary>
public class DownloadItem : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _url = string.Empty;
    private string? _thumbnailUrl;
    private double? _durationSeconds;
    private int? _playlistIndex;
    private string? _playlistTitle;
    private string? _uploader;
    private bool _isSelected = true;
    private DownloadItemStatus _status = DownloadItemStatus.Pending;
    private double _progress;
    private long? _downloadedBytes;
    private long? _totalBytes;
    private long? _totalBytesEstimate;
    private double? _speedBytesPerSecond;
    private double? _etaSeconds;
    private string? _filePath;
    private long? _fileSize;
    private string? _errorMessage;
    private bool _isSizeEstimated;

    public string Id { get; set; } = string.Empty;

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public string Url
    {
        get => _url;
        set => Set(ref _url, value);
    }

    public string? ThumbnailUrl
    {
        get => _thumbnailUrl;
        set => Set(ref _thumbnailUrl, value);
    }

    public double? DurationSeconds
    {
        get => _durationSeconds;
        set => Set(ref _durationSeconds, value);
    }

    public int? PlaylistIndex
    {
        get => _playlistIndex;
        set => Set(ref _playlistIndex, value);
    }

    public string? PlaylistTitle
    {
        get => _playlistTitle;
        set => Set(ref _playlistTitle, value);
    }

    public string? PlaylistUrl { get; set; }

    public string? Uploader
    {
        get => _uploader;
        set => Set(ref _uploader, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public DownloadItemStatus Status
    {
        get => _status;
        set
        {
            if (!Set(ref _status, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsFinished));
            OnPropertyChanged(nameof(IsFailed));
        }
    }

    public double Progress
    {
        get => _progress;
        set => Set(ref _progress, value);
    }

    public long? DownloadedBytes
    {
        get => _downloadedBytes;
        set => Set(ref _downloadedBytes, value);
    }

    public long? TotalBytes
    {
        get => _totalBytes;
        set => Set(ref _totalBytes, value);
    }

    /// <summary>Approximate size reported by yt-dlp when the exact size is unknown.</summary>
    public long? TotalBytesEstimate
    {
        get => _totalBytesEstimate;
        set => Set(ref _totalBytesEstimate, value);
    }

    public double? SpeedBytesPerSecond
    {
        get => _speedBytesPerSecond;
        set => Set(ref _speedBytesPerSecond, value);
    }

    public double? EtaSeconds
    {
        get => _etaSeconds;
        set => Set(ref _etaSeconds, value);
    }

    public string? FilePath
    {
        get => _filePath;
        set => Set(ref _filePath, value);
    }

    public long? FileSize
    {
        get => _fileSize;
        set => Set(ref _fileSize, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => Set(ref _errorMessage, value);
    }

    /// <summary>True when the size is a rough estimate rather than a value reported by yt-dlp.</summary>
    public bool IsSizeEstimated
    {
        get => _isSizeEstimated;
        set => Set(ref _isSizeEstimated, value);
    }

    public bool IsBusy => Status is DownloadItemStatus.Analyzing
        or DownloadItemStatus.Downloading
        or DownloadItemStatus.PostProcessing;

    public bool IsFinished => Status is DownloadItemStatus.Completed
        or DownloadItemStatus.Failed
        or DownloadItemStatus.Cancelled
        or DownloadItemStatus.Skipped;

    public bool IsFailed => Status == DownloadItemStatus.Failed;

    public long? BestKnownSize => FileSize ?? TotalBytes ?? TotalBytesEstimate;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
