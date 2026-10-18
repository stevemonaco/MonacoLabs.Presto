using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Controls;
using PrestoGallery.Builder;
using PrestoGallery.Showcase;

namespace PrestoGallery.Tests;

public sealed class BuilderPagesTests
{
    [AvaloniaFact]
    public void BuilderBasicsPrintsItsBuilderAndRunsItOnAttach()
    {
        var page = new BuilderBasicsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(30);

        Assert.Contains("Presto.Build()", page.GetControl<CodePanel>("CodeText").Code);
        Assert.True(page.Animation!.CurrentTime > 0);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void BuilderBasicsPlaysOnceWithoutTravelUnderReducedMotion()
    {
        using var reduced = ReducedMotion.On();
        var page = new BuilderBasicsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(100);

        var button = page.GetControl<Button>("LikeButton");
        Assert.Equal(1, page.Animation!.IterationCount);
        Assert.True(page.Animation.Completed);
        Assert.All(["X", "Y", "Rotate"], property => Assert.Equal(0, Convert.ToDouble(Presto.Get(button, property, withUnit: false)), 3));
        Assert.Contains(".WhenReduced(", page.GetControl<CodePanel>("CodeText").Code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void PropertiesPrintsEveryBuilderAndRunsThemOnAttach()
    {
        var page = new PropertiesPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(30);

        var codeBlocks = page.GetVisualDescendants().OfType<CodePanel>().ToList();
        Assert.Equal(4, codeBlocks.Count);
        Assert.All(codeBlocks, block => Assert.Contains("Presto.Build()", block.Code));
        Assert.All(page.Animations, animation => Assert.True(animation!.CurrentTime > 0));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void RecipesPrintsTheRecipeAndStaggersItOnAttach()
    {
        var page = new RecipesPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(30);

        var recipe = page.GetControl<CodePanel>("RecipeCode").Code;
        Assert.Contains("Pop = Presto.Build()", recipe);
        Assert.Contains("Pop.Duration(600);", recipe);
        Assert.Contains("Pop.With(b => b.Delay(Presto.Stagger(60)))", page.GetControl<CodePanel>("StaggeredCode").Code);
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
        var recipe = page.GetControl<CodePanel>("RecipeCode").Code;
        Assert.Contains("Pop.Duration(1200);", recipe);
        Assert.Contains("button.Animate(Pop)", recipe);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TimelineAndCompositorPrintTheirBuildersAndRunOnAttach()
    {
        var page = new TimelineBuilderPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(30);

        var timelineCode = page.GetControl<CodePanel>("TimelineCode").Code;
        Assert.Contains("var fadeUp = Presto.Build()", timelineCode);
        Assert.Contains(".Add(CardB, fadeUp, \"-=400\")", timelineCode);
        Assert.Contains(".Add(CardD, b => b.Opacity(", timelineCode);
        Assert.DoesNotContain("SyncScrub", timelineCode);
        Assert.Contains("Gear.AnimateOnCompositor(Presto.Build()", page.GetControl<CodePanel>("SpinCode").Code);
        Assert.True(page.Timeline!.CurrentTime > 0);
        Assert.True(page.Spin!.CurrentTime > 0);
        Assert.Equal(page.Timeline.IterationProgress, page.GetControl<Slider>("ScrubSlider").Value, 6);
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
        var staticCode = page.GetControl<CodeTabs>("StaticCode");
        Assert.Equal(Snippets.XamlFadeUp, staticCode.CSharp);
        Assert.Contains("Presto.Build()", staticCode.CSharp);
        Assert.Equal(Snippets.XamlStatic, staticCode.Xaml);
        Assert.Equal(Snippets.XamlExtension, page.GetControl<CodePanel>("ExtensionCode").Code);
        var bindingCode = page.GetControl<CodeTabs>("BindingCode").CSharp;
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

        Assert.True(idle.Canceled);
        Assert.Single(Presto.GetAnimations(bound, "X"));
        Assert.Empty(Presto.GetAnimations(bound, "Y"));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void XamlPageCuesShakeAnInvalidSaveAndToastAValidOne()
    {
        var page = new XamlPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var email = page.GetControl<TextBox>("CueEmail");
        var toasts = page.GetControl<ToastHost>("CueToasts");
        var save = page.GetControl<Button>("CueSave");
        var code = page.GetControl<CodeTabs>("CuesCode");
        Assert.Equal(Snippets.XamlCues, code.Xaml);
        Assert.Equal(Snippets.XamlCuesViewModel, code.CSharp);

        save.Command!.Execute(null);
        TestFrames.Tick();

        Assert.Single(Presto.GetAnimations(email, "X"));
        Assert.Empty(toasts.Items);

        email.Text = "ada@example.com";
        save.Command!.Execute(null);
        TestFrames.Tick();

        Assert.Equal("Saved ada@example.com", Assert.Single(toasts.Items));
        toasts.GetVisualDescendants().OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        TestFrames.Tick();

        Assert.Empty(toasts.Items);
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void XamlPageCheerBurstsUnlessMotionIsReduced(bool reduced, int overlays)
    {
        var page = new XamlPage();
        var window = SceneHarness.Show(page, 1280, 900);
        Presto.Defaults.ReducedMotion = reduced;
        try
        {
            page.GetControl<Button>("CueCheer").Command!.Execute(null);
            TestFrames.Tick(9);

            Assert.Equal(overlays, SceneHarness.ParticleOverlays(page).Length);
        }
        finally
        {
            Presto.Defaults.ReducedMotion = false;
            SceneHarness.Close(window);
        }
    }

    [AvaloniaTheory]
    [InlineData("DefaultLift")]
    [InlineData("StrongLift")]
    public void ReorderPageListsLiftWithTheirRecipeAndReorder(string name)
    {
        var page = new ReorderPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var list = page.GetControl<ListBox>(name);
        list.BringIntoView();
        TestFrames.Tick();
        var items = (ObservableCollection<string>)list.ItemsSource!;
        var row = list.ContainerFromIndex(0)!;
        var grab = SceneHarness.Center(row, window);

        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 4; i++)
        {
            window.MouseMove(grab + new Vector(0, row.Bounds.Height * .3 * i));
            TestFrames.Tick();
        }
        Assert.NotNull(row.Effect);
        window.MouseUp(grab + new Vector(0, row.Bounds.Height * 1.2), MouseButton.Left);

        Assert.Equal(["Drafts", "Inbox", "Sent", "Archive"], items);
        SceneHarness.Close(window);
    }

    private static List<Visual> AttachedElements(Visual page) =>
        [.. page.GetVisualDescendants().Where(visual => visual is Control control && (Presence.GetArrive(control) ?? Animate.GetOnAttached(control)) is not null)];
}
