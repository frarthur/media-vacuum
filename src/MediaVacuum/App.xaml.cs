using System.Windows;
using System.Windows.Threading;
using MediaVacuum.Core;
using MediaVacuum.Services;

namespace MediaVacuum;

public partial class App : Application
{
    private AppServices? _services;
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureAll();
        AppLog.Initialize();

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var options = CommandLineOptions.Parse(e.Args);
        var needsMainWindow = !options.Install && !options.Uninstall;

        try
        {
            _services = new AppServices(this, initializeMain: needsMainWindow);
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to initialise application services", ex);
            MessageBox.Show(ex.ToString(), "MediaVacuum", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        if (options.Install)
        {
            InstallContextMenu(options.Quiet);
            Shutdown();
            return;
        }

        if (options.Uninstall)
        {
            UninstallContextMenu(options.Quiet);
            Shutdown();
            return;
        }

        try
        {
            _window = new MainWindow(_services);
            MainWindow = _window;
            _window.Show();
            _services.Main!.ApplyStartup(options.Folder, options.Url);
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to show the main window", ex);
            MessageBox.Show(
                ex.Message,
                _services.Localization["ErrorFatal"],
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.YtDlp.KillActiveProcesses();
        _services?.Thumbnails.Dispose();
        AppLog.Info("Application stopped.");
        base.OnExit(e);
    }

    private void InstallContextMenu(bool quiet)
    {
        if (_services is null)
        {
            return;
        }

        try
        {
            _services.ContextMenu.Install(_services.Localization["ContextMenuVerb"]);
            if (!quiet)
            {
                MessageBox.Show(
                    _services.Localization["MenuInstallSuccess"],
                    _services.Localization["MenuInstalled"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to install context menu from command line", ex);
            if (!quiet)
            {
                MessageBox.Show(
                    ex.Message,
                    _services.Localization["MenuInstallFailed"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void UninstallContextMenu(bool quiet)
    {
        if (_services is null)
        {
            return;
        }

        try
        {
            _services.ContextMenu.Uninstall();
            if (!quiet)
            {
                MessageBox.Show(
                    _services.Localization["MenuRemoveSuccess"],
                    _services.Localization["MenuRemoved"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to remove context menu from command line", ex);
            if (!quiet)
            {
                MessageBox.Show(
                    ex.Message,
                    _services.Localization["MenuRemoveFailed"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        AppLog.Exception("Unhandled application exception", exception ?? new Exception("Unknown fatal error"));
        MessageBox.Show(
            exception?.ToString() ?? "Unknown fatal error",
            _services?.Localization["ErrorFatal"] ?? "Fatal error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Exception("Unhandled dispatcher exception", e.Exception);
        MessageBox.Show(
            e.Exception.Message,
            _services?.Localization["ErrorUnexpected"] ?? "Unexpected error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
