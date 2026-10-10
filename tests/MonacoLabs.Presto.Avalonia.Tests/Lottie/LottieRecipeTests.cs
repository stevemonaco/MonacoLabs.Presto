using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using MonacoLabs.Presto.Lottie;

namespace MonacoLabs.Presto.Tests.Lottie;

/// <summary><c>Recipes.PlayLottie</c> and <see cref="PlayLottieExtension"/>.</summary>
public sealed class LottieRecipeTests : BridgeTestBase
{
    [AvaloniaFact]
    public void TheRecipeReadsEachViewsComposition()
    {
        var slow = new LottieView { Composition = LottieSegmentTests.Composition() };
        var fast = new LottieView { Composition = LottieSegmentTests.Composition(frameRate: 30, outPoint: 30, markers: "") };

        var animation = Recipes.PlayLottie(loop: false).Animate(new[] { slow, fast });

        Assert.Equal(6000, animation.Duration, 6);
        Frames.Advance(1000, 10);
        Assert.Equal(10, slow.Frame, tolerance: 0.5);
        Assert.Equal(30, fast.Frame);
    }

    [AvaloniaFact]
    public void TheRecipePlaysANamedSegmentInReverseAfterItsDelay()
    {
        var view = new LottieView { Composition = LottieSegmentTests.Composition() };

        var builder = Recipes.PlayLottie("idle", loop: false, reverse: true, speed: 2, delay: 100);
        var animation = builder.Animate(view);

        Assert.Equal((TweenValue)100, builder.ToParams().Delay);
        Assert.Equal(1000, animation.Duration, 6);
        Frames.Advance(1200, 10);
        Assert.Equal(20, view.Frame);
    }

    [AvaloniaFact]
    public void AnyOtherTargetIsAZeroLengthSegment()
    {
        var parameters = Recipes.PlayLottie().ToParams();
        var duration = Assert.IsType<FunctionValue>(parameters.Duration!.Value.Raw);

        Assert.Equal((double?)0, duration(new Border(), 0, 1).AsDouble);
    }

    [AvaloniaFact]
    public void ReducedMotionLandsOnTheSegmentsEndWithoutLooping()
    {
        Presto.Defaults.ReducedMotion = true;
        var view = new LottieView { Composition = LottieSegmentTests.Composition() };

        var animation = Recipes.PlayLottie("intro").Animate(view);
        Frames.Advance(16);

        Assert.Equal(0, animation.Duration, 6);
        Assert.True(animation.Completed);
        Assert.Equal(20, view.Frame);
    }

    [AvaloniaFact]
    public void TheMarkupExtensionPlaysItsSegmentOnAttach()
    {
        var view = new LottieXamlView();
        Show(view);

        var animation = Assert.Single(Presto.GetAnimations(view.Toggle));
        Assert.Equal(1000d / 6, animation.Duration, 6);

        Frames.Advance(200, 16);

        Assert.True(animation.Completed);
        Assert.Equal(40, view.Toggle.Frame);
    }

    [AvaloniaFact]
    public void TheMarkupExtensionTakesAnExplicitDurationAndEase()
    {
        var builder = (AnimationBuilder)new PlayLottieExtension { Duration = 500, Ease = "outQuad" }.ProvideValue(null!);

        var parameters = builder.ToParams();

        Assert.Equal((TweenValue)500, parameters.Duration);
        Assert.Same(EaseParser.Parse("outQuad"), parameters.Ease?.Raw);
    }
}
