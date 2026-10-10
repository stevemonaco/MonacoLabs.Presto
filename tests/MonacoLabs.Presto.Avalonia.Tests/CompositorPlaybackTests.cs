using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MonacoLabs.Presto.Compositor;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Playback control on the compositor (COMPOSITOR-PLAN §4.3 step 6), upstream's <c>waapi</c> <c>pause</c>/<c>resume</c>/
/// <c>seek</c>/<c>reverse</c>/<c>speed</c> ported onto recompilation: a <c>KeyFrameAnimation</c> has none of them, so every
/// operation stops the server animations and starts freshly compiled ones from where the mirror stands.
/// </summary>
public sealed class CompositorPlaybackTests : CompositorTestBase
{
    [AvaloniaFact]
    public void PauseFreezesThePixelsAndResumeCarriesOnFromThere()
    {
        var bar = ShowBar();
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 60, Duration = 2000, Ease = "linear" });
        Run();
        var moved = Left(bar);
        Assert.True(moved > 31, "the translation never started");

        _ = animation.Pause();
        Tick();
        var frozen = Left(bar);

        Assert.True(Math.Abs(frozen - moved) <= 3, $"pausing moved the bar from {moved} to {frozen}");
        Assert.False(Pump(() => Left(bar) != frozen, rounds: 8), "the paused animation kept running");

        _ = animation.Resume();

        Assert.True(Pump(() => Left(bar) > frozen), "the resumed animation never moved on");
        Assert.True(Left(bar) < frozen + 20, "the resumed animation jumped ahead instead of carrying on");
    }

    [AvaloniaFact]
    public void SeekingToHalfLandsOnTheHalfwayValue()
    {
        var bar = ShowBar();
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 60, Duration = 2000, Ease = "linear" }).Pause();

        animation.Seek(1000);
        Tick();

        Assert.Equal(60d, Left(bar));
    }

    [AvaloniaFact]
    public void SeekingBeforeTheFirstResumeCarriesOnFromThere()
    {
        var bar = ShowBar();
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 60, Duration = 2000, Ease = "linear", Autoplay = false });

        animation.Seek(1000);
        _ = animation.Resume();
        Tick();

        Assert.InRange(Left(bar), 55, 70);
    }

    [AvaloniaFact]
    public void ReverseMidFlightWalksThePixelsBackTowardsTheStart()
    {
        var bar = ShowBar();
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 60, Duration = 2000, Ease = "linear" });
        Run();
        var forward = Left(bar);
        Assert.True(forward > 31, "the translation never started");

        _ = animation.Reverse();

        Assert.True(Pump(() => Left(bar) < forward - 1), "the reversed animation did not walk back");
    }

    [AvaloniaFact]
    public void DoublingTheSpeedCoversTwiceTheDistance()
    {
        var bar = ShowBar();
        var opaque = Capture(bar);
        var animation = bar.AnimateOnCompositor(new() { ["Opacity"] = 0, Duration = 3000, Ease = "linear" });

        Run();
        var slow = Difference(opaque, Capture(bar));
        Assert.True(slow > 0, "the fade never started");

        animation.Speed = 2;
        Run();
        var fast = Difference(opaque, Capture(bar)) - slow;

        Assert.True(fast > 1.5 * slow, $"{fast} of fade is not twice {slow}");
    }

    /// <remarks>
    /// An infinite animation has no remainder to compile, so it is the one case that rotates instead: the recompiled cycle
    /// starts on the phase the mirror is showing and comes back to it a period later.
    /// </remarks>
    [AvaloniaFact]
    public void SeekingAnInfiniteLoopRotatesThePhaseInsteadOfRestarting()
    {
        var (mirror, tracks) = CompositorCompiler.Compile(
            0,
            [new Border()],
            new() { ["X"] = 100, Duration = 400, Loop = true, Ease = "linear" });
        mirror.Seek(100, muteCallbacks: true);

        var rotated = Assert.Single(CompositorCompiler.CompileFrom(mirror, tracks));

        Assert.True(rotated.Forever);
        Assert.Equal(PlaybackDirection.Normal, rotated.Direction);
        Assert.Equal(TimeSpan.FromMilliseconds(400), rotated.Duration);
        Assert.Equal(25d, rotated.Values[0][0], 1e-6);
        Assert.Equal(75d, rotated.Values[(rotated.Values.Length - 1) / 2][0], 1e-6);
        Assert.Equal(25d, rotated.Values[^1][0], 1e-6);
        Assert.Equal(100, mirror.CurrentTime);
    }

    [AvaloniaFact]
    public void RestartAndCompleteLandOnTheStartAndEndValues()
    {
        var bar = ShowBar();
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 40, Duration = 2000, Ease = "linear" });
        Run();
        Assert.True(Left(bar) > 31, "the translation never started");

        _ = animation.Complete();
        Tick();
        Assert.Equal(70d, Left(bar));

        _ = animation.Restart().Pause();
        Tick();
        Assert.Equal(30d, Left(bar));
    }

    [AvaloniaFact]
    public void AReversedRunRestsOnItsStartValuesUntilReverted()
    {
        var bar = ShowBar();
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 60, Duration = 200, Ease = "linear", Reversed = true });
        Run(16);
        Assert.True(animation.Completed);

        for (var i = 0; i < 30; i++)
        {
            Assert.Equal(30d, Left(bar));
            Tick(Frame);
        }
    }

    /// <remarks>
    /// Reversed from mid-flight, the visual with the longest delay is the least far along and comes home first, so the
    /// animation finishes with the one that had got furthest.
    /// </remarks>
    [AvaloniaFact]
    public void AStaggerReversedMidFlightCompletesWhenTheLastVisualIsHome()
    {
        var bars = new[] { new Border(), new Border(), new Border() };
        new Window { Content = new StackPanel { Children = { bars[0], bars[1], bars[2] } } }.Show();
        Tick();
        var completions = 0;
        var animation = PrestoAvalonia.AnimateOnCompositor(bars, new()
        {
            ["X"] = 100,
            Duration = 400,
            Delay = Presto.Stagger(100),
            Ease = "linear",
            OnComplete = _ => completions++,
        });
        Frames.Advance(250);

        _ = animation.Reverse();
        var then = animation.Then();
        Frames.Advance(100);

        Assert.False(animation.Completed);
        Assert.False(then.IsCompleted);
        Assert.Equal(0, completions);

        Frames.Advance(250);

        Assert.True(animation.Completed);
        Assert.True(then.IsCompleted);
        Assert.Equal(1, completions);
    }

    private static double Left(Visual visual) => Extent(visual, Colors.Red)!.Value.Left;

    private void Run() => Run(24);
}
