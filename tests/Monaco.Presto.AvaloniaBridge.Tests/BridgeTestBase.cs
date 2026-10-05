using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Targets;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Initializes the bridge, then resets the engine exactly as <see cref="Tests.EngineTestBase"/> does so tests tick deterministically.</summary>
public abstract class BridgeTestBase : Monaco.Presto.Tests.EngineTestBase
{
    protected BridgeTestBase()
    {
        // A plain [Fact] resets the engine off the UI thread, cancelling an earlier test's leftovers from the wrong thread.
        Assert.True(Dispatcher.UIThread.CheckAccess(), $"{GetType().Name} touches Avalonia; mark its tests [AvaloniaFact] or [AvaloniaTheory].");
        PrestoAvalonia.EnsureInitialized();
        Presto.Engine.FrameSource = Frames; // EnsureInitialized installs the Avalonia source over the base's manual one
        Presto.Engine._timeOffset = 0;
        Presto.Engine.TimerCreated = DetachTracker.Track; // the base clears the hook EnsureInitialized installed
        PrestoAvalonia.WhenDetached = WhenDetached.Pause;
    }

    /// <summary>Shows <paramref name="content"/> in a 200x200 window and runs the queued jobs.</summary>
    protected static Window Show(Control content)
    {
        var window = new Window { Content = content, Width = 200, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>A red 50x50 border pinned to the top left.</summary>
    protected static Border Box() => new()
    {
        Width = 50, Height = 50, Background = Brushes.Red,
        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
    };

    /// <summary>Builds a paused linear 100ms animation and seeks it to <paramref name="progress"/>.</summary>
    protected static Animation Seeked(Visual visual, AnimationParams parameters, double progress = 1)
    {
        var animation = Presto.Animate(visual, parameters with { Duration = 100, Ease = "linear", Autoplay = false });
        animation.Seek(animation.Duration * progress);
        return animation;
    }

    protected static Point Centre(Control control, Window window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
}
