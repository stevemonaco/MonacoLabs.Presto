using System.Runtime.CompilerServices;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto.Tests;

public sealed class EngineDiagnosticsTests : EngineTestBase
{
    private static EngineDiagnostics Diagnostics => Presto.Engine.Diagnostics;

    [Fact]
    public void PausedAndUnplayedRootsAreListed()
    {
        Diagnostics.Enabled = true;
        var unplayed = Presto.CreateTimer(new() { Duration = 1000, Autoplay = false });
        var paused = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(16);
        paused.Pause();
        Frames.Advance(16);

        Assert.Equal(new[] { unplayed, paused }, Diagnostics.Roots);
    }

    [Fact]
    public void EnablingRegistersTheRunningRoots()
    {
        var running = Presto.CreateTimer(new() { Duration = 1000 });
        var unplayed = Presto.CreateTimer(new() { Duration = 1000, Autoplay = false });
        Frames.Advance(16);

        Diagnostics.Enabled = true;

        Assert.Equal(new[] { running }, Diagnostics.Roots);
        Assert.Null(unplayed._diagnostics);
    }

    [Fact]
    public void CollectedRootsDropOut()
    {
        Diagnostics.Enabled = true;
        CreateUnreferencedTimer();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.Empty(Diagnostics.Roots);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateUnreferencedTimer() => Presto.CreateTimer(new() { Autoplay = false });

    [Fact]
    public void RecentlyEndedKeepsTheLatestInOrder()
    {
        Diagnostics.Enabled = true;
        var timers = new List<Timer>();
        for (var i = 0; i < 40; i++) timers.Add(Presto.CreateTimer(new() { Autoplay = false }).Cancel());

        Assert.Equal(timers[8..], Diagnostics.RecentlyEnded);
    }

    [Fact]
    public void ARootThatEndsAgainIsListedOnceAsTheLatest()
    {
        Diagnostics.Enabled = true;
        var rerun = Presto.CreateTimer(new() { Duration = 50 });
        var other = Presto.CreateTimer(new() { Autoplay = false });
        RunUntil(() => rerun.Completed);
        Frames.Advance(16);
        other.Cancel();

        rerun.Restart();
        RunUntil(() => rerun.Completed);
        Frames.Advance(16);

        Assert.Equal(new[] { other, rerun }, Diagnostics.RecentlyEnded);
    }

    [Fact]
    public void CompletedCanceledAndRevertedRootsEnd()
    {
        Diagnostics.Enabled = true;
        var completed = Presto.CreateTimer(new() { Duration = 50 });
        var canceled = Presto.CreateTimer(new() { Duration = 1000 });
        var reverted = Presto.Animate(new StyleTarget(), new() { ["X"] = 100, Duration = 1000 });
        Frames.Advance(16);

        canceled.Cancel();
        reverted.Revert();
        RunUntil(() => completed.Completed);
        Frames.Advance(16);

        Assert.Equal(new Timer[] { canceled, reverted, completed }, Diagnostics.RecentlyEnded);
    }

    [Fact]
    public void TimerCreatedFiresOncePerRootAfterInit()
    {
        Diagnostics.Enabled = true;
        var created = new List<Timer>();
        var playingWhenCreated = new List<bool>();
        void OnCreated(Timer timer)
        {
            created.Add(timer);
            playingWhenCreated.Add(!timer.Paused);
        }
        Diagnostics.TimerCreated += OnCreated;
        try
        {
            var animation = Presto.Animate(new TestObject(), new() { ["PlainValue"] = 100, Duration = 1000 });
            var timeline = Presto.CreateTimeline().Add(new TestObject(), new() { ["PlainValue"] = 100 });
            timeline.Init();
            timeline.Restart();

            Assert.Equal(new Timer[] { animation, timeline }, created);
            Assert.True(playingWhenCreated[0], "the animation was announced before Init played it");
        }
        finally
        {
            Diagnostics.TimerCreated -= OnCreated;
        }
    }

    [Fact]
    public void LastFrameCountsRootsTweensValuesAndSinks()
    {
        Diagnostics.Enabled = true;
        Presto.Animate(new[] { new TestObject(), new TestObject(), new TestObject() }, new() { ["PlainValue"] = 100, Duration = 1000 });
        Presto.CreateTimer(new() { Duration = 1000 });
        var source = Presto.CreateMotionValue();
        source.Spring(new Spring(stiffness: 100, damping: 10));
        source.Set(100);
        var sink = Presto.CreateMotionValue();
        sink.Changed += _ => { };
        sink.Set(5);

        Frames.Advance(16);

        var frame = Diagnostics.LastFrame;
        Assert.Equal(Frames.Now, frame.EngineTime);
        Assert.Equal(16, frame.Delta);
        Assert.Equal(2, frame.RootsTicked);
        Assert.Equal(3, frame.TweensRendered);
        Assert.Equal(1, frame.ValuesStepped);
        Assert.Equal(1, frame.SinksFlushed);
    }

    [Fact]
    public void DisablingClearsEverything()
    {
        Diagnostics.Enabled = true;
        Assert.True(PrestoDiagnostics.Active);
        Presto.CreateTimer(new() { Duration = 1000 }).Cancel();
        Presto.CreateAnimatable(new TestObject(), new() { ["PlainValue"] = new() });
        Frames.Advance(16);
        Assert.NotEmpty(Diagnostics.Animatables);

        Diagnostics.Enabled = false;

        Assert.False(PrestoDiagnostics.Active);
        Assert.Empty(Diagnostics.Roots);
        Assert.Empty(Diagnostics.Animatables);
        Assert.Empty(Diagnostics.RecentlyEnded);
        Assert.Equal(default, Diagnostics.LastFrame);
    }

    [Fact]
    public void ACollectedEnabledEngineStopsCountingAsActive()
    {
        EnableDiagnosticsOnADroppedEngine();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(PrestoDiagnostics.Active);
    }

    [Fact]
    public void SettingEnabledToItsCurrentValueDoesNothing()
    {
        var running = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(16);

        Diagnostics.Enabled = true;
        Diagnostics.Enabled = true;

        Assert.Equal(new[] { running }, Diagnostics.Roots);
        Diagnostics.Enabled = false;
        Assert.False(PrestoDiagnostics.Active);
        Diagnostics.Enabled = false;
        Assert.False(PrestoDiagnostics.Active);
    }

    [Fact]
    public void ActiveValuesListsTheMovingMotionValuesEnabledOrNot()
    {
        var source = Presto.CreateMotionValue();
        var follower = source.Spring(new Spring(stiffness: 120, damping: 12));

        source.Set(100);
        Frames.Advance(16);

        Assert.Contains(follower, Diagnostics.ActiveValues);
        Diagnostics.Enabled = true;
        Assert.Contains(follower, Diagnostics.ActiveValues);
        RunUntil(() => !follower.IsAnimating);
        Frames.Advance(16);
        Assert.DoesNotContain(follower, Diagnostics.ActiveValues);
    }

    [Fact]
    public void ARepeatedCancelDoesNotEndARootAgain()
    {
        Diagnostics.Enabled = true;
        var first = Presto.CreateTimer(new() { Autoplay = false }).Cancel();
        var second = Presto.CreateTimer(new() { Autoplay = false }).Cancel();

        first.Cancel();

        Assert.Equal(new[] { first, second }, Diagnostics.RecentlyEnded);
    }

    [Fact]
    public void CaptureCreationTraceIsOffUnlessSet()
    {
        Diagnostics.Enabled = true;

        var timer = Presto.CreateTimer(new() { Autoplay = false });

        Assert.False(PrestoDiagnostics.CaptureCreationTrace);
        Assert.Null(timer.CreationTrace);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EnableDiagnosticsOnADroppedEngine()
    {
        var engine = new Engine(new ManualFrameSource());
        engine.Diagnostics.Enabled = true;
        Assert.True(PrestoDiagnostics.Active);
    }
}
