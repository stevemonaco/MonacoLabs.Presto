using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using MonacoLabs.Presto.Compositor;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="Recipes.Confetti"/>, <see cref="Recipes.Sparkles"/> and <see cref="Recipes.Burst"/> drawn into a window's shared particle overlay.</summary>
public sealed class ParticleRecipeTests : CompositorTestBase
{
    [AvaloniaFact]
    public void ConfettiLaunchesFromTheAnchorsCenterInTopLevelCoordinates()
    {
        var anchor = ShowAnchor();

        var effect = Recipes.Confetti(anchor, count: 8);
        Tick();

        Assert.Equal(2, effect.Systems.Count);
        Assert.All(effect.Systems, system => Assert.Equal((50d, 50d), (system.X, system.Y)));
        Assert.Equal(new Size(200, 200), Assert.Single(Overlays(anchor)).Bounds.Size);
    }

    [AvaloniaFact]
    public void OneOverlayIsSharedPerWindowAndRemovedOnceIdle()
    {
        var anchor = ShowAnchor();

        var first = Recipes.Burst(anchor, count: 4);
        var second = Recipes.Burst(anchor, count: 4);
        var overlay = Assert.Single(Overlays(anchor));
        Assert.False(overlay.IsHitTestVisible);
        Tick();
        Assert.False(first.Then().IsCompleted);
        Assert.True(Pump(() => first.Then().IsCompleted && second.Then().IsCompleted, 300), "the bursts never finished");

        RunUntil(() => false, max: ParticleOverlay.IdleTime - 100);
        Assert.Single(Overlays(anchor));
        RunUntil(() => Overlays(anchor).Length == 0, max: 200);
        Assert.Empty(Overlays(anchor));
    }

    [AvaloniaFact]
    public void SparklesRunUntilStopped()
    {
        var anchor = ShowAnchor();

        var effect = Recipes.Sparkles(anchor, rate: 200);
        Pump(() => false, 10);
        Assert.False(effect.Then().IsCompleted);
        effect.Stop();

        Assert.True(Pump(() => effect.Then().IsCompleted, 300), "the sparkles never finished");
    }

    [AvaloniaFact]
    public void CancelClearsAndCompletesAtOnce()
    {
        var anchor = ShowAnchor();
        var effect = Recipes.Confetti(anchor);
        Tick();

        effect.Cancel();
        Tick();

        Assert.True(effect.Then().IsCompleted);
        Assert.All(effect.Systems, system => Assert.Equal(0, system.Live));
    }

    [AvaloniaFact]
    public void UnderReducedMotionNothingSpawnsAndTheReducedFormPlays()
    {
        var anchor = ShowAnchor();
        Presto.Defaults.ReducedMotion = true;

        var effect = Recipes.Confetti(anchor, reduced: Presto.Build().Opacity(0.5).Duration(100));
        RunUntil(() => anchor.Opacity == 0.5);

        Assert.True(effect.Then().IsCompleted);
        Assert.Empty(Overlays(anchor));
        Assert.Equal(0.5, anchor.Opacity);
    }

    [AvaloniaFact]
    public void BurstOnACueEmitsFromItsTarget()
    {
        var anchor = ShowAnchor();
        var cue = new Cue();
        Animate.SetCue(anchor, cue);
        Animate.SetDoOnCue(anchor, (CueAction)new BurstExtension { Count = 4 }.ProvideValue(null!));

        cue.Raise();
        Tick();

        Assert.Single(Overlays(anchor));
    }

    [AvaloniaFact]
    public void BurstSkipsAFirstValueAndThrowsOffAControl()
    {
        var anchor = ShowAnchor();
        var action = (CueAction)new BurstExtension().ProvideValue(null!);

        action.Invoke(anchor, 1, first: true);
        Tick();

        Assert.Empty(Overlays(anchor));
        Assert.Throws<InvalidOperationException>(() => action.Invoke(new Visual(), 1, first: false));
    }

    [AvaloniaTheory]
    [InlineData("Confetti")]
    [InlineData("Sparkles")]
    [InlineData("Burst")]
    public void AnAnchorOutsideAWindowEmitsAndPlaysNothing(string recipe)
    {
        var anchor = new Border { Width = 40, Height = 20 };

        var effect = Play(recipe, anchor);
        Frames.Advance(1000, 16);

        Assert.True(effect.Then().IsCompleted);
        Assert.Empty(effect.Systems);
        Assert.Equal(1, anchor.Opacity);
        Assert.Null(anchor.RenderTransform);
    }

