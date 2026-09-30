using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Builder;

namespace PrestoSample.Tests;

public sealed class BuilderRecipePagesTests
{
    [AvaloniaFact]
    public void MotionTokensReplaysTheRecipesWithTheSelectedPresetAndFlagAndRestoresBothOnDetach()
    {
        var tokens = Recipes.Tokens;
        var page = new MotionTokensPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(50);

        Assert.Same(MotionTokens.Standard, Recipes.Tokens);
        Assert.All(page.Played, animation => Assert.True(animation.CurrentTime > 0));
        Assert.Contains("Recipes.FadeUp(stagger: true).Animate(Rows.Children);", page.GetControl<SelectableTextBlock>("PlayCode").Text);

        var standard = page.Played;
        page.GetControl<ComboBox>("TokensBox").SelectedIndex = 1;
        TestFrames.Tick();

        Assert.NotSame(standard, page.Played);
        Assert.Same(MotionTokens.Expressive, Recipes.Tokens);
        Assert.Equal(MotionTokens.Expressive.Medium, page.Played[1].Duration);

        page.GetControl<ToggleSwitch>("ReducedSwitch").IsChecked = true;
        TestFrames.Tick();

        Assert.True(Presto.Defaults.ReducedMotion);
        Assert.Equal(MotionTokens.Expressive.Short, page.Played[1].Duration);
        var settings = page.GetControl<SelectableTextBlock>("TokensCode").Text;
        Assert.Contains("Recipes.Tokens = MotionTokens.Expressive;", settings);
        Assert.Contains("Presto.Defaults.ReducedMotion = true;", settings);

        SceneHarness.Close(window);

        Assert.Same(tokens, Recipes.Tokens);
        Assert.False(Presto.Defaults.ReducedMotion);
    }

    [AvaloniaFact]
    public void RecipeGalleryPlaysTilesInTheirReducedFormsAndRestoresTheGlobalsOnDetach()
    {
        var tokens = Recipes.Tokens;
        var page = new RecipeGalleryPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var fadeUp = page.GetLogicalDescendants().OfType<Button>().First(button => button.Tag is "FadeUp");

        fadeUp.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        TestFrames.Tick(2);

        var specimen = (Control)page.Played!.Targets[0];
        Assert.True(Get(specimen, "Y") > 0);
        Assert.Contains("target.Animate(Recipes.FadeUp(duration: duration))", page.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text));

        page.GetControl<ComboBox>("TokensBox").SelectedIndex = 1;
        page.GetControl<ToggleSwitch>("ReducedSwitch").IsChecked = true;
        fadeUp.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        TestFrames.Tick(2);

        Assert.Same(MotionTokens.Expressive, Recipes.Tokens);
        Assert.Equal(0, Get(specimen, "Y"));
        Assert.InRange(specimen.Opacity, 0, .99);

        SceneHarness.Close(window);

