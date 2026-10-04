using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Monaco.Presto;
using PrestoSample.Showcase.Pages;
using Path = Avalonia.Controls.Shapes.Path;

namespace PrestoSample.Tests;

public sealed class MorphLabPageTests
{
    [AvaloniaFact]
    public void UnderReducedMotionEveryLoopPlaysOnceAndTheSignatureIsLeftDrawn()
    {
        using var reduced = ReducedMotion.On();
        var page = new MorphLabPage();
        var window = SceneHarness.Show(page, 1200, 1000);

        TestFrames.Tick(400);

        Control[] targets = [page.GetControl<Path>("MorphPath"), .. page.GetControl<Canvas>("TrackStage").Children];
        var animations = targets.SelectMany(target => Presto.GetAnimations(target)).ToList();
        Assert.All(animations, animation => Assert.True(animation.Completed));
        Assert.Empty(Presto.GetAnimations(page.GetControl<Path>("Signature")));
        SceneHarness.Close(window);
    }
}
