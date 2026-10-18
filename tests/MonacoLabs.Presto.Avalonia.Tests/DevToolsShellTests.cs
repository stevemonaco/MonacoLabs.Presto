using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using MonacoLabs.Presto.DevTools;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto.Tests;

public sealed class DevToolsShellTests : BridgeTestBase, IDisposable
{
    private readonly Window _window = new() { Width = 900, Height = 500 };

    public DevToolsShellTests()
    {
        _window.Show();
        Dispatcher.UIThread.RunJobs();
    }

    public void Dispose()
    {
        PrestoDevTools.Close(_window);
        _window.Close();
    }

    [AvaloniaFact]
    public void ToggleAddsAndRemovesTheOverlayAndDiagnostics()
    {
        var layer = OverlayLayer.GetOverlayLayer(_window)!;

        PrestoDevTools.Toggle(_window);
        Assert.Single(layer.Children.OfType<DevToolsOverlay>());
        Assert.True(Presto.Engine.Diagnostics.Enabled);

        PrestoDevTools.Toggle(_window);
        Assert.Empty(layer.Children.OfType<DevToolsOverlay>());
        Assert.False(Presto.Engine.Diagnostics.Enabled);
    }

    [AvaloniaFact]
    public void TheAttachedGestureTogglesTheOverlay()
    {
        PrestoDevTools.Attach(_window);
        try
        {
            _window.KeyPress(Key.F12, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.F12, null);
            Assert.True(PrestoDevTools.IsOpen(_window));

            _window.KeyPress(Key.F12, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.F12, null);
            Assert.False(PrestoDevTools.IsOpen(_window));
        }
        finally
        {
            PrestoDevTools.Detach(_window);
        }
    }

    [AvaloniaFact]
    public void FreezeHoldsTimeAndStepMovesARunningTimerByOneFrame()
    {
        var bar = Open();
        var timer = Presto.CreateTimer(new TimerParams { Duration = 1000 });
        Frames.Advance(100);

        Click(bar.Freeze);
        Assert.True(Presto.Engine.Frozen);
        var before = timer.CurrentTime;
        Frames.Advance(100);
        Assert.Equal(before, timer.CurrentTime);

        Click(bar.Step);
        Assert.Equal(before + (1000.0 / 60), timer.CurrentTime, 3);
    }

    [AvaloniaFact]
    public void ADilationPresetSetsTimeDilation()
    {
        var bar = Open();

        Click(bar.Presets[1]);

        Assert.Equal(0.25, Presto.Engine.TimeDilation);
        Assert.True(bar.Presets[1].IsChecked);
        Assert.False(bar.Presets[3].IsChecked);
    }

    [AvaloniaFact]
    public void ClosingWhileFrozenAndDilatedRestoresTime()
    {
        var bar = Open();
        Click(bar.Presets[1]);
        Click(bar.Freeze);

        PrestoDevTools.Close(_window);

        Assert.False(Presto.Engine.Frozen);
        Assert.Equal(1, Presto.Engine.TimeDilation);
    }

    [AvaloniaFact]
    public void ClosingRestoresCaptureTracesAndTheRootSpeedsSetFromTheInspector()
    {
        Open();
        var overlay = OverlayLayer.GetOverlayLayer(_window)!.Children.OfType<DevToolsOverlay>().Single();
        var timer = Presto.CreateTimer(new TimerParams { Duration = 1000 });
        overlay.List.CaptureTraces.IsChecked = true;
        overlay.Inspector.Refresh(timer, null);
        overlay.Inspector.Speed.Text = "2";
        overlay.Inspector.Speed.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        overlay.Inspector.Speed.Text = "3";
        overlay.Inspector.Speed.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Assert.True(PrestoDiagnostics.CaptureCreationTrace);
        Assert.Equal(3, timer.Speed);

        PrestoDevTools.Close(_window);

        Assert.False(PrestoDiagnostics.CaptureCreationTrace);
        Assert.Equal(1, timer.Speed);
    }

    [AvaloniaFact]
    public void TheOverlayRespondsToInputWhileFrozen()
    {
        var bar = Open();
        Presto.Engine.Freeze();

        Click(bar.Freeze);

        Assert.False(Presto.Engine.Frozen);
    }

