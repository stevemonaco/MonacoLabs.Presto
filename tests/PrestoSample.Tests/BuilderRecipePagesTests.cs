using System.Globalization;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Controls;
using PrestoSample.Builder;
using PrestoSample.Showcase;

namespace PrestoSample.Tests;

public sealed class BuilderRecipePagesTests
{
    [AvaloniaFact]
    public void MotionTokensReplaysTheRecipesWithTheSelectedPresetAndFlagAndRestoresThePresetOnDetach()
    {
        var tokens = Recipes.Tokens;
        var page = new MotionTokensPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(50);

        Assert.Same(MotionTokens.Standard, Recipes.Tokens);
        Assert.All(page.Played, animation => Assert.True(animation.CurrentTime > 0));
        Assert.Contains("Recipes.FadeUp(stagger: true).Animate(Rows.Children);", page.GetControl<CodePanel>("PlayCode").Code);

        var standard = page.Played;
        page.GetControl<ComboBox>("TokensBox").SelectedIndex = 1;
        TestFrames.Tick();

        Assert.NotSame(standard, page.Played);
        Assert.Same(MotionTokens.Expressive, Recipes.Tokens);
        Assert.Equal(MotionTokens.Expressive.Medium, page.Played[1].Duration);

        using var reduced = ReducedMotion.On();

        page.GetLogicalDescendants().OfType<Button>().First(button => button.Content is "Replay").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        TestFrames.Tick();

        Assert.Equal(MotionTokens.Expressive.Short, page.Played[1].Duration);
        var settings = page.GetControl<CodePanel>("TokensCode").Code;
        Assert.Contains("Recipes.Tokens = MotionTokens.Expressive;", settings);
        Assert.Contains("Presto.Defaults.ReducedMotion = true;", settings);

        SceneHarness.Close(window);

        Assert.Same(tokens, Recipes.Tokens);
        Assert.True(Presto.Defaults.ReducedMotion);
    }

    [AvaloniaFact]
    public void RecipeGalleryPlaysTilesInTheirReducedFormsAndRestoresThePresetOnDetach()
    {
        var tokens = Recipes.Tokens;
        var page = new RecipeGalleryPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var fadeUp = page.GetLogicalDescendants().OfType<Button>().First(button => button.Tag is "FadeUp");

        fadeUp.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        TestFrames.Tick(2);

        var specimen = (Control)page.Played!.Targets[0];
        Assert.True(Get(specimen, "Y") > 0);
        Assert.Contains("target.Animate(Recipes.FadeUp(duration: duration))", page.GetLogicalDescendants().OfType<CodePanel>().Select(panel => panel.Code));
        Assert.Equal($"{Snippets.RecipeGalleryShowcase}\n\n{Snippets.RecipeGalleryRow}",page.GetControl<CodePanel>("XamlCode").Code);

        page.GetControl<ComboBox>("TokensBox").SelectedIndex = 1;
        using var reduced = ReducedMotion.On();

        fadeUp.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        TestFrames.Tick(2);

        Assert.Same(MotionTokens.Expressive, Recipes.Tokens);
        Assert.Equal(0, Get(specimen, "Y"));
        Assert.InRange(specimen.Opacity, 0, .99);

        SceneHarness.Close(window);

        Assert.Same(tokens, Recipes.Tokens);
    }

    [Theory]
    [InlineData("(target, duration, _) => target.Animate(Recipes.Fade(duration))", "target.Animate(Recipes.Fade(duration))")]
    [InlineData("(button, _, _) => { Tilt.SetEnabled(button, true); }", "Tilt.SetEnabled(button, true);")]
    [InlineData("(button, _, intensity) => { Tilt.SetEnabled(button, true); Tilt.SetStrength(button, 10 * intensity); }",
        "Tilt.SetEnabled(button, true);\nTilt.SetStrength(button, 10 * intensity);")]
    [InlineData("(items, _, _) => { items.ForEach(item => { item.Play(); }); Done(); }", "items.ForEach(item => { item.Play(); });\nDone();")]
    public void RecipeGalleryBodyUnwrapsTheLambdaAfterItsFirstArrow(string lambda, string body) =>
        Assert.Equal(body, RecipeGalleryPage.Body(lambda));

