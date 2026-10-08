using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.AvaloniaBridge.Platform;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// The bridge drives <see cref="Engine.Hidden"/> from minimized windows and Background activations (GAPS-2-PLAN §6.3). The headless platform minimizes
/// windows but has no lifetime to list them, so each test hands <see cref="HiddenSignal"/> its own windows.
/// </summary>
public sealed class LifetimeHiddenTests : CompositorTestBase
{
    private readonly Func<IReadOnlyList<Window>?> _lifetimeWindows = HiddenSignal.Windows;
    private readonly List<Window> _windows = [];

    public LifetimeHiddenTests() => HiddenSignal.Windows = () => _windows;

    public override void Dispose()
    {
        HiddenSignal.Windows = _lifetimeWindows;
        HiddenSignal.OnActivation(ActivationKind.Background, activated: true);
        HiddenSignal.Update();
        base.Dispose();
    }

    [AvaloniaFact]
    public void MinimizingTheOnlyWindowPausesAndRestoringResumesAtTheSameProgress()
    {
        var (window, animation) = Looping();
        Frames.Advance(160, 16);
        var progress = animation.CurrentTime;

        window.WindowState = WindowState.Minimized;
        Frames.Advance(5000, 16);

        Assert.True(Presto.Engine.Paused);
        Assert.False(Frames.IsRunning);
        Assert.Equal(progress, animation.CurrentTime);

        window.WindowState = WindowState.Normal;
        Frames.Advance(16);

        Assert.Equal(progress + 16, animation.CurrentTime);
    }

    [AvaloniaFact]
    public void OneOfTwoWindowsMinimizedKeepsTicking()
    {
        var (window, animation) = Looping();
        _windows.Add(Show(new Border()));
        Frames.Advance(160, 16);
        var progress = animation.CurrentTime;

        window.WindowState = WindowState.Minimized;
        Frames.Advance(16);

        Assert.False(Presto.Engine.Hidden);
        Assert.Equal(progress + 16, animation.CurrentTime);
    }

    [AvaloniaFact]
    public void WithoutPauseOnHiddenAMinimizedWindowKeepsTicking()
    {
        Presto.Engine.PauseOnHidden = false;
        var (window, animation) = Looping();
        Frames.Advance(160, 16);
        var progress = animation.CurrentTime;

        window.WindowState = WindowState.Minimized;
        Frames.Advance(16);

        Assert.True(Presto.Engine.Hidden);
        Assert.Equal(progress + 16, animation.CurrentTime);
    }

    [AvaloniaFact]
    public void BackgroundingPausesAndForegroundingResumesAtTheSameProgress()
    {
        var (_, animation) = Looping();
        Frames.Advance(160, 16);
        var progress = animation.CurrentTime;

        HiddenSignal.OnActivation(ActivationKind.Background, activated: false);
        Frames.Advance(5000, 16);

        Assert.True(Presto.Engine.Hidden);
        Assert.True(Presto.Engine.Paused);
        Assert.Equal(progress, animation.CurrentTime);

        HiddenSignal.OnActivation(ActivationKind.Background, activated: true);
        Frames.Advance(16);

        Assert.False(Presto.Engine.Hidden);
        Assert.Equal(progress + 16, animation.CurrentTime);
    }

    [AvaloniaTheory]
    [InlineData(ActivationKind.File, false)]
    [InlineData(ActivationKind.File, true)]
    [InlineData(ActivationKind.OpenUri, false)]
    [InlineData(ActivationKind.OpenUri, true)]
    public void FileAndOpenUriActivationsChangeNothing(ActivationKind kind, bool activated)
    {
        var (_, animation) = Looping();
        Frames.Advance(160, 16);
        var progress = animation.CurrentTime;

        HiddenSignal.OnActivation(kind, activated);
        Frames.Advance(16);

        Assert.False(Presto.Engine.Hidden);
        Assert.Equal(progress + 16, animation.CurrentTime);
    }