        Assert.Same(tokens, Recipes.Tokens);
        Assert.False(Presto.Defaults.ReducedMotion);
    }

    [Fact]
    public void RecipeGalleryPageSnippetsMatchItsMarkup() =>
        BuilderPagesTests.AssertSnippetsMatchMarkup(typeof(RecipeGalleryPage), "samples/PrestoSample/Builder/RecipeGalleryPage.axaml", 2);

    [AvaloniaFact]
    public void PlaygroundPrintsTheChainItPlaysAndRebuildsAfterAChange()
    {
        var page = new PlaygroundPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var first = page.Animation;
        var code = page.GetControl<SelectableTextBlock>("CodeText");

        Assert.Contains(".Ease(\"outBack\")", code.Text);
        Assert.Contains("From = StaggerFrom.Center", code.Text);
        Assert.DoesNotContain(".X(", code.Text);

        page.GetControl<Slider>("XSlider").Value = 120;
        TestFrames.Tick(15);

        Assert.NotSame(first, page.Animation);
        Assert.Contains(".X(120)", code.Text);
        Assert.Equal(6, page.Animation!.Targets.Count);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void RippleGridDipsTheClickedCellBeforeTheFarthestOne()
    {
        var page = new RippleGridPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var cells = page.GetControl<UniformGrid>("Cells").Children;

        var centre = page.Animation;
        SceneHarness.Click(window, cells[0]);
        TestFrames.Tick(30);

        Assert.NotSame(centre, page.Animation);
        Assert.Equal(16 * 9, page.Animation!.Targets.Count);
        Assert.InRange(Scale(cells[0]), 0, .4);
        Assert.Equal(1, Scale(cells[^1]), 3);
        Assert.Contains("From = from", page.GetControl<SelectableTextBlock>("CodeText").Text);
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(false, "Send another")]
    [InlineData(true, "Try again")]
    public void SubmitFlowRunsToTheEndOfEitherBranch(bool fail, string label)
    {
        var page = new SubmitFlowPage();
        var window = SceneHarness.Show(page, 1280, 900);
        page.GetControl<CheckBox>("FailBox").IsChecked = fail;

        page.GetControl<Button>("Submit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        TestFrames.Tick(320);

        Assert.True(page.Flow!.IsCompletedSuccessfully);
        Assert.Equal(label, page.GetControl<TextBlock>("Label").Text);
        Assert.Equal(200, page.GetControl<Button>("Submit").Width, 1);
        Assert.Contains("static readonly AnimationBuilder DrawIn = Presto.Build()", page.GetControl<SelectableTextBlock>("RecipesCode").Text);
        Assert.Contains("Celebrate = button => Recipes.Confetti(button, count: 80);", page.GetControl<SelectableTextBlock>("RecipesCode").Text);
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void SubmitFlowThrowsConfettiOnSuccessUnlessMotionIsReduced(bool reduced, int overlays)
    {
        var page = new SubmitFlowPage();
        var window = SceneHarness.Show(page, 1280, 900);
        Presto.Defaults.ReducedMotion = reduced;
        try
        {
            page.GetControl<Button>("Submit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            TestFrames.Tick(120);

            Assert.Equal(overlays, SceneHarness.ParticleOverlays(page).Length);
        }
        finally
        {
            Presto.Defaults.ReducedMotion = false;
            SceneHarness.Close(window);
        }
    }

    /// <summary>Detaching the pressed, still-transformed button once re-asserted the render transform until the stack overflowed.</summary>
    [AvaloniaFact]
    public void SubmitFlowDetachesWithoutRecursingAfterARealClick()
    {
        var page = new SubmitFlowPage();
        var window = SceneHarness.Show(page, 1280, 900);

        SceneHarness.Click(window, page.GetControl<Button>("Submit"));
        TestFrames.Tick(320);
        window.Content = null;
        TestFrames.Tick();

        Assert.True(page.Flow!.IsCompletedSuccessfully);
        window.Close();
    }

    [AvaloniaFact]
    public void CountersLandOnTheirFormattedValuesAndTheOdometerOnItsDigits()
    {
        var page = new CountersPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(150);

        var readouts = page.GetControl<UniformGrid>("Tiles").GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("readout")).ToList();
        Assert.Equal(4, readouts.Count);
        Assert.StartsWith("$", readouts[0].Text);
        Assert.NotEqual("$0", readouts[0].Text);
        Assert.EndsWith("%", readouts[2].Text);

        var digits = page.OdometerValue.ToString("0000000", CultureInfo.InvariantCulture);
        var strips = page.Roll!.Targets;
        for (var i = 0; i < digits.Length; i++) Assert.Equal(-(digits[i] - '0') * 48, Convert.ToDouble(Presto.Get(strips[i], "Y"), CultureInfo.InvariantCulture), 1);
        Assert.Contains("textModifier: Metrics[i].Format", page.GetControl<SelectableTextBlock>("CountersCode").Text);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void CounterTilesBumpOnEachRefreshButNotTheFirstFill()
    {
        var page = new CountersPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(5);
        var tiles = page.GetControl<UniformGrid>("Tiles").Children;

        Assert.All(tiles, tile => Assert.Empty(Presto.GetAnimations(tile, "Scale")));

        page.GetControl<CheckBox>("MechanicalBox").IsChecked = true;
        TestFrames.Tick(8);

        Assert.All(tiles, tile => Assert.True(Scale(tile) > 1));
        Assert.Contains("Animate.SetOnChanged(_tiles[i], Presto.Build()", page.GetControl<SelectableTextBlock>("CountersCode").Text);

        TestFrames.Tick(30);

        Assert.All(tiles, tile => Assert.Empty(Presto.GetAnimations(tile, "Scale")));
        Assert.All(tiles, tile => Assert.Equal(1, Scale(tile), 3));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ToastStackTrimsToFourAndEveryToastExpires()
    {
        var page = new ToastsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(50);

        var first = page.GetControl<ItemsControl>("ToastList").GetLogicalDescendants().OfType<Border>().First(border => border.Classes.Contains("toast"));
        Assert.Equal(1, first.Opacity, 3);
        Assert.Equal(0, first.RenderTransform!.Value.M31, 1);

        for (var i = 0; i < 5; i++) page.ViewModel.NotifyCommand.Execute(null);
        TestFrames.Tick(40);

        Assert.Equal(4, page.ViewModel.Toasts.Count);

        TestFrames.Tick(340);

        Assert.Empty(page.ViewModel.Toasts);
        SceneHarness.Close(window);
    }

    [Fact]
    public void ToastsPageSnippetMatchesItsMarkup() =>
        BuilderPagesTests.AssertSnippetsMatchMarkup(typeof(ToastsPage), "samples/PrestoSample/Builder/ToastsPage.axaml", 1);

    [AvaloniaFact]
    public void FormStatesPlayEachStateAndReturnToRest()
    {
        var page = new FormStatesPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var card = page.GetLogicalDescendants().OfType<Border>().Single(border => border.Classes.Contains("form"));
        var model = page.ViewModel;

        model.Email = "nope";
        model.SubmitCommand.Execute(null);
        TestFrames.Tick(5);

        Assert.Equal(FormState.Validating, model.State);
        Assert.True(double.IsPositiveInfinity(Assert.Single(Presto.GetAnimations(card, "Scale")).IterationCount));

        TestFrames.Tick(80);

        Assert.Equal(FormState.Error, model.State);
        Assert.Single(Presto.GetAnimations(card, "X"));

        TestFrames.Tick(40);

        Assert.Equal(0, Get(card, "X"), 3);
        Assert.Equal(1, Scale(card), 3);
        Assert.Equal(1, card.Opacity, 3);

        model.Email = "ada@example.com";
        Assert.Equal(FormState.Idle, model.State);
        model.SubmitCommand.Execute(null);
        TestFrames.Tick(85);

        Assert.Equal(FormState.Success, model.State);
        Assert.Single(Presto.GetAnimations(card, "Y"));

        model.Email = "";
        TestFrames.Tick(60);

        Assert.Equal(FormState.Idle, model.State);
        Assert.Empty(Presto.GetAnimations(card));
        Assert.Equal(0, Get(card, "Y"), 3);
        Assert.Equal(1, Scale(card), 3);
        SceneHarness.Close(window);
    }

    [Fact]
    public void FormStatesPageSnippetsMatchItsMarkup() =>
        BuilderPagesTests.AssertSnippetsMatchMarkup(typeof(FormStatesPage), "samples/PrestoSample/Builder/FormStatesPage.axaml", 2);

    [AvaloniaFact]
    public void StoryboardWaitsForPlaySeeksAndKeepsItsTimeAcrossARebuild()
    {
        var page = new StoryboardPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(10);

        Assert.Equal(0, page.Animation!.CurrentTime);

        page.GetControl<Slider>("ScrubSlider").Value = .5;
        Assert.Equal(1200, page.Animation.CurrentTime, 1);

        page.GetControl<Button>("PlayButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        TestFrames.Tick(10);
        var time = page.Animation.CurrentTime;
        Assert.True(time > 1200);

        var before = page.Animation;
        page.GetControl<ComboBox>("FpsBox").SelectedIndex = 3;

        Assert.NotSame(before, page.Animation);
        Assert.Equal(time, page.Animation.CurrentTime, 1);
        Assert.False(page.Animation.Paused);
        Assert.Contains("fps = 6", page.GetControl<SelectableTextBlock>("CodeText").Text);
        SceneHarness.Close(window);
    }

    private static double Scale(Control cell) => Get(cell, "Scale");

    private static double Get(Control control, string property) => Convert.ToDouble(Presto.Get(control, property), CultureInfo.InvariantCulture);
}
