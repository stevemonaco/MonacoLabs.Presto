using System.Reflection;
using System.Runtime.Loader;
using static MonacoLabs.Presto.Tests.TestHelpers;

namespace MonacoLabs.Presto.Tests;

/// <summary>Port of anime.js engine.test.js, adapted to timers driven by a manual frame source.</summary>
public sealed class EngineTests : EngineTestBase
{
    [Fact]
    public void InitialOffsetPositionShouldBeProperlyCalculatedOnColdStart()
    {
        Timer? timer2 = null;

        Frames.Advance(55);
        var timer1 = Presto.CreateTimer(new()
        {
            Duration = 20,
            OnComplete = _ => timer2 = Presto.CreateTimer(new() { Duration = 20 }),
        });
        RunUntil(() => timer2 != null);

        Assert.NotNull(timer2);
        Assert.True(timer1._offset > 50);
        Assert.True(timer1._offset < timer2._offset);
    }

    [Fact]
    public void SetUseDefaultMainLoopToFalseShouldPreventTimersFromRunning()
    {
        Presto.Engine.UseDefaultMainLoop = false;
        var updates = 0;

        var timer = Presto.CreateTimer(new() { Duration = 20, OnUpdate = _ => updates++ });
        Frames.Advance(70, 16);

        Assert.False(timer.Began);
        Assert.Equal(0, timer.CurrentTime);
        Assert.Equal(0, updates);
    }

    [Fact]
    public void ManuallyTickTheEngineWithAnExternalLoop()
    {
        Presto.Engine.UseDefaultMainLoop = false;
        var updates = 0;

        var timer = Presto.CreateTimer(new() { Duration = 50, OnUpdate = _ => updates++ });
        for (var i = 0; i < 5; i++)
        {
            Frames.Advance(16);
            Presto.Engine.Update();
        }

        Assert.True(timer.Began);
        Assert.True(timer.Completed);
        Assert.Equal(50, timer.CurrentTime);
        Assert.True(updates > 2);
        Assert.Equal(0, GetChildLength(Presto.Engine));
    }

    [Fact]
    public void PauseAndResumeTheEngine()
    {
        var updates = 0;
        var timer = Presto.CreateTimer(new() { Duration = 50, OnUpdate = _ => updates++ });

        Presto.Engine.Pause();
        Frames.Advance(50, 16);

        Assert.False(timer.Began);
        Assert.False(timer.Completed);
        Assert.Equal(0, timer.CurrentTime);
        Assert.Equal(0, updates);
        Assert.Same(timer, GetChildAtIndex(Presto.Engine, 0));

        Presto.Engine.Resume();
        Frames.Advance(100, 16);

        Assert.True(timer.Began);
        Assert.True(timer.Completed);
        Assert.Equal(50, timer.CurrentTime);
        Assert.True(updates > 2);
    }

    [Fact]
    public void DefaultPrecisionShouldBe4()
    {
        Assert.Equal(4, Presto.Engine.Precision);

        var timer = Presto.CreateTimer(new() { Duration = 500, Autoplay = false });
        timer.Seek(0.123456);

        Assert.Equal(0.1235, timer.CurrentTime);
    }

    [Fact]
    public void ChangingPrecisionAffectsReportedTime()
    {
        Presto.Engine.Precision = 1;

        var timer = Presto.CreateTimer(new() { Duration = 500, Autoplay = false });
        timer.Seek(0.123456);

        Assert.Equal(0.1, timer.CurrentTime);
    }

    [Fact]
    public void ChangingTheTimeUnitShouldAffectDurationValues()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        Assert.Equal(1, Presto.Defaults.Duration.AsDouble);

        var timer = Presto.CreateTimer(new() { Duration = .75 });
        Frames.Advance(150, 16);

        Assert.True(timer.CurrentTime > .1);
        Assert.True(timer.CurrentTime < .75);

