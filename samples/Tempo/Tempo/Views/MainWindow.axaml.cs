using Avalonia;
using Avalonia.Controls;
using Tempo.Model;
using Tempo.ViewModels;

namespace Tempo.Views;

public partial class MainWindow : Window
{
    private bool _placementSaved;
    private bool _launchSized;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Sizes the window for this run only: it wins over the restored placement, which is then not saved over.</summary>
    public void UseLaunchSize(Size size)
    {
        WindowState = WindowState.Normal;
        Width = size.Width;
        Height = size.Height;
        _launchSized = true;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainViewModel viewModel)
            RestorePlacement(viewModel.Settings);
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || _placementSaved || DataContext is not MainViewModel viewModel)
            return;
        // The app shuts down once the window closes, so the save must land first.
        e.Cancel = true;
        _placementSaved = true;
        if (!_launchSized)
            viewModel.SaveWindowPlacement(Width, Height, Position.X, Position.Y, WindowState == WindowState.Maximized);
        await viewModel.FlushAsync();
        Close();
    }

    private void RestorePlacement(Settings settings)
    {
        if (settings is { WindowWidth: { } width, WindowHeight: { } height, WindowX: { } x, WindowY: { } y }
            && Screens.All.FirstOrDefault(s => s.WorkingArea.Intersects(new PixelRect(x, y, (int)(width * s.Scaling), (int)(height * s.Scaling)))) is { } screen)
        {
            var area = screen.WorkingArea;
            Width = Math.Min(width, area.Width / screen.Scaling);
            Height = Math.Min(height, area.Height / screen.Scaling);
            Position = new PixelPoint(
                Math.Clamp(x, area.X, area.Right - (int)(Width * screen.Scaling)),
                Math.Clamp(y, area.Y, area.Bottom - (int)(Height * screen.Scaling)));
        }
        if (settings.IsMaximized)
            WindowState = WindowState.Maximized;
    }
}
