using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Monaco.Presto;
using PrestoSample.Showcase;
using Timer = Monaco.Presto.Timer;

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
    public void AfterFirstArrangeRunsOnceTheControlHasASizeAndNotForADetachedPage()
    {
        var page = new ArrangeProbePage();
        var window = new Window { Content = page, Width = 400, Height = 300 };
        window.Show();
        Assert.Equal(0, page.Runs);

        window.Content = null;
        page.Target.Width = 50;
        window.UpdateLayout();
        window.Content = page;
        window.UpdateLayout();
        Assert.Equal(1, page.Runs);

        page.Target.Width = 80;
        window.UpdateLayout();
        Assert.Equal(1, page.Runs);

        window.Content = null;
        window.Close();
    }

    [AvaloniaFact]
    public void TransportBarFollowsItsTimerAndSeeksIt()
    {
        TestFrames.Install();

        var bar = new TransportBar();
        var window = new Window { Content = bar, Width = 720, Height = 200 };
        window.Show();

        var updates = 0;
        Action<Timer> onUpdate = _ => updates++;
        var timer = Presto.CreateTimer(new() { Duration = 1000, OnUpdate = onUpdate });
        bar.Timer = timer;
        var scrubber = bar.GetControl<Slider>("Scrubber");
        for (var i = 0; i < 20; i++) TestFrames.Tick();

        Assert.True(timer.Progress > 0.2);
        Assert.Equal(timer.Progress, scrubber.Value, 3);
        Assert.True(updates > 0);

        scrubber.Value = 0.75;
        Assert.Equal(0.75, timer.Progress, 3);

        bar.Timer = null;
        Assert.Same(onUpdate, timer.OnUpdate);

        window.Content = null;
        window.Close();
        timer.Revert();
    }

    [AvaloniaFact]
    public void TransportBarLoopTogglesTheLoopCountLiveAndPutsItBack()
    {
        TestFrames.Install();

        var bar = new TransportBar();
        var window = new Window { Content = bar, Width = 720, Height = 200 };
        window.Show();

        var timer = Presto.CreateTimer(new() { Duration = 200, Loop = 1 });
        bar.Timer = timer;
        var loop = bar.GetControl<ToggleButton>("LoopToggle");

        loop.IsChecked = true;
        Assert.True(timer.Loop.IsInfinite);
        TestFrames.Tick(40);
        Assert.False(timer.Completed);
        Assert.Equal(timer.IterationProgress, bar.GetControl<Slider>("Scrubber").Value, 3);

        loop.IsChecked = false;
        Assert.Equal(1, timer.Loop.Value);
        Assert.Equal(400, timer.Duration);
        Assert.InRange(timer.CurrentTime, 200, 400);
        TestFrames.Tick(20);
        Assert.True(timer.Completed);
        Assert.Equal(1, bar.GetControl<Slider>("Scrubber").Value);

        loop.IsChecked = true;
        bar.Timer = null;
        Assert.Equal(1, timer.Loop.Value);

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

    private sealed class ArrangeProbePage : ShowcasePage
    {
        public ArrangeProbePage() => Content = Target;

        public Border Target { get; } = new() { Width = 0 };

        public int Runs { get; private set; }

        protected override void OnAttached() => AfterFirstArrange(Target, () => Runs++);
    }
}
