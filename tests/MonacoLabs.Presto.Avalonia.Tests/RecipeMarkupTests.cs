using Avalonia.Headless;
using Avalonia.Headless.XUnit;

namespace MonacoLabs.Presto.Tests;

/// <summary>The recipe markup extensions: exit-form detection and the shared settings.</summary>
public sealed class RecipeMarkupTests : BridgeTestBase
{
    private static object? From(AnimationParams parameters, string property) =>
        Assert.IsType<TweenParams>(parameters.Properties[property].Raw).From?.Raw;

    [AvaloniaFact]
    public void PageMarkupExtensionsProduceTransitionsWithTheirSettings()
    {
        var view = new RecipeXamlView();
        PrestoPageTransition Transition(Avalonia.Controls.TransitioningContentControl host) => Assert.IsType<PrestoPageTransition>(host.PageTransition);

        Assert.Equal((TweenValue)300, Transition(view.FadePage).Enter!.ToParams().Duration);
        Assert.Equal((TweenValue)90, Transition(view.FadeThroughPage).Enter!.ToParams().Delay);
        Assert.Equal(40d, From(Transition(view.AxisPage).Enter!.ToParams(), "Y"));
        Assert.Equal(-40d, Transition(view.AxisPage).Exit!.ToParams().Properties["Y"].Raw);
        Assert.Contains("Y", Transition(view.SlidePage).Enter!.ToParams().Properties.Keys);
        Assert.Equal(["X", "Opacity"], Transition(view.PushPage).Exit!.ToParams().Properties.Keys);
    }

    [AvaloniaFact]
    public void ARecipeOnAPageTransitionsExitsIsTheExitForm()
    {
        var transition = Assert.IsType<PrestoPageTransition>(new RecipeXamlView().CustomPage.PageTransition);

        Assert.Equal(0d, From(transition.Enter!.ToParams(), "Opacity"));
        Assert.Equal(0d, transition.Exit!.ToParams().Properties["Opacity"].Raw);
        Assert.Equal(-4 * MotionTokens.Standard.Distance, transition.BackExit!.ToParams().Properties["X"].Raw);
    }

    [AvaloniaFact]
    public void AFadeIsAnEntranceOnEnterAndAnExitOnExit()
    {
        var view = new RecipeXamlView();

        var enter = Presence.GetEnter(view.Faded)!.ToParams();
        var exit = Presence.GetExit(view.Faded)!.ToParams();

        Assert.Equal(24d, From(enter, "Y"));
        Assert.Equal(0d, From(enter, "Opacity"));
        Assert.Equal(-24d, exit.Properties["Y"].Raw);
        Assert.Equal(0d, exit.Properties["Opacity"].Raw);
    }

    [AvaloniaFact]
    public void ScatterGathersOnEnterAndScattersOnExitOrWithOutTrue()
    {
        var view = new RecipeXamlView();
        static double[] At(AnimationBuilder builder, bool atEnd)
        {
            var card = new Avalonia.Controls.Border();
            var animation = builder.Autoplay(false).Animate(card);
            animation.Seek(atEnd ? animation.Duration : 0);
            return [.. new[] { "X", "Y", "Rotate", "Scale" }.Select(p => Math.Round(Convert.ToDouble(Presto.Get(card, p, withUnit: false)), 6)), card.Opacity];
        }

        var gather = At(Presence.GetEnter(view.Scattered)!, atEnd: false);
        var scatter = At(Presence.GetExit(view.Scattered)!, atEnd: true);
        var coded = At((AnimationBuilder)new ScatterExtension { Distance = 50, Spin = 30, Seed = 4, Out = true }.ProvideValue(null!), atEnd: true);

        Assert.Equal(scatter, gather);
        Assert.Equal(scatter, coded);
        Assert.Equal(At(Recipes.Scatter(50, 30, 4), atEnd: true), coded);
    }

    [AvaloniaFact]
    public void PopoverIsAnEntranceOnOpenAndAnExitOnClose()
    {
        var flyout = Assert.IsType<Avalonia.Controls.Flyout>(new RecipeXamlView().Popped.Flyout);

        var open = Popover.GetOpen(flyout)!.ToParams();
        var close = Popover.GetClose(flyout)!.ToParams();

        Assert.Equal(0d, From(open, "Opacity"));
        Assert.Equal(MotionTokens.Standard.Scale, From(open, "Scale"));
        Assert.Equal(0d, close.Properties["Opacity"].Raw);
        Assert.Equal(MotionTokens.Standard.Scale, close.Properties["Scale"].Raw);
    }

