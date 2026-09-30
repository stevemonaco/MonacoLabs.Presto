using Avalonia.Headless;
using Avalonia.Headless.XUnit;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>The recipe markup extensions (MOTION-KIT-PLAN §4.1 decision 7): exit-form detection and the shared settings.</summary>
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
        window.MouseMove(Centre(view.Hovered, window));
        Frames.Advance(400, 16);

        Assert.True((double)PrestoAvalonia.Get(view.Hovered, "Y")! < 0);
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
}
