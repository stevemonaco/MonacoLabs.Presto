namespace MonacoLabs.Presto.Values;

/// <summary>What a <see cref="DecomposedValue"/> holds, and therefore how a tween interpolates and recomposes it (upstream <c>valueTypes</c>).</summary>
public enum ValueKind
{
    /// <summary>A bare number, written to the target as a number wherever the property allows one.</summary>
    Number,

    /// <summary>A number with a unit suffix, recomposed as <c>"50%"</c> or <c>"90deg"</c>.</summary>
    Unit,

    /// <summary>Four RGBA channels, interpolated separately and recomposed as an <c>rgba()</c> string.</summary>
    /// <remarks>
    /// RGB interpolates pseudo-linearly (squared) with premultiplied alpha, as CSS Color 4 does, so a fade from transparent
    /// takes on the opaque end's hue. This deviates from anime.js, which blends straight alpha.
    /// </remarks>
    Color,

    /// <summary>A string with several embedded numbers, each interpolated separately and joined back with the literal text between them.</summary>
    Complex,
}
