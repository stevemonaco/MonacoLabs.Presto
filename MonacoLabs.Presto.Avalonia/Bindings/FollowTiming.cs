using System.Globalization;
using MonacoLabs.Presto.Core;

namespace MonacoLabs.Presto.Bindings;

/// <summary>Turns <see cref="FollowParams"/> into the ease, duration and momentum of a follower's animatable property.</summary>
internal static class FollowTiming
{
    private const double BounceDuration = 500;

    /// <summary>The timing in force, from <see cref="FollowParams.WhenReduced"/> when <paramref name="reduced"/> is set.</summary>
    /// <exception cref="ArgumentException">Two timing sources are given.</exception>
    /// <exception cref="FormatException"><see cref="FollowParams.Spring"/> names no spring.</exception>
    /// <exception cref="NotSupportedException">A builder's duration is not a number.</exception>
    public static AnimatableProperty Resolve(FollowParams p, bool reduced)
    {
        var conflict =
            p.Builder != null && (p.Duration != null || p.Ease != null || p.Spring != null || p.Bounce != null) ? "Builder gives the duration and ease, so Duration, Ease, Spring and Bounce cannot be set with it." :
            p.Spring != null && (p.Bounce != null || p.Duration != null || p.Ease != null) ? "Spring gives the whole spring, so Bounce, Duration and Ease cannot be set with it." :
            p.Bounce != null && p.Ease != null ? "Bounce builds a spring, so Ease cannot be set with it." :
            p.Ease?.Raw is Spring && p.Duration != null ? "A spring ease sets its own duration; give Bounce and Duration, or Spring=\"bounce,duration\", instead." :
            null;
        if (conflict != null) throw new ArgumentException(conflict, nameof(p));

        if (reduced) return From(p.WhenReduced!);
        if (p.Builder is { } builder) return From(builder);
        if (p.Spring is { } spring) return Of(ParseSpring(spring), null);
        if (p.Bounce is { } bounce) return Of(new Spring(bounce: bounce, duration: p.Duration ?? BounceDuration * Globals.TimeScale), null);
        if (p.Duration != null || p.Ease != null) return Of(p.Ease, p.Duration);
        if (p.Token is { } token) return Of(token.Ease, token.Medium);
        return Of(Springs.Gentle, null);
    }

    /// <summary>A <see cref="Springs"/> preset name, or <c>"bounce,duration"</c>, as <see cref="FollowParams.Spring"/> takes it.</summary>
    /// <exception cref="FormatException"><paramref name="text"/> names no spring.</exception>
    public static Spring ParseSpring(string text)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "gentle": return Springs.Gentle;
            case "snappy": return Springs.Snappy;
            case "bouncy": return Springs.Bouncy;
            case "stiff": return Springs.Stiff;
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

    private static AnimatableProperty From(AnimationBuilder builder)
    {
        var p = builder.ToParams();
        return Of(p.Ease, p.Duration switch
        {
            null => null,
            { AsDouble: { } duration } => duration,
            _ => throw new NotSupportedException("A value-driven animation moves one value, so the builder's Duration must be a number."),
        });
    }

    private static AnimatableProperty Of(EaseValue? ease, double? duration) =>
        new() { Ease = ease, Duration = duration, PreserveVelocity = ease?.Raw is Spring };
}
