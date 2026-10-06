using Avalonia.Controls;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// <see cref="Motion"/> states added from code. Each sits after the element's declared states, in the order added, and
/// holds until its handle is disposed; its properties then return like those of any state that stops matching.
/// </summary>
public static class MotionExtensions
{
    /// <summary>Plays <paramref name="motion"/> while the pointer is over this control (<c>:pointerover</c>).</summary>
    public static IDisposable WhileHover(this Control control, AnimationBuilder motion) => control.While(":pointerover", motion);

    /// <inheritdoc cref="WhileHover(Control, AnimationBuilder)"/>
    public static IDisposable WhileHover(this Control control, Action<AnimationBuilder> motion) => control.While(":pointerover", motion);

    /// <summary>Plays <paramref name="motion"/> while this control is pressed (the synthesized <c>:pressed</c>).</summary>
    public static IDisposable WhilePressed(this Control control, AnimationBuilder motion) => control.While(":pressed", motion);

    /// <inheritdoc cref="WhilePressed(Control, AnimationBuilder)"/>
    public static IDisposable WhilePressed(this Control control, Action<AnimationBuilder> motion) => control.While(":pressed", motion);

    /// <summary>Plays <paramref name="motion"/> while this control has keyboard focus (<c>:focus-visible</c>).</summary>
    public static IDisposable WhileFocused(this Control control, AnimationBuilder motion) => control.While(":focus-visible", motion);

    /// <inheritdoc cref="WhileFocused(Control, AnimationBuilder)"/>
    public static IDisposable WhileFocused(this Control control, Action<AnimationBuilder> motion) => control.While(":focus-visible", motion);

    /// <summary>Plays <paramref name="motion"/> while <paramref name="when"/> holds, as a <see cref="MotionState.When"/> does.</summary>
    public static IDisposable While(this Control control, string when, AnimationBuilder motion) =>
        Motion.Add(control, new MotionState { When = when, Motion = motion });

    /// <inheritdoc cref="While(Control, string, AnimationBuilder)"/>
    public static IDisposable While(this Control control, string when, Action<AnimationBuilder> motion) =>
        control.While(when, Presto.Build(motion));
}
