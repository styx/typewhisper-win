using System.Windows;
using TypeWhisper.Core.Interfaces;
using TypeWhisper.Core.Models;
using TypeWhisper.Windows.Native;

namespace TypeWhisper.Windows.Views;

public partial class MainWindow : Window
{
    private readonly ISettingsService _settings;

    public MainWindow(ViewModels.DictationViewModel viewModel, ISettingsService settings)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settings = settings;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowStyling.ApplyOverlayStyle(this);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionOverlay();
        _settings.SettingsChanged += _ => Dispatcher.Invoke(PositionOverlay);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        PositionOverlay();
    }

    private void PositionOverlay()
    {
        var work = WindowStyling.GetMonitorWorkAreaForCursor(this);

        var width  = ActualWidth  > 0 ? ActualWidth  : 300;
        var height = ActualHeight > 0 ? ActualHeight : 50;

        Left = work.Left + (work.Width - width) / 2;

        if (_settings.Current.OverlayPosition == OverlayPosition.Top)
            Top = work.Top;
        else
            Top = work.Bottom - height;
    }
}