    [AvaloniaFact]
    public void ARepeatAttachKeepsTheFirstGesture()
    {
        PrestoDevTools.Attach(_window, new KeyGesture(Key.F11));
        PrestoDevTools.Attach(_window);
        try
        {
            _window.KeyPress(Key.F12, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.F12, null);
            Assert.False(PrestoDevTools.IsOpen(_window));

            _window.KeyPress(Key.F11, RawInputModifiers.None, PhysicalKey.F11, null);
            Assert.True(PrestoDevTools.IsOpen(_window));
        }
        finally
        {
            PrestoDevTools.Detach(_window);
        }
    }

    [AvaloniaFact]
    public void ThePanelDocksAtTheBottomAndResizesFromItsTopEdgeWithinTheLayer()
    {
        Open();
        var dock = DockOf(Overlay);
        Assert.Equal(new Size(900, 500), Overlay.Bounds.Size);
        Assert.Equal(260, dock.Bounds.Height);
        Assert.Equal(500, dock.Bounds.Bottom);

        var grip = Center(GripOf(Overlay), _window);
        _window.MouseDown(grip, MouseButton.Left);
        Drag(grip.X, 100);
        Assert.Equal(400, dock.Bounds.Height);
        Drag(grip.X, 480);
        Assert.Equal(80, dock.Bounds.Height);
        Drag(grip.X, -200);
        Assert.Equal(500, dock.Bounds.Height);
        _window.MouseUp(new Point(grip.X, 0), MouseButton.Left);

        _window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        _window.UpdateLayout();
        Assert.Equal(new Size(900, 600), Overlay.Bounds.Size);
        Assert.Equal(600, dock.Bounds.Bottom);
    }

    [AvaloniaFact]
    public void CollapsingHidesAndStopsRefreshingTheBodyAndUncollapsingRestoresTheLastHeight()
    {
        var box = new Border { Width = 50, Height = 50 };
        _window.Content = new Panel { Children = { box } };
        Open();
        var overlay = Overlay;
        var dock = DockOf(overlay);
        var grip = Center(GripOf(overlay), _window);
        _window.MouseDown(grip, MouseButton.Left);
        Drag(grip.X, 200);
        _window.MouseUp(new Point(grip.X, 200), MouseButton.Left);
        Presto.Animate(box, new AvaloniaAnimationParams { ["Opacity"] = 0, Duration = 1000 });
        overlay.List.Refresh();
        overlay.List.Rows.SelectedItem = overlay.List.Items[0];
        overlay.Highlighter.Update(_window, null, overlay.List.SelectedRow);
        var adorners = AdornerLayer.GetAdornerLayer(box)!;
        Assert.Contains(overlay.Highlighter, adorners.Children);

        CollapseOf(overlay).IsChecked = true;
        _window.UpdateLayout();
        var later = Presto.Animate(box, new AvaloniaAnimationParams { ["Width"] = 100, Duration = 1000 });
        NextFrame();

        Assert.False(overlay.Body.IsVisible);
        Assert.False(GripOf(overlay).IsVisible);
        Assert.InRange(dock.Bounds.Height, 1, 79);
        Assert.DoesNotContain(overlay.Highlighter, adorners.Children);
        Assert.DoesNotContain(overlay.List.Items, row => row.Timer == later);

        CollapseOf(overlay).IsChecked = false;
        _window.UpdateLayout();
        NextFrame();

        Assert.Equal(300, dock.Bounds.Height);
        Assert.Contains(overlay.List.Items, row => row.Timer == later);
    }

