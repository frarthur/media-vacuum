using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MediaVacuum.Core.Models;

namespace MediaVacuum.Converters;

public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            DownloadItemStatus.Completed => "SuccessBrush",
            DownloadItemStatus.Failed => "ErrorBrush",
            DownloadItemStatus.PostProcessing => "WarningBrush",
            DownloadItemStatus.Downloading => "PrimaryBrush",
            DownloadItemStatus.Analyzing => "PrimaryBrush",
            DownloadItemStatus.Cancelled => "TextMutedBrush",
            DownloadItemStatus.Skipped => "TextMutedBrush",
            _ => "TextSecondaryBrush"
        };

        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
