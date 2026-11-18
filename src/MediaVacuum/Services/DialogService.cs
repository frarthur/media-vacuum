using System.Diagnostics;
using System.IO;
using System.Windows;
using MediaVacuum.Core;
using MediaVacuum.Core.Services;
using Microsoft.Win32;

namespace MediaVacuum.Services;

public class DialogService : IDialogService
{
    private readonly LocalizationService _localization;

    public DialogService(LocalizationService localization)
    {
        _localization = localization;
    }

    public void ShowInfo(string message, string title) =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string message, string title) =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowWarning(string message, string title) =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public bool ShowConfirm(string message, string title)
    {
        var owner = Application.Current?.MainWindow;
        return owner is null
            ? MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes
            : MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public string? BrowseFolder(string initialDirectory, string title)
    {
        try
        {
            var dialog = new OpenFolderDialog
            {
                Title = title,
                InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : string.Empty
            };

            return dialog.ShowDialog() == true ? dialog.FolderName : null;
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to open the folder picker", ex);
            return null;
        }
    }

    public void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLog.Exception($"Unable to open folder {path}", ex);
            ShowError(ex.Message, _localization["Error"]);
        }
    }

    public void OpenFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            AppLog.Exception($"Unable to open file {path}", ex);
            ShowError(ex.Message, _localization["Error"]);
        }
    }

    public void OpenFileLocation(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
                return;
            }

            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                OpenFolder(folder);
            }
        }
        catch (Exception ex)
        {
            AppLog.Exception($"Unable to show file location {path}", ex);
        }
    }

    public void ExitApp() => Application.Current?.Shutdown();

    private static void Show(string message, string title, MessageBoxButton button, MessageBoxImage image)
    {
        var owner = Application.Current?.MainWindow;
        if (owner is not null)
        {
            MessageBox.Show(owner, message, title, button, image);
        }
        else
        {
            MessageBox.Show(message, title, button, image);
        }
    }
}
