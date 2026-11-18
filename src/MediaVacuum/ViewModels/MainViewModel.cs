using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MediaVacuum.Core;
using MediaVacuum.Core.Interfaces;
using MediaVacuum.Core.Models;
using MediaVacuum.Core.Services;
using MediaVacuum.Core.Utilities;
using MediaVacuum.Services;
using MediaVacuum.Utilities;

namespace MediaVacuum.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly AppConfig _config;
    private readonly LocalizationService _localization;
    private readonly IYtDlpService _ytDlp;
    private readonly IUpdateService _updateService;
    private readonly IFfmpegService _ffmpeg;
    private readonly IJsRuntimeService _jsRuntime;
    private readonly IContextMenuService _contextMenu;
    private readonly IInstallerService _installer;
    private readonly IDialogService _dialog;
    private readonly IThemeService _theme;
    private readonly DownloadQueueService _queueService;
    private readonly ThumbnailService _thumbnails;
    private readonly IAppUpdateService _appUpdate;

    private string _url = string.Empty;
    private string _statusKey = "Ready";
    private object?[] _statusArgs = [];
    private bool _isAnalyzing;
    private bool _isDownloading;
    private bool _isInitializing = true;
    private bool _isFfmpegBusy;
    private bool _isDenoBusy;
    private bool _isAppUpdating;
    private AppUpdateInfo? _appUpdateInfo;
    private bool _isOverallIndeterminate;
    private double _overallProgress;
    private string _progressText = string.Empty;
    private string _ytDlpVersion = "—";
    private string? _ffmpegVersion;
    private string? _updateVersion;
    private bool _contextMenuInstalled;
    private CancellationTokenSource? _analyzeCts;
    private CancellationTokenSource? _probeCts;
    private CancellationTokenSource? _downloadCts;

    public MainViewModel(
        AppConfig config,
        LocalizationService localization,
        IYtDlpService ytDlp,
        IUpdateService updateService,
        IFfmpegService ffmpeg,
        IJsRuntimeService jsRuntime,
        IContextMenuService contextMenu,
        IInstallerService installer,
        IDialogService dialog,
        IThemeService theme,
        DownloadQueueService queueService,
        ThumbnailService thumbnails,
        IAppUpdateService appUpdate)
    {
        _config = config;
        _localization = localization;
        _ytDlp = ytDlp;
        _updateService = updateService;
        _ffmpeg = ffmpeg;
        _jsRuntime = jsRuntime;
        _contextMenu = contextMenu;
        _installer = installer;
        _dialog = dialog;
        _theme = theme;
        _queueService = queueService;
        _thumbnails = thumbnails;
        _appUpdate = appUpdate;

        _localization.SetLanguage(_config.SelectedLanguage, raiseEvent: false);
        T = new Translation(_localization);

        Queue.CollectionChanged += (_, _) => UpdateSummary();

        AnalyzeCommand = new AsyncRelayCommand(_ => AnalyzeAsync(), _ => CanAnalyze);
        StartDownloadCommand = new AsyncRelayCommand(_ => StartDownloadAsync(), _ => CanDownload);
        CancelCommand = new RelayCommand(_ => CancelAll(), _ => IsCancellable);
        ClearQueueCommand = new RelayCommand(_ => ClearQueue(), _ => Queue.Count > 0);
        SelectAllCommand = new RelayCommand(_ => SetSelection(true), _ => Queue.Count > 0);
        DeselectAllCommand = new RelayCommand(_ => SetSelection(false), _ => Queue.Count > 0);
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput());
        OpenOutputCommand = new RelayCommand(_ => _dialog.OpenFolder(OutputDirectory));
        RemoveItemCommand = new RelayCommand(p => RemoveItem(p as QueueItemViewModel), p => p is QueueItemViewModel);
        OpenFileCommand = new RelayCommand(p => OpenFile(p as QueueItemViewModel), p => p is QueueItemViewModel { CanOpenFile: true });
        OpenFileLocationCommand = new RelayCommand(p => OpenFileLocation(p as QueueItemViewModel), p => p is QueueItemViewModel { CanOpenFile: true });
        InstallContextMenuCommand = new RelayCommand(_ => InstallContextMenu());
        UninstallContextMenuCommand = new RelayCommand(_ => UninstallContextMenu());
        CheckUpdateCommand = new AsyncRelayCommand(_ => CheckUpdateAsync(), _ => !IsBusy);
        DownloadFfmpegCommand = new AsyncRelayCommand(_ => DownloadFfmpegAsync(prompt: true), _ => !_isFfmpegBusy && !IsDownloading);
        DownloadDenoCommand = new AsyncRelayCommand(_ => DownloadDenoAsync(), _ => !_isDenoBusy && !IsDownloading);
        InstallAppUpdateCommand = new AsyncRelayCommand(_ => InstallAppUpdateAsync(manual: true), _ => CanInstallAppUpdate);
        RetryYtDlpCommand = new AsyncRelayCommand(_ => EnsureYtDlpAsync(), _ => !IsBusy);
        OpenLogsCommand = new RelayCommand(_ => _dialog.OpenFolder(AppPaths.LogsDir));
        UninstallAppCommand = new RelayCommand(_ => UninstallApp());

        _localization.LanguageChanged += (_, _) => RefreshLocalizedState();
        _theme.ThemeChanged += (_, _) =>
        {
            foreach (var item in Queue)
            {
                item.RefreshTheme();
            }
        };

        _ = InitializeAsync();
    }

    public Translation T { get; }

    public ObservableCollection<QueueItemViewModel> Queue { get; } = [];

    public IReadOnlyList<LanguageOption> LanguageOptions => _localization.AvailableLanguages;

    public IReadOnlyList<int> ConcurrencyOptions { get; } = [1, 2, 3, 4];

    public IReadOnlyList<int> FragmentOptions { get; } = [1, 2, 4, 8, 16];

    public string[] AudioFormats => FormatSelector.AudioFormats;

    public string Url
    {
        get => _url;
        set
        {
            if (_url == value)
            {
                return;
            }

            _url = value;
            OnPropertyChanged();
            NotifyCommands();
        }
    }

    public string Status => _localization.Format(_statusKey, _statusArgs);

    public string OutputDirectory
    {
        get => _config.GetEffectiveOutputDirectory();
        set
        {
            if (string.Equals(_config.OutputDirectory, value, StringComparison.Ordinal))
            {
                return;
            }

            _config.OutputDirectory = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public LanguageOption SelectedLanguage
    {
        get => LanguageCatalog.Get(_config.SelectedLanguage);
        set
        {
            if (value is null || string.Equals(value.Code, _config.SelectedLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _config.SelectedLanguage = value.Code;
            _localization.SetLanguage(value.Code);
            SaveConfig();
            OnPropertyChanged();
            OnPropertyChanged(nameof(T));
        }
    }

    public string SelectedTheme
    {
        get => _config.Theme.ToString();
        set
        {
            if (!Enum.TryParse<AppTheme>(value, ignoreCase: true, out var mode) || _config.Theme == mode)
            {
                return;
            }

            _config.Theme = mode;
            _theme.Apply(mode);
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public bool IsVideoMode
    {
        get => _config.DownloadMode == DownloadMode.Video;
        set
        {
            if (!value || _config.DownloadMode == DownloadMode.Video)
            {
                return;
            }

            _config.DownloadMode = DownloadMode.Video;
            SaveConfig();
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAudioMode));
        }
    }

    public bool IsAudioMode
    {
        get => _config.DownloadMode == DownloadMode.Audio;
        set
        {
            if (!value || _config.DownloadMode == DownloadMode.Audio)
            {
                return;
            }

            _config.DownloadMode = DownloadMode.Audio;
            SaveConfig();
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsVideoMode));
        }
    }

    public string VideoPreset
    {
        get => _config.VideoPreset;
        set
        {
            if (string.Equals(_config.VideoPreset, value, StringComparison.Ordinal))
            {
                return;
            }

            _config.VideoPreset = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public string? CustomFormat
    {
        get => _config.CustomFormat;
        set
        {
            if (string.Equals(_config.CustomFormat, value, StringComparison.Ordinal))
            {
                return;
            }

            _config.CustomFormat = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public string VideoContainer
    {
        get => _config.VideoContainer;
        set
        {
            if (string.Equals(_config.VideoContainer, value, StringComparison.Ordinal))
            {
                return;
            }

            _config.VideoContainer = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public string AudioFormat
    {
        get => _config.AudioFormat;
        set
        {
            if (string.Equals(_config.AudioFormat, value, StringComparison.Ordinal))
            {
                return;
            }

            _config.AudioFormat = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public bool EmbedMetadata
    {
        get => _config.EmbedMetadata;
        set
        {
            if (_config.EmbedMetadata == value) return;
            _config.EmbedMetadata = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public bool EmbedThumbnail
    {
        get => _config.EmbedThumbnail;
        set
        {
            if (_config.EmbedThumbnail == value) return;
            _config.EmbedThumbnail = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public bool WriteThumbnail
    {
        get => _config.WriteThumbnail;
        set
        {
            if (_config.WriteThumbnail == value) return;
            _config.WriteThumbnail = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public bool WriteSubtitles
    {
        get => _config.WriteSubtitles;
        set
        {
            if (_config.WriteSubtitles == value) return;
            _config.WriteSubtitles = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public bool EmbedSubtitles
    {
        get => _config.EmbedSubtitles;
        set
        {
            if (_config.EmbedSubtitles == value) return;
            _config.EmbedSubtitles = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public string SubtitleLanguages
    {
        get => _config.SubtitleLanguages;
        set
        {
            if (string.Equals(_config.SubtitleLanguages, value, StringComparison.Ordinal)) return;
            _config.SubtitleLanguages = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public bool IncludePlaylist
    {
        get => _config.IncludePlaylist;
        set
        {
            if (_config.IncludePlaylist == value) return;
            _config.IncludePlaylist = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public bool CreatePlaylistFolder
    {
        get => _config.CreatePlaylistFolder;
        set
        {
            if (_config.CreatePlaylistFolder == value) return;
            _config.CreatePlaylistFolder = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public bool IncludeVideoId
    {
        get => _config.IncludeVideoId;
        set
        {
            if (_config.IncludeVideoId == value) return;
            _config.IncludeVideoId = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public int ConcurrentDownloads
    {
        get => _config.ConcurrentDownloads;
        set
        {
            if (_config.ConcurrentDownloads == value) return;
            _config.ConcurrentDownloads = Math.Clamp(value, 1, 4);
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public int ConcurrentFragments
    {
        get => _config.ConcurrentFragments;
        set
        {
            if (_config.ConcurrentFragments == value) return;
            _config.ConcurrentFragments = Math.Clamp(value, 1, 16);
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public string LimitRateText
    {
        get => _config.LimitRateKb <= 0 ? string.Empty : _config.LimitRateKb.ToString(CultureInfo.CurrentCulture);
        set
        {
            var parsed = 0;
            if (!string.IsNullOrWhiteSpace(value))
            {
                int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed);
            }

            parsed = Math.Clamp(parsed, 0, 10_000_000);
            if (_config.LimitRateKb == parsed) return;
            _config.LimitRateKb = parsed;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public bool ProbeSizes
    {
        get => _config.ProbeSizes;
        set
        {
            if (_config.ProbeSizes == value) return;
            _config.ProbeSizes = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public bool AutoCheckUpdates
    {
        get => _config.AutoCheckUpdates;
        set
        {
            if (_config.AutoCheckUpdates == value) return;
            _config.AutoCheckUpdates = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public bool OverwriteExisting
    {
        get => _config.OverwriteExisting;
        set
        {
            if (_config.OverwriteExisting == value) return;
            _config.OverwriteExisting = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public bool AutoUpdateYtDlp
    {
        get => _config.AutoUpdateYtDlp;
        set
        {
            if (_config.AutoUpdateYtDlp == value) return;
            _config.AutoUpdateYtDlp = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public bool AutoUpdateApp
    {
        get => _config.AutoUpdateApp;
        set
        {
            if (_config.AutoUpdateApp == value) return;
            _config.AutoUpdateApp = value;
            SaveConfig();
            OnPropertyChanged();
        }
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        private set
        {
            if (_isAnalyzing == value) return;
            _isAnalyzing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsCancellable));
            NotifyCommands();
        }
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            if (_isDownloading == value) return;
            _isDownloading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsCancellable));
            NotifyCommands();
        }
    }

    public bool IsBusy => IsAnalyzing || IsDownloading || _isInitializing || _isFfmpegBusy || _isDenoBusy || _isAppUpdating;

    public bool IsCancellable => IsAnalyzing || IsDownloading;

    public bool CanAnalyze => !IsBusy && !string.IsNullOrWhiteSpace(Url) && _ytDlp.Exists;

    public bool CanDownload => !IsBusy && Queue.Any(i => i.IsSelected && i.Item.Status != DownloadItemStatus.Completed);

    public bool HasQueue => Queue.Count > 0;

    public double OverallProgress
    {
        get => _overallProgress;
        private set
        {
            if (Math.Abs(_overallProgress - value) < 0.01) return;
            _overallProgress = value;
            OnPropertyChanged();
        }
    }

    public bool IsOverallIndeterminate
    {
        get => _isOverallIndeterminate;
        private set
        {
            if (_isOverallIndeterminate == value) return;
            _isOverallIndeterminate = value;
            OnPropertyChanged();
        }
    }

    public string ProgressText
    {
        get => _progressText;
        private set
        {
            if (_progressText == value) return;
            _progressText = value;
            OnPropertyChanged();
        }
    }

    public string YtDlpVersion
    {
        get => _ytDlpVersion;
        private set
        {
            if (_ytDlpVersion == value) return;
            _ytDlpVersion = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsYtDlpReady));
        }
    }

    public bool IsYtDlpReady => _ytDlp.Exists && YtDlpVersion != "0.0.0";

    public bool IsFfmpegAvailable => _ffmpeg.IsAvailable;

    public string FfmpegStatusText
    {
        get
        {
            if (_isFfmpegBusy)
            {
                return _localization["FfmpegDownloading"];
            }

            if (!_ffmpeg.IsAvailable)
            {
                return _localization["FfmpegMissing"];
            }

            var version = ShortFfmpegVersion(_ffmpegVersion);
            return string.IsNullOrWhiteSpace(version)
                ? _localization["FfmpegAvailable"]
                : _localization.Format("FfmpegAvailableFormat", version);
        }
    }

    public bool IsJsRuntimeAvailable => _jsRuntime.IsAvailable;

    public string JsRuntimeStatusText
    {
        get
        {
            if (_isDenoBusy)
            {
                return _localization["DenoDownloading"];
            }

            if (!_jsRuntime.IsAvailable)
            {
                return _localization["JsRuntimeMissing"];
            }

            return _localization.Format("JsRuntimeAvailableFormat", _jsRuntime.Runtime!.Name);
        }
    }

    public bool ContextMenuInstalled
    {
        get => _contextMenuInstalled;
        private set
        {
            if (_contextMenuInstalled == value) return;
            _contextMenuInstalled = value;
            OnPropertyChanged();
        }
    }

    public string? UpdateAvailableVersion => _updateVersion;

    public bool HasUpdate => !string.IsNullOrWhiteSpace(_updateVersion);

    public string UpdateAvailableText => HasUpdate
        ? _localization.Format("UpdateAvailableShort", _updateVersion)
        : string.Empty;

    public bool HasAppUpdate => _appUpdateInfo is not null;

    public string AppUpdateAvailableText => HasAppUpdate
        ? _localization.Format("AppUpdateAvailableShort", _appUpdateInfo!.Version)
        : string.Empty;

    public bool CanInstallAppUpdate => !IsBusy && _appUpdateInfo is not null;

    public string SelectedCountText =>
        _localization.Format("SelectedCountFormat", Queue.Count(i => i.IsSelected), Queue.Count);

    public string QueueCountText => _localization.Format("QueueCountFormat", Queue.Count);

    public string TotalSizeText
    {
        get
        {
            var selected = Queue.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return _localization["SizeUnknown"];
            }

            long total = 0;
            var approximate = false;
            foreach (var item in selected)
            {
                var size = item.Item.BestKnownSize;
                if (size is > 0)
                {
                    total += size.Value;
                    approximate |= item.Item.IsSizeEstimated;
                }
            }

            return total == 0
                ? _localization["SizeUnknown"]
                : (approximate ? "~" : string.Empty) + Humanize.Bytes(total);
        }
    }

    public string QueueSummary => _localization.Format(
        "QueueSummaryFormat",
        Queue.Count,
        TotalSizeText);

    public ICommand AnalyzeCommand { get; }
    public ICommand StartDownloadCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearQueueCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand OpenOutputCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand OpenFileLocationCommand { get; }
    public ICommand InstallContextMenuCommand { get; }
    public ICommand UninstallContextMenuCommand { get; }
    public ICommand CheckUpdateCommand { get; }
    public ICommand DownloadFfmpegCommand { get; }
    public ICommand DownloadDenoCommand { get; }
    public ICommand InstallAppUpdateCommand { get; }
    public ICommand RetryYtDlpCommand { get; }
    public ICommand OpenLogsCommand { get; }
    public ICommand UninstallAppCommand { get; }

    public async Task InitializeAsync()
    {
        _isInitializing = true;
        NotifyCommands();

        try
        {
            if (!_ytDlp.Exists)
            {
                SetStatus("YtDlpDownloading");
                IsOverallIndeterminate = true;

                var progress = new Progress<double>(value =>
                {
                    IsOverallIndeterminate = false;
                    OverallProgress = value;
                    ProgressText = $"{value:F0}%";
                });

                await _updateService.EnsureDownloadedAsync(progress);
                SetStatus("YtDlpDownloaded");
            }

            YtDlpVersion = await _ytDlp.GetVersionAsync();
            if (!IsYtDlpReady)
            {
                SetStatus("YtDlpNotFound");
            }
            else
            {
                SetStatus("Ready");
            }
        }
        catch (Exception ex)
        {
            SetStatus("YtDlpDownloadFailed");
            AppLog.Exception("Unable to download yt-dlp on startup", ex);
            _dialog.ShowError(ex.Message, _localization["YtDlpDownloadFailed"]);
        }

        if (_config.AutoUpdateYtDlp)
        {
            await AutoUpdateYtDlpAsync();
        }
        else if (_config.AutoCheckUpdates)
        {
            _ = SilentUpdateCheckAsync();
        }

        try
        {
            await Task.Run(() => _ffmpeg.DetectAsync());
            UpdateFfmpegState();
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to detect ffmpeg", ex);
        }

        try
        {
            await Task.Run(() => _jsRuntime.DetectAsync());
            UpdateJsRuntimeState();
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to detect a JavaScript runtime", ex);
        }

        ContextMenuInstalled = _contextMenu.IsInstalled;

        _isInitializing = false;
        IsOverallIndeterminate = false;
        OverallProgress = 0;
        ProgressText = string.Empty;
        UpdateSummary();
        NotifyCommands();

        _ = CheckAppUpdateAsync();
    }

    public void ApplyStartup(string? folder, string? url)
    {
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            OutputDirectory = folder;
            SetStatus("StartupFolderFormat", folder);
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            Url = url;
        }
    }

    public void HandleDroppedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var value = text.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (Directory.Exists(value))
        {
            OutputDirectory = value;
            return;
        }

        Url = value;
    }

    private bool ConfirmEnoughDiskSpace(
        IReadOnlyList<QueueItemViewModel> items,
        string outputDirectory)
    {
        try
        {
            var required = items.Sum(i => i.Item.BestKnownSize ?? 0);
            if (required <= 0)
            {
                return true;
            }

            var root = Path.GetPathRoot(Path.GetFullPath(outputDirectory));
            if (string.IsNullOrWhiteSpace(root))
            {
                return true;
            }

            var free = new DriveInfo(root).AvailableFreeSpace;
            const long safetyMargin = 200L * 1024 * 1024;

            if (free >= required + safetyMargin)
            {
                return true;
            }

            return _dialog.ShowConfirm(
                _localization.Format("DiskSpacePrompt", Humanize.Bytes(free), Humanize.Bytes(required)),
                _localization["DiskSpaceWarning"]);
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to check free disk space", ex);
            return true;
        }
    }

    public bool CanClose()
    {
        if (!IsDownloading)
        {
            return true;
        }

        if (!_dialog.ShowConfirm(_localization["ConfirmClose"], _localization["Close"]))
        {
            return false;
        }

        CancelAll();
        return true;
    }

    private async Task AnalyzeAsync()
    {
        if (string.IsNullOrWhiteSpace(Url) || !Uri.TryCreate(Url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _dialog.ShowError(_localization["InvalidUrl"], _localization["Error"]);
            return;
        }

        if (!_ytDlp.Exists)
        {
            _dialog.ShowError(_localization["YtDlpNotFound"], _localization["Error"]);
            return;
        }

        _analyzeCts?.Cancel();
        _analyzeCts = new CancellationTokenSource();
        IsAnalyzing = true;
        SetStatus("Analyzing");

        try
        {
            var options = BuildOptions();
            var result = await _queueService.AnalyzeAsync(Url.Trim(), options, _config.IncludePlaylist, _analyzeCts.Token);

            if (_analyzeCts.IsCancellationRequested)
            {
                return;
            }

            var added = 0;
            var newItems = new List<DownloadItem>();

            foreach (var item in result.Items)
            {
                var alreadyExists = Queue.Any(q =>
                    !q.Item.IsFinished
                    && string.Equals(q.Item.Url, item.Url, StringComparison.OrdinalIgnoreCase));

                if (alreadyExists)
                {
                    continue;
                }

                var viewModel = new QueueItemViewModel(item, _localization, _thumbnails);
                viewModel.PropertyChanged += OnQueueItemPropertyChanged;
                Queue.Add(viewModel);
                newItems.Add(item);
                added++;
            }

            UpdateSummary();
            UpdateOverallProgress();

            if (added == 0)
            {
                SetStatus("AlreadyInQueue");
            }
            else if (result.IsPlaylist)
            {
                SetStatus("PlaylistAddedFormat", added, result.Title);
            }
            else
            {
                SetStatus("VideoAddedFormat", result.Title);
            }

            if (_config.ProbeSizes && newItems.Count > 0)
            {
                _ = ProbeSizesAsync(newItems, options);
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("Ready");
        }
        catch (Exception ex)
        {
            SetStatus("AnalyzeFailed");
            AppLog.Exception("URL analysis failed", ex);
            _dialog.ShowError(_localization["AnalyzeFailed"], _localization["Error"]);
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    private async Task ProbeSizesAsync(IReadOnlyList<DownloadItem> items, DownloadOptions options)
    {
        _probeCts?.Cancel();
        _probeCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<DownloadItem>(_ =>
            {
                UpdateSummary();
                UpdateOverallProgress();
            });

            await _queueService.ProbeSizesAsync(items, options, 2, progress, _probeCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when the queue is cleared.
        }
        catch (Exception ex)
        {
            AppLog.Exception("Size probing failed", ex);
        }
    }

    private async Task StartDownloadAsync()
    {
        var selected = Queue
            .Where(i => i.IsSelected && i.Item.Status != DownloadItemStatus.Completed)
            .ToList();

        if (selected.Count == 0)
        {
            _dialog.ShowWarning(_localization["NoSelection"], _localization["Warning"]);
            return;
        }

        foreach (var item in selected)
        {
            if (item.Item.Status is DownloadItemStatus.Failed
                or DownloadItemStatus.Cancelled
                or DownloadItemStatus.Skipped)
            {
                item.Item.Status = DownloadItemStatus.Pending;
                item.Item.ErrorMessage = null;
                item.Item.Progress = 0;
                item.Item.SpeedBytesPerSecond = null;
                item.Item.EtaSeconds = null;
            }
        }

        if (!await EnsureYtDlpAsync())
        {
            return;
        }

        var options = BuildOptions();

        try
        {
            Directory.CreateDirectory(options.OutputDirectory);
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to create the output directory", ex);
            _dialog.ShowError(ex.Message, _localization["Error"]);
            return;
        }

        if (FormatSelector.RequiresFfmpeg(options) && !_ffmpeg.IsAvailable)
        {
            if (!await EnsureFfmpegAsync())
            {
                return;
            }
        }

        if (!ConfirmEnoughDiskSpace(selected, options.OutputDirectory))
        {
            return;
        }

        _probeCts?.Cancel();
        SaveConfig();
        IsDownloading = true;
        IsOverallIndeterminate = true;
        _downloadCts = new CancellationTokenSource();
        SetStatus("DownloadingQueueFormat", 0, selected.Count);

        try
        {
            await _queueService.StartAsync(
                selected.Select(vm => vm.Item).ToList(),
                options,
                _config.ConcurrentDownloads,
                _downloadCts.Token);

            var cancelled = selected.Count(i => i.Item.Status == DownloadItemStatus.Cancelled);
            var failed = selected.Count(i => i.Item.Status == DownloadItemStatus.Failed);

            if (cancelled > 0 && failed == 0)
            {
                SetStatus("QueueCancelled");
            }
            else if (failed > 0)
            {
                SetStatus("QueueCompletedWithErrors", failed);
            }
            else
            {
                SetStatus("QueueCompleted");
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("QueueCancelled");
        }
        catch (Exception ex)
        {
            SetStatus("DownloadError");
            AppLog.Exception("Queue failed", ex);
            _dialog.ShowError(ex.Message, _localization["DownloadError"]);
        }
        finally
        {
            IsDownloading = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
            UpdateSummary();
            UpdateOverallProgress();
        }
    }

    private void CancelAll()
    {
        if (!IsCancellable)
        {
            return;
        }

        SetStatus("Cancelling");
        _analyzeCts?.Cancel();
        _probeCts?.Cancel();

        if (IsDownloading)
        {
            _queueService.CancelAll();
            _downloadCts?.Cancel();
        }
    }

    private void ClearQueue()
    {
        if (IsDownloading)
        {
            if (!_dialog.ShowConfirm(_localization["ConfirmClearQueue"], _localization["ClearQueue"]))
            {
                return;
            }

            CancelAll();
        }

        _analyzeCts?.Cancel();
        _probeCts?.Cancel();

        foreach (var item in Queue)
        {
            item.PropertyChanged -= OnQueueItemPropertyChanged;
            item.Dispose();
        }

        Queue.Clear();
        OverallProgress = 0;
        ProgressText = string.Empty;
        IsOverallIndeterminate = false;
        SetStatus("QueueCleared");
        UpdateSummary();
    }

    private void RemoveItem(QueueItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.Item.IsBusy)
        {
            _queueService.CancelItem(item.Item);
        }
        else if (item.Item.Status is DownloadItemStatus.Pending or DownloadItemStatus.Ready)
        {
            // Prevent a pending item from starting after it has been removed.
            item.Item.Status = DownloadItemStatus.Cancelled;
        }

        item.PropertyChanged -= OnQueueItemPropertyChanged;
        item.Dispose();
        Queue.Remove(item);
        UpdateSummary();
        UpdateOverallProgress();
    }

    private void SetSelection(bool selected)
    {
        foreach (var item in Queue)
        {
            item.IsSelected = selected;
        }

        UpdateSummary();
        UpdateOverallProgress();
    }

    private void BrowseOutput()
    {
        var folder = _dialog.BrowseFolder(OutputDirectory, _localization["BrowseFolderTitle"]);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            OutputDirectory = folder;
        }
    }

    private void OpenFile(QueueItemViewModel? item)
    {
        if (item?.Item.FilePath is { Length: > 0 } path)
        {
            _dialog.OpenFile(path);
        }
    }

    private void OpenFileLocation(QueueItemViewModel? item)
    {
        if (item?.Item.FilePath is { Length: > 0 } path)
        {
            _dialog.OpenFileLocation(path);
        }
    }

    private void InstallContextMenu()
    {
        try
        {
            _contextMenu.Install(_localization["ContextMenuVerb"]);
            ContextMenuInstalled = _contextMenu.IsInstalled;
            SetStatus("MenuInstalled");
            _dialog.ShowInfo(_localization["MenuInstallSuccess"], _localization["MenuInstalled"]);
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to install context menu", ex);
            _dialog.ShowError(ex.Message, _localization["MenuInstallFailed"]);
        }
    }

    private void UninstallContextMenu()
    {
        try
        {
            _contextMenu.Uninstall();
            ContextMenuInstalled = _contextMenu.IsInstalled;
            SetStatus("MenuRemoved");
            _dialog.ShowInfo(_localization["MenuRemoveSuccess"], _localization["MenuRemoved"]);
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to remove context menu", ex);
            _dialog.ShowError(ex.Message, _localization["MenuRemoveFailed"]);
        }
    }

    private async Task CheckUpdateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        SetStatus("UpdateChecking");
        IsOverallIndeterminate = true;

        try
        {
            var update = await _updateService.CheckForUpdateAsync();
            if (update is null)
            {
                SetStatus("UpToDate");
                return;
            }

            _updateVersion = update.Version;
            OnPropertyChanged(nameof(UpdateAvailableVersion));
            OnPropertyChanged(nameof(HasUpdate));
            OnPropertyChanged(nameof(UpdateAvailableText));

            var prompt = _localization.Format(
                "UpdateAvailablePrompt",
                _updateService.CurrentVersion,
                update.Version);

            if (!_dialog.ShowConfirm(prompt, _localization["UpdateAvailable"]))
            {
                SetStatus("Ready");
                return;
            }

            SetStatus("UpdateInProgress");
            var progress = new Progress<double>(value =>
            {
                IsOverallIndeterminate = false;
                OverallProgress = value;
                ProgressText = $"{value:F0}%";
            });

            await _updateService.UpdateAsync(update, progress);
            YtDlpVersion = await _ytDlp.GetVersionAsync();
            _updateVersion = null;
            OnPropertyChanged(nameof(UpdateAvailableVersion));
            OnPropertyChanged(nameof(HasUpdate));
            OnPropertyChanged(nameof(UpdateAvailableText));
            SetStatus("UpdateComplete");
        }
        catch (Exception ex)
        {
            SetStatus("UpdateCheckFailed");
            AppLog.Exception("Update check failed", ex);
            _dialog.ShowError(ex.Message, _localization["UpdateCheckFailed"]);
        }
        finally
        {
            IsOverallIndeterminate = false;
            OverallProgress = 0;
            ProgressText = string.Empty;
            NotifyCommands();
        }
    }

    private async Task SilentUpdateCheckAsync()
    {
        try
        {
            var update = await _updateService.CheckForUpdateAsync();
            if (update is null)
            {
                return;
            }

            _updateVersion = update.Version;
            OnPropertyChanged(nameof(UpdateAvailableVersion));
            OnPropertyChanged(nameof(HasUpdate));
            OnPropertyChanged(nameof(UpdateAvailableText));

            if (!IsBusy)
            {
                SetStatus("UpdateAvailableShort", update.Version);
            }
        }
        catch (Exception ex)
        {
            AppLog.Exception("Silent update check failed", ex);
        }
    }

    private async Task AutoUpdateYtDlpAsync()
    {
        try
        {
            var update = await _updateService.CheckForUpdateAsync();
            if (update is null)
            {
                return;
            }

            SetStatus("YtDlpUpdating", update.Version);
            IsOverallIndeterminate = true;

            var progress = new Progress<double>(value =>
            {
                IsOverallIndeterminate = false;
                OverallProgress = value;
                ProgressText = $"{value:F0}%";
            });

            await _updateService.UpdateAsync(update, progress);
            YtDlpVersion = await _ytDlp.GetVersionAsync();
            SetStatus("UpdateComplete");
        }
        catch (Exception ex)
        {
            AppLog.Exception("Automatic yt-dlp update failed", ex);
            SetStatus("UpdateCheckFailed");
        }
        finally
        {
            IsOverallIndeterminate = false;
            OverallProgress = 0;
            ProgressText = string.Empty;
        }
    }

    private async Task DownloadDenoAsync()
    {
        if (_isDenoBusy)
        {
            return;
        }

        if (!_dialog.ShowConfirm(_localization["DenoDownloadPrompt"], _localization["JsRuntime"]))
        {
            return;
        }

        _isDenoBusy = true;
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(JsRuntimeStatusText));
        NotifyCommands();
        SetStatus("DenoDownloading");
        IsOverallIndeterminate = true;

        try
        {
            var progress = new Progress<double>(value =>
            {
                IsOverallIndeterminate = false;
                OverallProgress = value;
                ProgressText = $"{value:F0}%";
            });

            await _jsRuntime.DownloadDenoAsync(progress);
            UpdateJsRuntimeState();
            SetStatus("DenoDownloadComplete");
            _dialog.ShowInfo(_localization["DenoDownloadComplete"], _localization["JsRuntime"]);
        }
        catch (Exception ex)
        {
            SetStatus("DenoDownloadFailed");
            AppLog.Exception("Deno download failed", ex);
            _dialog.ShowError(ex.Message, _localization["DenoDownloadFailed"]);
        }
        finally
        {
            _isDenoBusy = false;
            IsOverallIndeterminate = false;
            OverallProgress = 0;
            ProgressText = string.Empty;
            OnPropertyChanged(nameof(IsBusy));
            NotifyJsRuntimeChanged();
        }
    }

    private void UpdateJsRuntimeState()
    {
        NotifyJsRuntimeChanged();
    }

    private void NotifyJsRuntimeChanged()
    {
        OnPropertyChanged(nameof(IsJsRuntimeAvailable));
        OnPropertyChanged(nameof(JsRuntimeStatusText));
        NotifyCommands();
    }

    private async Task CheckAppUpdateAsync()
    {
        if (IsDevelopmentBuild || IsDevelopmentPath)
        {
            return;
        }

        try
        {
            var update = await _appUpdate.CheckForUpdateAsync();
            if (update is null)
            {
                return;
            }

            _appUpdateInfo = update;
            OnPropertyChanged(nameof(HasAppUpdate));
            OnPropertyChanged(nameof(AppUpdateAvailableText));
            OnPropertyChanged(nameof(CanInstallAppUpdate));
            NotifyCommands();

            if (_config.AutoUpdateApp && !IsBusy && Queue.Count == 0)
            {
                await InstallAppUpdateAsync(manual: false);
            }
            else
            {
                SetStatus("AppUpdateAvailableShort", update.Version);
            }
        }
        catch (Exception ex)
        {
            AppLog.Exception("MediaVacuum update check failed", ex);
        }
    }

    private async Task InstallAppUpdateAsync(bool manual)
    {
        if (_appUpdateInfo is null || _isAppUpdating)
        {
            return;
        }

        if (manual && (IsBusy || Queue.Count > 0))
        {
            _dialog.ShowWarning(_localization["AppUpdateBusy"], _localization["AppUpdateTitle"]);
            return;
        }

        _isAppUpdating = true;
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanInstallAppUpdate));
        NotifyCommands();
        SetStatus("AppUpdateDownloading", _appUpdateInfo.Version);
        IsOverallIndeterminate = true;

        try
        {
            var progress = new Progress<double>(value =>
            {
                IsOverallIndeterminate = false;
                OverallProgress = value;
                ProgressText = $"{value:F0}%";
            });

            var newExecutable = await _appUpdate.DownloadAsync(_appUpdateInfo, progress);
            SetStatus("AppUpdateReady", _appUpdateInfo.Version);
            AppLog.Info($"MediaVacuum {_appUpdateInfo.Version} is ready; restarting for update.");

            // Let the status bar render before the process is replaced.
            await Task.Delay(700);
            _appUpdate.LaunchUpdater(newExecutable);
            _dialog.ExitApp();
        }
        catch (Exception ex)
        {
            SetStatus("AppUpdateFailed");
            AppLog.Exception("MediaVacuum self-update failed", ex);
            _dialog.ShowError(ex.Message, _localization["AppUpdateFailed"]);
        }
        finally
        {
            _isAppUpdating = false;
            IsOverallIndeterminate = false;
            OverallProgress = 0;
            ProgressText = string.Empty;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanInstallAppUpdate));
            NotifyCommands();
        }
    }

    private static bool IsDevelopmentBuild
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    private static bool IsDevelopmentPath =>
        Environment.ProcessPath?.Contains(@"\bin\", StringComparison.OrdinalIgnoreCase) == true;

    private async Task<bool> EnsureYtDlpAsync()
    {
        if (_ytDlp.Exists)
        {
            return true;
        }

        SetStatus("YtDlpDownloading");
        IsOverallIndeterminate = true;

        try
        {
            var progress = new Progress<double>(value =>
            {
                IsOverallIndeterminate = false;
                OverallProgress = value;
                ProgressText = $"{value:F0}%";
            });

            await _updateService.EnsureDownloadedAsync(progress);
            YtDlpVersion = await _ytDlp.GetVersionAsync();
            OnPropertyChanged(nameof(IsYtDlpReady));
            SetStatus(IsYtDlpReady ? "YtDlpDownloaded" : "YtDlpDownloadFailed");
            return IsYtDlpReady;
        }
        catch (Exception ex)
        {
            SetStatus("YtDlpDownloadFailed");
            AppLog.Exception("Unable to download yt-dlp", ex);
            _dialog.ShowError(ex.Message, _localization["YtDlpDownloadFailed"]);
            return false;
        }
        finally
        {
            IsOverallIndeterminate = false;
            OverallProgress = 0;
            ProgressText = string.Empty;
            NotifyCommands();
        }
    }

    private async Task<bool> EnsureFfmpegAsync()
    {
        var prompt = _localization["FfmpegRequiredPrompt"];
        if (!_dialog.ShowConfirm(prompt, _localization["FfmpegRequired"]))
        {
            return false;
        }

        await DownloadFfmpegAsync(prompt: false);
        return _ffmpeg.IsAvailable;
    }

    private async Task DownloadFfmpegAsync(bool prompt)
    {
        if (_isFfmpegBusy)
        {
            return;
        }

        if (prompt && !_dialog.ShowConfirm(_localization["FfmpegDownloadPrompt"], _localization["Ffmpeg"]))
        {
            return;
        }

        _isFfmpegBusy = true;
        SetStatus("FfmpegDownloading");
        IsOverallIndeterminate = true;
        NotifyCommands();

        try
        {
            var progress = new Progress<double>(value =>
            {
                IsOverallIndeterminate = false;
                OverallProgress = value;
                ProgressText = $"{value:F0}%";
            });

            await _ffmpeg.DownloadAsync(progress);
            UpdateFfmpegState();
            SetStatus("FfmpegDownloadComplete");

            if (prompt)
            {
                _dialog.ShowInfo(_localization["FfmpegDownloadComplete"], _localization["Ffmpeg"]);
            }
        }
        catch (Exception ex)
        {
            SetStatus("FfmpegDownloadFailed");
            AppLog.Exception("ffmpeg download failed", ex);
            _dialog.ShowError(ex.Message, _localization["FfmpegDownloadFailed"]);
        }
        finally
        {
            _isFfmpegBusy = false;
            IsOverallIndeterminate = false;
            OverallProgress = 0;
            ProgressText = string.Empty;
            NotifyCommands();
        }
    }

    private void UninstallApp()
    {
        if (!_dialog.ShowConfirm(_localization["UninstallPrompt"], _localization["Uninstall"]))
        {
            return;
        }

        try
        {
            var removeData = _dialog.ShowConfirm(
                _localization["RemoveAppDataPrompt"],
                _localization["Uninstall"]);

            _contextMenu.Uninstall();
            ContextMenuInstalled = false;

            if (removeData)
            {
                _installer.RemoveAppData(includeYtDlp: true, includeFfmpeg: true, includeLogs: true);
            }

            _dialog.ShowInfo(_localization["UninstallComplete"], _localization["Uninstall"]);
            _installer.ScheduleSelfDelete();
            _dialog.ExitApp();
        }
        catch (Exception ex)
        {
            AppLog.Exception("Uninstall failed", ex);
            _dialog.ShowError(ex.Message, _localization["UninstallError"]);
        }
    }

    private void UpdateFfmpegState()
    {
        _ffmpegVersion = _ffmpeg.Version;
        if (_ffmpeg.IsAvailable && !string.Equals(_config.FfmpegPath, _ffmpeg.FfmpegPath, StringComparison.OrdinalIgnoreCase))
        {
            _config.FfmpegPath = _ffmpeg.FfmpegPath;
            SaveConfig();
        }

        OnPropertyChanged(nameof(IsFfmpegAvailable));
        OnPropertyChanged(nameof(FfmpegStatusText));
    }

    private DownloadOptions BuildOptions() => new()
    {
        OutputDirectory = OutputDirectory,
        Mode = _config.DownloadMode,
        VideoPreset = _config.VideoPreset,
        CustomFormat = _config.CustomFormat,
        VideoContainer = _config.VideoContainer,
        AudioFormat = _config.AudioFormat,
        EmbedMetadata = _config.EmbedMetadata,
        EmbedThumbnail = _config.EmbedThumbnail,
        WriteThumbnail = _config.WriteThumbnail,
        WriteSubtitles = _config.WriteSubtitles,
        EmbedSubtitles = _config.EmbedSubtitles,
        SubtitleLanguages = _config.SubtitleLanguages,
        CreatePlaylistFolder = _config.CreatePlaylistFolder,
        IncludeVideoId = _config.IncludeVideoId,
        ConcurrentFragments = _config.ConcurrentFragments,
        LimitRateKb = _config.LimitRateKb,
        OverwriteExisting = _config.OverwriteExisting,
        FfmpegPath = _ffmpeg.FfmpegPath,
        JsRuntime = _jsRuntime.BuildYtDlpArgument()
    };

    private void SaveConfig() => _config.Save();

    private void SetStatus(string key, params object?[] args)
    {
        _statusKey = key;
        _statusArgs = args;
        OnPropertyChanged(nameof(Status));
    }

    private void RefreshLocalizedState()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(FfmpegStatusText));
        OnPropertyChanged(nameof(IsJsRuntimeAvailable));
        OnPropertyChanged(nameof(JsRuntimeStatusText));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(QueueCountText));
        OnPropertyChanged(nameof(TotalSizeText));
        OnPropertyChanged(nameof(QueueSummary));
        OnPropertyChanged(nameof(UpdateAvailableText));
        OnPropertyChanged(nameof(AppUpdateAvailableText));
        OnPropertyChanged(nameof(CanAnalyze));
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(CanInstallAppUpdate));
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(VideoPreset));
        OnPropertyChanged(nameof(VideoContainer));
    }

    private void OnQueueItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateSummary();
        UpdateOverallProgress();

        if (IsDownloading && e.PropertyName is nameof(QueueItemViewModel.Status))
        {
            var selected = Queue.Where(i => i.IsSelected).ToList();
            var finished = selected.Count(i => i.IsFinished);
            SetStatus("DownloadingQueueFormat", finished, selected.Count);
        }
    }

    private void UpdateSummary()
    {
        OnPropertyChanged(nameof(HasQueue));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(QueueCountText));
        OnPropertyChanged(nameof(TotalSizeText));
        OnPropertyChanged(nameof(QueueSummary));
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(CanAnalyze));
        NotifyCommands();
    }

    private void UpdateOverallProgress()
    {
        var selected = Queue.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            OverallProgress = 0;
            ProgressText = string.Empty;
            IsOverallIndeterminate = false;
            return;
        }

        OverallProgress = selected.Average(i => i.Progress);
        ProgressText = $"{OverallProgress:F0}% • {selected.Count(i => i.IsFinished)}/{selected.Count}";

        IsOverallIndeterminate = IsDownloading
            && OverallProgress <= 0
            && selected.Any(i => i.IsIndeterminate);
    }

    private void NotifyCommands()
    {
        CommandManager.InvalidateRequerySuggested();
        OnPropertyChanged(nameof(CanAnalyze));
        OnPropertyChanged(nameof(CanDownload));
    }

    private static string? ShortFfmpegVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        const string prefix = "ffmpeg version ";
        if (version.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var rest = version[prefix.Length..];
            return rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }

        return version.Length <= 40 ? version : version[..40];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
