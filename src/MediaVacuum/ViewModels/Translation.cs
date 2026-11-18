using System.ComponentModel;
using System.Runtime.CompilerServices;
using MediaVacuum.Core.Services;

namespace MediaVacuum.ViewModels;

/// <summary>
/// Indexer-based translation accessor. XAML binds to <c>{Binding T[SomeKey]}</c>.
/// Raising "Item[]" refreshes every indexed binding without listing every key.
/// </summary>
public class Translation : INotifyPropertyChanged
{
    private readonly LocalizationService _localization;

    public Translation(LocalizationService localization)
    {
        _localization = localization;
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public string this[string key] => _localization[key];

    public string Format(string key, params object?[] args) => _localization.Format(key, args);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnLanguageChanged(object? sender, EventArgs e) => RaiseAll();

    public void RaiseAll()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
