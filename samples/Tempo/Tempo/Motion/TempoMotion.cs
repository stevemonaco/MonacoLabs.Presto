using System.Globalization;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Values;
using Tempo.Model;
using Tempo.ViewModels;

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

    private static readonly Dictionary<TaskTag, IBrush> s_tagBrushes = new()
    {
        [TaskTag.Writing] = new ImmutableSolidColorBrush(Color.Parse("#F2B45E")),
        [TaskTag.Code] = new ImmutableSolidColorBrush(Color.Parse("#5EA4F2")),
        [TaskTag.Design] = new ImmutableSolidColorBrush(Color.Parse("#E27AC8")),
        [TaskTag.Admin] = new ImmutableSolidColorBrush(Color.Parse("#93A4B8")),
        [TaskTag.Personal] = new ImmutableSolidColorBrush(Color.Parse("#7CCF78")),
    };

    private static readonly IBrush s_untaggedBrush = new ImmutableSolidColorBrush(Color.Parse("#6B7080"));

    /// <summary>A <see cref="TaskTag"/>'s color as a brush, gray for no tag; null for anything else, such as the All chip.</summary>
    public static readonly IValueConverter TagBrush = new FuncValueConverter<object?, IBrush?>(value => value switch
    {
        TaskTag tag => s_tagBrushes[tag],
        null => s_untaggedBrush,
        _ => null,
    });

    public static readonly Geometry PauseIcon = Geometry.Parse("M7,5 L10,5 L10,19 L7,19 Z M14,5 L17,5 L17,19 L14,19 Z");
    public static readonly Geometry CheckIcon = Geometry.Parse("M9,16.2 L4.8,12 L3.4,13.4 L9,19 L21,7 L19.6,5.6 Z");
    public static readonly Geometry StopIcon = Geometry.Parse("M6,6 L18,6 L18,18 L6,18 Z");

    // The stop square's bounds, so the icon keeps its size through the morph.
    public static readonly Geometry CrossIcon = Geometry.Parse("M6,7.5 L7.5,6 L12,10.5 L16.5,6 L18,7.5 L13.5,12 L18,16.5 L16.5,18 L12,13.5 L7.5,18 L6,16.5 L10.5,12 Z");

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

    /// <summary>The selection indicator's glide between task rows, the view switch's segments and the bottom bar's items.</summary>
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

    /// <summary>
    /// The play triangle turning into the pause bars with a half-turn and a small pop; the bars are the same upside down,
    /// so the turn lands square, and leaving turns it back. Reduced: the morph alone, at once.
    /// </summary>
    public static readonly AnimationBuilder IconToPause = Presto.Build()
        .Data(PrestoAvalonia.MorphTo(PauseIcon))
        .Rotate(180)
        .Scale(k => k.To(.75, duration: 180).To(1, duration: 240, ease: Eases.OutBack()))
        .Duration(420)
        .Ease(Eases.InOutCubic)
        .WhenReduced(reduced => reduced.Data(PrestoAvalonia.MorphTo(PauseIcon)).Duration(0));

    /// <summary>The accent halo pinging out from the start pill as a session starts or resumes. Reduced: none.</summary>
    public static readonly AnimationBuilder StartPing = Presto.Build()
        .Scale(1.45)
        .Opacity(k => k.To(.5, duration: 1).To(0, duration: 720))
        .Duration(720)
        .Ease(Eases.OutCubic)
        .WhenReduced(_ => { });

    public static readonly Geometry SkipIcon = Geometry.Parse("M6,5 L15,12 L6,19 Z M17,5 L19,5 L19,19 L17,19 Z");

    // The skip glyph's bounds, so the icon keeps its size through the morph.
    public static readonly Geometry FastForwardIcon = Geometry.Parse("M6,5 L12.5,12 L6,19 Z M12.5,5 L19,12 L12.5,19 Z");

    /// <summary>The skip glyph opening into a fast-forward while hovered, and back. Reduced: the morph, at once.</summary>
    public static readonly AnimationBuilder SkipToForward = Presto.Build()
        .Data(PrestoAvalonia.MorphTo(FastForwardIcon))
        .Duration(240)
        .Ease(Eases.OutCubic)
        .WhenReduced(reduced => reduced.Data(PrestoAvalonia.MorphTo(FastForwardIcon)).Duration(0));

    /// <summary>The glass disc behind skip and stop brightening while hovered. Reduced: the same fade.</summary>
    public static readonly AnimationBuilder DiscLight = Presto.Build()
        .Opacity(.14)
        .Duration(180)
        .Ease(Eases.OutQuad)
        .WhenReduced(reduced => reduced.Opacity(.14).Duration(180));

    /// <summary>The start/pause pill's width easing between pill and circle as its label shows and hides. Reduced: instant.</summary>
    public static readonly AnimationBuilder PillResize = Presto.Build()
        .Duration(360)
        .Ease(Eases.OutCubic)
        .WhenReduced(reduced => reduced.Duration(0));

    /// <summary>The add box's plus turning into a check while it has text, and back. Reduced: the morph alone, at once.</summary>
    public static readonly AnimationBuilder PlusToCheck = Presto.Build()
        .Data(PrestoAvalonia.MorphTo(CheckIcon))
        .Rotate(360)
        .Duration(420)
        .Ease(Eases.InOutCubic)
        .WhenReduced(reduced => reduced.Data(PrestoAvalonia.MorphTo(CheckIcon)).Duration(0));

    /// <summary>The skip icon fast-forwarding: out to the right, and a fresh one in from the left. Reduced: an opacity blink.</summary>
    public static readonly AnimationBuilder SkipForward = Presto.Build()
        .X(k => k.To(14, duration: 140, ease: Eases.InCubic).To(-14, duration: 1).To(0, duration: 260, ease: Eases.OutCubic))
        .Opacity(k => k.To(0, duration: 140).To(0, duration: 1).To(1, duration: 260))
        .WhenReduced(reduced => reduced.Opacity(k => k.To(.4).To(1)).Duration(240));

    /// <summary>One sweep round the skip button's ring as it skips, echoing the stop button's hold. Reduced: none.</summary>
    public static readonly AnimationBuilder SkipSweep = Presto.Build()
        .Draw(from: "0 0", to: "0 1")
        .Opacity(k => k.To(.9, duration: 1).To(.9, duration: 300).To(0, duration: 220))
        .Duration(420)
        .Ease(Eases.OutCubic)
        .WhenReduced(_ => { });

    /// <summary>The settings button's quarter turn while hovered or while settings are open. Reduced: none.</summary>
    public static readonly AnimationBuilder GearSpin = Presto.Build()
        .Rotate(90)
        .Duration(360)
        .Ease(Eases.OutBack())
        .WhenReduced(_ => { });

    /// <summary>The hold-to-abandon control let go before it confirms. Reduced: an opacity blink.</summary>
    public static readonly AnimationBuilder AbandonShake = Recipes.Shake(.6);

    /// <summary>The stop square crossing out as an abandon confirms, then squaring up again. Reduced: an opacity blink.</summary>
    public static readonly AnimationBuilder AbandonCross = Presto.Build()
        .Data(k => k.To(PrestoAvalonia.MorphTo(CrossIcon), duration: 200).To(PrestoAvalonia.MorphTo(StopIcon), duration: 260, delay: 420))
        .Ease(Eases.InOutCubic)
        .WhenReduced(reduced => reduced.Opacity(k => k.To(.3).To(1)).Duration(300));

    /// <summary>The full hold ring collapsing inward as an abandon confirms, and springing back once it has rewound. Reduced: an opacity blink.</summary>
    public static readonly AnimationBuilder AbandonCollapse = Presto.Build()
        .Scale(k => k.To(.55, duration: 240, ease: Eases.InCubic).To(1, duration: 420, delay: 400, ease: Eases.OutBack()))
        .Opacity(k => k.To(0, duration: 240).To(1, duration: 300, delay: 400))
        .WhenReduced(reduced => reduced.Opacity(k => k.To(.3).To(1)).Duration(300));

    /// <summary>The confirm dialog on Escape once its hold has been pressed. Reduced: an opacity blink.</summary>
    public static readonly AnimationBuilder DialogShake = Recipes.Shake();

    /// <summary>The task rows flying apart, top row first, before a confirmed clear empties them. Reduced: a fade.</summary>
    public static readonly AnimationBuilder ClearScatter = Recipes.Scatter(seed: 7, stagger: true);

    public static readonly AnimationBuilder RingBreathe = Presto.Build()
        .Scale(1.015)
        .Duration(2400)
        .Ease(Eases.InOutSine)
        .Loop()
        .Alternate()
        .WhenReduced(reduced => reduced.Loop(false).Alternate(false));

    /// <summary>The ring's glow, in its own color, once a second through the last ten seconds. Reduced: an opacity blink.</summary>
    public static readonly AnimationBuilder FinalPulse = Presto.Build()
        .ShadowColor(to: (FunctionValue)((target, _, _) => TweenValue.Of(Presto.Get(target, "Fill") is ISolidColorBrush brush ? brush.Color : FocusAccent)), duration: 0)
        .ShadowBlur(k => k.To(28).To(0))
        .ShadowOpacity(k => k.To(.9).To(0))
        .Duration(700)
        .Ease(Eases.OutQuad)
        .WhenReduced(reduced => reduced.Opacity(k => k.To(.6).To(1)).Duration(240));

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

    /// <summary>The chart bars dropping to their baseline in turn during a clear, after <see cref="ClearScatter"/>. Reduced: a fade, all at once.</summary>
    public static readonly AnimationBuilder BarCollapse = Presto.Build()
        .ScaleY(0)
        .Duration(320)
        .Ease(Eases.InCubic)
        .Delay(Presto.Stagger(30))
        .WhenReduced(reduced => reduced.Opacity(0).Duration(200).Delay(0));

    /// <summary>A heatmap day popping in, each farther from today's cell a little later. Reduced: a fade, all at once.</summary>
    public static readonly AnimationBuilder HeatCellArrive = Presto.Build()
        .Opacity(from: 0)
        .Scale(1, from: .3, ease: Eases.OutBack())
        .Duration(380)
        .Ease(Eases.OutCubic)
        .Delay((FunctionValue)((target, _, _) => target is StyledElement { DataContext: HeatmapCell cell } ? 150 + 28 * cell.Distance : 0))
        .WhenReduced(reduced => reduced.Opacity(from: 0).Duration(300).Delay(0));

    /// <summary>The donut's segments sweeping round from the top to their shares. Reduced: a fade.</summary>
    public static readonly AnimationBuilder DonutDraw = Presto.Build()
        .Draw(from: "0 0", to: (FunctionValue)((target, _, _) => target is StyledElement { DataContext: DonutSegment segment }
            ? string.Create(CultureInfo.InvariantCulture, $"0 {segment.End}") : "0 0"))
        .Duration(900)
        .Delay(150)
        .Ease(Eases.OutCubic)
        .WhenReduced(reduced => reduced.Opacity(from: 0).Duration(300));

    /// <summary>A segment easing to its new share as the range changes (<c>Follow.Timing</c>). Reduced: instant, as every follow is.</summary>
    public static readonly AnimationBuilder DonutMorph = Presto.Build()
        .Duration(600)
        .Ease(Eases.InOutCubic)
        .WhenReduced(reduced => reduced.Duration(0));

    private static readonly (double X, double Y, double Scale, double Duration)[] s_blobDrifts =
        [(180, 120, 1.2, 26000), (-200, 90, .85, 33000), (140, -130, 1.15, 39000), (-120, -90, 1.1, 21000)];

    /// <summary>How far from a blob's center, in DIPs, the pointer swells and brightens it.</summary>
    public const double BlobReach = 520;

    /// <summary>The blobs' slow, soft follow of the pointer, matching their <c>Magnetic.Spring</c>.</summary>
    public static readonly Spring BlobSpring = new(bounce: 0, duration: 1400);

    /// <summary>A blob's scale at a pointer proximity from 0 to 1: up to 15% larger with the pointer at its center.</summary>
    public static double BlobSwell(double proximity) => 1 + .15 * proximity;

    /// <summary>A blob's opacity at a pointer proximity: resting at 80%, full with the pointer at its center.</summary>
    public static double BlobGlow(double proximity) => .8 + .2 * proximity;

    /// <summary>The backdrop blobs' drift, each blob on its own period so they never line up. Runs on the compositor; reduced, the blobs hold still.</summary>
    public static readonly AnimationBuilder BlobDrift = Presto.Build()
        .X((FunctionValue)((_, i, _) => s_blobDrifts[i].X))
        .Y((FunctionValue)((_, i, _) => s_blobDrifts[i].Y))
        .Scale((FunctionValue)((_, i, _) => s_blobDrifts[i].Scale))
        .Duration((FunctionValue)((_, i, _) => s_blobDrifts[i].Duration))
        .Ease(Eases.InOutSine)
        .Loop()
        .Alternate()
        .WhenReduced(reduced => reduced.Loop(false).Alternate(false));

    public static readonly AnimationBuilder FlyoutOpen = Recipes.PopoverIn(duration: 220);

    /// <summary>Every tooltip opening, a quicker <see cref="FlyoutOpen"/>.</summary>
    public static readonly AnimationBuilder TipOpen = Recipes.PopoverIn(duration: 160);

    /// <summary>Every tooltip closing, on a ghost since nothing can hold a tooltip open (BRIDGE-POPOVER).</summary>
    public static readonly AnimationBuilder TipClose = Recipes.PopoverOut(duration: 120);

    /// <summary>The settings panel sliding back into the button, the reverse of <see cref="FlyoutOpen"/>. Reduced: the panel closes at once (BRIDGE-POPOVER-026).</summary>
    public static readonly AnimationBuilder FlyoutClose = Recipes.PopoverOut(duration: 180);

    /// <summary>The settings panel's headings and rows rising in turn, a beat after the panel. Reduced: a fade, all at once.</summary>
    public static readonly AnimationBuilder SettingsRowArrive = Presto.Build()
        .Opacity(from: 0)
        .Y(from: 8)
        .Duration(320)
        .Ease(Eases.OutCubic)
        .Delay(Presto.Stagger(25, new() { Start = 60 }))
        .WhenReduced(reduced => reduced.Opacity(from: 0).Duration(200).Delay(0));

    /// <summary>The motion setting's preview dot, bouncing while the panel is open. Reduced: one fade in.</summary>
    public static readonly AnimationBuilder MotionPreview = Presto.Build()
        .Y(-7)
        .Duration(380)
        .Ease(Eases.OutQuad)
        .Loop()
        .Alternate()
        .WhenReduced(reduced => reduced.Opacity(from: 0).Duration(600).Loop(false).Alternate(false));

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

    /// <summary>
    /// The page swap, wide and compact: <see cref="Recipes.PageSharedAxis"/> along X, going back with the mirrored pair rather than the
    /// forward pair reversed, so the back motion eases out too.
    /// </summary>
    public static readonly PrestoPageTransition PageSwap = SharedAxisX();

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

    private static PrestoPageTransition SharedAxisX()
    {
        var swap = Recipes.PageSharedAxis(PageAxis.X);
        var mirrored = Recipes.PageSharedAxis(PageAxis.X, -Recipes.Tokens.Distance);
        swap.BackEnter = mirrored.Enter;
        swap.BackExit = mirrored.Exit;
        return swap;
    }

    /// <summary>Recolors <paramref name="property"/> from its current color to <paramref name="accent"/> through OKLCH, so coral to aqua never passes through gray.</summary>
    private static AnimationBuilder Accent(PropertyKey property, Color accent) =>
        Recipes.Recolor(accent, property, ColorSpace.Oklch, duration: 700, ease: Eases.InOutSine);
}
