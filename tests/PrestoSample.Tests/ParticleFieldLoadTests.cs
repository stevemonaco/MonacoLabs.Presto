using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Monaco.Presto.AvaloniaBridge.Compositor;
using PrestoSample.Showcase;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

/// <summary>PLAN.md Phase 54: how long the Particle field page takes to show its default field, in the size the Desktop sample opens at.</summary>
public sealed class ParticleFieldLoadTests
{
    private const double Width = 1400;
    private const double Height = 1000;

    // Before 2d4df43 a first visit took about 0.6 s here; with it, 6.5 s.
    private static readonly TimeSpan FirstVisitBudget = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan RepeatVisitBudget = TimeSpan.FromSeconds(1);

    [AvaloniaFact]
    public async Task AFirstVisitDrawsTheDefaultFieldWithinBudget()
    {
        var clock = Stopwatch.StartNew();
        var page = new ParticleFieldPage();
        var window = SceneHarness.Show(page, Width, Height);

        Assert.True(await SceneHarness.Pump(() => Drawn(page, window)), "the field never drew");
        var elapsed = clock.Elapsed;
        SceneHarness.Close(window);
        Assert.True(elapsed < FirstVisitBudget, $"the first visit took {elapsed.TotalMilliseconds:0} ms");
    }

    [AvaloniaFact]
    public async Task ARepeatVisitDrawsTheDefaultFieldWithinBudget()
    {
        var page = new ParticleFieldPage();
        var window = SceneHarness.Show(page, Width, Height);
        Assert.True(await SceneHarness.Pump(() => Drawn(page, window)), "the field never drew");

        var clock = Stopwatch.StartNew();
        window.Content = null;
        TestFrames.Tick();
        window.Content = page;
        Assert.True(await SceneHarness.Pump(() => Drawn(page, window)), "the field never drew again");
        var elapsed = clock.Elapsed;
        SceneHarness.Close(window);
        Assert.True(elapsed < RepeatVisitBudget, $"leaving and revisiting took {elapsed.TotalMilliseconds:0} ms");
    }

    // A whole field, laid out by the scene the page attached last, and on screen.
    private static bool Drawn(ParticleFieldPage page, Window window)
    {
        var field = page.GetControl<CompositorCanvas>("Field");
        var count = 0;
        field.Invoke<ParticleFieldScene>(scene => count = scene.Particles.Count);
        TestFrames.Tick();
        return count == ParticleFieldScene.DefaultCount &&
            SceneHarness.CountPixels(window, SceneHarness.Slot(field, window), Palette.Background.Color) > ParticleFieldScene.DefaultCount;
    }
}