    [AvaloniaFact]
    public void OutTrueGivesTheExitFormInAStyleSetter()
    {
        var view = new RecipeXamlView();
        Show(view);

        var exit = Presence.GetExit(view.Styled)!.ToParams();

        Assert.Equal(["Opacity"], exit.Properties.Keys);
        Assert.Equal(0d, exit.Properties["Opacity"].Raw);
    }

    [AvaloniaFact]
    public void LiftPlaysOnHoverAndShakePlaysOnAttach()
    {
        var view = new RecipeXamlView();
        var window = Show(view);

        Assert.Single(Presto.GetAnimations(view.Shaken));
        window.MouseMove(Center(view.Hovered, window));
        Frames.Advance(400, 16);

        Assert.True((double)PrestoAvalonia.Get(view.Hovered, "Y")! < 0);
    }

    [AvaloniaFact]
    public void ReorderParsesItsIntensityAndDuration()
    {
        var lift = Motion.GetPress(new RecipeXamlView().Pressed)!.ToParams();

        Assert.Equal((TweenValue)200, lift.Duration);
        Assert.Equal(1.045, (double)lift.Properties["Scale"].Raw!, 6);
        Assert.Equal(24d, lift.Properties["ShadowBlur"].Raw);
    }

    [AvaloniaFact]
    public void ReflowTakesItsDurationPositionallyAndParsesItsEase()
    {
        var reflow = Presence.GetEnter(new RecipeXamlView().Reflowed)!.ToParams();

        Assert.Equal((TweenValue)320, reflow.Duration);
        Assert.Same(EaseParser.Parse("outQuint"), reflow.Ease?.Raw);
    }

    [AvaloniaFact]
    public void EachElementGetsItsOwnBuilder()
    {
        var view = new RecipeXamlView();

        Assert.NotSame(Presence.GetEnter(view.Twin), Presence.GetEnter(view.OtherTwin));
    }

    [AvaloniaFact]
    public void EaseIsParsedAndStaggerStepsEachElement()
    {
        var builder = Presence.GetEnter(new RecipeXamlView().Tuned)!;

        Assert.Same(EaseParser.Parse("outBack"), builder.ToParams().Ease?.Raw);
        var animation = builder.Animate(new[] { new Avalonia.Controls.Border(), new(), new() });
        Assert.Equal((2 * 60) + MotionTokens.Standard.Medium, animation.Duration);
    }

    [AvaloniaFact]
    public void PlayFramesReadsEachSheetsFrameCount()
    {
        var view = new RecipeXamlView();
        Show(view);

        var shortPlay = Assert.Single(Presto.GetAnimations(view.ShortSheet));
        var longPlay = Assert.Single(Presto.GetAnimations(view.LongSheet));
        Assert.Equal(400, shortPlay.Duration, 6);
        Assert.Equal(1200, longPlay.Duration, 6);

        Frames.Advance(1300, 16);

        Assert.Equal(3, view.ShortSheet.CurrentFrame);
        Assert.Equal(11, view.LongSheet.CurrentFrame);
    }

    [AvaloniaFact]
    public void TextRevealExtensionsConfigureTheBlockUnlessAlreadySet()
    {
        var view = new RecipeXamlView();

        Assert.Equal(TextPieces.Lines, TextReveal.GetPieces(view.Masked));
        Assert.True(TextReveal.GetMask(view.Masked));
        Assert.Equal(TextPieces.Chars, TextReveal.GetPieces(view.Explicit));
    }

    [AvaloniaFact]
    public void ATextRevealExtensionConfiguresNothingFromAStyleSetterOrForAMaskItDoesNotUse()
    {
        var view = new RecipeXamlView();
        Show(view);

        Assert.NotNull(Presence.GetExit(view.StyledReveal));
        Assert.Equal(TextPieces.None, TextReveal.GetPieces(view.StyledReveal));
        Assert.False(view.StyledReveal.IsSet(TextReveal.MaskProperty));
        Assert.Equal(TextPieces.Chars, TextReveal.GetPieces(view.Cascaded));
        Assert.False(view.Cascaded.IsSet(TextReveal.MaskProperty));
    }

