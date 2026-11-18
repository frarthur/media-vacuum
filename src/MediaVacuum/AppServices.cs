using System.Reflection;
using System.Windows;
using MediaVacuum.Core;
using MediaVacuum.Core.Interfaces;
using MediaVacuum.Core.Services;
using MediaVacuum.Installer;
using MediaVacuum.Services;
using MediaVacuum.ViewModels;

namespace MediaVacuum;

/// <summary>Composition root (lightweight manual dependency injection).</summary>
public sealed class AppServices
{
    public AppServices(Application application, bool initializeMain = true)
    {
        AppPaths.EnsureAll();

        Config = AppConfig.Load();
        // Persist the migrated/current schema.
        Config.Save();

        Localization = new LocalizationService();
        Localization.SetLanguage(Config.SelectedLanguage, raiseEvent: false);
        Dialog = new DialogService(Localization);
        Theme = new ThemeService(application);
        Theme.Apply(Config.Theme);

        YtDlp = new YtDlpService(AppPaths.YtDlpPath);
        Update = new UpdateService(AppPaths.YtDlpPath);
        Ffmpeg = new FfmpegService(Config.FfmpegPath);
        JsRuntime = new JsRuntimeService();
        ContextMenu = new ContextMenuManager(Environment.ProcessPath ?? AppPaths.YtDlpPath);
        Installer = new InstallerService();
        Queue = new DownloadQueueService(YtDlp);
        Thumbnails = new ThumbnailService(AppPaths.ThumbsDir);

        var version = Assembly.GetEntryAssembly()?.GetName().Version
            ?? typeof(AppServices).Assembly.GetName().Version
            ?? new Version(0, 0, 0);
        AppUpdate = new SelfUpdateService(version);

        if (initializeMain)
        {
            Main = new MainViewModel(
                Config,
                Localization,
                YtDlp,
                Update,
                Ffmpeg,
                JsRuntime,
                ContextMenu,
                Installer,
                Dialog,
                Theme,
                Queue,
                Thumbnails,
                AppUpdate);
        }
    }

    public AppConfig Config { get; }

    public LocalizationService Localization { get; }

    public IDialogService Dialog { get; }

    public IThemeService Theme { get; }

    public IYtDlpService YtDlp { get; }

    public IUpdateService Update { get; }

    public IFfmpegService Ffmpeg { get; }

    public IJsRuntimeService JsRuntime { get; }

    public IAppUpdateService AppUpdate { get; }

    public IContextMenuService ContextMenu { get; }

    public IInstallerService Installer { get; }

    public DownloadQueueService Queue { get; }

    public ThumbnailService Thumbnails { get; }

    public MainViewModel? Main { get; }
}
