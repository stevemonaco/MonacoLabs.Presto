using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Monaco.Presto;
using PrestoSample.Builder;

namespace PrestoSample.Tests;

public sealed class BuilderRecipePagesTests
{
    [AvaloniaFact]
    public void MotionTokensReplaysTheRecipesWithTheSelectedSet()
    {
        var page = new MotionTokensPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(50);

        Assert.Contains("Func<MotionTokens, AnimationBuilder> Enter = t => Presto.Build()", page.GetControl<SelectableTextBlock>("RecipesCode").Text);
        Assert.All(page.Played, animation => Assert.True(animation.CurrentTime > 0));

        var standard = page.Played;
        page.GetControl<RadioButton>("ReducedOption").IsChecked = true;
        TestFrames.Tick();

        Assert.NotSame(standard, page.Played);
        Assert.Contains("Distance: 0", page.GetControl<SelectableTextBlock>("TokensCode").Text);
        Assert.Contains("Enter(tokens)", page.GetControl<SelectableTextBlock>("PlayCode").Text);
        SceneHarness.Close(window);
    }

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
        SceneHarness.Close(window);
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
