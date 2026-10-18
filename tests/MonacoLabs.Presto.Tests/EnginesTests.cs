using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Values;
using static MonacoLabs.Presto.Tests.TestHelpers;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// The acceptance suite for COMPOSITOR-PLAN §4.1: a second engine owns its own timers, defaults, scopes and additive state,
/// even when it is ticked from another thread. Upstream has no equivalent, since anime.js only ever has one engine.
/// </summary>
public sealed class EnginesTests : EngineTestBase
{
    [Fact]
    public void AdvancingOneEngineLeavesTheOtherWhereItWas()
    {
        var first = new IsolatedEngine();
        var second = new IsolatedEngine();
        var firstTarget = new StyleTarget();
        var secondTarget = new StyleTarget();

        Animation firstAnimation, secondAnimation;
        using (first.Engine.MakeCurrent()) firstAnimation = Presto.Animate(firstTarget, new() { ["X"] = 100, Duration = 100 });
        using (second.Engine.MakeCurrent()) secondAnimation = Presto.Animate(secondTarget, new() { ["X"] = 100, Duration = 100 });

        first.Frames.Advance(100, 16);

        Assert.Equal(100, firstTarget.X);
        Assert.Equal(100, firstAnimation.CurrentTime);
        Assert.Equal(0, secondTarget.X);
        Assert.Equal(0, secondAnimation.CurrentTime);

        second.Frames.Advance(100, 16);

        Assert.Equal(100, secondTarget.X);
        Assert.Equal(100, secondAnimation.CurrentTime);
    }

    [Fact]
    public void TimersCreatedUnderAnEngineRegisterWithItAndNotTheMainEngine()
    {
        var scene = new IsolatedEngine();

        using (scene.Engine.MakeCurrent())
        {
            var timer = Presto.CreateTimer(new() { Duration = 100 });

            Assert.Same(timer, GetChildAtIndex(scene.Engine, 0));
            Assert.Equal(1, GetChildLength(scene.Engine));
            Assert.Equal(0, GetChildLength(Presto.Engine));
        }
    }

    [Fact]
    public void ResumingAfterTheBlockEndsReRegistersWithTheEngineTheTimerWasCreatedUnder()
    {
        var scene = new IsolatedEngine();
        Timer timer;
        using (scene.Engine.MakeCurrent()) timer = Presto.CreateTimer(new() { Duration = 100 });

        timer.Pause();
        scene.Frames.Advance(16);
        Assert.Equal(0, GetChildLength(scene.Engine));

        timer.Resume();

        Assert.Same(scene.Engine, timer.OwnerEngine);
        Assert.Same(timer, GetChildAtIndex(scene.Engine, 0));
        Assert.Equal(0, GetChildLength(Presto.Engine));
    }

    [Fact]
    public void CurrentIsTheMainEngineUntilMakeCurrentNestsOne()
    {
        var outer = new IsolatedEngine();
        var inner = new IsolatedEngine();

        Assert.Same(Presto.Engine, Engine.Current);

        using (outer.Engine.MakeCurrent())
        {
            Assert.Same(outer.Engine, Engine.Current);
            using (inner.Engine.MakeCurrent()) Assert.Same(inner.Engine, Engine.Current);
            Assert.Same(outer.Engine, Engine.Current);
        }

        Assert.Same(Presto.Engine, Engine.Current);
    }

    [Fact]
    public void DefaultsStartAsACopyOfTheMainEnginesAndThenDiverge()
    {
        Presto.Defaults.Duration = 250;
        var scene = new IsolatedEngine();

        Assert.NotSame(Presto.Defaults, scene.Engine.Defaults);
        Assert.Equal(250, scene.Engine.Defaults.Duration.AsDouble);

        scene.Engine.Defaults.Duration = 500;
        Presto.Defaults.Delay = 20;

        Assert.Equal(250, Presto.Defaults.Duration.AsDouble);
        Assert.Equal(0, scene.Engine.Defaults.Delay.AsDouble);
    }

