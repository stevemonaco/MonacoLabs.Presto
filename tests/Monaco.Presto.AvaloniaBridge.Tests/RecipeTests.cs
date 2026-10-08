using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.AvaloniaBridge.Controls;
using Monaco.Presto.AvaloniaBridge.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Tests;

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
        var reduced = build().ToParams();

        Assert.Equal(["Opacity"], reduced.Properties.Keys);
        Assert.Equal((TweenValue)MotionTokens.Standard.Short, reduced.Duration);
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

        var reduced = Recipes.Reflow(delay: 100).ToParams();

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
        var properties = Interactions[name]().ToParams().Properties.Keys.ToArray();
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

        Assert.Equal(["ShadowBlur", "ShadowY", "ShadowOpacity"], Recipes.ReorderLift().ToParams().Properties.Keys);
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
        var reduced = Build(name)().ToParams();

        Assert.Null(reduced.Keyframes);
        Assert.DoesNotContain(reduced.Properties.Keys, p => p is "X" or "Y" || p.StartsWith("Scale") || p.StartsWith("Rotate") || p.StartsWith("Skew"));
        if (Loading.ContainsKey(name)) Assert.Equal(["Opacity"], reduced.Properties.Keys);
        else Assert.False(reduced.Loop is { Value: not 0 });
        if (Loops.ContainsKey(name)) Assert.Empty(reduced.Properties);
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

        var enter = transition.Enter!.ToParams();
        var exit = transition.Exit!.ToParams();

        foreach (var reduced in PageBuilders(transition).Select(b => b.ToParams()))
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
            Assert.Equal(["Opacity"], build().ToParams().Properties.Keys);
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
        var plain = Recipes.Recolor(Colors.Gold).ToParams();
        Presto.Defaults.ReducedMotion = true;
        var reduced = Recipes.Recolor(Colors.Gold).ToParams();
        var markup = ((AnimationBuilder)new RecolorExtension { To = Colors.Gold, Property = "BorderBrush", Space = ColorSpace.Oklch }.ProvideValue(null!)).ToParams();

        Assert.Equal(["Background"], plain.Properties.Keys);
        Assert.Equal(["Background"], reduced.Properties.Keys);
        Assert.Equal(((TweenValue[])plain["Background"].Raw!).Length, ((TweenValue[])reduced["Background"].Raw!).Length);
        Assert.Equal(["BorderBrush"], markup.Properties.Keys);
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
