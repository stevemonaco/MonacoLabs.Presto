using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MonacoLabs.Presto.Events;

namespace MonacoLabs.Presto.Tests;

/// <summary>Covers <see cref="Scroll.SmoothProperty"/>: a 200 by 200 scroller over 1000 units of content, so the offset runs 0 to 800.</summary>
public sealed class ScrollSmoothTests : BridgeTestBase
{
    private const double Ease = 300;
    private static readonly Point Inside = new(100, 100);

    private static ScrollStage Show(double trail = 600)
    {
        var stage = ScrollStage.Show(trail: trail);
        Scroll.SetSmooth(stage.Scroller, true);
        return stage;
    }

    [AvaloniaFact]
    public void ANotchEasesToItsStepAndLandsExactly()
    {
        var (window, scroller, _, _) = Show();

        window.MouseWheel(Inside, new Vector(0, -1));
        Assert.Equal(0, scroller.Offset.Y);

        Frames.Advance(100, 16);
        Assert.InRange(scroller.Offset.Y, 1, 49);

        Frames.Advance(Ease, 16);
        Assert.Equal(50, scroller.Offset.Y);
        Assert.False(Presto.Engine.IsRunning);
    }

    [AvaloniaFact]
    public void QuickNotchesAccumulateIntoOneTarget()
    {
        var (window, scroller, _, _) = Show();

        for (var i = 0; i < 3; i++)
        {
            window.MouseWheel(Inside, new Vector(0, -1));
            Frames.Advance(32, 16);
        }
        Assert.InRange(scroller.Offset.Y, 1, 149);

        Frames.Advance(Ease, 16);
        Assert.Equal(150, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void TheTargetClampsAtTheEnd()
    {
        var (window, scroller, _, _) = Show();
        scroller.Offset = new Vector(0, 780);
        Dispatcher.UIThread.RunJobs();

        window.MouseWheel(Inside, new Vector(0, -3));
        Frames.Advance(Ease + 100, 16);

        Assert.Equal(800, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void APreciseDeltaScrollsAtOnceUnsmoothed()
    {
        var (window, scroller, _, _) = Show();

        window.MouseWheel(Inside, new Vector(0, -.3));

        Assert.Equal(15, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void ADirectOffsetSetCancelsTheEase()
    {
        var (window, scroller, _, _) = Show();
        window.MouseWheel(Inside, new Vector(0, -2));
        Frames.Advance(100, 16);

        scroller.Offset = new Vector(0, 400);
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(Ease, 16);

        Assert.Equal(400, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void ASnapWaitsForTheSmoothScrollToSettle()
    {
        var (window, scroller, target, _) = Show(300);
        PrestoAvalonia.OnScroll(new ScrollObserverParams { Target = target, Snap = .25 });
        Frames.Advance(16);
        scroller.Offset = new Vector(0, 100);
        Dispatcher.UIThread.RunJobs();

        window.MouseWheel(Inside, new Vector(0, -2));
        Frames.Advance(Ease, 16);
        Assert.Equal(200, scroller.Offset.Y);

        Frames.Advance(600, 16);
        Assert.Equal(175, scroller.Offset.Y);
    }

    [AvaloniaFact]
    public void TurningSmoothOffRestoresNativeNotches()
    {
        var (window, scroller, _, _) = Show();
        Scroll.SetSmooth(scroller, false);

        window.MouseWheel(Inside, new Vector(0, -1));

        Assert.Equal(50, scroller.Offset.Y);
    }
}