    [AvaloniaFact]
    public void TheCloseButtonClosesTheOverlay()
    {
        Open();

        ActionsOf(Overlay)[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.False(PrestoDevTools.IsOpen(_window));
        Assert.False(Presto.Engine.Diagnostics.Enabled);
    }

    [AvaloniaTheory]
    [InlineData("Light", "#F3F3F3", "#D0D0D0")]
    [InlineData("Dark", "#202020", "#3C3C3C")]
    public void ThePaletteFollowsTheThemeVariant(string variant, string background, string border)
    {
        _window.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        Open();

        var dock = DockOf(Overlay);

        Assert.Equal(Color.Parse(background), Assert.IsType<SolidColorBrush>(dock.Background).Color);
        Assert.Equal(Color.Parse(border), Assert.IsType<SolidColorBrush>(dock.BorderBrush).Color);
    }

    [AvaloniaFact]
    public void TheBarShowsTheEngineSpeedApartFromTheDilation()
    {
        var bar = Open();
        Presto.Engine.Speed = 0.5;

        bar.Refresh();

        Assert.Equal("speed 0.5×", TextsOf(bar)[1].Text);
        Assert.Equal(1, Presto.Engine.TimeDilation);
        Assert.False(bar.Presets[2].IsChecked);
        Assert.True(bar.Presets[3].IsChecked);
    }

    [AvaloniaFact]
    public void EachNewEngineFrameIsSampledIntoTheCountersAndTheSparkline()
    {
        var box = new Border { Width = 50, Height = 50 };
        _window.Content = new Panel { Children = { box } };
        var bar = Open();
        var sparkline = bar.Children.OfType<Border>().Select(border => border.Child).OfType<Sparkline>().Single();
        Presto.Animate(box, new AvaloniaAnimationParams { ["Opacity"] = 0, ["Width"] = 100, Duration = 1000 });
        NextFrame();
        Assert.Equal(0, Ink(sparkline));

        for (var i = 0; i < 30; i++)
        {
            Frames.Advance(16);
            bar.Refresh();
        }
        NextFrame();

        Assert.Matches(@"^1 roots · 2 tweens · 0 springs · \d+\.\d\d ms$", TextsOf(bar)[0].Text);
        Assert.True(Ink(sparkline) > 0);
    }

    [AvaloniaFact]
    public void TheBarShowsHiddenPausedOnlyWhileHiddenWithPauseOnHidden()
    {
        var bar = Open();
        var hidden = TextsOf(bar)[2];
        Assert.False(hidden.IsVisible);

        Presto.Engine.Hidden = true;
        bar.Refresh();
        Assert.True(hidden.IsVisible);
        Assert.Equal("hidden · paused", hidden.Text);

        Presto.Engine.PauseOnHidden = false;
        bar.Refresh();
        Assert.False(hidden.IsVisible);
    }

    private TransportBar Open()
    {
        PrestoDevTools.Open(_window);
        Dispatcher.UIThread.RunJobs();
        return Overlay.Transport;
    }

    private DevToolsOverlay Overlay => OverlayLayer.GetOverlayLayer(_window)!.Children.OfType<DevToolsOverlay>().Single();

    private static Border DockOf(DevToolsOverlay overlay) => (Border)overlay.Children[0];

    private static Control GripOf(DevToolsOverlay overlay) => ((DockPanel)DockOf(overlay).Child!).Children[0];

    private static Avalonia.Controls.Controls ActionsOf(DevToolsOverlay overlay) =>
        ((StackPanel)((DockPanel)((DockPanel)DockOf(overlay).Child!).Children[1]).Children[0]).Children;

    private static ToggleButton CollapseOf(DevToolsOverlay overlay) => (ToggleButton)ActionsOf(overlay)[0];

    private static TextBlock[] TextsOf(TransportBar bar) => bar.Children.OfType<TextBlock>().ToArray();

    private static void NextFrame()
    {
        for (var i = 0; i < 2; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private void Drag(double x, double y)
    {
        _window.MouseMove(new Point(x, y), RawInputModifiers.LeftMouseButton);
        _window.UpdateLayout();
    }

    private int Ink(Control control)
    {
        using var frame = _window.GetLastRenderedFrame()!;
        using var locked = frame.Lock();
        var origin = control.TranslatePoint(default, _window)!.Value;
        var background = Marshal.ReadInt32(locked.Address, ((int)origin.Y * locked.RowBytes) + ((int)(origin.X + control.Bounds.Width - 1) * 4));
        var ink = 0;
        for (var y = (int)origin.Y - 1; y <= (int)(origin.Y + control.Bounds.Height); y++)
        {
            for (var x = (int)origin.X; x < (int)(origin.X + control.Bounds.Width); x++)
            {
                if (Marshal.ReadInt32(locked.Address, (y * locked.RowBytes) + (x * 4)) != background) ink++;
            }
        }
        return ink;
    }

    private void Click(Control button)
    {
        var center = Center(button, _window);
        _window.MouseDown(center, MouseButton.Left);
        _window.MouseUp(center, MouseButton.Left);
    }
}
