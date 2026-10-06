using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge.Tests;

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
