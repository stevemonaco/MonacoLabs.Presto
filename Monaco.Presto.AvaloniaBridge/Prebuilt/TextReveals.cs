using Avalonia.Controls;
using Monaco.Presto.AvaloniaBridge.Text;

namespace Monaco.Presto.AvaloniaBridge.Prebuilt;

/// <summary>
/// Text reveal recipes, meant for the split pieces a trigger plays on when <see cref="TextReveal.PiecesProperty"/> is set
/// on a <see cref="TextBlock"/>. Each states only where a piece starts, and each becomes a <see cref="MotionTokens.Short"/>
/// fade of the whole, unsplit block while <see cref="PrestoDefaults.ReducedMotion"/> is set.
/// </summary>
/// <remarks>
/// Arguments left null come from <see cref="Recipes.Tokens"/>, read when the recipe is called. Pieces start after
/// <c>delay</c> as <see cref="TextSplitter.StaggerByLine"/> orders them: <c>lineStep</c> ms per line (default three times
/// <see cref="MotionTokens.StaggerStep"/>) plus <c>pieceStep</c> ms per piece before it on its line, which line pieces never have.
/// </remarks>
public static class TextReveals
{
    private static readonly FunctionValue OneLineDown = (target, _, _) => target is Control piece ? TextSplitter.LineHeightOf(piece) : 0;

    /// <summary>Lines fade in while rising one line height, over <see cref="MotionTokens.Long"/>. Needs <see cref="TextPieces.Lines"/>.</summary>
    public static AnimationBuilder LineRise(double? duration = null, double? delay = null, EaseValue? ease = null, double? lineStep = null, double? pieceStep = null)
    {
        var tokens = Recipes.Tokens;
        return Reveal(tokens, duration ?? tokens.Long, delay, ease ?? tokens.Ease, lineStep, pieceStep ?? 0).Opacity(from: 0).Y(from: OneLineDown);
    }

    /// <summary>
    /// Lines rise one line height from behind their own clips, without fading, over <see cref="MotionTokens.Long"/>. Needs
    /// <see cref="TextPieces.Lines"/> and <see cref="TextReveal.MaskProperty"/> set.
    /// </summary>
    public static AnimationBuilder LineMask(double? duration = null, double? delay = null, EaseValue? ease = null, double? lineStep = null, double? pieceStep = null)
    {
        var tokens = Recipes.Tokens;
        return Reveal(tokens, duration ?? tokens.Long, delay, ease ?? tokens.Ease, lineStep, pieceStep ?? 0).Y(from: OneLineDown);
    }

    /// <summary>
    /// Each line is wiped into view left to right as its <c>ClipInset</c> right inset closes from 100%, over
    /// <see cref="MotionTokens.Long"/>, ending unclipped. Needs <see cref="TextPieces.Lines"/> without a mask.
    /// </summary>
    public static AnimationBuilder LineWipe(double? duration = null, double? delay = null, EaseValue? ease = null, double? lineStep = null, double? pieceStep = null)
    {
        var tokens = Recipes.Tokens;
        return Reveal(tokens, duration ?? tokens.Long, delay, ease ?? tokens.Ease, lineStep, pieceStep ?? 0).ClipInset(from: "0 0 100% 0", to: "0 0 0% 0");
    }

    /// <summary>
    /// Words fade in while rising <see cref="MotionTokens.Distance"/>, <paramref name="pieceStep"/> ms (default half
    /// <see cref="MotionTokens.StaggerStep"/>) apart on a line. Needs <see cref="TextPieces.Words"/>.
    /// </summary>
    public static AnimationBuilder WordFade(double? duration = null, double? delay = null, EaseValue? ease = null, double? lineStep = null, double? pieceStep = null)
    {
        var tokens = Recipes.Tokens;
        return Reveal(tokens, duration ?? tokens.Medium, delay, ease ?? tokens.Ease, lineStep, pieceStep ?? tokens.StaggerStep / 2)
            .Opacity(from: 0).Y(from: tokens.Distance);
    }

    /// <summary>
    /// Words fade in while sharpening from <see cref="MotionTokens.BlurRadius"/>, as <see cref="Entrances.BlurIn"/> does, spaced
    /// as <see cref="WordFade"/> is. Needs <see cref="TextPieces.Words"/>. UI thread only: the compositor cannot blur.
    /// </summary>
    public static AnimationBuilder WordBlur(double? duration = null, double? delay = null, EaseValue? ease = null, double? lineStep = null, double? pieceStep = null)
    {
        var tokens = Recipes.Tokens;
        return Reveal(tokens, duration ?? tokens.Medium, delay, ease ?? tokens.Ease, lineStep, pieceStep ?? tokens.StaggerStep / 2)
            .Opacity(from: 0).Blur(from: tokens.BlurRadius);
    }

    /// <summary>
    /// Characters fade in while dropping <see cref="MotionTokens.Distance"/> with <see cref="MotionTokens.EmphasizedEase"/>,
    /// <paramref name="pieceStep"/> ms (default a fifth of <see cref="MotionTokens.StaggerStep"/>) apart on a line. Needs
    /// <see cref="TextPieces.Chars"/>.
    /// </summary>
    public static AnimationBuilder CharCascade(double? duration = null, double? delay = null, EaseValue? ease = null, double? lineStep = null, double? pieceStep = null)
    {
        var tokens = Recipes.Tokens;
        return Reveal(tokens, duration ?? tokens.Medium, delay, ease ?? tokens.EmphasizedEase, lineStep, pieceStep ?? tokens.StaggerStep / 5)
            .Opacity(from: 0).Y(from: -tokens.Distance);
    }

    private static AnimationBuilder Reveal(MotionTokens tokens, double duration, double? delay, EaseValue ease, double? lineStep, double pieceStep) =>
        Recipes.Start(tokens, duration, delay: null, ease, stagger: false)
            .Delay(TextSplitter.StaggerByLine(lineStep ?? tokens.StaggerStep * 3, pieceStep, delay))
            .WhenReduced(reduced => reduced.Opacity(from: 0).Duration(tokens.Short));
}
