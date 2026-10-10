using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using MonacoLabs.Presto.Targets;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// The Phase 60 spike (GAPS-2-PLAN §6.3), kept as pinning tests: what Avalonia 12.1.2's headless platform reports when a
/// window is minimized, which lifetime features it offers, and what an always-on owner mapping and a per-visual detach
/// subscription would cost.
/// </summary>
public sealed class LifetimeSpikeTests : BridgeTestBase
{
    private const int Count = 10000;

    [AvaloniaFact]
    public void AHeadlessWindowTakesMinimizedRaisesTheStateChangeAndNoActivationChange()
    {
        var window = Show(new Border());
        var states = new List<WindowState>();
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty) states.Add((WindowState)e.NewValue!);
        };
        var activations = new List<string>();
        window.Activated += (_, _) => activations.Add("activated");
        window.Deactivated += (_, _) => activations.Add("deactivated");

        window.WindowState = WindowState.Minimized;
        Dispatcher.UIThread.RunJobs();
        window.WindowState = WindowState.Normal;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([WindowState.Minimized, WindowState.Normal], states);
        Assert.Empty(activations);
        Assert.True(window.IsVisible);
    }

    [AvaloniaFact]
    public void AMinimizedHeadlessWindowStillDeliversAnimationFrames()
    {
        var window = Show(new Border());
        window.WindowState = WindowState.Minimized;
        Dispatcher.UIThread.RunJobs();
        var frames = 0;

        for (var i = 0; i < 3; i++)
        {
            window.RequestAnimationFrame(_ => frames++);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(3, frames);
    }

    [AvaloniaFact]
    public void TheHeadlessPlatformOffersNoActivatableLifetimeAndNoApplicationLifetime()
    {
        Assert.Null(Application.Current!.TryGetFeature<IActivatableLifetime>());
        Assert.Null(Application.Current!.ApplicationLifetime);
    }

    [AvaloniaFact]
    public void RecordingAnOwnerForEveryCreatedBrushCostsOneTableEntryAndOneWeakReference()
    {
        var owner = new Border();
        var brushes = new SolidColorBrush[Count];
        for (var i = 0; i < Count; i++) brushes[i] = new SolidColorBrush(Colors.Red);
        TargetOwner.Record(new SolidColorBrush(Colors.Blue), owner);

        var watch = Stopwatch.StartNew();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Count; i++) TargetOwner.Record(brushes[i], owner);
        var bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)Count;
        watch.Stop();

        TestContext.Current.TestOutputHelper?.WriteLine($"{bytes:0.##} bytes, {watch.Elapsed.TotalNanoseconds / Count:0} ns per record");
        Assert.True(bytes <= 128, $"{bytes:0.##} bytes per record");
        Assert.Same(owner, TargetOwner.Find(brushes[^1]));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADetachSubscriptionPerVisualCostsNothingSharedOrOneClosureAndFiresOnRemoval(bool perVisual)
    {
        var panel = new StackPanel();
        Show(panel);
        var borders = new Border[Count];
        for (var i = 0; i < Count; i++) panel.Children.Add(borders[i] = new Border());
        var detached = 0;
        EventHandler<VisualTreeAttachmentEventArgs> shared = (_, _) => detached++;

        var watch = Stopwatch.StartNew();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Count; i++) borders[i].DetachedFromVisualTree += perVisual ? Capturing(borders[i], shared) : shared;
        var bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)Count;
        watch.Stop();
        panel.Children.Clear();

        TestContext.Current.TestOutputHelper?.WriteLine($"{bytes:0.##} bytes, {watch.Elapsed.TotalNanoseconds / Count:0} ns per subscription");
        Assert.True(bytes <= (perVisual ? 128 : 0), $"{bytes:0.##} bytes per subscription");
        Assert.Equal(Count, detached);
    }

    private static EventHandler<VisualTreeAttachmentEventArgs> Capturing(Visual owner, EventHandler<VisualTreeAttachmentEventArgs> then) =>
        (_, e) => then(owner, e);
}
