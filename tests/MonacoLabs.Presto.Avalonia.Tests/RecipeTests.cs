using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MonacoLabs.Presto.Compositor;
using MonacoLabs.Presto.Controls;
using MonacoLabs.Presto.Targets;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="Recipes"/> (MOTION-KIT-PLAN §4): every family, their reduced forms and <see cref="Recipes.Tokens"/>.</summary>
public sealed class RecipeTests : CompositorTestBase
{
    private static readonly string[] UIThreadOnly = ["BlurIn", "FlipIn", "BlurOut", "FlipOut", "Lift", "ReorderLift", "Glow", "Shimmer"];

    private static readonly Dictionary<string, Func<AnimationBuilder>> Entrances = new()
    {
        ["Fade"] = () => Recipes.Fade(),
        ["FadeUp"] = () => Recipes.FadeUp(),
        ["FadeDown"] = () => Recipes.FadeDown(),
        ["FadeLeft"] = () => Recipes.FadeLeft(),
        ["FadeRight"] = () => Recipes.FadeRight(),
        ["ScaleIn"] = () => Recipes.ScaleIn(),
        ["PopIn"] = () => Recipes.PopIn(),
        ["BlurIn"] = () => Recipes.BlurIn(),
        ["SlideIn"] = () => Recipes.SlideIn(Edge.Top),
        ["FlipIn"] = () => Recipes.FlipIn(),
        ["Gather"] = () => Recipes.Gather(),
    };

    private static readonly Dictionary<string, (Func<AnimationBuilder> Build, string Property, double End)> Exits = new()
    {
        ["FadeOut"] = (() => Recipes.FadeOut(), "Opacity", 0),
        ["FadeOutUp"] = (() => Recipes.FadeOutUp(), "Y", -24),
        ["FadeOutDown"] = (() => Recipes.FadeOutDown(), "Y", 24),
        ["FadeOutLeft"] = (() => Recipes.FadeOutLeft(), "X", -24),
        ["FadeOutRight"] = (() => Recipes.FadeOutRight(), "X", 24),
        ["ScaleOut"] = (() => Recipes.ScaleOut(), "Scale", 0.9),
        ["PopOut"] = (() => Recipes.PopOut(), "Scale", 0.9),
        ["BlurOut"] = (() => Recipes.BlurOut(), "Blur", 12),
        ["SlideOut"] = (() => Recipes.SlideOut(Edge.Right), "X", 96),
        ["FlipOut"] = (() => Recipes.FlipOut(), "RotateX", -90),
        ["Scatter"] = (() => Recipes.Scatter(), "Scale", 0.6),
        ["Fall"] = (() => Recipes.Fall(), "Y", 144),
    };

    private static readonly Dictionary<string, Func<double, AnimationBuilder>> Attention = new()
    {
        ["Shake"] = i => Recipes.Shake(i),
        ["Pulse"] = i => Recipes.Pulse(i),
        ["Wobble"] = i => Recipes.Wobble(i),
        ["Jelly"] = i => Recipes.Jelly(i),
        ["Heartbeat"] = i => Recipes.Heartbeat(i),
        ["Tada"] = i => Recipes.Tada(i),
        ["Bounce"] = i => Recipes.Bounce(i),
    };

    private static readonly Dictionary<string, Func<AnimationBuilder>> Loops = new()
    {
        ["Float"] = () => Recipes.Float(),
        ["Spin"] = () => Recipes.Spin(),
        ["Breathe"] = () => Recipes.Breathe(),
        ["Blink"] = () => Recipes.Blink(),
        ["Wave"] = () => Recipes.Wave(),
    };

    private static readonly Dictionary<string, Func<AnimationBuilder>> Interactions = new()
    {
        ["Lift"] = () => Recipes.Lift(),
        ["ReorderLift"] = () => Recipes.ReorderLift(),
        ["Grow"] = () => Recipes.Grow(),
        ["PressIn"] = () => Recipes.PressIn(),
        ["Glow"] = () => Recipes.Glow(),
        ["Dim"] = () => Recipes.Dim(),
    };

    private static readonly Dictionary<string, Func<AnimationBuilder>> Loading = new()
    {
        ["Shimmer"] = () => Recipes.Shimmer(),
        ["Spinner"] = () => Recipes.Spinner(),
        ["Dots"] = () => Recipes.Dots(),
        ["Indeterminate"] = () => Recipes.Indeterminate(100),
    };

    private static readonly Dictionary<string, Func<PrestoPageTransition>> Pages = new()
    {
        ["PageFade"] = () => Recipes.PageFade(),
        ["PageFadeThrough"] = () => Recipes.PageFadeThrough(),
        ["PageSharedAxisX"] = () => Recipes.PageSharedAxis(PageAxis.X),
        ["PageSharedAxisY"] = () => Recipes.PageSharedAxis(PageAxis.Y),
        ["PageSharedAxisZ"] = () => Recipes.PageSharedAxis(PageAxis.Z),
        ["PageSlide"] = () => Recipes.PageSlide(Edge.Bottom),
        ["PagePush"] = () => Recipes.PagePush(Edge.Right),
    };

    public static TheoryData<string> PageNames => [.. Pages.Keys];

