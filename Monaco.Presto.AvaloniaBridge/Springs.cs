using System.Globalization;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>Named springs for value-driven animation. Each read returns a new <see cref="Spring"/>, because a spring carries completion state.</summary>
public static class Springs
{
    /// <summary>Critically damped over 500ms: no overshoot. The default of <see cref="AnimatedBindingParams"/>.</summary>
    public static Spring Gentle => new(bounce: 0, duration: 500);

    /// <summary>A slight overshoot over 300ms.</summary>
    public static Spring Snappy => new(bounce: .15, duration: 300);

    /// <summary>A visible bounce over 500ms.</summary>
    public static Spring Bouncy => new(bounce: .35, duration: 500);

    /// <summary>Critically damped over 250ms.</summary>
    public static Spring Stiff => new(bounce: 0, duration: 250);

    /// <summary>A preset by name, ignoring case, or a spring from <c>"bounce,duration"</c>.</summary>
    /// <exception cref="FormatException"><paramref name="text"/> is neither.</exception>
    internal static Spring Parse(string text)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "gentle": return Gentle;
            case "snappy": return Snappy;
            case "bouncy": return Bouncy;
            case "stiff": return Stiff;
        }
        var parts = text.Split(',');
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var bounce)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var duration))
        {
            return new Spring(bounce: bounce, duration: duration);
        }
        throw new FormatException($"'{text}' is not a spring. Name a preset (Gentle, Snappy, Bouncy, Stiff) or give \"bounce,duration\", such as \"0.2,400\".");
    }
}
