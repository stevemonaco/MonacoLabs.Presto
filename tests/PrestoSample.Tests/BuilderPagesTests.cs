using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Builder;

namespace PrestoSample.Tests;

public sealed class BuilderPagesTests
{
    [AvaloniaFact]
    public void BuilderBasicsPrintsItsBuilderAndRunsItOnAttach()
    {
        var page = new BuilderBasicsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(30);

        Assert.Contains("Presto.Build()", page.GetControl<SelectableTextBlock>("CodeText").Text);
        Assert.True(page.Animation!.CurrentTime > 0);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void PropertiesPrintsEveryBuilderAndRunsThemOnAttach()
    {
        var page = new PropertiesPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(30);

        var codeBlocks = page.GetVisualDescendants().OfType<SelectableTextBlock>().Where(t => t.Classes.Contains("code")).ToList();
        Assert.Equal(4, codeBlocks.Count);
        Assert.All(codeBlocks, block => Assert.Contains("Presto.Build()", block.Text));
        Assert.All(page.Animations, animation => Assert.True(animation!.CurrentTime > 0));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void RecipesPrintsTheRecipeAndStaggersItOnAttach()
    {
        var page = new RecipesPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(30);

        Assert.Contains("Presto.Build()", page.GetControl<SelectableTextBlock>("RecipeCode").Text);
        Assert.Contains("Pop.With(b => b.Delay(Presto.Stagger(60)))", page.GetControl<SelectableTextBlock>("StaggeredCode").Text);
        Assert.True(page.Staggered!.CurrentTime > 0);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void RecipesSliderChangesTheNextPlayButNotTheRunningOne()
    {
        var page = new RecipesPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var buttons = page.GetControl<StackPanel>("Row").Children.OfType<Button>().ToList();

        buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var first = page.Played!;
        TestFrames.Tick(5);
        page.GetControl<Slider>("DurationSlider").Value = 1200;
        buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(600, first.Duration);
        Assert.Equal(1200, page.Played!.Duration);
        Assert.Contains("button.Animate(Pop)", page.GetControl<SelectableTextBlock>("RecipeCode").Text);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TimelineAndCompositorPrintTheirBuildersAndRunOnAttach()
    {
        var page = new TimelineBuilderPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(30);

        var timelineCode = page.GetControl<SelectableTextBlock>("TimelineCode").Text;
        Assert.Contains("var fadeUp = Presto.Build()", timelineCode);
        Assert.Contains(".Add(CardB, fadeUp, \"-=400\")", timelineCode);
        Assert.Contains(".Add(CardD, b => b.Opacity(", timelineCode);
        Assert.Contains("Gear.AnimateOnCompositor(Presto.Build()", page.GetControl<SelectableTextBlock>("SpinCode").Text);
        Assert.True(page.Timeline!.CurrentTime > 0);
        Assert.True(page.Spin!.CurrentTime > 0);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void XamlPageAnimatesEveryAttachedElement()
    {
        var page = new XamlPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(5);

        var attached = AttachedElements(page);
        Assert.Equal(9, attached.Count);
        Assert.All(attached, visual => Assert.Single(Presto.GetAnimations(visual)));
        Assert.Contains("Presto.Build()", page.GetControl<SelectableTextBlock>("StaticCode").Text);
        Assert.Contains("Recipes.FadeUp(distance: 48, delay: 100)", page.GetControl<SelectableTextBlock>("ExtensionCode").Text);
        var bindingCode = page.GetControl<SelectableTextBlock>("BindingCode").Text;
        Assert.Contains("Idle = Recipes.Float()", bindingCode);
        Assert.Contains("Alert = Recipes.Shake().Loop().LoopDelay(700)", bindingCode);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void XamlPageToggleSwapsTheBoundAnimation()
    {
        var page = new XamlPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var bound = AttachedElements(page).Single(visual => visual.DataContext is EmphasisViewModel);
        var idle = Presto.GetAnimations(bound).Single();

        page.GetVisualDescendants().OfType<ToggleSwitch>().Single().IsChecked = true;
        TestFrames.Tick();

        Assert.True(idle.Cancelled);
        Assert.Single(Presto.GetAnimations(bound, "X"));
        Assert.Empty(Presto.GetAnimations(bound, "Y"));
        SceneHarness.Close(window);
    }

    [Fact]
    public void XamlPageSnippetsMatchItsMarkup() => AssertSnippetsMatchMarkup(typeof(XamlPage), "samples/PrestoSample/Builder/XamlPage.axaml", 3);

    internal static void AssertSnippetsMatchMarkup(Type page, string axaml, int count)
    {
        var markup = Normalise(File.ReadAllText(RepoFile(axaml)));
        var snippets = page.GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral).ToList();

        Assert.Equal(count, snippets.Count);
        Assert.All(snippets, field => Assert.Contains(Normalise((string)field.GetValue(null)!), markup));
    }

    private static List<Visual> AttachedElements(Visual page) =>
        [.. page.GetVisualDescendants().Where(visual => Animate.GetOnAttached(visual) is not null)];

    private static string Normalise(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    private static string RepoFile(string relative, [CallerFilePath] string source = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (!File.Exists(Path.Combine(dir.FullName, "Monaco.Presto.slnx"))) dir = dir.Parent!;
        return Path.Combine(dir.FullName, relative);
    }
}
