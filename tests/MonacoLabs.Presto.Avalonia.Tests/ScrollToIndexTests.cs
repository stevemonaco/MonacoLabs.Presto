using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Covers <see cref="PrestoAvalonia.ScrollToIndex(Avalonia.Controls.ItemsControl, int, ScrollAlignment, AnimationBuilder?)"/>
/// on <see cref="VirtualListStage"/>, items 20 to 60 high, averaging 40.
/// </summary>
public sealed class ScrollToIndexTests : BridgeTestBase
{
    private const double Viewport = VirtualListStage.ViewportSize;

    [AvaloniaTheory]
    [InlineData(ScrollAlignment.Start)]
    [InlineData(ScrollAlignment.Center)]
    [InlineData(ScrollAlignment.End)]
    [InlineData(ScrollAlignment.Nearest)]
    public void AFarIndexLandsWithTheItemAligned(ScrollAlignment alignment)
    {
        var stage = VirtualListStage.Show(Varied);

        var animation = PrestoAvalonia.ScrollToIndex(stage.List, 3001, alignment);
        Run();

        Assert.True(animation.Completed);
        Assert.Equal(Aligned(alignment, Varied(3001)), stage.Top(3001));
    }

    [AvaloniaFact]
    public void ANearIndexAnimatesTheWholeWayWithoutAJump()
    {
        var stage = VirtualListStage.Show(Varied);

        PrestoAvalonia.ScrollToIndex(stage.List, 8);
        Assert.Equal(0, stage.Scroller.Offset.Y);
        var offsets = new List<double>();
        for (var i = 0; i < 60; i++)
        {
            Step();
            offsets.Add(stage.Scroller.Offset.Y);
        }

        Assert.InRange(offsets[0], 1, 290 / 4d);
        // The panel shifts realized slots by a fraction of a pixel as items leave the top, and the aim follows.
        Assert.All(offsets.Zip(offsets.Skip(1)), step => Assert.True(step.Second >= step.First - .5, $"{step.First} -> {step.Second}"));
        Assert.Equal(0, stage.Top(8));
    }

    [AvaloniaFact]
    public void AFarIndexJumpsFirstThenAnimatesAboutAViewport()
    {
        var stage = VirtualListStage.Show(Varied);

        PrestoAvalonia.ScrollToIndex(stage.List, 3000);
        var jumped = stage.Scroller.Offset.Y;
        Run();

        Assert.True(jumped > 100_000);
        Assert.InRange(stage.Scroller.Offset.Y - jumped, 0, 1.5 * Viewport);
        Assert.Equal(0, stage.Top(3000));
    }

    [AvaloniaFact]
    public void TheLastIndexAtTheEndAndIndexZeroAtTheStartLandAtTheExtremes()
    {
        var stage = VirtualListStage.Show(Varied);

        PrestoAvalonia.ScrollToIndex(stage.List, VirtualListStage.Count - 1, ScrollAlignment.End);
        Run();
        Assert.Equal(stage.Scroller.ScrollBarMaximum.Y, stage.Scroller.Offset.Y);
        Assert.Equal(Viewport - Varied(VirtualListStage.Count - 1), stage.Top(VirtualListStage.Count - 1));

        PrestoAvalonia.ScrollToIndex(stage.List, 0);
        Run();
        Assert.Equal(0, stage.Scroller.Offset.Y);
        Assert.Equal(0, stage.Top(0));
    }

    [AvaloniaFact]
    public void AWheelMidFlightCancels()
    {
        var stage = VirtualListStage.Show(Varied);
        var animation = PrestoAvalonia.ScrollToIndex(stage.List, 3000);
        Step();
        Step();

        stage.Window.MouseWheel(new Point(100, 100), new Vector(0, -1));
        Dispatcher.UIThread.RunJobs();
        var left = stage.Scroller.Offset.Y;
        Run();

        Assert.Equal(left, stage.Scroller.Offset.Y);
        Assert.False(animation.Completed);
        Assert.Empty(Presto.GetAnimations(stage.Scroller, "ScrollY"));
    }

    [AvaloniaFact]
    public void ItemsInsertedBeforeTheTargetMidFlightStillLandOnTheTargetItem()
    {
        var stage = VirtualListStage.Show(Varied);
        PrestoAvalonia.ScrollToIndex(stage.List, 3000);
        Step();
        Step();

        for (var i = 0; i < 5; i++) stage.Items.Insert(2990, 10_000 + i);
        stage.Items.Insert(0, 20_000);
        Run();

        Assert.Equal(3000, stage.Items[3006]);
        Assert.Equal(0, stage.Top(3006));
    }

    [AvaloniaFact]
    public void ReducedMotionLandsAlignedAndTheEngineIdles()
    {
        var stage = VirtualListStage.Show(Varied);
        Presto.Defaults.ReducedMotion = true;

        PrestoAvalonia.ScrollToIndex(stage.List, 3001, ScrollAlignment.Center);
        Step();

        Assert.Equal(Aligned(ScrollAlignment.Center, Varied(3001)), stage.Top(3001));
        Step();
        Assert.False(Presto.Engine.IsRunning);
    }

    [AvaloniaFact]
    public void AnIndexOutOfRangeThrows()
    {
        var stage = VirtualListStage.Show(Varied);

        Assert.Throws<ArgumentOutOfRangeException>(() => PrestoAvalonia.ScrollToIndex(stage.List, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrestoAvalonia.ScrollToIndex(stage.List, VirtualListStage.Count));
    }

    private static double Varied(int index) => 20 + (index % 5 * 10);

    private static double Aligned(ScrollAlignment alignment, double height) => alignment switch
    {
        ScrollAlignment.Start => 0,
        ScrollAlignment.Center => (Viewport - height) / 2,
        _ => Viewport - height,
    };

    private void Step()
    {
        Frames.Advance(16);
        Dispatcher.UIThread.RunJobs();
    }

    private void Run()
    {
        for (var i = 0; i < 60; i++) Step();
    }
}