    public static TheoryData<string> EntranceNames => [.. Entrances.Keys];

    public static TheoryData<string> ExitNames => [.. Exits.Keys];

    public static TheoryData<string> AllNames => [.. Entrances.Keys, .. Exits.Keys];

    public static TheoryData<string> AttentionNames => [.. Attention.Keys];

    public static TheoryData<string> LoopingNames => [.. Loops.Keys, .. Loading.Keys];

    public static TheoryData<string, bool> InteractionCases => [.. Interactions.Keys.SelectMany(name => new[] { (name, false), (name, true) })];

    public static TheoryData<string> LaterFamilyNames => [.. Attention.Keys, .. Loops.Keys, .. Interactions.Keys, .. Loading.Keys];

    public static TheoryData<string> EveryName => [.. Entrances.Keys, .. Exits.Keys, .. Attention.Keys, .. Loops.Keys, .. Interactions.Keys, .. Loading.Keys];

    [AvaloniaTheory]
    [MemberData(nameof(EveryName))]
    public void EachRecipeRunsOnTheUIThreadAndOnTheCompositorUnlessDocumentedOtherwise(string name)
    {
        var build = Build(name);

        var animation = build().Animate(new Border());
        if (Loops.ContainsKey(name) || Loading.ContainsKey(name))
        {
            Frames.Advance(10000, 16);
            Assert.False(animation.Completed);
        }
        else
        {
            RunUntil(() => animation.Completed);
            Assert.True(animation.Completed);
        }

        var bar = ShowBar();
        if (UIThreadOnly.Contains(name)) Assert.Throws<NotSupportedException>(() => bar.AnimateOnCompositor(build()));
        else bar.AnimateOnCompositor(build()).Cancel();
    }

    [AvaloniaTheory]
    [MemberData(nameof(EntranceNames))]
    public void AnEntranceLandsOnTheElementsRestingValues(string name)
    {
        var card = new Border { Opacity = 0.6 };

        var animation = Entrances[name]().Animate(card);
        RunUntil(() => animation.Completed);

        Assert.Equal(0.6, card.Opacity, 6);
        foreach (var (property, rest) in new[] { ("X", 0.0), ("Y", 0.0), ("Scale", 1.0), ("RotateX", 0.0), ("Blur", 0.0) })
        {
            Assert.Equal(rest, Value(card, property), 6);
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(ExitNames))]
    public void AnExitEndsAtItsStatedValues(string name)
    {
        var card = new Border { Opacity = 0.6 };
        var (build, property, end) = Exits[name];

        var animation = build().Animate(card);
        RunUntil(() => animation.Completed);

        Assert.Equal(0, card.Opacity, 6);
        Assert.Equal(end, Value(card, property), 6);
    }

    [AvaloniaTheory]
    [MemberData(nameof(AllNames))]
    public void EachReducedFormOnlyFades(string name)
    {
        var build = Entrances.TryGetValue(name, out var entrance) ? entrance : Exits[name].Build;
        Presto.Defaults.ReducedMotion = true;
        var reduced = build().Resolve();

        Assert.Equal(["Opacity"], reduced.Properties.Keys);
        Assert.Equal((TweenValue)MotionTokens.Standard.Short, reduced.Duration);
    }

    [AvaloniaFact]
    public void TheReducedPresetIsShortAndLinearWithNoTravel()
    {
        var reduced = MotionTokens.Reduced;

        Assert.Equal((120d, 150d, 200d), (reduced.Short, reduced.Medium, reduced.Long));
        Assert.Same(Eases.None, reduced.Ease.Raw);
        Assert.Same(Eases.None, reduced.EmphasizedEase.Raw);
        Assert.Equal((0d, 1d, 0d, 0d), (reduced.Distance, reduced.Scale, reduced.BlurRadius, reduced.StaggerStep));
    }

    [AvaloniaFact]
    public void TokensWithoutAColorSpaceOrHueMixInOklabTheShorterWay()
    {
        var tokens = new MotionTokens(1, 2, 3, Eases.None, Eases.None, 0, 1, 0, 0);

        Assert.Equal(ColorSpace.Oklab, tokens.ColorSpace);
        Assert.Equal(HueInterpolation.Shorter, tokens.Hue);
    }

    [AvaloniaFact]
    public void SwappingTheTokensRestylesLaterCallsButNotBuildersAlreadyMade()
    {
        var before = Recipes.FadeUp();
        Recipes.Tokens = MotionTokens.Standard with { Medium = 999 };
        try
        {
            Assert.Equal((TweenValue)999, Recipes.FadeUp().ToParams().Duration);
            Assert.Equal((TweenValue)MotionTokens.Standard.Medium, before.ToParams().Duration);
        }
        finally
        {
            Recipes.Tokens = MotionTokens.Standard;
        }
    }

    [AvaloniaFact]
    public void ReflowTakesItsTimingFromTheTokensAndIsALayoutMotion()
    {
        Recipes.Tokens = MotionTokens.Standard with { Medium = 999 };
        try
        {
            var reflow = Recipes.Reflow().ToParams();

            Assert.Equal((TweenValue)999, reflow.Duration);
            Assert.Same(MotionTokens.Standard.Ease.Raw, reflow.Ease?.Raw);
            Assert.Equal(999, LayoutMotion.From(Recipes.Reflow()).Duration);
        }
        finally
        {
            Recipes.Tokens = MotionTokens.Standard;
        }
    }

