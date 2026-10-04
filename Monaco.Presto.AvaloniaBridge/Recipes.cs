using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Monaco.Presto.AvaloniaBridge.Prebuilt;
using Monaco.Presto.Diagnostics;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// The recipe catalogue in one place: each builder recipe forwards to its family in <see cref="Prebuilt"/> and returns a new
/// <see cref="AnimationBuilder"/>, so chaining onto one never touches another caller's copy. The text reveals are builders
/// played on the pieces <see cref="TextReveal"/> splits a block into; <see cref="Typewriter"/>, <see cref="CountUp"/> and <see cref="RollDigits"/> need
/// the <see cref="TextBlock"/> itself, so they play at once and return the <see cref="Animation"/>.
/// </summary>
public static class Recipes
{
    internal const double FlipPerspective = 800;

    private static readonly ConditionalWeakTable<TextBlock, Animation> Rolls = new();

    /// <summary>The durations, eases and travel every recipe defaults to, read on each call: replacing it restyles later calls, not builders already made.</summary>
    public static MotionTokens Tokens { get; set; } = MotionTokens.Standard;

    /// <inheritdoc cref="Entrances.Fade"/>
    public static AnimationBuilder Fade(double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Entrances.Fade(duration, delay, ease, stagger);

    /// <inheritdoc cref="Entrances.FadeUp"/>
    public static AnimationBuilder FadeUp(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Entrances.FadeUp(distance, duration, delay, ease, stagger);

    /// <inheritdoc cref="Entrances.FadeDown"/>
    public static AnimationBuilder FadeDown(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Entrances.FadeDown(distance, duration, delay, ease, stagger);

    /// <inheritdoc cref="Entrances.FadeLeft"/>
    public static AnimationBuilder FadeLeft(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Entrances.FadeLeft(distance, duration, delay, ease, stagger);

    /// <inheritdoc cref="Entrances.FadeRight"/>
    public static AnimationBuilder FadeRight(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Entrances.FadeRight(distance, duration, delay, ease, stagger);

    /// <inheritdoc cref="Entrances.ScaleIn"/>
    public static AnimationBuilder ScaleIn(double? scale = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Entrances.ScaleIn(scale, duration, delay, ease, stagger);

    /// <inheritdoc cref="Entrances.PopIn"/>
    public static AnimationBuilder PopIn(double? scale = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Entrances.PopIn(scale, duration, delay, ease, stagger);

    /// <inheritdoc cref="Entrances.BlurIn"/>
    public static AnimationBuilder BlurIn(double? radius = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Entrances.BlurIn(radius, duration, delay, ease, stagger);

    /// <inheritdoc cref="Entrances.SlideIn"/>
    public static AnimationBuilder SlideIn(Edge edge, double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Entrances.SlideIn(edge, distance, duration, delay, ease, stagger);

    /// <inheritdoc cref="Entrances.FlipIn"/>
    public static AnimationBuilder FlipIn(double angle = 90, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Entrances.FlipIn(angle, duration, delay, ease, stagger);

    /// <inheritdoc cref="Entrances.Develop"/>
    public static AnimationBuilder Develop(double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Entrances.Develop(duration, delay, ease, stagger);

    /// <inheritdoc cref="Exits.FadeOut"/>
    public static AnimationBuilder FadeOut(double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exits.FadeOut(duration, delay, ease, stagger);

    /// <inheritdoc cref="Exits.FadeOutUp"/>
    public static AnimationBuilder FadeOutUp(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exits.FadeOutUp(distance, duration, delay, ease, stagger);

    /// <inheritdoc cref="Exits.FadeOutDown"/>
    public static AnimationBuilder FadeOutDown(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exits.FadeOutDown(distance, duration, delay, ease, stagger);

    /// <inheritdoc cref="Exits.FadeOutLeft"/>
    public static AnimationBuilder FadeOutLeft(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exits.FadeOutLeft(distance, duration, delay, ease, stagger);

    /// <inheritdoc cref="Exits.FadeOutRight"/>
    public static AnimationBuilder FadeOutRight(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exits.FadeOutRight(distance, duration, delay, ease, stagger);

    /// <inheritdoc cref="Exits.ScaleOut"/>
    public static AnimationBuilder ScaleOut(double? scale = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exits.ScaleOut(scale, duration, delay, ease, stagger);

    /// <inheritdoc cref="Exits.PopOut"/>
    public static AnimationBuilder PopOut(double? scale = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exits.PopOut(scale, duration, delay, ease, stagger);

    /// <inheritdoc cref="Exits.BlurOut"/>
    public static AnimationBuilder BlurOut(double? radius = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exits.BlurOut(radius, duration, delay, ease, stagger);

    /// <inheritdoc cref="Exits.SlideOut"/>
    public static AnimationBuilder SlideOut(Edge edge, double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exits.SlideOut(edge, distance, duration, delay, ease, stagger);

    /// <inheritdoc cref="Exits.FlipOut"/>
    public static AnimationBuilder FlipOut(double angle = -90, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Exits.FlipOut(angle, duration, delay, ease, stagger);

    /// <inheritdoc cref="Attention.Shake"/>
    public static AnimationBuilder Shake(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Attention.Shake(intensity, duration, delay, ease, stagger);

    /// <inheritdoc cref="Attention.Pulse"/>
    public static AnimationBuilder Pulse(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Attention.Pulse(intensity, duration, delay, ease, stagger);

    /// <inheritdoc cref="Attention.Wobble"/>
    public static AnimationBuilder Wobble(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Attention.Wobble(intensity, duration, delay, ease, stagger);

    /// <inheritdoc cref="Attention.Jelly"/>
    public static AnimationBuilder Jelly(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Attention.Jelly(intensity, duration, delay, ease, stagger);

    /// <inheritdoc cref="Attention.Heartbeat"/>
    public static AnimationBuilder Heartbeat(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Attention.Heartbeat(intensity, duration, delay, ease, stagger);

    /// <inheritdoc cref="Attention.Tada"/>
    public static AnimationBuilder Tada(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Attention.Tada(intensity, duration, delay, ease, stagger);

    /// <inheritdoc cref="Attention.Bounce"/>
    public static AnimationBuilder Bounce(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Attention.Bounce(intensity, duration, delay, ease, stagger);

    /// <inheritdoc cref="Attention.FlashTint"/>
    public static AnimationBuilder FlashTint(Color? color = null, double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Attention.FlashTint(color, intensity, duration, delay, ease, stagger);

    /// <inheritdoc cref="Loops.Float"/>
    public static AnimationBuilder Float(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Loops.Float(distance, duration, delay, ease, stagger);

    /// <inheritdoc cref="Loops.Spin"/>
    public static AnimationBuilder Spin(double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Loops.Spin(duration, delay, ease, stagger);

    /// <inheritdoc cref="Loops.Breathe"/>
    public static AnimationBuilder Breathe(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Loops.Breathe(intensity, duration, delay, ease, stagger);

    /// <inheritdoc cref="Loops.Blink"/>
    public static AnimationBuilder Blink(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Loops.Blink(intensity, duration, delay, ease, stagger);

    /// <inheritdoc cref="Interactions.Lift"/>
    public static AnimationBuilder Lift(double? distance = null, double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Interactions.Lift(distance, intensity, duration, delay, ease);

    /// <inheritdoc cref="Interactions.Grow"/>
    public static AnimationBuilder Grow(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Interactions.Grow(intensity, duration, delay, ease);

    /// <inheritdoc cref="Interactions.PressIn"/>
    public static AnimationBuilder PressIn(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Interactions.PressIn(intensity, duration, delay, ease);

    /// <inheritdoc cref="Interactions.ReorderLift"/>
    public static AnimationBuilder ReorderLift(double? scale = null, double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Interactions.ReorderLift(scale, intensity, duration, delay, ease);

    /// <inheritdoc cref="Interactions.Glow"/>
    public static AnimationBuilder Glow(Color? color = null, double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Interactions.Glow(color, intensity, duration, delay, ease);

    /// <inheritdoc cref="Interactions.Dim"/>
    public static AnimationBuilder Dim(double intensity = 1, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Interactions.Dim(intensity, duration, delay, ease);

    /// <inheritdoc cref="Interactions.Colorize"/>
    public static AnimationBuilder Colorize(double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Interactions.Colorize(duration, delay, ease);

    /// <inheritdoc cref="Layouts.Reflow"/>
    public static AnimationBuilder Reflow(double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Layouts.Reflow(duration, delay, ease);

    /// <inheritdoc cref="Loading.Shimmer"/>
    public static AnimationBuilder Shimmer(double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Loading.Shimmer(duration, delay, ease, stagger);

    /// <inheritdoc cref="Loading.Spinner"/>
    public static AnimationBuilder Spinner(double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Loading.Spinner(duration, delay, ease, stagger);

    /// <inheritdoc cref="Loading.Dots"/>
    public static AnimationBuilder Dots(double? distance = null, double? duration = null, double? delay = null, EaseValue? ease = null) =>
        Loading.Dots(distance, duration, delay, ease);

    /// <inheritdoc cref="Loading.Indeterminate"/>
    public static AnimationBuilder Indeterminate(double distance, double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Loading.Indeterminate(distance, duration, delay, ease, stagger);

    /// <inheritdoc cref="Sprites.PlayFrames"/>
    public static AnimationBuilder PlayFrames(double fps = 24, bool loop = true, bool reverse = false, double? delay = null) =>
        Sprites.PlayFrames(fps, loop, reverse, delay);

    /// <inheritdoc cref="Pages.PageFade"/>
    public static PrestoPageTransition PageFade(double? duration = null) => Pages.PageFade(duration);

    /// <inheritdoc cref="Pages.PageFadeThrough"/>
    public static PrestoPageTransition PageFadeThrough(double? duration = null) => Pages.PageFadeThrough(duration);

    /// <inheritdoc cref="Pages.PageSharedAxis"/>
    public static PrestoPageTransition PageSharedAxis(PageAxis axis, double? distance = null) => Pages.PageSharedAxis(axis, distance);

    /// <inheritdoc cref="Pages.PageSlide"/>
    public static PrestoPageTransition PageSlide(Edge edge) => Pages.PageSlide(edge);

    /// <inheritdoc cref="Pages.PagePush"/>
    public static PrestoPageTransition PagePush(Edge edge) => Pages.PagePush(edge);

    /// <inheritdoc cref="TextReveals.LineRise"/>
    public static AnimationBuilder LineRise(double? duration = null, double? delay = null, EaseValue? ease = null, double? lineStep = null, double? pieceStep = null) =>
        TextReveals.LineRise(duration, delay, ease, lineStep, pieceStep);

    /// <inheritdoc cref="TextReveals.LineMask"/>
    public static AnimationBuilder LineMask(double? duration = null, double? delay = null, EaseValue? ease = null, double? lineStep = null, double? pieceStep = null) =>
        TextReveals.LineMask(duration, delay, ease, lineStep, pieceStep);

    /// <inheritdoc cref="TextReveals.LineWipe"/>
    public static AnimationBuilder LineWipe(double? duration = null, double? delay = null, EaseValue? ease = null, double? lineStep = null, double? pieceStep = null) =>
        TextReveals.LineWipe(duration, delay, ease, lineStep, pieceStep);

    /// <inheritdoc cref="TextReveals.WordFade"/>
    public static AnimationBuilder WordFade(double? duration = null, double? delay = null, EaseValue? ease = null, double? lineStep = null, double? pieceStep = null) =>
        TextReveals.WordFade(duration, delay, ease, lineStep, pieceStep);

    /// <inheritdoc cref="TextReveals.WordBlur"/>
    public static AnimationBuilder WordBlur(double? duration = null, double? delay = null, EaseValue? ease = null, double? lineStep = null, double? pieceStep = null) =>
        TextReveals.WordBlur(duration, delay, ease, lineStep, pieceStep);

    /// <inheritdoc cref="TextReveals.CharCascade"/>
    public static AnimationBuilder CharCascade(double? duration = null, double? delay = null, EaseValue? ease = null, double? lineStep = null, double? pieceStep = null) =>
        TextReveals.CharCascade(duration, delay, ease, lineStep, pieceStep);

    /// <inheritdoc cref="Particles.Confetti"/>
    public static ParticleEffect Confetti(Control anchor, int count = 120, IReadOnlyList<Color>? colors = null, Edge? from = null, AnimationBuilder? reduced = null) =>
        Particles.Confetti(anchor, count, colors, from, reduced);

    /// <inheritdoc cref="Particles.Sparkles"/>
    public static ParticleEffect Sparkles(Control anchor, double rate = 30, Color? color = null, AnimationBuilder? reduced = null) =>
        Particles.Sparkles(anchor, rate, color, reduced);

    /// <inheritdoc cref="Particles.Burst"/>
    public static ParticleEffect Burst(Control anchor, int count = 24, Color? color = null, AnimationBuilder? reduced = null) =>
        Particles.Burst(anchor, count, color, reduced);

    /// <summary>
    /// Types out <paramref name="textBlock"/>'s text: it is split into characters, each starting <paramref name="charDelay"/> ms
    /// (default <see cref="MotionTokens.StaggerStep"/>) after the one before and fading in over <paramref name="duration"/>
    /// (default <paramref name="charDelay"/>), and put back together once the last has appeared. Reduced, the whole block
    /// fades in as <see cref="Fade"/> does.
    /// </summary>
    /// <remarks>
    /// The block has to be laid out, since the split follows its wrapped lines. <see cref="CharCascade"/> is the trigger form,
    /// splitting when the block is first laid out.
    /// </remarks>
    public static Animation Typewriter(TextBlock textBlock, double? charDelay = null, double? duration = null, double? delay = null, EaseValue? ease = null)
    {
        using var origin = PrestoDiagnostics.Origin("Recipe");
        if (Engine.Current.Defaults.ReducedMotion) return Fade(delay: delay).Animate(textBlock);
        var step = charDelay ?? Tokens.StaggerStep;
        var split = PrestoAvalonia.SplitText(textBlock, new() { Words = null, Chars = new() });
        return Presto.Build()
            .Opacity(from: 0)
            .Duration(duration ?? step)
            .Delay(Presto.Stagger(step, new() { Start = delay }))
            .Ease(ease ?? Tokens.Ease)
            .OnComplete(_ => split.Revert())
            .Animate(split.Chars);
    }

    /// <summary>
    /// Counts <paramref name="textBlock"/>'s text up (or down) from the number it shows, or 0, to <paramref name="to"/>, each value
    /// formatted with <paramref name="format"/> (default <c>"N0"</c>) in the current culture, over <see cref="MotionTokens.Long"/>
    /// by default. Reduced, the text is set to the end value on the first frame.
    /// </summary>
    public static Animation CountUp(TextBlock textBlock, double to, string? format = null, double? duration = null, double? delay = null, EaseValue? ease = null)
    {
        format ??= "N0";
        Func<double, string> text = value => value.ToString(format, CultureInfo.CurrentCulture);
        var from = double.TryParse(textBlock.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var shown) ? shown : 0;
        var tokens = Tokens;
        using var origin = PrestoDiagnostics.Origin("Recipe");
        return Start(tokens, duration ?? tokens.Long, delay, ease ?? tokens.Ease, stagger: false)
            .Property("Text", to: to, from: from, textModifier: text)
            .WhenReduced(r => r.Property("Text", to: to, textModifier: text).Duration(0))
            .Animate(textBlock);
    }

    /// <summary>
    /// Rolls <paramref name="textBlock"/>'s number to <paramref name="to"/> like an odometer, rolling only the changed characters
    /// and staggering them right to left. Reduced, the end text is set at once.
    /// </summary>
    public static Animation RollDigits(TextBlock textBlock, double to, string? format = null, double? duration = null, double? delay = null, EaseValue? ease = null)
    {
        if (Rolls.TryGetValue(textBlock, out var running)) running.Complete();
        if (Engine.Current.Defaults.ReducedMotion) return CountUp(textBlock, to, format, delay: delay);

        format ??= "N0";
        var from = double.TryParse(textBlock.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var shown) ? shown : 0;
        var oldText = from.ToString(format, CultureInfo.CurrentCulture);
        var newText = to.ToString(format, CultureInfo.CurrentCulture);
        var line = textBlock.TextLayout.TextLines[0];
        var travel = to >= from ? line.Height : -line.Height;
        var tokens = Tokens;

        var width = Math.Max(oldText.Length, newText.Length);
        var columns = new Control[width];
        var glyphs = new List<Control>();
        var incoming = new List<bool>();
        var delays = new List<double>();
        var start = delay ?? 0;
        for (var c = width - 1; c >= 0; c--)
        {
            var o = c - (width - oldText.Length);
            var n = c - (width - newText.Length);
            char? leaving = o >= 0 ? oldText[o] : null;
            char? entering = n >= 0 ? newText[n] : null;
            if (leaving == entering)
            {
                columns[c] = new TextBlock { Text = entering.ToString() };
                continue;
            }
            var column = new Panel { ClipToBounds = true };
            foreach (var (glyph, enters) in new[] { (leaving, false), (entering, true) })
            {
                if (glyph == null) continue;
                var piece = new TextBlock { Text = glyph.ToString(), HorizontalAlignment = HorizontalAlignment.Center };
                column.Children.Add(piece);
                glyphs.Add(piece);
                incoming.Add(enters);
                delays.Add(start);
            }
            start += tokens.StaggerStep;
            columns[c] = column;
        }

        // An inline control sits on the baseline, below half the line gap and above the descent; the margins give back the plain text's line box.
        var gap = line.TextRuns.Count > 0 && line.TextRuns[0] is ShapedTextRun run ? line.Baseline + run.TextMetrics.Ascent : 0;
        var row = new StackPanel { Orientation = Orientation.Horizontal, UseLayoutRounding = false, Margin = new(0, -gap, 0, line.Baseline - line.Height) };
        row.Children.AddRange(columns);
        textBlock.Text = null;
        textBlock.Inlines = [new InlineUIContainer(row)];

        using var origin = PrestoDiagnostics.Origin("Recipe");
        var roll = Presto.Build()
            .Duration(duration ?? tokens.Medium)
            .Ease(ease ?? tokens.Ease)
            .Delay((FunctionValue)((_, i, _) => delays[i]))
            .Property("Y", from: (FunctionValue)((_, i, _) => incoming[i] ? travel : 0), to: (FunctionValue)((_, i, _) => incoming[i] ? 0 : -travel))
            .Property("Opacity", from: (FunctionValue)((_, i, _) => incoming[i] ? 0 : 1), to: (FunctionValue)((_, i, _) => incoming[i] ? 1 : 0))
            .OnComplete(_ =>
            {
                Rolls.Remove(textBlock);
                textBlock.Inlines = null;
                textBlock.Text = newText;
            })
            .Animate(glyphs);
        Rolls.AddOrUpdate(textBlock, roll);
        return roll;
    }

    /// <summary>A builder with a recipe's timing:<paramref name="stagger"/> steps each element of a batch by <see cref="MotionTokens.StaggerStep"/> after <paramref name="delay"/>.</summary>
    internal static AnimationBuilder Start(MotionTokens tokens, double duration, double? delay, EaseValue ease, bool stagger)
    {
        var builder = Presto.Build().Duration(duration).Ease(ease);
        if (stagger) return builder.Delay(Presto.Stagger(tokens.StaggerStep, new() { Start = delay }));
        return delay is { } ms ? builder.Delay(ms) : builder;
    }

    /// <summary>The transform shorthand and signed offset that put an element <paramref name="distance"/> DIPs towards <paramref name="edge"/>.</summary>
    internal static (string Property, double Offset) Offset(Edge edge, double distance) => edge switch
    {
        Edge.Left => ("X", -distance),
        Edge.Right => ("X", distance),
        Edge.Top => ("Y", -distance),
        _ => ("Y", distance),
    };
}
