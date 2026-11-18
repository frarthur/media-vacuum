using MediaVacuum.Core;
using MediaVacuum.Core.Interfaces;
using Microsoft.Win32;

namespace MediaVacuum.Installer;

/// <summary>
/// Registers a per-user Explorer context menu (HKCU, no elevation required).
/// Works both on a folder background and on a folder item.
/// </summary>
public class ContextMenuManager : IContextMenuService
{
    private const string BackgroundKeyPath =
        @"Software\Classes\Directory\Background\shell\MediaVacuum";

    private const string DirectoryKeyPath =
        @"Software\Classes\Directory\shell\MediaVacuum";

    private readonly string _appPath;

    public ContextMenuManager(string appPath)
    {
        _appPath = appPath;
    }

    public bool IsInstalled
    {
        get
        {
            using var background = Registry.CurrentUser.OpenSubKey(BackgroundKeyPath + @"\command");
            using var directory = Registry.CurrentUser.OpenSubKey(DirectoryKeyPath + @"\command");
            return background is not null && directory is not null;
        }
    }

    public void Install(string verb)
    {
        var label = string.IsNullOrWhiteSpace(verb) ? "Download media" : verb;
        InstallEntry(BackgroundKeyPath, label, "%V");
        InstallEntry(DirectoryKeyPath, label, "%1");
        AppLog.Info("Explorer context menu installed.");
    }

    public void Uninstall()
    {
        DeleteTree(BackgroundKeyPath);
        DeleteTree(DirectoryKeyPath);
        AppLog.Info("Explorer context menu removed.");
    }

    private void InstallEntry(string keyPath, string label, string argument)
    {
        using var shellKey = Registry.CurrentUser.CreateSubKey(keyPath);
        shellKey.SetValue("MUIVerb", label);
        shellKey.SetValue("Icon", _appPath);

        using var commandKey = Registry.CurrentUser.CreateSubKey(keyPath + @"\command");
        commandKey.SetValue(string.Empty, $"\"{_appPath}\" --folder \"{argument}\"");
    }

    private static void DeleteTree(string keyPath)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            AppLog.Exception($"Unable to remove registry key {keyPath}", ex);
        }
    }
}
