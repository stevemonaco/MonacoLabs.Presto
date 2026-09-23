using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// Playback control on the compositor (COMPOSITOR-PLAN §4.3 step 6), upstream's <c>waapi</c> <c>pause</c>/<c>resume</c>/
/// <c>seek</c>/<c>reverse</c>/<c>speed</c> ported onto recompilation: a <c>KeyFrameAnimation</c> has none of them, so every
/// operation stops the server animations and starts freshly compiled ones from where the mirror stands.
/// </summary>
public sealed class CompositorPlaybackTests : CompositorTestBase
{
    [AvaloniaFact]
    public async Task PauseFreezesThePixelsAndResumeCarriesOnFromThere()
    {
        var bar = ShowBar();
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 60, Duration = 2000, Ease = "linear" });
        await Run();
        var moved = Left(bar);
        Assert.True(moved > 31, "the translation never started");

        _ = animation.Pause();
        Tick();
        var frozen = Left(bar);

        Assert.True(Math.Abs(frozen - moved) <= 3, $"pausing moved the bar from {moved} to {frozen}");
        Assert.False(await Pump(() => Left(bar) != frozen, rounds: 8), "the paused animation kept running");

        _ = animation.Resume();

        Assert.True(await Pump(() => Left(bar) > frozen), "the resumed animation never moved on");
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
    public async Task ReverseMidFlightWalksThePixelsBackTowardsTheStart()
    {
        var bar = ShowBar();
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 60, Duration = 2000, Ease = "linear" });
        await Run();
        var forward = Left(bar);
        Assert.True(forward > 31, "the translation never started");

        _ = animation.Reverse();

        Assert.True(await Pump(() => Left(bar) < forward - 1), "the reversed animation did not walk back");
    }

    [AvaloniaFact]
    public async Task DoublingTheSpeedCoversTwiceTheDistance()
    {
        var bar = ShowBar();
        var opaque = Capture(bar);
        var animation = bar.AnimateOnCompositor(new() { ["Opacity"] = 0, Duration = 3000, Ease = "linear" });

        var slowMs = await Run();
        var slow = Difference(opaque, Capture(bar));
        Assert.True(slow > 0, "the fade never started");

        animation.Speed = 2;
        var fastMs = await Run();
        var fast = Difference(opaque, Capture(bar)) - slow;

        Assert.True(
            fast / fastMs > 1.5 * (slow / slowMs),
            $"{fast} of fade over {fastMs} ms is not twice {slow} over {slowMs} ms");
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
    public async Task RestartAndCompleteLandOnTheStartAndEndValues()
    {
        var bar = ShowBar();
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 40, Duration = 2000, Ease = "linear" });
        await Run();
        Assert.True(Left(bar) > 31, "the translation never started");

        _ = animation.Complete();
        Tick();
        Assert.Equal(70d, Left(bar));

        _ = animation.Restart().Pause();
        Tick();
        Assert.Equal(30d, Left(bar));
    }

    private static double Left(Visual visual) => Extent(visual, Colors.Red)!.Value.Left;

    /// <summary>
    /// Runs 24 compositor frames, advancing the mirror by the real time each one took, since the render thread reads its
    /// own stopwatch and the engine reads the test's frame source. Returns the elapsed ms.
    /// </summary>
    private async Task<double> Run()
    {
        var elapsed = 0d;
        for (var i = 0; i < 24; i++)
        {
            var at = Stopwatch.GetTimestamp();
            Tick();
            await Task.Delay(8);
            var frame = Stopwatch.GetElapsedTime(at).TotalMilliseconds;
            Frames.Advance(frame);
            elapsed += frame;
        }
        return elapsed;
    }
}
