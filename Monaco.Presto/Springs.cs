namespace Monaco.Presto;

/// <summary>Named springs for value-driven animation. Each read returns a new <see cref="Spring"/>, because a spring carries completion state.</summary>
public static class Springs
{
    /// <summary>Critically damped over 500ms: no overshoot.</summary>
    public static Spring Gentle => new(bounce: 0, duration: 500);

    /// <summary>A slight overshoot over 300ms.</summary>
    public static Spring Snappy => new(bounce: .15, duration: 300);

    /// <summary>A visible bounce over 500ms.</summary>
    public static Spring Bouncy => new(bounce: .35, duration: 500);

    /// <summary>Critically damped over 250ms.</summary>
    public static Spring Stiff => new(bounce: 0, duration: 250);
}
