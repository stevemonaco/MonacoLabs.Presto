using Avalonia.Controls.Shapes;
using Avalonia.Media;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Values;

namespace Tempo.Motion;

/// <summary>
/// Every animation in Tempo, assigned from XAML with <c>{x:Static}</c>. Each states its reduced form: motion becomes a
/// fade, loops stop and colors still change.
/// </summary>
public static class TempoMotion
{
    public static readonly Color FocusAccent = Color.Parse("#F2735E");
    public static readonly Color ShortBreakAccent = Color.Parse("#3CC6BE");
    public static readonly Color LongBreakAccent = Color.Parse("#9B82F5");

    public static readonly Geometry PauseIcon = Geometry.Parse("M7,5 L10,5 L10,19 L7,19 Z M14,5 L17,5 L17,19 L14,19 Z");

    public static readonly AnimationBuilder RowEnter = Presto.Build()
        .Opacity(from: 0)
        .Y(from: 12)
        .Duration(420)
        .Ease(Eases.OutCubic)
        .Delay(Presto.Stagger(40))
        .WhenReduced(reduced => reduced.Opacity(from: 0).Duration(240));

    public static readonly AnimationBuilder RowExit = Presto.Build()
        .Opacity(0)
        .X(-24)
        .Duration(220)
        .Ease(Eases.InCubic)
        .WhenReduced(reduced => reduced.Opacity(0).Duration(160));

    public static readonly AnimationBuilder RowLift = Recipes.ReorderLift(duration: 180);

    /// <summary>The selection indicator's glide between task rows and between the view switch's segments.</summary>
    public static readonly AnimationBuilder RowSelect = Presto.Build()
        .Duration(280)
        .Ease(Eases.OutCubic)
        .WhenReduced(reduced => reduced.Duration(0));

    public static readonly AnimationBuilder CheckPop = Presto.Build()
        .Scale(k => k.To(1.25).To(1))
        .Duration(300)
        .Ease(Eases.OutQuad)
        .WhenReduced(reduced => reduced.Opacity(k => k.To(.4).To(1)));

    /// <summary>A task's dot when a session lands on it and fills it.</summary>
    public static readonly AnimationBuilder DotPop = Presto.Build()
        .Scale(k => k.To(1.3).To(1))
        .Duration(380)
        .Ease(Eases.OutQuad)
        .WhenReduced(reduced => reduced.Opacity(k => k.To(.4).To(1)));

    public static readonly AnimationBuilder IconToPause = Presto.Build()
        .Data(PrestoAvalonia.MorphTo(PauseIcon))
        .Duration(260)
        .Ease(Eases.InOutCubic)
        // Not 0: a zero-duration state never settles its return to rest (BACKLOG, BRIDGE-MOTION).
        .WhenReduced(reduced => reduced.Data(PrestoAvalonia.MorphTo(PauseIcon)).Duration(1));

    public static readonly AnimationBuilder RingBreathe = Presto.Build()
        .Scale(1.015)
        .Duration(2400)
        .Ease(Eases.InOutSine)
        .Loop()
        .Alternate()
        .WhenReduced(reduced => reduced.Loop(false).Alternate(false));

    /// <summary>The breathing guide: in for two seconds, out for two.</summary>
    public static readonly AnimationBuilder Breathe = Presto.Build()
        .Scale(1.15, from: .8)
        .Duration(2000)
        .Ease(Eases.InOutSine)
        .Loop()
        .Alternate()
        .WhenReduced(reduced => reduced.Opacity(from: 0).Duration(400).Loop(false).Alternate(false));

    /// <summary>A chart bar growing from its baseline, starting as the Stats page settles in.</summary>
    public static readonly AnimationBuilder BarGrow = Presto.Build()
        .ScaleY(from: 0)
        .Duration(600)
        .Ease(Eases.OutCubic)
        .Delay(Presto.Stagger(50, new() { Start = 120 }))
        .WhenReduced(reduced => reduced.Opacity(from: 0).Duration(300).Delay(0));

    public static readonly AnimationBuilder FlyoutOpen = Recipes.PopoverIn(duration: 220);

    public static readonly AnimationBuilder IntroFlight = Presto.Build()
        .Duration(650)
        .Ease(Eases.OutQuint)
        .WhenReduced(reduced => reduced.Duration(0));

    /// <summary>The task list while a break runs: dimmed and blurred back.</summary>
    public static readonly AnimationBuilder ListDim = Presto.Build()
        .Opacity(.45)
        .Blur(6)
        .Duration(500)
        .Ease(Eases.InOutSine)
        .WhenReduced(reduced => reduced.Opacity(.45));

    public static readonly AnimationBuilder FocusBackground = Accent("Background", FocusAccent);
    public static readonly AnimationBuilder ShortBreakBackground = Accent("Background", ShortBreakAccent);
    public static readonly AnimationBuilder LongBreakBackground = Accent("Background", LongBreakAccent);
    public static readonly AnimationBuilder FocusForeground = Accent("Foreground", FocusAccent);
    public static readonly AnimationBuilder ShortBreakForeground = Accent("Foreground", ShortBreakAccent);
    public static readonly AnimationBuilder LongBreakForeground = Accent("Foreground", LongBreakAccent);
    public static readonly AnimationBuilder FocusFill = Accent("Fill", FocusAccent);
    public static readonly AnimationBuilder ShortBreakFill = Accent("Fill", ShortBreakAccent);
    public static readonly AnimationBuilder LongBreakFill = Accent("Fill", LongBreakAccent);