    [Fact]
    public void ScopeExecuteSwapsOnlyItsOwnEnginesDefaults()
    {
        var scene = new IsolatedEngine();
        var mainDefaults = Presto.Defaults;
        var sceneDefaults = scene.Engine.Defaults;
        var target = new StyleTarget();

        using (scene.Engine.MakeCurrent())
        {
            var scope = Presto.CreateScope(new() { Defaults = defaults => defaults.Duration = 500 });
            var inside = scope.Execute(_ =>
            {
                Assert.Same(scope, scene.Engine.CurrentScope);
                Assert.Null(Presto.Engine.CurrentScope);
                Assert.Same(mainDefaults, Presto.Defaults);
                return Presto.Animate(target, new() { ["X"] = 100 });
            });

            Assert.Equal(500, inside.Duration);
            Assert.Same(sceneDefaults, scene.Engine.Defaults);
            Assert.Null(scene.Engine.CurrentScope);

            scene.Frames.Advance(600, 16);
            Assert.Equal(100, target.X);

            scope.Revert();

            Assert.Equal(0, target.X);
            Assert.True(inside.Canceled);
        }

        Assert.Same(mainDefaults, Presto.Defaults);
    }

    [Fact]
    public void ScopeExecuteMakesItsOwnEngineAmbientOutsideAMakeCurrentBlock()
    {
        var scene = new IsolatedEngine();
        var target = new StyleTarget();
        Scope scope;
        using (scene.Engine.MakeCurrent()) scope = Presto.CreateScope(new() { Defaults = defaults => defaults.Duration = 500 });

        var animation = scope.Execute(_ => Presto.Animate(target, new() { ["X"] = 100 }));

        Assert.Same(scene.Engine, animation.OwnerEngine);
        Assert.Equal(500, animation.Duration);
        Assert.Equal(0, GetChildLength(Presto.Engine));

        scene.Frames.Advance(600, 16);
        Assert.Equal(100, target.X);

        scope.Revert();

        Assert.Equal(0, target.X);
        Assert.True(animation.Canceled);
    }

    [Fact]
    public void AnEngineCreatedInSecondsModeRunsAtTheSameSpeedAsTheMainOne()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        var scene = new IsolatedEngine();
        var target = new StyleTarget();

        using (scene.Engine.MakeCurrent()) Presto.Animate(target, new() { ["X"] = 100, Duration = 1, Ease = "linear" });

        Assert.Equal(1, scene.Engine.Speed);

        scene.Frames.Advance(500, 16);
        Assert.True(target.X is > 40 and < 60, $"expected roughly half way after 500 ms, was {target.X}");

