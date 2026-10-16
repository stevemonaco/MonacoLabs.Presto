using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
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

    private TransportBar Open()
    {
        PrestoDevTools.Open(_window);
        Dispatcher.UIThread.RunJobs();
        return OverlayLayer.GetOverlayLayer(_window)!.Children.OfType<DevToolsOverlay>().Single().Transport;
    }

    private void Click(Control button)
    {
        var center = Center(button, _window);
        _window.MouseDown(center, MouseButton.Left);
        _window.MouseUp(center, MouseButton.Left);
    }
}
