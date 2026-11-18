using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using MediaVacuum.Core.Models;
using MediaVacuum.Core.Services;
using MediaVacuum.Services;
using MediaVacuum.Utilities;

namespace MediaVacuum.ViewModels;

/// <summary>UI wrapper around a Core <see cref="DownloadItem"/>.</summary>
public class QueueItemViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly LocalizationService _localization;
    private readonly ThumbnailService _thumbnails;
    private readonly SynchronizationContext? _sync;
    private BitmapImage? _thumbnail;
    private bool _disposed;

    public QueueItemViewModel(
        DownloadItem item,
        LocalizationService localization,
        ThumbnailService thumbnails)
    {
        Item = item;
        _localization = localization;
        _thumbnails = thumbnails;
        _sync = SynchronizationContext.Current;

        Item.PropertyChanged += OnItemPropertyChanged;
        _localization.LanguageChanged += OnLanguageChanged;

        _ = LoadThumbnailAsync();
    }

    public DownloadItem Item { get; }

    public string Title => Item.Title;

    public string IndexText => Item.PlaylistIndex is > 0 ? $"{Item.PlaylistIndex:D2}" : string.Empty;

    public string UploaderText => Item.Uploader ?? string.Empty;

    public bool IsSelected
    {
        get => Item.IsSelected;
        set
        {
            if (Item.IsSelected == value)
            {
                return;
            }

            Item.IsSelected = value;
            OnPropertyChanged();
        }
    }

    public DownloadItemStatus Status => Item.Status;

    public string StatusText => _localization[Status switch
    {
        DownloadItemStatus.Pending => "Pending",
        DownloadItemStatus.Analyzing => "AnalyzingShort",
        DownloadItemStatus.Ready => "Ready",
        DownloadItemStatus.Downloading => "Downloading",
        DownloadItemStatus.PostProcessing => "PostProcessing",
        DownloadItemStatus.Completed => "Completed",
        DownloadItemStatus.Failed => "Failed",
        DownloadItemStatus.Cancelled => "Cancelled",
        DownloadItemStatus.Skipped => "Skipped",
        _ => "Pending"
    }];

    public double Progress => Item.Progress;

    public bool IsIndeterminate =>
        Item.Status == DownloadItemStatus.PostProcessing
        || (Item.Status == DownloadItemStatus.Downloading && Item.Progress <= 0 && Item.BestKnownSize is null);

    public string DurationText => Humanize.Duration(Item.DurationSeconds);

    public string SizeText
    {
        get
        {
            var size = Item.BestKnownSize;
            if (size is null or <= 0)
            {
                return "—";
            }

            return (Item.IsSizeEstimated ? "~" : string.Empty) + Humanize.Bytes(size);
        }
    }

    public string SpeedText => Humanize.Speed(Item.SpeedBytesPerSecond);

    public string EtaText => Item.EtaSeconds is > 0
        ? _localization.Format("RemainingFormat", Humanize.Duration(Item.EtaSeconds))
        : string.Empty;

    public string ProgressDetailText
    {
        get
        {
            if (Item.Status == DownloadItemStatus.Failed)
            {
                return string.IsNullOrWhiteSpace(Item.ErrorMessage) ? StatusText : Compact(Item.ErrorMessage!, 120);
            }

            if (Item.Status == DownloadItemStatus.Cancelled || Item.Status == DownloadItemStatus.Skipped)
            {
                return StatusText;
            }

            if (Item.Status == DownloadItemStatus.Completed)
            {
                return string.IsNullOrWhiteSpace(Item.FilePath)
                    ? StatusText
                    : Path.GetFileName(Item.FilePath);
            }

            if (Item.Status == DownloadItemStatus.PostProcessing)
            {
                return StatusText;
            }

            var parts = new List<string>();
            if (Item.Progress > 0)
            {
                parts.Add($"{Item.Progress:F0}%");
            }

            if (Item.SpeedBytesPerSecond is > 0)
            {
                parts.Add(Humanize.Speed(Item.SpeedBytesPerSecond));
            }

            if (Item.EtaSeconds is > 0)
            {
                parts.Add(_localization.Format("RemainingFormat", Humanize.Duration(Item.EtaSeconds)));
            }

            return parts.Count > 0 ? string.Join(" • ", parts) : StatusText;
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(Item.ErrorMessage);

    public string? ErrorMessage
    {
        get
        {
            var error = Item.ErrorMessage;
            if (string.IsNullOrWhiteSpace(error))
            {
                return null;
            }

            if (error.Contains("403", StringComparison.Ordinal)
                || error.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
            {
                return error + Environment.NewLine + _localization["Error403Hint"];
            }

            if (error.Contains("JavaScript runtime", StringComparison.OrdinalIgnoreCase))
            {
                return error + Environment.NewLine + _localization["JsRuntimeMissing"];
            }

            return error;
        }
    }

    public bool CanOpenFile =>
        !string.IsNullOrWhiteSpace(Item.FilePath) && File.Exists(Item.FilePath);

    public bool CanCancel => Item.IsBusy;

    public bool IsFinished => Item.IsFinished;

    public BitmapImage? Thumbnail
    {
        get => _thumbnail;
        private set
        {
            _thumbnail = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasThumbnail));
        }
    }

    public bool HasThumbnail => _thumbnail is not null;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ProgressDetailText));
        OnPropertyChanged(nameof(EtaText));
        OnPropertyChanged(nameof(ErrorMessage));
    }

    /// <summary>Re-evaluates the status brush after a theme change.</summary>
    public void RefreshTheme() => OnPropertyChanged(nameof(Status));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Item.PropertyChanged -= OnItemPropertyChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
    }

    private async Task LoadThumbnailAsync()
    {
        if (string.IsNullOrWhiteSpace(Item.ThumbnailUrl))
        {
            return;
        }

        var image = await _thumbnails.GetAsync(Item.ThumbnailUrl).ConfigureAwait(false);
        if (image is null || _disposed)
        {
            return;
        }

        void Apply()
        {
            if (!_disposed)
            {
                Thumbnail = image;
            }
        }

        if (_sync is null)
        {
            Apply();
        }
        else
        {
            _sync.Post(_ => Apply(), null);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RefreshLocalization();

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(IsIndeterminate));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(SpeedText));
        OnPropertyChanged(nameof(EtaText));
        OnPropertyChanged(nameof(ProgressDetailText));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(CanOpenFile));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(IsFinished));
    }

    private static string Compact(string value, int maxLength)
    {
        value = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }

    public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