        scene.Frames.Advance(500, 16);
        Assert.Equal(100, target.X);
    }

    [Fact]
    public void SettingTheTimeUnitOnAnotherEngineThrowsAndLeavesTheTimeScale()
    {
        var scene = new IsolatedEngine();

        Assert.Throws<InvalidOperationException>(() => scene.Engine.TimeUnit = TimeUnit.Seconds);
        Assert.Throws<InvalidOperationException>(() => scene.Engine.TimeUnit = TimeUnit.Milliseconds);

        Assert.Equal(1, Globals.TimeScale);
        Assert.Equal(1, scene.Engine.Speed);
    }

    [Fact]
    public void AdditiveTweensAreInvisibleToAnotherEngine()
    {
        var first = new IsolatedEngine();
        var second = new IsolatedEngine();
        var target = new StyleTarget();

        using (first.Engine.MakeCurrent())
        {
            Presto.Animate(target, new() { ["X"] = 100, Duration = 100, Composition = Composition.Additive });
        }

        Assert.Single(first.Engine.Additive.Lookups);
        Assert.Empty(second.Engine.Additive.Lookups);
        Assert.Null(second.Engine.Additive.Animation);

        second.Frames.Advance(100, 16);
        second.Engine.Update();
        Assert.Equal(0, target.X);

        first.Frames.Advance(100, 16);
        Assert.Equal(100, target.X);
    }

    [Fact]
    public void ReplaceTweensDoNotComposeAcrossEngines()
    {
        var first = new IsolatedEngine();
        var second = new IsolatedEngine();
        var target = new StyleTarget();

        Animation firstAnimation, secondAnimation;
        using (first.Engine.MakeCurrent()) firstAnimation = Presto.Animate(target, new() { ["X"] = new TweenParams { From = 0, To = 100 }, Duration = 100 });
        using (second.Engine.MakeCurrent()) secondAnimation = Presto.Animate(target, new() { ["X"] = new TweenParams { From = 200, To = 300 }, Duration = 100 });

        first.Frames.Advance(50, 10);
        Assert.InRange(target.X, 1, 99);

        second.Frames.Advance(50, 10);
        Assert.InRange(target.X, 201, 299);

        Assert.False(firstAnimation.Canceled);
        Assert.False(secondAnimation.Canceled);
        Assert.False(firstAnimation._headTween!._isOverridden);
        Assert.False(secondAnimation._headTween!._isOverridden);
    }

    [Fact]
    public async Task ATimerTickedOnAWorkerThreadStaysOnItsOwnEngine()
    {
        var (sceneEngine, timer) = await Task.Run(() =>
        {
            var scene = new IsolatedEngine();
            using (scene.Engine.MakeCurrent())
            {
                var sceneTimer = Presto.CreateTimer(new() { Duration = 100 });
                scene.Frames.Advance(100, 16);
                return (scene.Engine, sceneTimer);
            }
        });

        Assert.Same(sceneEngine, timer.OwnerEngine);
        Assert.True(timer.Completed);
        Assert.Equal(100, timer.CurrentTime);
        Assert.Equal(0, GetChildLength(Presto.Engine));
    }

    [Fact]
    public void DisposingADefaultScopeFallsBackToTheMainEngine()
    {
        var scene = new IsolatedEngine();

        using (scene.Engine.MakeCurrent())
        {
            default(EngineScope).Dispose();
            Assert.Same(Presto.Engine, Engine.Current);
        }
    }

    [Fact]
    public void ATimelineChildBindsToItsTimelinesEngineWhateverEngineIsCurrent()
    {
        var scene = new IsolatedEngine();
        var other = new IsolatedEngine();
        Timeline timeline;
        using (scene.Engine.MakeCurrent()) timeline = Presto.CreateTimeline();

        using (other.Engine.MakeCurrent()) timeline.Add(new StyleTarget(), new() { ["X"] = 100, Duration = 100 }).Call(_ => { });

        Assert.Same(scene.Engine, GetChildAtIndex(timeline, 0)!.OwnerEngine);
        Assert.Same(scene.Engine, GetChildAtIndex(timeline, 1)!.OwnerEngine);
        Assert.Equal(0, GetChildLength(other.Engine));
    }

    [Fact]
    public void AnEngineCreatedInsideAnotherEnginesBlockCopiesTheMainEnginesDefaults()
    {
        Presto.Defaults.Duration = 250;
        var scene = new IsolatedEngine();
        scene.Engine.Defaults.Duration = 500;

        Engine nested;
        using (scene.Engine.MakeCurrent()) nested = new Engine(new ManualFrameSource());

        Assert.Equal(250, nested.Defaults.Duration.AsDouble);
    }

    [Fact]
    public void TheMainEnginesTimeUnitSetsTheTimeScaleAndTickThresholdOfEveryEngine()
    {
        var scene = new IsolatedEngine();

        Presto.Engine.TimeUnit = TimeUnit.Seconds;

        Assert.Equal(TimeUnit.Seconds, scene.Engine.TimeUnit);
        Assert.Equal(.001, Globals.TimeScale);
        Assert.Equal(.2, Globals.TickThreshold, 12);
    }

    [Fact]
    public void PrecisionSetOnAnyEngineAppliesToEveryEngine()
    {
        var scene = new IsolatedEngine();
        var timer = Presto.CreateTimer(new() { Duration = 100, Autoplay = false });

        scene.Engine.Precision = 2;
        timer.Seek(12.3456);

        Assert.Equal(2, Presto.Engine.Precision);
        Assert.Equal(12.35, timer.CurrentTime);
    }

    [Fact]
    public void ACodecRegisteredOnceServesAnimationsOnEveryEngine()
    {
        Presto.Codecs[typeof(Furlongs)] = new StructCodec<Furlongs>(f => [f.Value], channels => new Furlongs(channels[0]));
        try
        {
            var course = new Course();
            RunOnThread(() =>
            {
                var scene = new IsolatedEngine();
                using (scene.Engine.MakeCurrent()) Presto.Animate(course, new() { ["Distance"] = TweenValue.Of(new Furlongs(8)), Duration = 100, Ease = "linear" });
                scene.Frames.Advance(50, 10);
            });

            Assert.InRange(course.Distance.Value, 2, 6);
        }
        finally
        {
            Presto.Codecs.Remove(typeof(Furlongs));
        }
    }

    [Fact]
    public void IdsAndRandomSeedsStayUniqueAcrossThreads()
    {
        const int threads = 4, perThread = 2000;
        var timerIds = new ConcurrentBag<string>();
        var animationIds = new ConcurrentBag<string>();
        var tweenIds = new ConcurrentBag<int>();
        var seeds = new ConcurrentBag<(double, double)>();

        RunOnThreads(threads, _ =>
        {
            var scene = new IsolatedEngine();
            using var current = scene.Engine.MakeCurrent();
            for (var i = 0; i < perThread; i++)
            {
                timerIds.Add(Presto.CreateTimer(new() { Autoplay = false }).Id);
                var animation = Presto.Animate(new StyleTarget(), new() { ["X"] = 1, Autoplay = false });
                animationIds.Add(animation.Id);
                tweenIds.Add(animation._headTween!.Id);
                var random = Utils.CreateSeededRandom();
                seeds.Add((random(0, 1e9, 0), random(0, 1e9, 0)));
            }
        });

        Assert.Equal(threads * perThread, timerIds.Distinct().Count());
        Assert.Equal(threads * perThread, animationIds.Distinct().Count());
        Assert.Equal(threads * perThread, tweenIds.Distinct().Count());
        Assert.Equal(threads * perThread, seeds.Distinct().Count());
    }

    [Fact]
    public void AnimationsBuiltOnSeveralThreadsAtOnceResolveTheirOwnValues()
    {
        RunOnThreads(4, thread =>
        {
            var scene = new IsolatedEngine();
            using var current = scene.Engine.MakeCurrent();
            for (var i = 0; i < 2000; i++)
            {
                var from = thread * 10000 + i;
                var animation = Presto.Animate(new StyleTarget(), new() { ["TranslateX"] = TweenValue.FromTo($"{from}px", $"{from * 2}em"), Autoplay = false });
                var tween = animation._headTween!;
                Assert.Equal(from, tween._fromNumber);
                Assert.Equal(from * 2, tween._toNumber);
                Assert.Equal("em", tween._unit);
            }
        });
    }

    [Fact]
    public void TheEaseAndAccessorCachesFillFromSeveralThreadsAtOnce()
    {
        string[] gauges = ["A", "B", "C", "D", "E", "F", "G", "H"];

        RunOnThreads(4, _ =>
        {
            for (var i = 0; i < 500; i++)
            {
                var power = 1 + i / 1000d;
                var ease = EaseParser.Parse(string.Create(CultureInfo.InvariantCulture, $"out({power})"));
                Assert.Equal(Eases.Out(power)(.3), ease(.3), 12);
            }

            var scene = new IsolatedEngine();
            using var current = scene.Engine.MakeCurrent();
            for (var i = 0; i < 200; i++)
            {
                var name = gauges[i % gauges.Length];
                var target = new Gauges();
                var animation = Presto.Animate(target, new() { [name] = i, Autoplay = false });
                animation.Seek(animation.Duration);
                Assert.Equal(i, target.Read(name));
            }
        });
    }