    [AvaloniaTheory]
    [InlineData(nameof(Recipes.LineRise), false)]
    [InlineData(nameof(Recipes.LineMask), false)]
    [InlineData(nameof(Recipes.LineWipe), false)]
    [InlineData(nameof(Recipes.WordFade), false)]
    [InlineData(nameof(Recipes.WordBlur), false)]
    [InlineData(nameof(Recipes.CharCascade), false)]
    [InlineData(nameof(Recipes.LineRise), true)]
    [InlineData(nameof(Recipes.LineMask), true)]
    [InlineData(nameof(Recipes.LineWipe), true)]
    [InlineData(nameof(Recipes.WordFade), true)]
    [InlineData(nameof(Recipes.WordBlur), true)]
    [InlineData(nameof(Recipes.CharCascade), true)]
    public void TextRevealExtensionsProvideTheirRecipe(string recipe, bool exit)
    {
        var ease = EaseParser.Parse("outQuad");
        (TextRevealExtension extension, AnimationBuilder expected) = recipe switch
        {
            nameof(Recipes.LineRise) => ((TextRevealExtension)new LineRiseExtension(), exit ? Recipes.LineRiseOut(321, null, ease) : Recipes.LineRise(321, null, ease)),
            nameof(Recipes.LineMask) => (new LineMaskExtension(), exit ? Recipes.LineMaskOut(321, null, ease) : Recipes.LineMask(321, null, ease)),
            nameof(Recipes.LineWipe) => (new LineWipeExtension(), exit ? Recipes.LineWipeOut(321, null, ease) : Recipes.LineWipe(321, null, ease)),
            nameof(Recipes.WordFade) => (new WordFadeExtension(), exit ? Recipes.WordFadeOut(321, null, ease) : Recipes.WordFade(321, null, ease)),
            nameof(Recipes.WordBlur) => (new WordBlurExtension(), exit ? Recipes.WordBlurOut(321, null, ease) : Recipes.WordBlur(321, null, ease)),
            _ => (new CharCascadeExtension(), exit ? Recipes.CharCascadeOut(321, null, ease) : Recipes.CharCascade(321, null, ease)),
        };
        extension.Out = exit;
        extension.Duration = 321;
        extension.Ease = "outQuad";

        var actual = ((AnimationBuilder)extension.ProvideValue(null!)).ToParams();

        static object?[] Values(AnimationParams parameters) => parameters.Properties
            .SelectMany(pair => pair.Value.Raw is TweenParams tween ? [pair.Key, tween.From?.Raw, tween.To?.Raw] : new[] { pair.Key, pair.Value.Raw })
            .ToArray();
        Assert.Equal(expected.ToParams().Duration, actual.Duration);
        Assert.Same(ease, actual.Ease?.Raw);
        Assert.Equal(Values(expected.ToParams()), Values(actual));
    }

    [AvaloniaFact]
    public void TextRevealsAreTheirExitFormOnExitAndTheirEntranceOnEnter()
    {
        var view = new RecipeXamlView();
        var tokens = MotionTokens.Standard;

        foreach (var block in new[] { view.Cascaded, view.Risen })
        {
            var enter = Presence.GetEnter(block)!.ToParams();
            var exit = Presence.GetExit(block)!.ToParams();
            Assert.Equal(0d, From(enter, "Opacity"));
            Assert.Equal(0d, exit.Properties["Opacity"].Raw);
        }
        Assert.Equal(tokens.Distance, Presence.GetExit(view.Cascaded)!.ToParams().Properties["Y"].Raw);
        Assert.IsType<FunctionValue>(Presence.GetExit(view.Risen)!.ToParams().Properties["Y"].Raw);
        Assert.Equal(TextPieces.Chars, TextReveal.GetPieces(view.Cascaded));
    }

    [AvaloniaFact]
    public void AnExtensionWithNoExitFormThrowsWhereTheExitFormIsAskedFor()
    {
        var card = new Avalonia.Controls.Border();
        RecipeExtension[] exitless = [new ShakeExtension(), new LiftExtension(), new PressInExtension(), new GlowExtension(), new ShimmerExtension(),
            new RecolorExtension(), new ReflowExtension(), new PlayFramesExtension(), new ReorderExtension()];

        Assert.Throws<NotSupportedException>(() => new ShakeExtension().ProvideValue(new ProvidedTarget(card, Presence.ExitProperty)));
        Assert.Throws<NotSupportedException>(() => new ShakeExtension().ProvideValue(new ProvidedTarget(new Avalonia.Controls.Flyout(), Popover.CloseProperty)));
        Assert.All(exitless, extension => Assert.Throws<NotSupportedException>(() => { extension.Out = true; extension.ProvideValue(null!); }));
        Assert.IsType<AnimationBuilder>(new ShakeExtension().ProvideValue(new ProvidedTarget(card, Presence.EnterProperty)));
    }

