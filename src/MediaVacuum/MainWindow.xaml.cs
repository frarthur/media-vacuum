using System.Windows;
using MediaVacuum.Core;
using MediaVacuum.ViewModels;

namespace MediaVacuum;

public partial class MainWindow : Window
{
    private readonly AppServices _services;

    public MainWindow(AppServices services)
    {
        _services = services;

        try
        {
            InitializeComponent();
            MaxHeight = SystemParameters.WorkArea.Height;
            MaxWidth = SystemParameters.WorkArea.Width;
            DataContext = services.Main;
        }
        catch (Exception ex)
        {
            AppLog.Exception("Unable to create the main window", ex);
            throw;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _services.Theme.ApplyWindowTheme(this);
        _services.Theme.ThemeChanged += (_, _) => _services.Theme.ApplyWindowTheme(this);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.Text)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnUrlDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.Text))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.Text) is string text && DataContext is MainViewModel viewModel)
        {
            viewModel.HandleDroppedText(text);
        }

        e.Handled = true;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && !viewModel.CanClose())
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }
}