#if DEBUG
    [Fact]
    public void APinnedEngineAndItsTimersFailFromAnotherThread()
    {
        var scene = new IsolatedEngine();
        Timer timer;
        using (scene.Engine.MakeCurrent()) timer = Presto.CreateTimer(new() { Duration = 100 });
        using var asserts = new ThrowingAsserts();

        RunOnThread(() =>
        {
            scene.Engine.Update();
            scene.Engine.Speed = 1;
            timer.Seek(10);
        });

        Assert.Throws<ThreadPinException>(() => scene.Engine.Pause());
        Assert.Throws<ThreadPinException>(() => scene.Engine.Resume());
        Assert.Throws<ThreadPinException>(() => scene.Engine.Freeze());
        Assert.Throws<ThreadPinException>(() => scene.Engine.Thaw());
        Assert.Throws<ThreadPinException>(() => scene.Engine.Speed = 2);
        Assert.Throws<ThreadPinException>(() => scene.Engine.Precision = 2);
        Assert.Throws<ThreadPinException>(() => scene.Engine.Hidden = true);
        Assert.Throws<ThreadPinException>(() => scene.Engine.PauseOnHidden = false);
        Assert.Throws<ThreadPinException>(() => timer.Pause());
        Assert.Throws<ThreadPinException>(() => timer.Resume());
        Assert.Throws<ThreadPinException>(() => timer.Seek(20));
        Assert.Throws<ThreadPinException>(() => timer.Reset());
    }

    [Fact]
    public void ThePinnedMainEngineRejectsATimeUnitChangeFromAnotherThread()
    {
        using var asserts = new ThrowingAsserts();
        try
        {
            RunOnThread(Presto.Engine.Update);

            Assert.Throws<ThreadPinException>(() => Presto.Engine.TimeUnit = TimeUnit.Seconds);
            Assert.Equal(TimeUnit.Milliseconds, Presto.Engine.TimeUnit);
        }
        finally
        {
            Presto.Engine.FrameSource = Frames;
        }
    }

    [Fact]
    public void ReplacingTheFrameSourceDropsThePinUntilTheNextUpdate()
    {
        var scene = new IsolatedEngine();
        using var asserts = new ThrowingAsserts();
        RunOnThread(scene.Engine.Update);

        scene.Engine.FrameSource = new ManualFrameSource();
        scene.Engine.Pause();
        scene.Engine.Update();
        scene.Engine.Pause();

        Assert.Throws<ThreadPinException>(() => RunOnThread(() => scene.Engine.Pause()));
    }

    private sealed class ThreadPinException(string? message) : Exception(message);

    /// <summary>Turns a failed <see cref="Debug.Assert(bool)"/> into a <see cref="ThreadPinException"/> until disposed.</summary>
    private sealed class ThrowingAsserts : TraceListener
    {
        private readonly TraceListener[] _previous;

        public ThrowingAsserts()
        {
            _previous = [.. Trace.Listeners.Cast<TraceListener>()];
            Trace.Listeners.Clear();
            Trace.Listeners.Add(this);
        }

        public override void Fail(string? message, string? detailMessage) => throw new ThreadPinException(message);

        public override void Write(string? message) { }

        public override void WriteLine(string? message) { }

        protected override void Dispose(bool disposing)
        {
            Trace.Listeners.Remove(this);
            Trace.Listeners.AddRange(_previous);
            base.Dispose(disposing);
        }
    }
