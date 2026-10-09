using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Builder;
using PrestoSample.Showcase;
using Path = Avalonia.Controls.Shapes.Path;

namespace PrestoSample.Tests;

public sealed class WarpStudioPageTests
{
    private static List<TextBlock> Lines(WarpStudioPage page) => [.. page.GetControl<StackPanel>("Lines").Children.OfType<TextBlock>()];

    private static Rect[] Outlines(WarpStudioPage page) => [.. page.Splits.SelectMany(split => split.Chars).Select(piece => ((Path)piece).Data!.Bounds)];

    [AvaloniaFact]
    public void EachPresetSetsItsKindAndParameters()
    {
        TestFrames.Install();
        var page = new WarpStudioPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var buttons = page.GetControl<WrapPanel>("PresetRow").Children.OfType<Button>().ToList();

        foreach (var preset in WarpStudioPage.Presets)
        {
            buttons.Single(button => (string)button.Content! == preset.Name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.All(Lines(page), line => Assert.Equal(preset.Kind, TextWarp.GetKind(line)));
            Assert.Equal(preset.Warp, page.GetControl<Slider>("WarpSlider").Value);
            Assert.Equal(preset.Radius, page.GetControl<Slider>("RadiusSlider").Value);
            Assert.Equal(preset.Stagger, page.GetControl<Slider>("StaggerSlider").Value);
            Assert.Equal(preset.Animate, page.GetControl<CheckBox>("AnimateBox").IsChecked);
            Assert.Equal(preset.Follow, page.GetControl<CheckBox>("FollowBox").IsChecked);
            Assert.Equal(preset.Duration + (2 * preset.Stagger), page.Warp!.Duration);
            Assert.Equal(preset.Animate, page.Phase != null);
        }
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ThePrintedCodeFollowsTheSettings()
    {
        TestFrames.Install();
        var page = new WarpStudioPage();
        var window = SceneHarness.Show(page, 1280, 900);

        page.GetControl<ComboBox>("KindBox").SelectedItem = TextWarpKind.Twist;
        page.GetControl<Slider>("WarpSlider").Value = 1.25;
        page.GetControl<Slider>("RadiusSlider").Value = 90;
        page.GetControl<Slider>("StaggerSlider").Value = 40;
        page.GetControl<CheckBox>("AnimateBox").IsChecked = false;
        page.GetControl<CheckBox>("FollowBox").IsChecked = true;
        TestFrames.Tick(15);

        var code = page.GetControl<CodePanel>("CodeText").Code!;
        Assert.Contains("TextWarp.SetKind(line, TextWarpKind.Twist);", code);
        Assert.Contains("Outlines = true", code);
        Assert.Contains("[PrestoKeys.Warp] = 1.25,", code);
        Assert.Contains("[PrestoKeys.WarpRadius] = 90,", code);
        Assert.Contains("Delay = Presto.Stagger(40),", code);
        Assert.DoesNotContain("TweenValue.FromTo", code);
        Assert.Contains("PrestoAvalonia.PointerValue(Stage, Orientation.Horizontal)", code);
        Assert.All(Lines(page), line => Assert.Equal(TextWarpKind.Twist, TextWarp.GetKind(line)));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void FullMotionWarpsTheHeadline()
    {
        TestFrames.Install();
        var page = new WarpStudioPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var outlines = Outlines(page);
        TestFrames.Tick(60);

        Assert.NotEmpty(outlines);
        Assert.Contains(Outlines(page).Zip(outlines), pair => pair.First != pair.Second);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ReducedMotionLeavesTheHeadlineStraightAndThePhaseRunsOnce()
    {
        TestFrames.Install();
        using var reduced = ReducedMotion.On();
        var page = new WarpStudioPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var outlines = Outlines(page);
        TestFrames.Tick(60);

        Assert.NotEmpty(outlines);
        Assert.Equal(outlines, Outlines(page));
        Assert.False(page.Phase!.Loop.IsInfinite);
        SceneHarness.Close(window);
    }
}
