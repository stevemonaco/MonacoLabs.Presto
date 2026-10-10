using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Tempo.Model;
using Tempo.ViewModels;

namespace Tempo.Views;

public partial class MainWindow : Window
{
    private readonly KeyBinding[] _plainKeys;
    private bool _placementSaved;

    public MainWindow()
    {
        InitializeComponent();
        _plainKeys = [.. KeyBindings.Where(b => b.Gesture?.KeyModifiers == KeyModifiers.None)];
        // Avalonia runs key bindings before the focused control sees the key, so Space, N and Del would fire while typing.
        AddHandler(GotFocusEvent, (_, e) => EnablePlainKeys(e.Source is not TextBox));
        AddHandler(LostFocusEvent, (_, _) => EnablePlainKeys(true));
    }

    /// <summary>
    /// Plays the intro over <paramref name="load"/> and then shows the shell, unless the intro setting is off or
    /// <paramref name="skipIntro"/> is set, in which case the shell shows with its normal entrance.
    /// </summary>
    public async void Start(Task load, bool skipIntro)
    {
        if (skipIntro || DataContext is not MainViewModel { PlayIntro: true })
            return;
        var shell = Content;
        var intro = new IntroView(load);
        Content = intro;
        EnablePlainKeys(false);
        await intro.HandedOff;
        Content = shell;
        EnablePlainKeys(true);
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

    private void EnablePlainKeys(bool enabled)
    {
        foreach (var binding in _plainKeys)
            KeyBindings.Remove(binding);
        // The intro takes Space as a skip.
        if (enabled && Content is not IntroView)
            KeyBindings.AddRange(_plainKeys);
    }
}