    [AvaloniaFact]
    public void AnInitialBackgroundActivationWithNothingHiddenChangesNothing()
    {
        var (_, animation) = Looping();
        Frames.Advance(160, 16);
        var progress = animation.CurrentTime;
        var changes = 0;
        void Count() => changes++;
        HiddenSignal.Changed += Count;
        try
        {
            HiddenSignal.OnActivation(ActivationKind.Background, activated: true);
            Frames.Advance(16);
        }
        finally
        {
            HiddenSignal.Changed -= Count;
        }

        Assert.Equal(0, changes);
        Assert.False(Presto.Engine.Hidden);
        Assert.Equal(progress + 16, animation.CurrentTime);
    }

    [AvaloniaFact]
    public void MinimizedAndBackgroundedStaysHiddenUntilBothClear()
    {
        var (window, animation) = Looping();
        Frames.Advance(160, 16);
        var progress = animation.CurrentTime;

        window.WindowState = WindowState.Minimized;
        HiddenSignal.OnActivation(ActivationKind.Background, activated: false);
        window.WindowState = WindowState.Normal;
        Frames.Advance(160, 16);

        Assert.True(Presto.Engine.Hidden);
        Assert.Equal(progress, animation.CurrentTime);

        window.WindowState = WindowState.Minimized;
        HiddenSignal.OnActivation(ActivationKind.Background, activated: true);
        Frames.Advance(160, 16);

        Assert.True(Presto.Engine.Hidden);
        Assert.Equal(progress, animation.CurrentTime);

        window.WindowState = WindowState.Normal;
        Frames.Advance(16);

        Assert.False(Presto.Engine.Hidden);
        Assert.Equal(progress + 16, animation.CurrentTime);
    }

    [AvaloniaFact]
    public async Task AThenStartedBeforeHidingCompletesAfterShowing()
    {
        var box = Box();
        var window = Show(box);
        _windows.Add(window);
        var animation = Presto.Animate(box, new() { ["Opacity"] = 0, Duration = 100 });
        var done = animation.Then();
        Frames.Advance(48, 16);

        window.WindowState = WindowState.Minimized;
        Frames.Advance(5000, 16);
        Dispatcher.UIThread.RunJobs();
        Assert.False(done.IsCompleted);

        window.WindowState = WindowState.Normal;
        RunUntil(() => animation.Completed);
        Dispatcher.UIThread.RunJobs();

        Assert.True(done.IsCompleted);
        await done;
    }

    [AvaloniaFact]
    public void ASceneEnginePausesWhileHiddenAndResumesWhereItWas()
    {
        var scene = new ProbeScene(4000);
        var (_, canvas) = ShowScene(scene);
        var window = WindowOf(canvas);
        _windows.Add(window);
        Assert.True(Pump(() => scene.Target.Opacity < 1), "the scene animation never advanced");

        window.WindowState = WindowState.Minimized;
        Tick();
        var hidden = scene.Target.Opacity;
        TickFor(500);

        Assert.True(scene.Engine.Paused);
        Assert.Equal(hidden, scene.Target.Opacity);

        window.WindowState = WindowState.Normal;
        Tick(Frame);

        Assert.False(scene.Engine.Paused);
        Assert.True(Pump(() => scene.Target.Opacity < hidden), "the scene animation never resumed");
        Assert.InRange(scene.Target.Opacity, hidden - (3 * Frame / 4000), hidden);
    }

    [AvaloniaFact]
    public void ACanvasDetachedWhileHiddenIsCollectable()
    {
        var canvas = DetachWhileHidden();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(canvas.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference DetachWhileHidden()
    {
        var (panel, canvas) = ShowScene(new ProbeScene());
        var window = WindowOf(canvas);
        _windows.Add(window);
        window.WindowState = WindowState.Minimized;
        Tick();
        panel.Children.Remove(canvas);
        Tick();
        return new WeakReference(canvas);
    }

    private (Window Window, Animation Animation) Looping()
    {
        var box = Box();
        var window = Show(box);
        _windows.Add(window);
        return (window, Presto.Animate(box, new() { ["Opacity"] = 0, Duration = 1000, Loop = true, Ease = "linear" }));
    }
}
