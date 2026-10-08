namespace Monaco.Presto.Tests;

public sealed class IntrospectionTests : EngineTestBase
{
    [Fact]
    public void ChildrenAndTweensEnumerateInOrderWithoutAllocating()
    {
        var target = new StyleTarget();
        var timeline = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(target, new() { ["X"] = 100, ["Y"] = 50, Duration = 100 })
            .Add(target, new() { ["Opacity"] = 1, Duration = 100 });
        var first = TestHelpers.GetChildAtIndex(timeline, 0)!;
        var second = TestHelpers.GetChildAtIndex(timeline, 1)!;

        var before = GC.GetAllocatedBytesForCurrentThread();
        var count = 0;
        foreach (var child in timeline.Children)
        {
            foreach (var _ in child.Tweens) count++;
        }
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(3, count);
        Assert.Equal(0, bytes);
        List<Timer> children = [.. timeline.Children];
        List<Tween> firstTweens = [.. first.Tweens];
        List<Timer> grandchildren = [.. first.Children];
        Assert.Equal([first, second], children);
        Assert.Equal(["X", "Y"], firstTweens.Select(t => t.Property));
        Assert.Empty(grandchildren);
    }

    [Fact]
    public void TimerGettersMatchTheParameters()
    {
        var timer = Presto.CreateTimer(new() { Duration = 200, Delay = 100, LoopDelay = 50, Loop = 2, Alternate = true, Priority = 3, Autoplay = false });

        Assert.Equal(100, timer.Delay);
        Assert.Equal(50, timer.LoopDelay);
        Assert.True(timer.IsAlternating);
        Assert.Equal(3, timer.Priority);
        Assert.Same(Presto.Engine, timer.Engine);
        Assert.False(timer.IsScheduled);
        timer.Play();
        Assert.True(timer.IsScheduled);
    }

    [Fact]
    public void TimelineChildOffsetAndPlaybackEaseMatchTheTimeline()
    {
        var target = new StyleTarget();
        var timeline = Presto.CreateTimeline(new() { Autoplay = false, PlaybackEase = Eases.OutQuad })
            .Add(target, new() { ["X"] = 100, Duration = 100 })
            .Add(target, new() { ["Y"] = 100, Duration = 100 }, 300);
        List<Timer> children = [.. timeline.Children];

        Assert.Equal([0d, 300d], children.Select(c => c.Offset));
        Assert.Same(Eases.OutQuad, timeline.PlaybackEase);
        Assert.Null(TestHelpers.GetChildAtIndex(timeline, 0)!.PlaybackEase);
    }

    [Fact]
    public void TweenSpanGettersMatchTheTimelinePosition()
    {
        var target = new StyleTarget();
        var timeline = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(target, new()
            {
                ["X"] = TweenValue.Keyframes(new TweenParams { To = 10, Duration = 100 }, new TweenParams { To = 20, Duration = 200, Delay = 50 }),
            }, 500);
        List<Tween> tweens = [.. TestHelpers.GetChildAtIndex(timeline, 0)!.Tweens];

        Assert.Equal((0d, 0d, 100d), (tweens[0].Delay, tweens[0].StartTime, tweens[0].Duration));
        Assert.Equal((50d, 150d, 200d), (tweens[1].Delay, tweens[1].StartTime, tweens[1].Duration));
        Assert.Equal(timeline.Offset + 500, tweens[0].AbsoluteStart);
        Assert.Equal(timeline.Offset + 850, tweens[1].AbsoluteEnd);
    }

    [Fact]
    public void IsOverriddenFlipsWhenANewerAnimationTakesTheProperty()
    {
        var target = new StyleTarget();
        List<Tween> tweens = [.. Presto.Animate(target, new() { ["X"] = 100, Duration = 1000 }).Tweens];
        var tween = tweens[0];
        Assert.False(tween.IsOverridden);

        Presto.Animate(target, new() { ["X"] = 0, Duration = 1000 });

        Assert.True(tween.IsOverridden);
        Assert.Equal(Composition.Replace, tween.Composition);
    }

    [Fact]
    public void GetCurrentMatchesTheTarget()
    {
        var plain = new TestObject();
        var style = new StyleTarget();
        List<Tween> number = [.. Presto.Animate(plain, new() { ["PlainValue"] = 110, ["ValueWithUnit"] = 110, Duration = 1000 }).Tweens];
        List<Tween> colors = [.. Presto.Animate(style, new() { ["BackgroundColor"] = "#FF0000", Duration = 1000 }).Tweens];
        var color = colors[0];
        Frames.Advance(400);

        Assert.Equal(Values.ValueKind.Number, number[0].ValueKind);
        Assert.Equal((double)Presto.Get(plain, "PlainValue")!, number[0].GetCurrent().Number);
        Assert.Equal("px", number[1].Unit);
        Assert.Equal(Presto.Get(plain, "ValueWithUnit"), number[1].GetCurrent().Text);
        Assert.Equal([255d, 255, 255, 1], color.GetFrom().Channels);
        Assert.Equal([255d, 0, 0, 1], color.GetTo().Channels);
        Assert.Equal(Presto.Get(style, "BackgroundColor"), color.GetCurrent().Text);
    }

    [Fact]
    public void EaseNamesForFactoriesAreKeptOnlyWhileDiagnosticsAreActive()
    {
        var inactive = Eases.Out(3);
        Presto.Engine.Diagnostics.Enabled = true;
        var active = Eases.Out(3);

        Assert.True(Eases.TryGetName(Eases.OutQuad, out var fixedName));
        Assert.Equal("outQuad", fixedName);
        Assert.True(Eases.TryGetName(Eases.None, out var linearName));
        Assert.Equal("linear", linearName);
        Assert.False(Eases.TryGetName(inactive, out _));
        Assert.True(Eases.TryGetName(active, out var activeName));
        Assert.Equal("out(3)", activeName);
        Assert.True(Eases.TryGetName(EaseParser.Parse("outElastic(1.25, .5)"), out var parsedName));
        Assert.Equal("outElastic(1.25, 0.5)", parsedName);
        Assert.False(Eases.TryGetName(t => t, out _));
    }

    [Fact]
    public void TheDefaultEaseAndEasesParsedBeforeEnablingAreNamed()
    {
        var parsedEarlier = EaseParser.Parse("inOut(1.375)");
        Assert.False(Eases.TryGetName(parsedEarlier, out _));

        Presto.Engine.Diagnostics.Enabled = true;

        Assert.True(Eases.TryGetName(Eases.OutQuad, out _));
        Assert.True(Eases.TryGetName(Presto.Engine.Defaults.Ease.Resolve(this, 0, 1, out _), out var defaultName));
        Assert.Equal("out(2)", defaultName);
        Assert.True(Eases.TryGetName(parsedEarlier, out var parsedName));
        Assert.Equal("inOut(1.375)", parsedName);
    }
}
