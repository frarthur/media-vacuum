using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MediaVacuum.Core;
using MediaVacuum.Core.Models;
using Microsoft.Win32;

namespace MediaVacuum.Services;

public class ThemeService : IThemeService
{
    private readonly Application _application;

    public ThemeService(Application application)
    {
        _application = application;
    }

    public AppTheme Current { get; private set; } = AppTheme.System;

    public AppTheme EffectiveTheme => Current == AppTheme.System ? GetSystemTheme() : Current;

    public event EventHandler? ThemeChanged;

    public void Apply(AppTheme mode)
    {
        Current = mode;
        var effective = EffectiveTheme;
        var source = effective == AppTheme.Dark ? "Styles/Dark.xaml" : "Styles/Light.xaml";

        var dictionaries = _application.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(d =>
            d.Source?.OriginalString.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase) == true
            || d.Source?.OriginalString.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase) == true);

        var replacement = new ResourceDictionary
        {
            Source = new Uri(source, UriKind.Relative)
        };

        if (existing is not null)
        {
            var index = dictionaries.IndexOf(existing);
            dictionaries[index] = replacement;
        }
        else
        {
            dictionaries.Insert(0, replacement);
        }

        foreach (Window window in _application.Windows)
        {
            ApplyWindowTheme(window);
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
        AppLog.Info($"Theme applied: {mode} (effective: {effective}).");
    }

    public void ApplyWindowTheme(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var dark = EffectiveTheme == AppTheme.Dark ? 1 : 0;
            // DWMWA_USE_IMMERSIVE_DARK_MODE (20 on recent Windows, 19 on older builds).
            DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            DwmSetWindowAttribute(handle, 19, ref dark, sizeof(int));
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to apply the window dark title bar", ex);
        }
    }

    private static AppTheme GetSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int intValue)
            {
                return intValue == 0 ? AppTheme.Dark : AppTheme.Light;
            }
        }
        catch
        {
            // Fall back to light.
        }

        return AppTheme.Light;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int size);
}