    [AvaloniaFact]
    public void PlaygroundPrintsTheChainItPlaysAndRebuildsAfterAChange()
    {
        var page = new PlaygroundPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var first = page.Animation;
        var code = page.GetControl<CodePanel>("CodeText");

        Assert.Contains(".Ease(\"outBack\")", code.Code);
        Assert.Contains("From = StaggerFrom.Center", code.Code);
        Assert.DoesNotContain(".X(", code.Code);

        page.GetControl<Slider>("XSlider").Value = 120;
        TestFrames.Tick(15);

        Assert.NotSame(first, page.Animation);
        Assert.Contains(".X(120)", code.Code);
        Assert.Equal(6, page.Animation!.Targets.Count);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void PlaygroundPrintsABuilderThatReplaysToTheSameTweenValues()
    {
        var page = new PlaygroundPage();
        var window = SceneHarness.Show(page, 1280, 900);
        page.GetControl<Slider>("XSlider").Value = 120;
        page.GetControl<Slider>("YSlider").Value = -40;
        page.GetControl<Slider>("RotateSlider").Value = 90;
        page.GetControl<Slider>("ScaleSlider").Value = 1.5;
        page.GetControl<Slider>("DurationSlider").Value = 800;
        page.GetControl<ComboBox>("EaseBox").SelectedIndex = 1;
        page.GetControl<Slider>("StaggerSlider").Value = 50;
        page.GetControl<ComboBox>("FromBox").SelectedIndex = 2;
        page.GetControl<CheckBox>("AlternateBox").IsChecked = false;
        TestFrames.Tick(15);

        Assert.Equal("""
            Presto.Build()
                .X(120)
                .Y(-40)
                .Rotate(90)
                .Scale(1.5)
                .Duration(800)
                .Ease("outQuad")
                .Delay(Presto.Stagger(50, new() { From = StaggerFrom.Last }))
                .Loop()
                .Animate(Squares.Children);
            """, page.GetControl<CodePanel>("CodeText").Code, ignoreLineEndingDifferences: true);

        var squares = new StackPanel();
        for (var i = 0; i < 6; i++) squares.Children.Add(new Border { Width = 40, Height = 40 });
        var replayWindow = SceneHarness.Show(squares, 400, 100);
        var replay = Presto.Build()
            .X(120)
            .Y(-40)
            .Rotate(90)
            .Scale(1.5)
            .Duration(800)
            .Ease("outQuad")
            .Delay(Presto.Stagger(50, new() { From = StaggerFrom.Last }))
            .Loop()
            .Animate(squares.Children);
        var played = page.Animation!;
        played.Pause();
        replay.Pause();

        var shown = page.GetControl<StackPanel>("Squares").Children;
        foreach (var time in new[] { 0, 1050, 525 })
        {
            played.Seek(time);
            replay.Seek(time);
            for (var i = 0; i < 6; i++)
                foreach (var property in new[] { "X", "Y", "Rotate", "Scale" })
                    Assert.Equal(Presto.Get(shown[i], property, withUnit: false), Presto.Get(squares.Children[i], property, withUnit: false));
        }
        Assert.NotEqual(0d, Presto.Get(shown[5], "X", withUnit: false));
        SceneHarness.Close(replayWindow);
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
        Assert.Contains("var wave = new StaggerParams { Grid = [Columns, Rows], From = from };", page.GetControl<CodePanel>("CodeText").Code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void RippleGridOnlyRecoloursUnderReducedMotion()
    {
        using var reduced = ReducedMotion.On();
        var page = new RippleGridPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var cells = page.GetControl<UniformGrid>("Cells").Children;

        SceneHarness.Click(window, cells[0]);
        TestFrames.Tick(10);

        Assert.All(cells, cell => Assert.Equal(1, Scale(cell), 3));
        Assert.All(cells, cell => Assert.Equal(0, Get(cell, "Rotate"), 3));
        Assert.NotEqual(Palette.AccentDeep.Color, ((ISolidColorBrush)((Border)cells[0]).Background!).Color);
        Assert.Contains(".WhenReduced(reduced => reduced.Background(", page.GetControl<CodePanel>("CodeText").Code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void SubmitFlowCompletesWithoutSpinningUnderReducedMotion()
    {
        using var reduced = ReducedMotion.On();
        var page = new SubmitFlowPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var spinner = page.GetControl<Control>("Spinner");

        page.GetControl<Button>("Submit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        TestFrames.Tick(40);

        Assert.Equal(0, Get(spinner, "Rotate"), 3);

        TestFrames.Tick(280);

        Assert.True(page.Flow!.IsCompletedSuccessfully);
        Assert.Equal(200, page.GetControl<Button>("Submit").Width, 1);
        Assert.Contains(".WhenReduced(reduced => reduced.Loop(false));", page.GetControl<CodePanel>("RecipesCode").Code);
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
        Assert.Contains("static readonly AnimationBuilder DrawIn = Recipes.DrawIn(", page.GetControl<CodePanel>("RecipesCode").Code);
        Assert.Contains("Celebrate = button => Recipes.Confetti(button, count: 80);", page.GetControl<CodePanel>("RecipesCode").Code);
        Assert.Contains("Recipes.Shake(", page.GetControl<CodePanel>("RecipesCode").Code);
        Assert.Contains("Recipes.Pulse(", page.GetControl<CodePanel>("RecipesCode").Code);
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
        Assert.Contains("Animate.SetOnChanged(tile, bump);", page.GetControl<CodePanel>("CountersCode").Code);

        TestFrames.Tick(30);

        Assert.All(tiles, tile => Assert.Empty(Presto.GetAnimations(tile, "Scale")));
        Assert.All(tiles, tile => Assert.Equal(1, Scale(tile), 3));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void CountersPrintTheWholeTileLoopWithoutPagePlumbing()
    {
        var code = new CountersPage().GetControl<CodePanel>("CountersCode").Code;

        Assert.Contains("textModifier: metric.Format", code);
        Assert.Contains("Animate.SetWatch(tile, next);", code);
        Assert.DoesNotContain("_readouts[", code);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void OdometerPrintsItsModifierAndAppliesItOnlyWhenMechanical(bool mechanical)
    {
        var page = new CountersPage();
        var window = SceneHarness.Show(page, 1280, 900);
        page.GetControl<CheckBox>("MechanicalBox").IsChecked = mechanical;
        TestFrames.Tick(20);

        var snapped = page.Roll!.Targets.All(strip => Convert.ToDouble(Presto.Get(strip, "Y"), CultureInfo.InvariantCulture) % 48 == 0);
        Assert.Equal(mechanical, snapped);
        if (mechanical) Assert.Contains("odometer.Modifier(SnapToDigit);", page.GetControl<CodePanel>("OdometerCode").Code);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void RollDigitsRollsTheReadoutAndEndsAsPlainText()
    {
        var page = new CountersPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick();
        page.GetLogicalDescendants().OfType<Button>().First(button => button.Content is "Roll to a random value").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        TestFrames.Tick(2);

        var readout = page.GetControl<TextBlock>("RollReadout");
        Assert.NotEmpty(page.Rolled!.Targets);
        Assert.Null(readout.Text);

        TestFrames.Tick(60);

        Assert.True(page.Rolled.Completed);
        Assert.True(double.TryParse(readout.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out _));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ToastStackTrimsToFourAndEveryToastExpires()
    {
        var page = new ToastsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(50);
        var list = page.GetControl<ToastHost>("ToastList");

        var first = list.GetLogicalDescendants().OfType<Border>().First(border => border.Classes.Contains("toast"));
        Assert.Equal(1, first.Opacity, 3);
        Assert.Equal(0, first.RenderTransform!.Value.M31, 1);

        for (var i = 0; i < 5; i++) SceneHarness.Click(window, page.GetControl<Button>("NotifyButton"));
        TestFrames.Tick(40);

        Assert.Equal(4, list.Items.Count);
        Assert.Contains("presto:Presence.Exit=", page.GetControl<CodePanel>("TemplateCode").Code);
        Assert.Contains("presto:Animate.ItemLayout=", page.GetControl<CodePanel>("TemplateCode").Code);

        TestFrames.Tick(340);

        Assert.Empty(list.Items);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ToastCountdownFadesInsteadOfShrinkingUnderReducedMotionAndStillExpires()
    {
        using var reduced = ReducedMotion.On();
        var page = new ToastsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(50);

        var list = page.GetControl<ToastHost>("ToastList");
        var bar = list.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("countdown"));
        Assert.Equal(1, Get(bar, "ScaleX"), 3);
        Assert.InRange(bar.Opacity, .35, .99);

        TestFrames.Tick(340);

        Assert.Empty(list.Items);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ClosingAToastRemovesItAtOnceAndPlaysItsExitOnAGhost()
    {
        var page = new ToastsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(50);
        var list = page.GetControl<ToastHost>("ToastList");
        Assert.Single(list.Items);
        var close = list.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("close"));

        SceneHarness.Click(window, close);

        Assert.Empty(list.Items);
        Assert.Single(OverlayLayer.GetOverlayLayer(page)!.Children.OfType<Canvas>());
        SceneHarness.Close(window);
    }

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
        Assert.Equal($"{Snippets.FormStatesCard}\n\n{Snippets.FormStatesBadge}", page.GetControl<CodeTabs>("StatesCode").Xaml);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void FormStatesValidateWithoutSpinOrPulseUnderReducedMotion()
    {
        using var reduced = ReducedMotion.On();
        var page = new FormStatesPage();
        var window = SceneHarness.Show(page, 1280, 900);
        var card = page.GetLogicalDescendants().OfType<Border>().Single(border => border.Classes.Contains("form"));
        var badge = page.GetLogicalDescendants().OfType<Border>().Single(border => border.Classes.Contains("badge"));

        page.ViewModel.Email = "nope";
        page.ViewModel.SubmitCommand.Execute(null);
        TestFrames.Tick(40);

        Assert.Equal(FormState.Validating, page.ViewModel.State);
        Assert.Equal(0, Get(badge, "Rotate"), 3);
        Assert.Empty(Presto.GetAnimations(card, "Scale"));
        Assert.All(Presto.GetAnimations(card), animation => Assert.Equal(1, animation.IterationCount));
        SceneHarness.Close(window);
    }

    [Theory]
    [InlineData(Snippets.FormStatesCard)]
    [InlineData(Snippets.FormStatesBadge)]
    public void FormStatesXamlSnippetsAreCompleteElements(string snippet)
    {
        var body = string.Join("\n", snippet.Split('\n').Where(line => line.Trim() != "…"));

        var root = XDocument.Parse($"""
            <Root xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  xmlns:presto="https://github.com/stevemonaco/Monaco.Presto" xmlns:local="using:PrestoSample.Builder">{body}</Root>
            """).Root!;

        Assert.Equal("Border", Assert.Single(root.Elements()).Name.LocalName);
    }

    [AvaloniaFact]
    public void FormStatesPrintsTheFormMotionsRegion()
    {
        var code = new FormStatesPage().GetControl<CodeTabs>("StatesCode").CSharp;

        Assert.Equal($"// FormMotions.cs\n{Snippets.FormMotions}", code);
        Assert.All([nameof(FormMotions.CardValidating), nameof(FormMotions.CardError), nameof(FormMotions.CardSuccess), nameof(FormMotions.BadgeIdle),
            nameof(FormMotions.BadgeValidating), nameof(FormMotions.BadgeError), nameof(FormMotions.BadgeSuccess)],
            name => Assert.Contains($"AnimationBuilder {name} = Presto.Build()", code));
    }

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
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void StoryboardPrintsTheSettingsAsCodeWithoutItsReadoutPlumbing()
    {
        var code = new StoryboardPage().GetControl<CodePanel>("CodeText").Code;

        Assert.Contains(".PlaybackRate(rate)", code);
        Assert.Contains(".FrameRate(fps)", code);
        Assert.DoesNotContain("ShowProgress", code);
        Assert.DoesNotContain("LoopsText", code);
    }

    private static double Scale(Control cell) => Get(cell, "Scale");

    private static double Get(Control control, string property) => Convert.ToDouble(Presto.Get(control, property, withUnit: false), CultureInfo.InvariantCulture);
}