    [AvaloniaFact]
    public void ReducedReflowMovesInstantly()
    {
        Presto.Defaults.ReducedMotion = true;

        var reduced = Recipes.Reflow(delay: 100).Resolve();

        Assert.Equal((TweenValue)0, reduced.Duration);
        Assert.Equal((TweenValue)0, reduced.Delay);
    }

    [AvaloniaFact]
    public void NamedArgumentsOverrideTheTokens()
    {
        var card = new Border();

        var animation = Recipes.FadeUp(distance: 40, duration: 300, ease: "linear").Autoplay(false).Animate(card);
        animation.Seek(150);

        Assert.Equal(300, animation.Duration);
        Assert.Equal(20, Value(card, "Y"), 6);
    }

    [AvaloniaFact]
    public void StaggerDelaysEachElementOfABatchByTheTokenStep()
    {
        Border[] cards = [new(), new(), new()];

        var animation = Recipes.Fade(delay: 100, stagger: true).Animate(cards);

        Assert.Equal((2 * MotionTokens.Standard.StaggerStep) + MotionTokens.Standard.Medium, animation.Duration);
        Frames.Advance(200, 16);
        Assert.True(cards[0].Opacity > cards[1].Opacity && cards[1].Opacity > cards[2].Opacity);
    }

    [AvaloniaFact]
    public void ADelayWithoutStaggerDelaysTheWholeBatchAndNoDelaySetsNone()
    {
        Border[] cards = [new(), new(), new()];

        Assert.Null(Recipes.Fade().ToParams().Delay);
        Assert.Equal((TweenValue)100, Recipes.Fade(delay: 100).ToParams().Delay);
        Recipes.Fade(delay: 100, ease: "linear").Animate(cards);

        Frames.Advance(70, 10);
        Assert.All(cards, card => Assert.Equal(0, card.Opacity));
        Frames.Advance(100, 10);
        Assert.InRange(cards[0].Opacity, .01, .99);
        Assert.All(cards, card => Assert.Equal(cards[0].Opacity, card.Opacity));
    }

