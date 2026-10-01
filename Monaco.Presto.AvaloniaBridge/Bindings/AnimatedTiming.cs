namespace Monaco.Presto.AvaloniaBridge.Bindings;

/// <summary>Turns <see cref="AnimatedBindingParams"/> into the ease, duration and momentum of a follower's animatable property.</summary>
internal static class AnimatedTiming
{
    private const double BounceDuration = 500;

    /// <summary>The timing in force, from <see cref="AnimatedBindingParams.WhenReduced"/> when <paramref name="reduced"/> is set.</summary>
    /// <exception cref="ArgumentException">Two timing sources are given.</exception>
    /// <exception cref="FormatException"><see cref="AnimatedBindingParams.Spring"/> names no spring.</exception>
    /// <exception cref="NotSupportedException">A builder's duration is not a number.</exception>
    public static AnimatableProperty Resolve(AnimatedBindingParams p, bool reduced)
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
        if (p.Spring is { } spring) return Of(Springs.Parse(spring), null);
        if (p.Bounce is { } bounce) return Of(new Spring(bounce: bounce, duration: p.Duration ?? BounceDuration), null);
        if (p.Duration != null || p.Ease != null) return Of(p.Ease, p.Duration);
        if (p.Token is { } token) return Of(token.Ease, token.Medium);
        return Of(Springs.Gentle, null);
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
