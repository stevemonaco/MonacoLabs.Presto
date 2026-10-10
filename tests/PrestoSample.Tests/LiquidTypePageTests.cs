using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using MonacoLabs.Presto.Controls;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class LiquidTypePageTests
{
    [AvaloniaFact]
    public void TheTitleFlowsAtTheSlidersStrengthAndReturnsToIdentityAtZero()
    {
        var page = new LiquidTypePage();
        var window = SceneHarness.Show(page, 1280, 1400, frames: 2);
        var flow = page.GetControl<Distort>("FlowFx");
        var phase = flow.TurbulencePhase;
        TestFrames.Tick(30);

        Assert.Equal(8, flow.Turbulence);
        Assert.True(flow.TurbulencePhase > phase);

        page.GetControl<Slider>("Strength").Value = 0;
        Assert.Equal(0, flow.Turbulence);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheGooWordsFuseAndSeparateAgain()
    {
        var page = new LiquidTypePage();
        var window = SceneHarness.Show(page, 1280, 1400, frames: 2);
        var fuse = page.GetControl<Distort>("FuseFx");
        var start = Gap(page);

        TestFrames.Tick(100);
        Assert.True(fuse.Goo > 0);
        Assert.True(Gap(page) < fuse.Goo, $"gap {Gap(page)} at the meeting");

        TestFrames.Tick(120);
        Assert.Equal(140, Gap(page), 1);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void HoveringGlitchesTheHeadingBackToZeroWithin300Ms()
    {
        var page = new LiquidTypePage();
        var window = SceneHarness.Show(page, 1280, 1400, frames: 2);
        var heading = page.GetControl<Distort>("Heading");

        window.MouseMove(SceneHarness.Center(heading, window));
        TestFrames.Tick(4);
        Assert.NotEqual(0, heading.Glitch);

        TestFrames.Tick(16);
        Assert.Equal(0, heading.Glitch);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheErrorBannerGlitchesIn()
    {
        var page = new LiquidTypePage();
        var window = SceneHarness.Show(page, 1280, 1400, frames: 2);
        var banner = page.GetControl<Distort>("Banner");

        SceneHarness.Click(window, page.GetControl<Button>("ErrorButton"));
        TestFrames.Tick(3);
        Assert.NotEqual(0, banner.Glitch);

        TestFrames.Tick(50);
        Assert.Equal(0, banner.Glitch);
        Assert.Equal(1, banner.Opacity);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionEveryDistortStaysAtIdentity()
    {
        using var reduced = ReducedMotion.On();
        var page = new LiquidTypePage();
        var window = SceneHarness.Show(page, 1280, 1400, frames: 2);
        var distorts = page.GetLogicalDescendants().OfType<Distort>().ToList();
        window.MouseMove(SceneHarness.Center(page.GetControl<Distort>("Heading"), window));
        SceneHarness.Click(window, page.GetControl<Button>("ErrorButton"));
        page.GetControl<Slider>("Strength").Value = 20;
        var changed = new List<string>();

        for (var frame = 0; frame < 120; frame++)
        {
            TestFrames.Tick();
            changed.AddRange(distorts
                .Where(d => d.Turbulence != 0 || d.TurbulencePhase != 0 || d.Goo != 0 || d.Glitch != 0)
                .Select(d => d.Name!));
        }

        Assert.Equal(5, distorts.Count);
        Assert.Empty(changed);
        Assert.Equal(1, page.GetControl<Distort>("Banner").Opacity);
        SceneHarness.Close(window);
    }

    private static double Gap(LiquidTypePage page)
    {
        var fuse = page.GetControl<Distort>("FuseFx");
        var left = page.GetControl<TextBlock>("LeftWord");
        var right = page.GetControl<TextBlock>("RightWord");
        return right.TranslatePoint(default, fuse)!.Value.X - left.TranslatePoint(new Point(left.Bounds.Width, 0), fuse)!.Value.X;
    }
}