#endif

    private static void RunOnThread(Action action) => RunOnThreads(1, _ => action());

    /// <summary>Runs <paramref name="body"/> on <paramref name="count"/> fresh threads released together, rethrowing the first failure.</summary>
    private static void RunOnThreads(int count, Action<int> body)
    {
        using var gate = new ManualResetEventSlim();
        var errors = new ConcurrentQueue<Exception>();
        var threads = new Thread[count];
        for (var i = 0; i < count; i++)
        {
            var index = i;
            threads[i] = new Thread(() =>
            {
                gate.Wait();
                try { body(index); }
                catch (Exception e) { errors.Enqueue(e); }
            });
            threads[i].Start();
        }
        gate.Set();
        foreach (var thread in threads) thread.Join();
        if (errors.TryDequeue(out var error)) ExceptionDispatchInfo.Throw(error);
    }

    private readonly record struct Furlongs(double Value);

    private sealed class Course
    {
        public Furlongs Distance { get; set; }
    }

    private sealed class Gauges
    {
        public double A { get; set; }
        public double B { get; set; }
        public double C { get; set; }
        public double D { get; set; }
        public double E { get; set; }
        public double F { get; set; }
        public double G { get; set; }
        public double H { get; set; }

        public double Read(string name) => name switch
        {
            "A" => A, "B" => B, "C" => C, "D" => D, "E" => E, "F" => F, "G" => G, _ => H,
        };
    }

    /// <summary>An engine of its own with its own frame source, for the tests that need a second engine beside <see cref="Presto.Engine"/>.</summary>
    private sealed class IsolatedEngine
    {
        public IsolatedEngine()
        {
            Frames = new ManualFrameSource();
            Engine = new Engine(Frames);
        }

        public Engine Engine { get; }

        public ManualFrameSource Frames { get; }
    }
}
