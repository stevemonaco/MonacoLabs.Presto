using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Pages;
using PrestoSample.Showcase;

namespace PrestoSample.Tests;

/// <summary>The reference pages print the exact code they ran.</summary>
public sealed class ReferencePageCodeTests
{
    [AvaloniaFact]
    public void ShapesPrintsTheDrawAnimationsTimingAndLoop()
    {
        var page = new ShapesPage();
        var window = SceneHarness.Show(page, 900, 1600);

        var code = page.GetControl<CodePanel>("DrawCode").Code;
        Assert.Contains("Duration = 2200", code);
        Assert.Contains("Loop = true", code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ShapesPrintsTheMorphDestinationItUsed()
    {
        var page = new ShapesPage();
        var window = SceneHarness.Show(page, 900, 1600);
        var morph = ButtonNamed(page, "Morph");
        var code = page.GetControl<CodePanel>("MorphCode");

        SceneHarness.Click(window, morph);
        Assert.Contains("\"StarGeometry\"", code.Code);

        SceneHarness.Click(window, morph);
        Assert.Contains("\"CircleGeometry\"", code.Code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void PressingMorphTwiceLeavesOneAnimationOnThePath()
    {
        var page = new ShapesPage();
        var window = SceneHarness.Show(page, 900, 1600);

        SceneHarness.Click(window, ButtonNamed(page, "Morph"));
        TestFrames.Tick(5);
        SceneHarness.Click(window, ButtonNamed(page, "Morph"));

        Assert.Single(Presto.GetAnimations(page.GetControl<Avalonia.Controls.Shapes.Path>("MorphPath")));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void KeyframesPrintsThePercentAndPlaybackEaseCalls()
    {
        var page = new KeyframesPage();
        var window = SceneHarness.Show(page, 900, 900);

        Assert.Contains("Keyframes = new Dictionary<string, Keyframe>", page.GetControl<CodePanel>("PercentCode").Code);
        Assert.Contains("PlaybackEase = Eases.InOutQuad", page.GetControl<CodePanel>("MarchCode").Code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ValueTypesPrintsEverySpecimensCallWithItsLoopingSettings()
    {
        var page = new ValueTypesPage();
        var window = SceneHarness.Show(page, 900, 2400);

        Assert.All(page.GetLogicalDescendants().OfType<CodePanel>(), panel => Assert.False(string.IsNullOrWhiteSpace(panel.Code), panel.Name));
        var gradient = page.GetControl<CodePanel>("GradientCode").Code;
        Assert.Contains("new LinearGradientBrush", gradient);
        Assert.Contains("GradientStops", gradient);
        Assert.Contains("Duration = 1400", page.GetControl<CodePanel>("NumberCode").Code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void StaggerPrintsTheTimeStripsWholeCall()
    {
        var page = new StaggerPage();
        var window = SceneHarness.Show(page, 900, 1600);

        var code = page.GetControl<CodePanel>("TimeCode").Code;
        Assert.Contains("Presto.Stagger(60)", code);
        Assert.Contains("Duration = 450", code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void StaggerPrintsTheGridOriginThatWasPicked()
    {
        var page = new StaggerPage();
        var window = SceneHarness.Show(page, 900, 1600);
        var code = page.GetControl<CodePanel>("ExplicitCode");
        Assert.DoesNotContain("Center", code.Code);

        page.GetControl<ComboBox>("FromBox").SelectedIndex = 1;

        Assert.Contains("StaggerFrom from = StaggerFrom.Center;", code.Code);
        Assert.Contains("Grid = [7, 4]", code.Code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ScrollPrintsItsObserversInCSharp()
    {
        var page = new ScrollPage();
        var window = SceneHarness.Show(page, 900, 900);

        var code = string.Join("\n", page.GetLogicalDescendants().OfType<CodePanel>().Select(panel => panel.Code));
        Assert.Contains("Enter = \"bottom-=120 top\"", code);
        Assert.Contains("Sync = true", code);
        Assert.DoesNotContain("enter:", code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TextPrintsItsRevealsInXamlAndTheScrambleItRan()
    {
        var page = new TextPage();
        var window = SceneHarness.Show(page, 900, 1600);

        Assert.Equal(Snippets.TextLineRise, page.GetControl<CodePanel>("LineRiseCode").Code);
        Assert.Contains("{presto:LineRise}", Snippets.TextLineRise);
        SceneHarness.Click(window, ButtonNamed(page, "Change text"));
        SceneHarness.Click(window, ButtonNamed(page, "Scramble"));
        Assert.Contains("ScrambleText(new ScrambleTextParams", page.GetControl<CodePanel>("ScrambleCode").Code);
        Assert.Contains("From = ScrambleFrom.Center", page.GetControl<CodePanel>("ScrambleCode").Code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AnimatablePrintsTheTossItRuns()
    {
        var page = new AnimatablePage();
        var window = SceneHarness.Show(page, 900, 1400);

        Assert.Contains("Projectile.Launch(speed: .6", page.GetControl<CodePanel>("TossCode").Code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void DetachingAnimatableMidTossLeavesTheBallWithNoAnimations()
    {
        var page = new AnimatablePage();
        var window = SceneHarness.Show(page, 900, 1400);
        var ball = page.GetControl<Border>("Ball");
        SceneHarness.Click(window, ButtonNamed(page, "Toss"));
        TestFrames.Tick(5);
        Assert.NotEmpty(Presto.GetAnimations(ball));

        SceneHarness.Close(window);

        Assert.Empty(Presto.GetAnimations(ball));
    }

    [AvaloniaFact]
    public void TimelinePrintsItsWholeChainInOnePanel()
    {
        var page = new TimelinePage();
        var window = SceneHarness.Show(page, 900, 1200);

        var code = page.GetControl<CodePanel>("Code").Code;
        Assert.Contains(".Label(\"flash\", \"+=200\")", code);
        Assert.Contains(".Sync(", code);
        Assert.Contains("var synced = SyncedButton.Animate(", code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ScrubbingTheTimelineSeeksWithoutTheReadoutEchoReseeking()
    {
        var page = new TimelinePage();
        var readout = Scrub(page, out var window);

        var times = Regex.Match(readout, @"^\s*(\d+) / (\d+) ms");
        Assert.Equal(Math.Round(0.37 * double.Parse(times.Groups[2].Value)), double.Parse(times.Groups[1].Value));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ScrubbingControlsSeeksWithoutTheReadoutEchoReseeking()
    {
        var page = new ControlsPage();
        var readout = Scrub(page, out var window);

        Assert.Matches(@"Progress\s+0\.370", readout);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void DraggablePrintsTheRealParamsForTheSelectedRelease()
    {
        var page = new DraggablePage();
        var window = SceneHarness.Show(page, 900, 1200);
        var spring = page.GetControl<CodePanel>("SpringCode");

        Assert.Contains("Snap = Utils.Snap(40)", page.GetControl<CodePanel>("SnapCode").Code);
        Assert.Contains("Trigger = Handle", page.GetControl<CodePanel>("RailCode").Code);
        Assert.Contains("ReleaseStiffness = 40", spring.Code);
        Assert.Contains("OnGrab = Record(\"OnGrab\")", spring.Code);

        page.GetControl<ComboBox>("ReleaseBox").SelectedIndex = 1;

        Assert.Contains("ReleaseInertia = new Inertia(0)", spring.Code);
        Assert.DoesNotContain("ReleaseStiffness", spring.Code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void EasesPrintsTheSelectedEaseExpressionInItsAnimateCall()
    {
        var page = new EasesPage();
        var window = SceneHarness.Show(page, 900, 900);
        var box = page.GetControl<ComboBox>("EaseBox");
        var code = page.GetControl<CodePanel>("Code");

        for (var i = 0; i < box.ItemCount; i++)
        {
            box.SelectedIndex = i;
            var name = box.SelectedItem!.ToString()!;
            var expression = name == "linear" ? "Eases.None"
                : name.StartsWith("Eases.", StringComparison.Ordinal) ? name
                : $"Eases.{char.ToUpperInvariant(name[0])}{name[1..]}";
            Assert.Contains($"var ease = {expression};", code.Code);
            Assert.Contains("Dot.Animate(new()", code.Code);
        }

        page.GetControl<TextBox>("EaseNameBox").Text = "out(2)";
        TestFrames.Tick();
        Assert.Contains("EaseParser.Parse(\"out(2)\")", code.Code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void PageTransitionsPrintsTheSelectedEdgeWithTheRecipeCall()
    {
        var page = new PageTransitionsPage();
        var window = SceneHarness.Show(page, 900, 1200);

        page.GetControl<ComboBox>("RecipePicker").SelectedIndex = 3;
        page.GetControl<ComboBox>("OptionPicker").SelectedItem = Edge.Left;

        var code = page.GetControl<CodeTabs>("HostCode");
        Assert.Contains("Recipes.PageSlide(", code.CSharp);
        Assert.Contains("Edge.Left", code.CSharp);
        Assert.Contains("PageTransition=\"{presto:PageSlide Edge=Left}\"", code.Xaml);

        page.GetControl<ComboBox>("RecipePicker").SelectedIndex = 5;
        Assert.Null(code.Xaml);
        SceneHarness.Close(window);
    }

    // Returns the page's readout after the slider moves to 0.37, asserting the echo left the slider there.
    private static string Scrub(Control page, out Window window)
    {
        window = SceneHarness.Show(page, 900, 1200);
        var slider = page.GetControl<Slider>("ScrubSlider");

        slider.Value = 0.37;

        Assert.Equal(0.37, slider.Value, 6);
        return page.GetControl<TextBlock>("ReadoutText").Text!;
    }

    private static Button ButtonNamed(Control page, string content) =>
        page.GetLogicalDescendants().OfType<Button>().First(button => Equals(button.Content, content));
}