    [AvaloniaFact]
    public void FlipInStartsAtNinetyDegreesByDefault()
    {
        var card = new Border();

        Recipes.FlipIn().Autoplay(false).Animate(card).Seek(0);

        Assert.Equal(90, Value(card, "RotateX"), 6);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void FlipInAndFlipOutHoldFullPerspectiveFromTheFirstFrame(bool entrance)
    {
        var card = new Border();

        (entrance ? Recipes.FlipIn() : Recipes.FlipOut()).Autoplay(false).Animate(card).Seek(0);

        Assert.Equal(800, Value(card, "Perspective"));
    }

    [AvaloniaFact]
    public void ThemeRevealWithoutAnOverlayLayerThrowsBeforeRunningTheChange()
    {
        var changed = false;

        Assert.Throws<InvalidOperationException>(() => Recipes.ThemeReveal(new Border(), () => changed = true));
        Assert.False(changed);
    }

    [AvaloniaFact]
    public void ThemeRevealDefaultsToTheSurfacesCenterTheLongDurationAndTheTokenEase()
    {
        Ease ease = t => t;
        Recipes.Tokens = MotionTokens.Standard with { Ease = ease, Long = 777 };
        try
        {
            var content = new Border { Background = Brushes.Red };
            var window = new Window { Content = content, Width = 200, Height = 200 };
            window.Show();
            Tick();

            var reveal = Recipes.ThemeReveal(window, () => content.Background = Brushes.Blue);

            Assert.Equal(777, reveal.Duration);
            Assert.All(Tweens(reveal), tween => Assert.Same(ease, tween.Ease));
            Assert.Equal(RelativePoint.Center, Presto.Get(reveal.Targets[0], "ClipCircleCenter"));
        }
        finally
        {
            Recipes.Tokens = MotionTokens.Standard;
        }
    }

    [AvaloniaFact]
    public void WithoutAnEaseTheseRecipesUseTheTokenEase()
    {
        Ease ease = t => t;
        Recipes.Tokens = MotionTokens.Standard with { Ease = ease };
        try
        {
            AnimationBuilder[] builders =
            [
                Recipes.PopoverIn(), Recipes.PopoverOut(), Recipes.CircularReveal(),
                Recipes.LineRise(), Recipes.LineMask(), Recipes.LineWipe(), Recipes.WordFade(), Recipes.WordBlur(),
                Recipes.LineRiseOut(), Recipes.LineMaskOut(), Recipes.LineWipeOut(), Recipes.WordFadeOut(), Recipes.WordBlurOut(),
            ];
            var animations = builders.Select(builder => builder.Autoplay(false).Animate(new TextBlock())).ToList();
            var typed = new TextBlock { Text = "Hi there" };
            var counted = new TextBlock { Text = "1" };
            var rolled = new TextBlock { Text = "1" };
            new Window { Content = new StackPanel { Children = { typed, counted, rolled } }, Width = 200, Height = 200 }.Show();
            Tick();
            animations.Add(Recipes.Typewriter(typed));
            animations.Add(Recipes.CountUp(counted, 10));
            animations.Add(Recipes.RollDigits(rolled, 2));

            Assert.All(animations, animation => Assert.All(Tweens(animation), tween => Assert.Same(ease, tween.Ease)));
            Assert.All(animations, animation => Assert.NotEmpty(Tweens(animation)));
            Assert.NotSame(ease, Tweens(Recipes.CharCascade().Autoplay(false).Animate(new TextBlock()))[0].Ease);
        }
        finally
        {
            Recipes.Tokens = MotionTokens.Standard;
        }
    }

    private static List<Tween> Tweens(Animation animation)
    {
        var tweens = new List<Tween>();
        for (var tween = animation._headTween; tween is not null; tween = tween._next) tweens.Add(tween);
        return tweens;
    }

    [AvaloniaFact]
    public void ReducedMotionMakesAPresenceEnterRecipePlayItsFade()
    {
        var panel = new StackPanel();
        Show(panel);
        var card = new Border { Height = 20 };
        Presence.SetEnter(card, Recipes.FadeUp());
        Presto.Defaults.ReducedMotion = true;
        panel.Children.Add(card);

        var animation = Assert.Single(Presto.GetAnimations(card));
        Assert.Equal(MotionTokens.Standard.Short, animation.Duration);
        Frames.Advance(100, 16);
        Assert.InRange(card.Opacity, 0.01, 0.99);
        Assert.Equal(0, Value(card, "Y"));
    }

    [AvaloniaTheory]
    [MemberData(nameof(AttentionNames))]
    public void AnAttentionRecipeEndsAtIdentity(string name)
    {
        var card = new Border();

        var animation = Attention[name](1).Animate(card);
        RunUntil(() => animation.Completed);

        foreach (var (property, rest) in new[] { ("X", 0.0), ("Y", 0.0), ("ScaleX", 1.0), ("ScaleY", 1.0), ("Rotate", 0.0) })
        {
            Assert.Equal(rest, Value(card, property), 6);
        }
    }

    [AvaloniaFact]
    public void IntensityScalesTheTravel()
    {
        Border once = new(), twice = new();

        Recipes.Shake(1, duration: 700, ease: "linear").Autoplay(false).Animate(once).Seek(100);
        Recipes.Shake(2, duration: 700, ease: "linear").Autoplay(false).Animate(twice).Seek(100);

        Assert.Equal(-10, Value(once, "X"), 6);
        Assert.Equal(-20, Value(twice, "X"), 6);
    }

    [AvaloniaFact]
    public void DotsHopOneAfterAnother()
    {
        Border[] dots = [new(), new(), new()];

        Recipes.Dots(duration: 600, ease: "linear").Animate(dots);
        Frames.Advance(150, 10);

        Assert.True(Value(dots[0], "Y") < Value(dots[1], "Y") && Value(dots[1], "Y") < Value(dots[2], "Y"));
    }

    [AvaloniaTheory]
    [MemberData(nameof(InteractionCases))]
    public void AnInteractionWorksAsAHoverStateAndReturnsToRest(string name, bool reduced)
    {
        var box = Box();
        var window = Show(box);
        Presto.Defaults.ReducedMotion = reduced;
        var properties = Interactions[name]().Resolve().Properties.Keys.ToArray();
        var rest = properties.Select(p => PrestoAvalonia.Get(box, p)).ToArray();
        Motion.SetHover(box, Interactions[name]());

        window.MouseMove(new Point(25, 25));
        Frames.Advance(400, 10);
        for (var i = 0; i < properties.Length; i++) Assert.NotEqual(rest[i], PrestoAvalonia.Get(box, properties[i]));

        window.MouseMove(new Point(150, 150));
        Frames.Advance(400, 10);
        for (var i = 0; i < properties.Length; i++) Assert.Equal(rest[i], PrestoAvalonia.Get(box, properties[i]));
        Assert.Empty(Presto.GetAnimations(box));
    }

    [AvaloniaFact]
    public void ReorderLiftGrowsOverARaisedShadowWithTheTokenTimingReadAtCallTime()
    {
        Recipes.Tokens = MotionTokens.Standard with { Short = 999 };
        try
        {
            var lift = Recipes.ReorderLift().ToParams();
            var stronger = Recipes.ReorderLift(scale: 1.1, intensity: 2).ToParams();

            Assert.Equal((TweenValue)999, lift.Duration);
            Assert.Same(MotionTokens.Standard.Ease.Raw, lift.Ease?.Raw);
            Assert.Equal(["ShadowBlur", "ShadowY", "ShadowOpacity", "Scale"], lift.Properties.Keys);
            Assert.Equal(1.03, (double)lift.Properties["Scale"].Raw!, 6);
            Assert.Equal(16d, lift.Properties["ShadowBlur"].Raw);
            Assert.Equal(6d, lift.Properties["ShadowY"].Raw);
            Assert.Equal(0.3, lift.Properties["ShadowOpacity"].Raw);
            Assert.Equal(1.2, (double)stronger.Properties["Scale"].Raw!, 6);
            Assert.Equal(32d, stronger.Properties["ShadowBlur"].Raw);
        }
        finally
        {
            Recipes.Tokens = MotionTokens.Standard;
        }
    }

    [AvaloniaFact]
    public void ReducedReorderLiftOnlyRaisesTheShadow()
    {
        Presto.Defaults.ReducedMotion = true;

        Assert.Equal(["ShadowBlur", "ShadowY", "ShadowOpacity"], Recipes.ReorderLift().Resolve().Properties.Keys);
    }

    [AvaloniaFact]
    public void TogglingReducedMotionTakesEffectAtTheNextTransition()
    {
        var box = Box();
        var window = Show(box);
        Motion.SetHover(box, Recipes.Lift());
        window.MouseMove(new Point(25, 25));
        Frames.Advance(400, 10);
        Assert.Equal(-4, Value(box, "Y"), 6);
        Presto.Defaults.ReducedMotion = true;
        window.MouseMove(new Point(150, 150));
        Frames.Advance(400, 10);
        Assert.Equal(0, Value(box, "Y"), 6);

        window.MouseMove(new Point(25, 25));
        Frames.Advance(400, 10);
        Assert.Equal(0, Value(box, "Y"), 6);
        Assert.Equal(12, Value(box, "ShadowBlur"), 6);
    }

    [AvaloniaTheory]
    [MemberData(nameof(LaterFamilyNames))]
    public void EachReducedFormDropsTravelAndScaleAndOnlyLoadingLoops(string name)
    {
        Presto.Defaults.ReducedMotion = true;
        var reduced = Build(name)().Resolve();

        Assert.Null(reduced.Keyframes);
        Assert.DoesNotContain(reduced.Properties.Keys, p => p is "X" or "Y" || p.StartsWith("Scale") || p.StartsWith("Rotate") || p.StartsWith("Skew"));
        if (Loading.ContainsKey(name)) Assert.Equal(["Opacity"], reduced.Properties.Keys);
        else Assert.False(reduced.Loop is { Value: not 0 });
        if (Loops.ContainsKey(name)) Assert.Empty(reduced.Properties);
    }

    [AvaloniaFact]
    public void ScatterEndsEachElementWithinItsDistanceAndSpin()
    {
        var cards = Enumerable.Range(0, 8).Select(_ => new Border()).ToArray();

        var animation = Recipes.Scatter(distance: 50, spin: 30).Animate(cards);
        RunUntil(() => animation.Completed);

        Assert.All(cards, card =>
        {
            Assert.InRange(Value(card, "X"), -50, 50);
            Assert.InRange(Value(card, "Y"), -50, 50);
            Assert.InRange(Value(card, "Rotate"), -30, 30);
        });
        Assert.True(cards.Select(card => Value(card, "X")).Distinct().Count() > 1);
    }

    [AvaloniaFact]
    public void FallDropsItsDistanceWithASmallDriftAndFadesOverItsLastThird()
    {
        var cards = Enumerable.Range(0, 8).Select(_ => new Border()).ToArray();

        var animation = Recipes.Fall(distance: 100, seed: 2, duration: 900, ease: "linear").Autoplay(false).Animate(cards);

        animation.Seek(590);
        Assert.All(cards, card => Assert.Equal(1, card.Opacity, 6));
        animation.Seek(750);
        Assert.All(cards, card => Assert.Equal(0.5, card.Opacity, 6));
        animation.Seek(900);
        Assert.All(cards, card =>
        {
            Assert.Equal(0, card.Opacity, 6);
            Assert.Equal(100, Value(card, "Y"), 6);
            Assert.InRange(Value(card, "X"), -25, 25);
            Assert.InRange(Value(card, "Rotate"), -45, 45);
        });
    }

    [AvaloniaFact]
    public void ASeedReplaysTheSameOffsetsAndGatherStartsWhereScatterEnds()
    {
        static double[] Offsets(AnimationBuilder builder, bool atEnd)
        {
            var cards = Enumerable.Range(0, 5).Select(_ => new Border()).ToArray();
            var animation = builder.Autoplay(false).Animate(cards);
            animation.Seek(atEnd ? animation.Duration : 0);
            return [.. cards.SelectMany(card => new[] { "X", "Y", "Rotate" }.Select(property => Math.Round(Value(card, property), 6)))];
        }

        var scatter = Recipes.Scatter(seed: 7);
        var ends = Offsets(scatter, atEnd: true);

        Assert.Equal(ends, Offsets(scatter, atEnd: true));
        Assert.Equal(ends, Offsets(Recipes.Scatter(seed: 7), atEnd: true));
        Assert.Equal(ends, Offsets(Recipes.Gather(seed: 7), atEnd: false));
        Assert.NotEqual(ends, Offsets(Recipes.Scatter(seed: 8), atEnd: true));
        Assert.Equal(Offsets(Recipes.Fall(seed: 7), atEnd: true), Offsets(Recipes.Fall(seed: 7), atEnd: true));
        Assert.NotEqual(Offsets(Recipes.Fall(seed: 7), atEnd: true), Offsets(Recipes.Fall(seed: 8), atEnd: true));
    }

    [AvaloniaFact]
    public void WaveDelaysEachElementByItsIndexTimesTheStep()
    {
        Border[] cards = [new(), new(), new()];

        Recipes.Wave(distance: 40, step: 50, delay: 100, duration: 400, ease: "linear").Animate(cards);
        Frames.Advance(300, 10);

        Assert.Equal(-20, Value(cards[0], "Y"), 2d);
        Assert.Equal(5, Value(cards[1], "Y") - Value(cards[0], "Y"), 6);
        Assert.Equal(5, Value(cards[2], "Y") - Value(cards[1], "Y"), 6);
    }

    [AvaloniaFact]
    public void TypewriterRevealsTheCharactersInOrder()
    {
        var block = new TextBlock { Text = "Hello" };
        Show(block);

        var animation = Recipes.Typewriter(block, charDelay: 100, duration: 10);
        var chars = animation.Targets.Cast<Control>().ToArray();
        Frames.Advance(250, 10);

        Assert.Equal(5, chars.Length);
        Assert.All(chars[..3], c => Assert.Equal(1, c.Opacity));
        Assert.All(chars[3..], c => Assert.Equal(0, c.Opacity));
        RunUntil(() => animation.Completed);
        Assert.All(chars, c => Assert.Equal(1, c.Opacity));
        Assert.Equal("Hello", block.Text);
    }

    [AvaloniaFact]
    public void ReducedTypewriterFadesTheWholeBlock()
    {
        var block = new TextBlock { Text = "Hello" };
        Show(block);
        Presto.Defaults.ReducedMotion = true;
        var animation = Recipes.Typewriter(block);

        Assert.Same(block, Assert.Single(animation.Targets));
        Assert.Equal(MotionTokens.Standard.Short, animation.Duration);
    }

    [AvaloniaFact]
    public void CountUpEndsOnTheFormattedValue()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var block = new TextBlock { Text = "1,000" };

            var animation = Recipes.CountUp(block, 1234, "N0", duration: 400, ease: "linear");
            Frames.Advance(200, 10);
            Assert.InRange(double.Parse(block.Text!, NumberStyles.Number, CultureInfo.InvariantCulture), 1050, 1180);
            RunUntil(() => animation.Completed);

            Assert.Equal("1,234", block.Text);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [AvaloniaFact]
    public void ReducedCountUpSetsTheEndValueOnTheFirstFrame()
    {
        var block = new TextBlock { Text = "0" };
        Presto.Defaults.ReducedMotion = true;
        Recipes.CountUp(block, 42.5, "0.0");
        Frames.Advance(16);

        Assert.Equal(42.5.ToString("0.0", CultureInfo.CurrentCulture), block.Text);
    }

    [AvaloniaTheory]
    [MemberData(nameof(PageNames))]
    public void EveryPropertyOfAPageRecipeHasACompositionCounterpart(string name)
    {
        var builders = PageBuilders(Pages[name]()).ToArray();

        Assert.True(builders.Length >= 2);
        Assert.All(builders.SelectMany(b => b.ToParams().Properties.Keys), p => Assert.NotNull(CompositorProperty.Find(p)));
    }

    [AvaloniaTheory]
    [MemberData(nameof(PageNames))]
    public void ReducedMotionPlaysEachPageRecipesShortCrossfade(string name)
    {
        var transition = Pages[name]();
        Presto.Defaults.ReducedMotion = true;

        var enter = transition.Enter!.Resolve();
        var exit = transition.Exit!.Resolve();

        foreach (var reduced in PageBuilders(transition).Select(b => b.Resolve()))
        {
            Assert.Equal(["Opacity"], reduced.Properties.Keys);
            Assert.Equal((TweenValue)MotionTokens.Standard.Short, reduced.Duration);
            Assert.Equal((TweenValue)0, reduced.Delay);
        }
        Assert.Equal(0d, Assert.IsType<TweenParams>(enter.Properties["Opacity"].Raw).From?.Raw);
        Assert.Equal(0d, exit.Properties["Opacity"].Raw);
    }

    [AvaloniaFact]
    public void APageSlideTravelsTheWidthOfTheHostsParent()
    {
        Border from = new() { Width = 100, HorizontalAlignment = HorizontalAlignment.Left }, to = new() { Width = 100, HorizontalAlignment = HorizontalAlignment.Left };
        Show(new Grid { Width = 300, Height = 200, Children = { from, to } });
        Tick();
        var transition = Recipes.PageSlide(Edge.Right);
        transition.Thread = AnimationThread.UIThread;

        var done = transition.Start(from, to, forward: true, CancellationToken.None);

        Assert.Equal(300, Value(to, "X"), 6);
        Frames.Advance(MotionTokens.Standard.Medium / 2, 16);
        Assert.InRange(Value(from, "X"), -299, -1);
        RunUntil(() => done.IsCompleted);
        Assert.Equal(0, Value(to, "X"), 6);
    }

    [AvaloniaFact]
    public void PlayFramesPlaysAnImageSequencesFrames()
    {
        var sequence = new ImageSequence { Frames = [new RenderTargetBitmap(new PixelSize(1, 1)), new RenderTargetBitmap(new PixelSize(1, 1)), new RenderTargetBitmap(new PixelSize(1, 1))] };

        var animation = Recipes.PlayFrames(fps: 10, loop: false).Animate(sequence);
        RunUntil(() => animation.Completed);

        Assert.Equal(300, animation.Duration, 6);
        Assert.Equal(2, sequence.CurrentFrame);
    }

    [AvaloniaFact]
    public void PlayFramesDelaysOnlyWhenGivenADelayAndDefaultsToTwentyFourFramesASecond()
    {
        var sheet = new SpriteSheet { Columns = 4, Rows = 2 };

        var plain = Recipes.PlayFrames().Animate(sheet);
        Assert.Equal(0, plain.Delay);
        Assert.Equal(8 * 1000 / 24d, plain.IterationDuration, 6);
        plain.Cancel();

        var delayed = Recipes.PlayFrames(fps: 10, delay: 200).Animate(sheet);
        Frames.Advance(0);
        Frames.Advance(150);
        Assert.Equal(200, delayed.Delay);
        Assert.Equal(0, sheet.CurrentFrame);
        Frames.Advance(200);
        Assert.Equal(1, sheet.CurrentFrame);
    }

    [AvaloniaFact]
    public void ReducedPlayFramesHoldsFrameZero()
    {
        var sheet = new SpriteSheet { Columns = 4, Rows = 2, Frame = 5 };
        Presto.Defaults.ReducedMotion = true;

        var animation = Recipes.PlayFrames(reverse: true).Animate(sheet);
        RunUntil(() => animation.Completed);

        Assert.Equal(0, sheet.Frame);
    }

    [AvaloniaFact]
    public void TheImageRecipesTargetTheFilters()
    {
        var colorize = Recipes.Colorize().ToParams();
        var develop = Recipes.Develop().ToParams();
        var flash = Recipes.FlashTint().ToParams();

        Assert.Equal([Key(PrestoImageBase.GrayscaleProperty), Key(PrestoImageBase.SaturateProperty)], colorize.Properties.Keys);
        Assert.Equal(0d, colorize[Key(PrestoImageBase.GrayscaleProperty)].Raw);
        Assert.Equal(1d, colorize[Key(PrestoImageBase.SaturateProperty)].Raw);
        Assert.Equal(["Opacity", Key(PrestoImageBase.SepiaProperty), Key(PrestoImageBase.BrightnessProperty), "Blur"], develop.Properties.Keys);
        Assert.Equal((TweenValue)MotionTokens.Standard.Long, develop.Duration);
        Assert.Equal([Key(PrestoImageBase.TintColorProperty), Key(PrestoImageBase.TintProperty)], flash.Properties.Keys);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ColorizeHoversAGrayscaleImageToFullColorAndBack(bool reduced)
    {
        var image = Photo();
        image.Grayscale = 1;
        var window = Show(image);
        Presto.Defaults.ReducedMotion = reduced;
        Motion.SetHover(image, Recipes.Colorize());

        window.MouseMove(new Point(25, 25));
        Frames.Advance(400, 10);
        Assert.Equal(0, image.Grayscale, 6);
        Assert.Equal(1, image.Saturate, 6);
        Assert.False(image.IsFiltering);

        window.MouseMove(new Point(150, 150));
        Frames.Advance(400, 10);
        Assert.Equal(1, image.Grayscale, 6);
        Assert.Empty(Presto.GetAnimations(image));
    }

    [AvaloniaFact]
    public void DevelopSettlesFromSepiaBrightnessAndBlurOntoTheImagesRestingValues()
    {
        var image = Photo();

        var animation = Recipes.Develop(duration: 400).Autoplay(false).Animate(image);
        animation.Seek(0);
        Assert.Equal(0.8, image.Sepia, 6);
        Assert.Equal(1.6, image.Brightness, 6);
        Assert.Equal(MotionTokens.Standard.BlurRadius, Value(image, "Blur"), 6);
        animation.Seek(400);

        Assert.Equal(0, image.Sepia, 6);
        Assert.Equal(1, image.Brightness, 6);
        Assert.Equal(0, Value(image, "Blur"), 6);
        Assert.Equal(1, image.Opacity, 6);
        Assert.False(image.IsFiltering);
    }

    [AvaloniaFact]
    public void FlashTintRaisesTheTintInItsColorAndEndsAtIdentity()
    {
        var image = Photo();

        var animation = Recipes.FlashTint(Colors.Gold, 0.5, duration: 400, ease: "linear").Autoplay(false).Animate(image);
        animation.Seek(200);
        Assert.Equal(0.5, image.Tint, 6);
        Assert.Equal(Colors.Gold, image.TintColor);
        animation.Seek(400);

        Assert.Equal(0, image.Tint, 6);
        Assert.False(image.IsFiltering);
    }

    [AvaloniaFact]
    public void ReducedDevelopAndFlashTintOnlyFadeOrBlink()
    {
        var image = Photo();
        Presto.Defaults.ReducedMotion = true;

        foreach (var build in new Func<AnimationBuilder>[] { () => Recipes.Develop(), () => Recipes.FlashTint() })
        {
            Assert.Equal(["Opacity"], build().Resolve().Properties.Keys);
            var animation = build().Animate(image);
            Assert.Equal(MotionTokens.Standard.Short, animation.Duration);
            RunUntil(() => animation.Completed);
            Assert.Equal(1, image.Opacity, 6);
            Assert.False(image.IsFiltering);
        }
    }

    [AvaloniaFact]
    public void RecolorFollowsTheTokensSpaceFromTheCurrentBackgroundAndEndsExactly()
    {
        var border = new Border { Background = new SolidColorBrush(Color.Parse("#2563eb")) };
        var amber = Color.Parse("#f59e0b");
        Recipes.Tokens = MotionTokens.Standard with { ColorSpace = ColorSpace.Oklch };
        try
        {
            var animation = Recipes.Recolor(amber, duration: 400, ease: "linear").Autoplay(false).Animate(border);
            animation.Seek(0);
            Assert.Equal(Color.Parse("#2563eb"), Background(border));
            animation.Seek(200);
            Assert.True(Background(border).ToPresto().ToOklch().C > 0.12, $"{Background(border)} lost its chroma");
            animation.Seek(400);
            Assert.Equal(amber, Background(border));
        }
        finally
        {
            Recipes.Tokens = MotionTokens.Standard;
        }
    }

    [AvaloniaFact]
    public void RecolorStartsEachElementFromItsOwnColorAndStaggers()
    {
        var red = new Border { Background = Brushes.Red };
        var bare = new Border();
        var animation = Recipes.Recolor(Colors.Blue, duration: 400, stagger: true).Autoplay(false).Animate(new[] { red, bare });

        animation.Seek(0);
        Assert.Equal(Colors.Red, Background(red));
        Assert.Equal(0, Background(bare).A);
        animation.Seek(400 + MotionTokens.Standard.StaggerStep);
        Assert.Equal(Colors.Blue, Background(red));
        Assert.Equal(Colors.Blue, Background(bare));
    }

    [AvaloniaFact]
    public void RecolorIsTheSameUnderReducedMotionAndFromXaml()
    {
        var plain = Recipes.Recolor(Colors.Gold).Resolve();
        Presto.Defaults.ReducedMotion = true;
        var reduced = Recipes.Recolor(Colors.Gold).Resolve();
        var markup = ((AnimationBuilder)new RecolorExtension { To = Colors.Gold, Property = "BorderBrush", Space = ColorSpace.Oklch }.ProvideValue(null!)).ToParams();

        Assert.Equal(["Background"], plain.Properties.Keys);
        Assert.Equal(["Background"], reduced.Properties.Keys);
        Assert.Equal(((TweenValue[])plain["Background"].Raw!).Length, ((TweenValue[])reduced["Background"].Raw!).Length);
        Assert.Equal(["BorderBrush"], markup.Properties.Keys);
    }

    [AvaloniaFact]
    public void RecolorIsLeftAloneByReduceAll()
    {
        var border = new Border { Background = Brushes.Red };
        Presto.Defaults.ReducedMotion = true;
        Presto.Defaults.ReduceAll = true;

        var animation = Recipes.Recolor(Colors.Blue, duration: 400, ease: "linear").Autoplay(false).Animate(border);
        animation.Seek(200);

        Assert.Equal(400, animation.Duration);
        Assert.NotEqual(Colors.Red, Background(border));
        Assert.NotEqual(Colors.Blue, Background(border));
    }

    [AvaloniaFact]
    public void RecolorAnimatesBackgroundWithoutAPropertyAndAnExplicitKeyOtherwise()
    {
        var border = new Border { Background = Brushes.Red, BorderBrush = Brushes.Red };

        Recipes.Recolor(Colors.Blue, duration: 100).Autoplay(false).Animate(border).Seek(100);
        Recipes.Recolor(Colors.Gold, PrestoKeys.Of(Border.BorderBrushProperty), duration: 100).Autoplay(false).Animate(border).Seek(100);

        Assert.Equal(Colors.Blue, Background(border));
        Assert.Equal(Colors.Gold, ((ISolidColorBrush)border.BorderBrush!).Color);
    }

    private static Color Background(Border border) => ((ISolidColorBrush)border.Background!).Color;

    private static string Key(AvaloniaProperty property) => AvaloniaObjectTargetAdapter.Encode(property);

    private static FilteredImage Photo()
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(10, 10));
        using (var context = bitmap.CreateDrawingContext()) context.FillRectangle(Brushes.Red, new Rect(0, 0, 10, 10));
        return new FilteredImage
        {
            Source = bitmap, Width = 50, Height = 50, Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
    }

    private static IEnumerable<AnimationBuilder> PageBuilders(PrestoPageTransition transition) =>
        new[] { transition.Enter, transition.Exit, transition.BackEnter, transition.BackExit }.OfType<AnimationBuilder>();

    private static Func<AnimationBuilder> Build(string name) =>
        Entrances.TryGetValue(name, out var entrance) ? entrance
        : Exits.TryGetValue(name, out var exit) ? exit.Build
        : Attention.TryGetValue(name, out var attention) ? () => attention(1)
        : Loops.TryGetValue(name, out var loop) ? loop
        : Interactions.TryGetValue(name, out var interaction) ? interaction
        : Loading[name];

    private static double Value(Visual card, string property) => Convert.ToDouble(Presto.Get(card, property, withUnit: false));
}