    /// <summary>The intro's elements that are not flying enter alongside the ring's flight (<c>Connected.Anchor</c>).</summary>
    public static readonly AnimationBuilder HandoffFade = Presto.Build()
        .Opacity(from: 0)
        .Duration(400)
        .Delay(150)
        .Ease(Eases.OutCubic)
        .WhenReduced(reduced => reduced.Opacity(from: 0).Duration(300).Delay(0));

    public static readonly AnimationBuilder IntroGlowBloom = Presto.Build()
        .Scale(1, from: .6)
        .Opacity(1, from: 0)
        .Background(k => k.Color(FocusAccent.ToPresto(), LongBreakAccent.ToPresto(), ColorSpace.Oklch, duration: 600, ease: Eases.OutCubic))
        .Duration(600)
        .Ease(Eases.OutCubic)
        .WhenReduced(reduced => reduced
            .Opacity(1, from: 0)
            .Background(k => k.Color(FocusAccent.ToPresto(), LongBreakAccent.ToPresto(), ColorSpace.Oklch, duration: 400))
            .Duration(400));

    public static readonly AnimationBuilder IntroTrackDraw = Presto.Build()
        .Draw(from: "0 0", to: "0 1")
        .Duration(750)
        .Ease(Eases.OutExpo)
        .WhenReduced(reduced => reduced.Draw(to: "0 1", duration: 0).Opacity(1, from: 0).Duration(400));

    public static readonly AnimationBuilder IntroArcPop = Presto.Build()
        .Scale(1, from: 0)
        .Opacity(1, from: 0, duration: 120, ease: Eases.OutQuad)
        .Ease(Eases.Spring(bounce: .45, duration: 600))
        .Delay(Presto.Stagger(90))
        .WhenReduced(reduced => reduced.Opacity(1, from: 0).Duration(400).Ease(Eases.OutQuad).Delay(0));

    public static readonly AnimationBuilder IntroTagline = Presto.Build()
        .Opacity(1, from: 0)
        .Y(0, from: 8)
        .Duration(400)
        .Ease(Eases.OutCubic)
        .Delay(Presto.Stagger(50))
        .WhenReduced(reduced => reduced.Opacity(1, from: 0).Delay(0));

    /// <summary>The tick dot's one lap of <paramref name="track"/>, in step with <see cref="IntroTickFill"/>.</summary>
    /// <remarks>A method: a motion path is read from a laid-out shape (BACKLOG, BRIDGE-SHAPES).</remarks>
    public static AnimationBuilder IntroTick(Shape track)
    {
        var path = PrestoAvalonia.CreateMotionPath(track);
        return Presto.Build()
            .X(path.X)
            .Y(path.Y)
            .Opacity(1, from: 0, duration: 120)
            .Duration(600)
            .Ease(Eases.InOutSine)
            .WhenReduced(reduced => reduced.Opacity(0));
    }

    public static readonly AnimationBuilder IntroTickFill = Presto.Build()
        .Draw(from: "0 0", to: "0 1")
        .Duration(600)
        .Ease(Eases.InOutSine)
        .WhenReduced(reduced => reduced.Draw(to: "0 1", duration: 0));

    public static readonly AnimationBuilder IntroMerge = Presto.Build()
        .Opacity(0)
        .Scale(.92)
        .Duration(400)
        .Ease(Eases.InCubic)
        .WhenReduced(reduced => reduced.Opacity(0));

    public static readonly AnimationBuilder IntroPulse = Presto.Build()
        .Scale(k => k.To(1.1).To(1))
        .Opacity(k => k.To(1).To(0))
        .Duration(400)
        .Ease(Eases.OutQuad)
        .WhenReduced(reduced => reduced.Opacity(k => k.To(1).To(0)));

    /// <summary>Shows the wordmark fully drawn when a skip cancels its particles, which leaves it hidden.</summary>
    public static readonly AnimationBuilder IntroWordmarkLand = Presto.Build()
        .Opacity(1)
        .WhenReduced(reduced => reduced.Opacity(1));

    /// <summary>Recolors <paramref name="property"/> from its current color to <paramref name="accent"/> through OKLCH, so coral to aqua never passes through gray.</summary>
    private static AnimationBuilder Accent(PropertyKey property, Color accent)
    {
        void Recolor(AnimationBuilder builder) => builder.Property(property, k => k.Color(
            (FunctionValue)((target, _, _) => Presto.Get(target, property) is ISolidColorBrush brush ? brush.Color.ToPresto() : accent.ToPresto()),
            accent.ToPresto(), ColorSpace.Oklch, duration: 700, ease: Eases.InOutSine));

        var builder = Presto.Build();
        Recolor(builder);
        return builder.WhenReduced(Recolor);
    }
}
