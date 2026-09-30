namespace Monaco.Presto.AvaloniaBridge;

/// <summary>A motion system's named durations, eases and travel, which every recipe reads through <see cref="Recipes.Tokens"/> instead of literals.</summary>
/// <param name="Short">Milliseconds for exits and reduced-motion fades.</param>
/// <param name="Medium">Milliseconds for entrances.</param>
/// <param name="Long">Milliseconds for large or deliberate moves.</param>
/// <param name="Ease">The ease most recipes use.</param>
/// <param name="EmphasizedEase">The ease of recipes that should draw the eye, such as a pop or a flip.</param>
/// <param name="Distance">How far, in DIPs, a fade travels.</param>
/// <param name="Scale">The scale a scaling entrance starts at and a scaling exit ends at.</param>
/// <param name="BlurRadius">The blur radius a blur entrance starts at and a blur exit ends at.</param>
/// <param name="StaggerStep">Milliseconds between the elements of a staggered batch.</param>
public sealed record MotionTokens(
    double Short,
    double Medium,
    double Long,
    EaseValue Ease,
    EaseValue EmphasizedEase,
    double Distance,
    double Scale,
    double BlurRadius,
    double StaggerStep)
{
    /// <summary>Brisk, understated motion; the default <see cref="Recipes.Tokens"/>.</summary>
    public static MotionTokens Standard { get; } = new(200, 450, 650, Eases.OutCubic, Eases.OutBack(), 24, 0.9, 12, 50);

    /// <summary>Slower, farther motion with overshoot.</summary>
    public static MotionTokens Expressive { get; } = new(450, 900, 1200, Eases.OutBack(2.2), Eases.OutElastic(1, .5), 64, 0.6, 24, 90);

    /// <summary>Short linear moves with no travel, for an app that tones motion down without <see cref="PrestoDefaults.ReducedMotion"/>.</summary>
    public static MotionTokens Reduced { get; } = new(120, 150, 200, Eases.None, Eases.None, 0, 1, 0, 0);
}
