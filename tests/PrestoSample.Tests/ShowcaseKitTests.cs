using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Monaco.Presto;
using PrestoSample.Showcase;

namespace PrestoSample.Tests;

/// <summary>The shared kit the showcase demos are built from: the page base's scope and the transport bar.</summary>
public sealed class ShowcaseKitTests
{
    [AvaloniaFact]
    public void ShowcasePageRevertsItsScopeOnceWhenDetached()
    {
        var page = new ProbePage();
        var window = new Window { Content = page, Width = 400, Height = 300 };
        window.Show();

        Assert.Equal(1, page.Builds);
        Assert.Equal(0, page.Reverts);

        window.Content = null;
        window.Close();

        Assert.Equal(1, page.Reverts);
    }

    [AvaloniaFact]
    public void TransportBarFollowsItsTimerAndSeeksIt()
    {
        TestFrames.Install();

        var bar = new TransportBar();
        var window = new Window { Content = bar, Width = 720, Height = 200 };
        window.Show();

        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        bar.Timer = timer;
        var scrubber = bar.GetControl<Slider>("Scrubber");
        for (var i = 0; i < 20; i++) TestFrames.Tick();

        Assert.True(timer.Progress > 0.2);
        // The bar reads the timer on a frame of its own, so it can be one behind.
        Assert.InRange(scrubber.Value, timer.Progress - 0.05, timer.Progress + 0.05);

        scrubber.Value = 0.75;
        Assert.Equal(0.75, timer.Progress, 3);

        window.Content = null;
        window.Close();
        timer.Revert();
    }

    [AvaloniaFact]
    public void FpsMeterCountsUiFramesAndShowsTheSceneRateItIsGiven()
    {
        TestFrames.Install();

        var meter = new FpsMeter();
        var window = new Window { Content = meter, Width = 300, Height = 100 };
        window.Show();

        var readouts = meter.GetLogicalDescendants().OfType<TextBlock>().ToList();
        Assert.Equal("ui 0 fps", readouts[0].Text);
        Assert.False(readouts[1].IsVisible);

        for (var i = 0; i < 40; i++) TestFrames.Tick();
        Assert.NotEqual("ui 0 fps", readouts[0].Text);

        meter.SceneFps = 118;
        Assert.Equal("scene 118 fps", readouts[1].Text);
        Assert.True(readouts[1].IsVisible);

        window.Content = null;
        window.Close();
    }

    private sealed class ProbePage : ShowcasePage
    {
        public ProbePage() => Scope.Add(_ => () => Reverts++);

        public int Builds { get; private set; }

        public int Reverts { get; private set; }

        protected override void OnAttached() => Builds++;
    }
}