    [AvaloniaFact]
    public void ShimmerProvidesALoopingMaskSweep()
    {
        var parameters = ((AnimationBuilder)new ShimmerExtension().ProvideValue(null!)).ToParams();

        Assert.Equal(["MaskSweep"], parameters.Properties.Keys);
        Assert.Equal(0d, From(parameters, "MaskSweep"));
        Assert.Equal(Recipes.Shimmer().ToParams().Loop, parameters.Loop);
        Assert.Throws<NotSupportedException>(() => new ShimmerExtension { Out = true }.ProvideValue(null!));
    }

    [AvaloniaTheory]
    [InlineData(typeof(PageFadeExtension))]
    [InlineData(typeof(PageFadeThroughExtension))]
    [InlineData(typeof(PageSharedAxisExtension))]
    [InlineData(typeof(PageSlideExtension))]
    [InlineData(typeof(PagePushExtension))]
    [InlineData(typeof(PageBloomExtension))]
    public void PageExtensionsTakeNoneOfTheRecipeSettings(Type extension)
    {
        Assert.False(extension.IsSubclassOf(typeof(RecipeExtension)));
        foreach (var setting in new[] { nameof(RecipeExtension.Delay), nameof(RecipeExtension.Ease), nameof(RecipeExtension.Stagger), nameof(RecipeExtension.Out) })
            Assert.Null(extension.GetProperty(setting));
    }

    [AvaloniaFact]
    public void PlayFramesDefaultsTo24FpsLoopingForwards()
    {
        var parameters = ((AnimationBuilder)new PlayFramesExtension().ProvideValue(null!)).ToParams();
        var recipe = Recipes.PlayFrames().ToParams();

        var duration = Assert.IsType<FunctionValue>(parameters.Duration!.Value.Raw);
        Assert.Equal(1000 / 24d, Convert.ToDouble(duration(new object(), 0, 1).Raw), 6);
        Assert.Equal(recipe.Loop, parameters.Loop);
        Assert.NotEqual(Recipes.PlayFrames(loop: false).ToParams().Loop, parameters.Loop);
        Assert.Equal(recipe.Reversed, parameters.Reversed);
        Assert.NotEqual(true, parameters.Reversed);
    }

    [AvaloniaFact]
    public void SlideAndThePageSlidesDefaultToTheLeftEdge()
    {
        var slide = ((AnimationBuilder)new SlideExtension().ProvideValue(null!)).ToParams();
        var pageSlide = (PrestoPageTransition)new PageSlideExtension().ProvideValue(null!);
        var pagePush = (PrestoPageTransition)new PagePushExtension().ProvideValue(null!);

        Assert.Equal(Describe(Recipes.SlideIn(Edge.Left).ToParams()), Describe(slide));
        Assert.NotEqual(Describe(Recipes.SlideIn(Edge.Right).ToParams()), Describe(slide));
        Assert.Equal(Describe(Recipes.PageSlide(Edge.Left).Enter!.ToParams()), Describe(pageSlide.Enter!.ToParams()));
        Assert.Equal(Describe(Recipes.PagePush(Edge.Left).Enter!.ToParams()), Describe(pagePush.Enter!.ToParams()));
        Assert.NotEqual(Describe(Recipes.PagePush(Edge.Right).Enter!.ToParams()), Describe(pagePush.Enter!.ToParams()));
    }

    [AvaloniaFact]
    public void PageSharedAxisDefaultsToTheXAxis()
    {
        var transition = (PrestoPageTransition)new PageSharedAxisExtension().ProvideValue(null!);

        Assert.Contains("X", transition.Enter!.ToParams().Properties.Keys);
        Assert.DoesNotContain("Y", transition.Enter.ToParams().Properties.Keys);
    }

    private static string Describe(AnimationParams parameters) => string.Join(";", parameters.Properties.Select(property =>
        property.Value.Raw is TweenParams tween ? $"{property.Key}:{Value(tween.From)}>{Value(tween.To)}" : $"{property.Key}:{Value(property.Value)}"));

    private static object? Value(TweenValue? value) =>
        value?.Raw is FunctionValue function ? function(new Avalonia.Controls.Border(), 0, 1).Raw : value?.Raw;

    [AvaloniaFact]
    public void PlayFramesTakesAnExplicitDurationAndEaseOverTheFrameTiming()
    {
        var builder = (AnimationBuilder)new PlayFramesExtension { Duration = 500, Ease = "outQuad" }.ProvideValue(null!);

        var parameters = builder.ToParams();

        Assert.Equal((TweenValue)500, parameters.Duration);
        Assert.Same(EaseParser.Parse("outQuad"), parameters.Ease?.Raw);
    }
}
