using MonacoLabs.Presto.Core;

namespace MonacoLabs.Presto.Tests;

public sealed class ReducedMotionTests : EngineTestBase
{
    [Fact]
    public void ReduceAllWritesTheEndValueOnTheFirstFrameAndCompletes()
    {
        var target = new StyleTarget();
        var calls = new List<string>();
        Presto.Defaults.ReduceAll = true;

        var animation = Presto.Animate(target, new()
        {
            ["X"] = 100,
            Duration = 1000,
            Delay = 300,
            Loop = 3,
            OnBegin = _ => calls.Add("begin"),
            OnComplete = _ => calls.Add("complete"),
        });
        Frames.Advance(16);

        Assert.Equal(100, target.X);
        Assert.Equal(Constants.MinValue, animation.Duration);
        Assert.True(animation.Completed);
        Assert.Equal(["begin", "complete"], calls);
    }

    [Fact]
    public void ReduceAllEndsAKeyframedPropertyOnItsLastKeyframe()
    {
        var target = new StyleTarget();
        Presto.Defaults.ReduceAll = true;

        Presto.Animate(target, new()
        {
            ["X"] = new TweenParams[] { new() { To = 50, Duration = 400 }, new() { To = 20, Duration = 400, Delay = 100 } },
            Keyframes = new Keyframe[] { new() { ["Y"] = 80 }, new() { ["Y"] = 30 } },
            Duration = 1000,
        });
        Frames.Advance(16);

        Assert.Equal(20, target.X);
        Assert.Equal(30, target.Y);
    }

    [Fact]
    public void ReduceAllCollapsesTimelineChildren()
    {
        var target = new StyleTarget();
        Presto.Defaults.ReduceAll = true;

        Presto.CreateTimeline()
            .Add(target, new() { ["X"] = 100, Duration = 1000 })
            .Add(target, new() { ["Y"] = 50, Duration = 1000 });
        Frames.Advance(16);

        Assert.Equal(100, target.X);
        Assert.Equal(50, target.Y);
    }

    [Fact]
    public void ReduceAllCollapsesTimelinePositions()
    {
        var target = new StyleTarget();
        var calls = new List<string>();
        Presto.Defaults.ReduceAll = true;

        var timeline = Presto.CreateTimeline()
            .Add(target, new() { ["X"] = 100, Duration = 300 }, 0)
            .Call(_ => calls.Add("call"), 400)
            .Add(target, new() { ["Y"] = 50, Duration = 300 }, 500)
            .Label("end", 1900)
            .Add(target, new() { ["X"] = 20, Duration = 300 }, 2000)
            .Add(target, new() { ["Opacity"] = 0.5, Duration = 300 }, "end");
        Frames.Advance(16);

        Assert.True(timeline.Completed);
        Assert.Equal(0, timeline.Labels["end"]);
        Assert.Equal(20, target.X);
        Assert.Equal(50, target.Y);
        Assert.Equal(0.5, target.Opacity);
        Assert.Equal(["call"], calls);
    }

    [Fact]
    public void ReduceAllLeavesAReducedAlternativeAndAPlainTimerAlone()
    {
        Presto.Defaults.ReduceAll = true;
        Presto.Defaults.ReducedMotion = true;

        var animation = Presto.Build()
            .Property("X", 100)
            .Duration(1000)
            .WhenReduced(b => b.Property("Opacity", 1).Duration(200))
            .Animate(new StyleTarget());
        var timer = Presto.CreateTimer(new() { Duration = 1000, Loop = 1 });

        Assert.Equal(200, animation.Duration);
        Assert.Equal(2000, timer.Duration);
    }

    [Fact]
    public void AScopeKeepsItsOwnFlags()
    {
        var scope = Presto.CreateScope(new() { Defaults = d => { d.ReduceAll = true; d.ReducedMotion = true; } });
        var builder = Presto.Build().Property("X", 100).WhenReduced(b => b.Property("Opacity", 1));

        var inside = scope.Execute(_ => Presto.Animate(new StyleTarget(), new() { ["X"] = 100, Duration = 1000 }));
        var insideParams = scope.Execute(_ => builder.Resolve());
        var outside = Presto.Animate(new StyleTarget(), new() { ["X"] = 100, Duration = 1000 });

        Assert.Equal(Constants.MinValue, inside.Duration);
        Assert.Equal(["Opacity"], insideParams.Properties.Keys);
        Assert.Equal(1000, outside.Duration);
        Assert.Equal(["X"], builder.Resolve().Properties.Keys);
    }

    // GAPS-2-PLAN §1.3: WhenReduced on dictionary params.
    [Fact]
    public void WhenReducedReplacesThePropertiesAndKeepsTheCallbacks()
    {
        var target = new StyleTarget();
        var completed = false;
        Presto.Defaults.ReducedMotion = true;

        Presto.Animate(target, new()
        {
            ["X"] = 100,
            Duration = 400,
            OnComplete = _ => completed = true,
            WhenReduced = new() { ["Opacity"] = 0.5 },
        });
        Frames.Advance(500);

        Assert.Equal(0, target.X);
        Assert.Equal(0.5, target.Opacity);
        Assert.True(completed);
    }

    [Fact]
    public void WhenReducedKeepsTheStaggerAndTheSettingsItDoesNotSet()
    {
        var targets = new[] { new StyleTarget(), new StyleTarget() };
        Presto.Defaults.ReducedMotion = true;

        var animation = Presto.Animate(targets, new()
        {
            ["X"] = 100,
            Delay = Presto.Stagger(100),
            Duration = 400,
            Loop = 1,
            Ease = "linear",
            WhenReduced = new() { ["Opacity"] = 0.5, Duration = 200 },
        });

        Assert.Equal(2 * (100 + 200), animation.Duration);
    }

