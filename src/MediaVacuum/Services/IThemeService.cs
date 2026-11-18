using MediaVacuum.Core.Models;

namespace MediaVacuum.Services;

public interface IThemeService
{
    AppTheme Current { get; }

    AppTheme EffectiveTheme { get; }

    void Apply(AppTheme mode);

    void ApplyWindowTheme(System.Windows.Window window);

    event EventHandler? ThemeChanged;
}
