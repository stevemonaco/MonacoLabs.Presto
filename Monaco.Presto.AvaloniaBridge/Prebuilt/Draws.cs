using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge.Prebuilt;

/// <summary>
/// Stroke recipes for a <see cref="Avalonia.Controls.Shapes.Shape"/>, played through its <c>"Draw"</c> property, and the
/// check and cross marks they are most often played on. Each lands at once while <see cref="PrestoDefaults.ReducedMotion"/> is set.
/// </summary>
/// <remarks>
/// Arguments left null come from <see cref="Recipes.Tokens"/>, read when the recipe is called: the duration is
/// <see cref="MotionTokens.Medium"/> and the ease <see cref="MotionTokens.Ease"/>. <c>stagger</c> delays each element of a
/// batch by <see cref="MotionTokens.StaggerStep"/> more than the one before, starting at <c>delay</c>. UI thread only.
/// </remarks>
public static class Draws
{
    /// <summary>A check mark in a 20 × 20 box, a new geometry on each read.</summary>
    public static Geometry Check => Geometry.Parse("M3,10.5 L8,15 L17,5");

    /// <summary>A cross of two strokes in a 20 × 20 box, a new geometry on each read.</summary>
    public static Geometry Cross => Geometry.Parse("M5,5 L15,15 M15,5 L5,15");

    /// <summary>Draws the stroke from its start to its end.</summary>
    public static AnimationBuilder DrawIn(double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Start(duration, delay, ease, stagger).Draw(from: "0 0", to: "0 1").WhenReduced(reduced => reduced.Draw("0 1").Duration(0));

    /// <summary>Erases the stroke from its start, so the end goes last, as a pen lifting off.</summary>
    public static AnimationBuilder DrawOut(double? duration = null, double? delay = null, EaseValue? ease = null, bool stagger = false) =>
        Start(duration, delay, ease, stagger).Draw("1 1").WhenReduced(reduced => reduced.Draw("1 1").Duration(0));

    private static AnimationBuilder Start(double? duration, double? delay, EaseValue? ease, bool stagger)
    {
        var tokens = Recipes.Tokens;
        return Recipes.Start(tokens, duration ?? tokens.Medium, delay, ease ?? tokens.Ease, stagger);
    }
}
