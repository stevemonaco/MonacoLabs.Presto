using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Values;
using PrestoSample.Builder;
using PrestoSample.Showcase;

namespace PrestoSample.Tests;

public sealed class ColorSpacesPageTests
{
    [AvaloniaFact]
    public void OklchGlowKeepsItsLightnessWhileTheRgbOnePulses()
    {
        var page = new ColorSpacesPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var rgb = new List<double>();
        var oklch = new List<double>();

        for (var frame = 0; frame < 8000 / 16; frame++)
        {
            TestFrames.Tick();
            rgb.Add(Oklch(page.GetControl<Border>("RgbGlow")).L);
            oklch.Add(Oklch(page.GetControl<Border>("OklchGlow")).L);
        }

        Assert.True(page.RgbCycle!.CurrentTime > 0);
        Assert.True(page.OklchCycle!.CurrentTime > 0);
        Assert.All(oklch, l => Assert.InRange(l, 0.60, 0.66));
        Assert.True(rgb.Max() - rgb.Min() > 0.3);
        Assert.Contains("k.Color(\"#ff0000\", \"#ff0000\", ColorSpace.Oklch, HueInterpolation.Increasing", page.GetControl<CodePanel>("HueCode").Code);
        Assert.StartsWith("L 0.", page.GetControl<TextBlock>("OklchReadout").Text);

        page.RgbCycle.Seek(0);
        page.OklchCycle.Seek(0);
        Assert.Equal(Colors.Red, Background(page.GetControl<Border>("RgbGlow")));
        Assert.Equal(Colors.Red, Background(page.GetControl<Border>("OklchGlow")));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void EverySpaceFlipsTogetherAndOnlyOklchStaysSaturatedFromBlueToAmber()
    {
        var page = new ColorSpacesPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var chroma = page.Pills.Select(_ => new List<double>()).ToArray();

        while (page.StatusFlips.Any(flip => !flip.Completed))
        {
            TestFrames.Tick();
            for (var i = 0; i < page.Pills.Count; i++) chroma[i].Add(Oklch(page.Pills[i]).C);
        }

        Assert.Equal(5, page.Pills.Count);
        Assert.Contains(page.GetLogicalDescendants().OfType<TextBlock>(), text => text.Text == "Default (≈ LinearSrgb)");
        Assert.All(page.StatusFlips, flip => Assert.Equal(1500, flip.Duration));
        Assert.All(page.Pills, pill => Assert.Equal("Warning", ((TextBlock)pill.Child!).Text));
        Assert.All(page.Pills, pill => Assert.Equal(Color.Parse("#f59e0b"), ((ISolidColorBrush)pill.Background!).Color));
        Assert.True(chroma[0].Min() < 0.08, "Default should dip through gray");
        Assert.All(chroma[4], c => Assert.True(c > 0.1, $"Oklch chroma {c}"));
        Assert.Contains("ColorSpace", page.GetControl<CodePanel>("StatusCode").Code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void NextStatusFlipsEveryPillToError()
    {
        var page = new ColorSpacesPage();
        var window = SceneHarness.Show(page, 1280, 900);

        page.GetControl<Button>("NextButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        while (page.StatusFlips.Any(flip => !flip.Completed)) TestFrames.Tick();

        Assert.All(page.Pills, pill => Assert.Equal("Error", ((TextBlock)pill.Child!).Text));
        Assert.All(page.Pills, pill => Assert.Equal(Color.Parse("#ef4444"), ((ISolidColorBrush)pill.Background!).Color));
        SceneHarness.Close(window);
    }
    private static Color Background(Border border) => ((ISolidColorBrush)border.Background!).Color;

    private static OklchColor Oklch(Border border)
    {
        var color = ((ISolidColorBrush)border.Background!).Color;
        return color.ToPresto().ToOklch();
    }
}