    [AvaloniaTheory]
    [InlineData(Edge.Left, 0, 100, 0, 200)]
    [InlineData(Edge.Top, 100, 0, 200, 0)]
    [InlineData(Edge.Right, 200, 100, 0, 200)]
    [InlineData(Edge.Bottom, 100, 200, 200, 0)]
    public void ConfettiFromAnEdgeLaunchesAlongItAndIntoTheWindow(Edge from, double x, double y, double width, double height)
    {
        var anchor = ShowAnchor();

        var effect = Recipes.Confetti(anchor, count: 8, from: from);
        Tick();

        Assert.Equal(2, effect.Systems.Count);
        Assert.All(effect.Systems, system =>
        {
            Assert.Equal((x, y), (system.X, system.Y));
            Assert.Equal((width, height), system.Params.Area);
            Assert.Equal(20, system.Params.Spread);
            var aim = Utils.DegToRad(system.Params.Angle);
            var inward = new Vector(100 - x, 100 - y);
            Assert.True(Math.Cos(aim) * inward.X + Math.Sin(aim) * inward.Y > 0, $"aimed at {system.Params.Angle} degrees, out of the window");
        });
    }

    [AvaloniaFact]
    public void TheRecipesDefaultToOneHundredTwentyConfettiThirtySparklesASecondAndTwentyFourBurstCircles()
    {
        var anchor = ShowAnchor();

        var confetti = Recipes.Confetti(anchor);
        var sparkles = Recipes.Sparkles(anchor);
        var burst = Recipes.Burst(anchor);
        Tick();

        Assert.Equal([90, 30], confetti.Systems.Select(system => system.Params.Burst));
        Assert.Equal(30, Assert.Single(sparkles.Systems).Params.Rate);
        Assert.Equal(24, Assert.Single(burst.Systems).Params.Burst);
    }

    [AvaloniaFact]
    public void RevertClearsAndCompletesAtOnceAsCancelDoes()
    {
        var anchor = ShowAnchor();
        var effect = Recipes.Confetti(anchor);
        Tick();
        Assert.All(effect.Systems, system => Assert.True(system.Live > 0));

        effect.Revert();
        Tick();

        Assert.True(effect.Then().IsCompleted);
        Assert.All(effect.Systems, system => Assert.Equal(0, system.Live));
    }

    [AvaloniaFact]
    public void PauseFreezesOnlyItsOwnEffectAndResumeContinuesIt()
    {
        var anchor = ShowAnchor();
        var paused = Recipes.Sparkles(anchor, rate: 200);
        var running = Recipes.Sparkles(anchor, rate: 200);
        TickFor(100);

        paused.Pause();
        Tick();
        var frozen = Snapshot(paused);
        var moving = Snapshot(running);
        TickFor(100);

        Assert.Equal(frozen, Snapshot(paused));
        Assert.NotEqual(moving, Snapshot(running));

        paused.Resume();
        TickFor(50);
        Assert.NotEqual(frozen, Snapshot(paused));
    }

    [AvaloniaFact]
    public void ACompleteEffectIgnoresStopCancelPauseAndResume()
    {
        var outside = Recipes.Burst(new Border());
        outside.Stop();
        outside.Cancel();
        outside.Pause();
        outside.Resume();
        Assert.True(outside.Then().IsCompleted);

        var anchor = ShowAnchor();
        var effect = Recipes.Sparkles(anchor, rate: 200);
        TickFor(50);
        effect.Cancel();
        Tick();

        effect.Pause();
        Tick();
        Assert.All(effect.Systems, system => Assert.False(system.Paused));
    }

    [AvaloniaFact]
    public async Task AwaitingTheEffectCompletesAsThenDoes()
    {
        var anchor = ShowAnchor();
        var effect = Recipes.Burst(anchor, count: 4);
        Tick();
        var awaited = Await(effect);
        Assert.False(awaited.IsCompleted);

        Assert.True(Pump(() => effect.Then().IsCompleted, 300), "the burst never finished");
        await awaited;
        Assert.True(awaited.IsCompletedSuccessfully);

        static async Task Await(ParticleEffect effect) => await effect;
    }

    private static ParticleEffect Play(string recipe, Control anchor) => recipe switch
    {
        "Confetti" => Recipes.Confetti(anchor),
        "Sparkles" => Recipes.Sparkles(anchor),
        _ => Recipes.Burst(anchor),
    };

    private static (int Live, double X, double Y)[] Snapshot(ParticleEffect effect) =>
        effect.Systems.Select(system => (system.Live, system.Live > 0 ? system.Particles[0].X : 0, system.Live > 0 ? system.Particles[0].Y : 0)).ToArray();

    private static Border ShowAnchor()
    {
        var anchor = new Border { Width = 40, Height = 20 };
        Canvas.SetLeft(anchor, 30);
        Canvas.SetTop(anchor, 40);
        new Window { Content = new Canvas { Children = { anchor } }, Width = 200, Height = 200 }.Show();
        Tick();
        return anchor;
    }

    private static CompositorCanvas[] Overlays(Visual anchor) => OverlayLayer.GetOverlayLayer(anchor)!.Children.OfType<CompositorCanvas>().ToArray();
}