        Presto.Engine.TimeUnit = TimeUnit.Milliseconds;
        Assert.Equal(1000, Presto.Defaults.Duration.AsDouble);
    }

    [Theory]
    [InlineData(4, "1.1235px")]
    [InlineData(1, "1.1px")]
    public void PrecisionRoundsOnlyTheInBetweenFrames(int precision, string midValue)
    {
        Presto.Engine.Precision = precision;
        var target = new StyleTarget { TranslateX = "0.12345px" };
        var animation = Presto.Animate(target, new()
        {
            ["TranslateX"] = 2.12345,
            Ease = "linear",
            Autoplay = false,
            Duration = 500,
        });

        Assert.Equal("0.12345px", target.TranslateX);
        animation.Seek(250);
        Assert.Equal(midValue, target.TranslateX);
        animation.Seek(500);
        Assert.Equal("2.12345px", target.TranslateX);
    }

    [Fact]
    public void EngineSpeedReads1InSecondsMode()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;

        Assert.Equal(1, Presto.Engine.Speed);
        Assert.Equal(.001, Presto.Engine._speed);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    public void RemovingATargetFromACallbackKeepsTheOtherChildrenTicking(int remover, int removed)
    {
        StyleTarget[] targets = [new(), new(), new()];
        var updatesSinceRemoval = new int[3];
        var hasRemoved = false;
        var animations = new Animation[3];
        for (var i = 0; i < 3; i++)
        {
            var index = i;
            animations[i] = Presto.Animate(targets[i], new()
            {
                ["TranslateX"] = 100,
                Duration = 200,
                OnUpdate = self =>
                {
                    if (hasRemoved) updatesSinceRemoval[index]++;
                    else if (index == remover && self.CurrentTime >= 50)
                    {
                        hasRemoved = true;
                        Presto.Remove(targets[removed]);
                    }
                },
            });
        }

        RunUntil(() => hasRemoved);
        Frames.Advance(16);

        Assert.True(animations[removed].Paused);
        for (var i = 0; i < 3; i++)
        {
            if (i != removed) Assert.Equal(i > remover ? 2 : 1, updatesSinceRemoval[i]);
        }

        var later = Presto.Animate(new StyleTarget(), new() { ["TranslateX"] = 100, Duration = 200 });
        Frames.Advance(32, 16);

        Assert.True(later.CurrentTime > 0);
        Assert.Equal(3, GetChildLength(Presto.Engine));
    }

    [Fact]
    public void TheMainEngineStartsOnAStopwatchFrameSourceWithFreshDefaultsInMilliseconds()
    {
        var context = new AssemblyLoadContext(nameof(TheMainEngineStartsOnAStopwatchFrameSourceWithFreshDefaultsInMilliseconds), isCollectible: true);
        try
        {
            var assembly = context.LoadFromAssemblyPath(typeof(Presto).Assembly.Location);
            var presto = assembly.GetType(typeof(Presto).FullName!)!;
            var engine = presto.GetProperty(nameof(Presto.Engine))!.GetValue(null)!;
            object Read(string name) => engine.GetType().GetProperty(name)!.GetValue(engine)!;
            var freshDefaults = Activator.CreateInstance(assembly.GetType(typeof(PrestoDefaults).FullName!)!);

            Assert.Equal(typeof(StopwatchFrameSource).FullName, Read(nameof(Engine.FrameSource)).GetType().FullName);
            Assert.Equal(freshDefaults, Read(nameof(Engine.Defaults)));
            Assert.Equal(nameof(TimeUnit.Milliseconds), Read(nameof(Engine.TimeUnit)).ToString());
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void ANewEnginePumpsItselfWhileShownAtTheFrameSourcesRateAndUpTo240Fps()
    {
        var source = new ManualFrameSource();
        var engine = new Engine(source);

        Assert.True(engine.UseDefaultMainLoop);
        Assert.False(engine.Hidden);
        Assert.Equal(1, engine.TimeDilation);
        Assert.Equal(240, engine.Fps);

        var now = engine.Now;
        source.Advance(100);
        Assert.Equal(now + 100, engine.Now);

        engine.Wake();
        Assert.True(source.IsRunning);
        Assert.True(engine.IsRunning);
    }

    [Fact]
    public void ReplacingTheFrameSourceWhileRunningMovesThePumpWithoutAJump()
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(100, 16);
        var (before, now) = (timer.CurrentTime, Presto.Engine.Now);
        var replacement = new ManualFrameSource();
        replacement.Advance(5000);

        Presto.Engine.FrameSource = replacement;

        Assert.False(Frames.IsRunning);
        Assert.True(replacement.IsRunning);
        Assert.Equal(now, Presto.Engine.Now);
        replacement.Advance(16);
        Assert.Equal(before + 16, timer.CurrentTime);
    }

    [Fact]
    public void ReplacingTheFrameSourceWhileIdleDoesNotStartTheNewOne()
    {
        var now = Presto.Engine.Now;
        var replacement = new ManualFrameSource();
        replacement.Advance(5000);

        Presto.Engine.FrameSource = replacement;

        Assert.False(replacement.IsRunning);
        Assert.Equal(now, Presto.Engine.Now);
    }

    [Fact]
    public void UpdateRunsTheAdditiveAnimationThenStepsValuesThenFlushesSinks()
    {
        var log = new List<string>();
        var target = new LoggingTarget(log);
        var source = Presto.CreateMotionValue();
        var follower = source.Map(v =>
        {
            log.Add("step");
            return v;
        }).Spring(new(stiffness: 120, damping: 12));
        follower.Changed += _ => log.Add("flush");
        var frame = 0;
        Presto.CreateTimer(new() { Duration = 1000, OnUpdate = _ =>
        {
            log.Add("children");
            source.Set(++frame * 10);
        } });
        Presto.Animate(target, new() { ["X"] = 100, Duration = 1000, Ease = "linear", Composition = Composition.Additive });
        Frames.Advance(48, 16);

        log.Clear();
        Frames.Advance(16);

        Assert.Equal(["children", "additive", "step", "flush"], log);
    }

    [Fact]
    public void PausingAnIdleEngineChangesNothing()
    {
        var engine = Presto.Engine;
        engine.Resume();
        Frames.Advance(16);
        Assert.False(engine.IsRunning);

        engine.Pause();

        Assert.False(engine.Paused);
        var timer = Presto.CreateTimer(new() { Duration = 100 });
        Frames.Advance(32, 16);
        Assert.True(timer.CurrentTime > 0);
    }

    [Fact]
    public void AnEngineReadsPausedUntilItsFirstResume()
    {
        var source = new ManualFrameSource();
        var engine = new Engine(source);
        Assert.True(engine.Paused);

        engine.Wake();
        Assert.True(engine.Paused);

        engine.Resume();
        Assert.False(engine.Paused);
    }

    [Fact]
    public void ResumingAnEngineThatIsNotPausedDoesNothing()
    {
        var engine = Presto.Engine;
        engine.UseDefaultMainLoop = false;
        engine.Resume();
        Presto.CreateTimer(new() { Duration = 100 });
        engine.UseDefaultMainLoop = true;

        engine.Resume();

        Assert.False(engine.Paused);
        Assert.False(engine.IsRunning);
        Assert.False(Frames.IsRunning);
    }

    [Fact]
    public void PlayingATimerAfterPauseRestartsThePumpWithoutRebasing()
    {
        var engine = Presto.Engine;
        engine.Resume();
        var timer = Presto.CreateTimer(new() { Duration = 10000 });
        Frames.Advance(100, 16);
        var before = timer.CurrentTime;

        engine.Pause();
        Frames.Advance(500);
        Presto.CreateTimer(new() { Duration = 10000 });

        Assert.True(engine.Paused);
        Assert.True(Frames.IsRunning);
        Frames.Advance(16);
        Assert.Equal(before + 516, timer.CurrentTime);
    }

    [Fact]
    public void SettingTheTimeUnitInForceChangesNothing()
    {
        var engine = Presto.Engine;
        engine.TimeUnit = TimeUnit.Milliseconds;
        Assert.Equal(1000, Presto.Defaults.Duration.AsDouble);
        Assert.Equal(1, engine._speed);

        engine.TimeUnit = TimeUnit.Seconds;
        engine.TimeUnit = TimeUnit.Seconds;
        Assert.Equal(1, Presto.Defaults.Duration.AsDouble);
        Assert.Equal(.001, engine._speed);

        var other = new Engine(new ManualFrameSource());
        Assert.Throws<InvalidOperationException>(() => other.TimeUnit = TimeUnit.Seconds);
    }

    [Fact]
    public void AFunctionBasedDefaultDurationIsNotRescaledByTheTimeUnit()
    {
        FunctionValue duration = new((_, _, _) => 2);
        Presto.Defaults.Duration = duration;

        Presto.Engine.TimeUnit = TimeUnit.Seconds;

        Assert.Null(Presto.Defaults.Duration.AsDouble);
        Assert.Equal(2, Presto.Animate(new StyleTarget(), new() { ["X"] = 1, Autoplay = false }).Duration);
    }

    private sealed class LoggingTarget(List<string> log)
    {
        public double X
        {
            get;
            set
            {
                field = value;
                log.Add("additive");
            }
        }
    }
}