    [Fact]
    public void WhenReducedOverridesTheEaseItSets()
    {
        var target = new StyleTarget();
        Presto.Defaults.ReducedMotion = true;

        Presto.Animate(target, new() { ["X"] = 100, Ease = "linear", Duration = 400, WhenReduced = new() { ["Y"] = 100, Ease = Eases.Steps(1) } });
        Frames.Advance(200);

        Assert.Equal(0, target.Y);
    }

    [Fact]
    public void WhenReducedIsIgnoredWhileTheFlagIsClear()
    {
        var target = new StyleTarget();

        var animation = Presto.Animate(target, new() { ["X"] = 100, Duration = 400, WhenReduced = new() { ["Opacity"] = 0.5, Duration = 200 } });
        Frames.Advance(500);

        Assert.Equal(400, animation.Duration);
        Assert.Equal(100, target.X);
        Assert.Equal(0, target.Opacity);
    }

    [Fact]
    public void ReduceAllLeavesADictionaryReducedFormAlone()
    {
        Presto.Defaults.ReduceAll = true;
        Presto.Defaults.ReducedMotion = true;

        var animation = Presto.Animate(new StyleTarget(), new() { ["X"] = 100, Duration = 1000, WhenReduced = new() { ["Opacity"] = 0.5, Duration = 200 } });

        Assert.Equal(200, animation.Duration);
    }

    [Fact]
    public void ATimelineChildPicksItsOwnReducedForm()
    {
        var target = new StyleTarget();
        Presto.Defaults.ReducedMotion = true;

        var timeline = Presto.CreateTimeline()
            .Add(target, new() { ["X"] = 100, Duration = 1000, WhenReduced = new() { ["Opacity"] = 0.5, Duration = 200 } })
            .Add(target, new() { ["Y"] = 50, Duration = 300 });
        Frames.Advance(600);

        Assert.Equal(500, timeline.Duration);
        Assert.Equal(0, target.X);
        Assert.Equal(0.5, target.Opacity);
        Assert.Equal(50, target.Y);
    }

    [Fact]
    public void ChangingEitherFlagRaisesTheSignalOnceWithTheEngine()
    {
        var raised = new List<Engine>();
        Presto.Engine.ReducedMotionChanged += raised.Add;
        try
        {
            Presto.Defaults.ReducedMotion = true;
            Presto.Defaults.ReduceAll = true;
        }
        finally
        {
            Presto.Engine.ReducedMotionChanged -= raised.Add;
        }

        Assert.Equal([Presto.Engine, Presto.Engine], raised);
    }

    [Fact]
    public void WritingTheCurrentValueRaisesNothing()
    {
        var raised = 0;
        void OnChanged(Engine _) => raised++;
        Presto.Engine.ReducedMotionChanged += OnChanged;
        try
        {
            Presto.Defaults.ReducedMotion = false;
            Presto.Defaults.ReduceAll = false;
        }
        finally
        {
            Presto.Engine.ReducedMotionChanged -= OnChanged;
        }

        Assert.Equal(0, raised);
    }

    [Fact]
    public void AWithCopyAndAScopesDefaultsRaiseNothing()
    {
        var raised = 0;
        void OnChanged(Engine _) => raised++;
        var scope = Presto.CreateScope(new() { Defaults = _ => { } });
        Presto.Engine.ReducedMotionChanged += OnChanged;
        try
        {
            (Presto.Defaults with { }).ReducedMotion = true;
            scope.Execute(_ => { Presto.Defaults.ReducedMotion = true; });
            scope.Defaults.ReduceAll = true;
        }
        finally
        {
            Presto.Engine.ReducedMotionChanged -= OnChanged;
        }

        Assert.Equal(0, raised);
        Assert.False(Presto.Defaults.ReducedMotion);
    }

    [Fact]
    public void ANewEnginesCopyRaisesOnTheNewEngineOnly()
    {
        var engine = new Engine(new ManualFrameSource());
        var raised = new List<Engine>();
        Presto.Engine.ReducedMotionChanged += raised.Add;
        engine.ReducedMotionChanged += raised.Add;
        try
        {
            engine.Defaults.ReducedMotion = true;
        }
        finally
        {
            Presto.Engine.ReducedMotionChanged -= raised.Add;
        }

        Assert.Equal([engine], raised);
    }

    [Fact]
    public void ACopyOfAnEnginesDefaultsStillEqualsIt()
    {
        var copy = Presto.Defaults with { };

        Assert.Equal(Presto.Defaults, copy);
        Assert.Equal(Presto.Defaults.GetHashCode(), copy.GetHashCode());
        copy.ReducedMotion = true;
        Assert.NotEqual(Presto.Defaults, copy);
    }

    [Fact]
    public void TheSignalLeavesARunningAnimationInItsForm()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new() { ["X"] = 100, Duration = 400, WhenReduced = new() { ["Opacity"] = 0.5, Duration = 200 } });
        Frames.Advance(100);

        Presto.Defaults.ReducedMotion = true;
        Presto.Defaults.ReduceAll = true;
        Frames.Advance(100);

        Assert.Equal(400, animation.Duration);
        Assert.InRange(target.X, 1, 99);
        Assert.Equal(0, target.Opacity);
    }
}
