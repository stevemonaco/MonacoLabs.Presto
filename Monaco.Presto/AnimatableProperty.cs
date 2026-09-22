namespace Monaco.Presto;

/// <summary>
/// Settings for one property of an <see cref="Animatable"/>. A bare number is its duration (<c>["X"] = 20</c>), the object
/// form adds the unit and the rest (<c>["Rotate"] = new() { Unit = "rad", Duration = 0 }</c>).
/// </summary>
public sealed record AnimatableProperty
{
    /// <summary>The unit the property is animated in, converted from whatever unit the target currently holds.</summary>
    public string? Unit { get; init; }

    public TweenValue? Duration { get; init; }
    public EaseValue? Ease { get; init; }
    public Func<double, double>? Modifier { get; init; }
    public Composition? Composition { get; init; }

    public static implicit operator AnimatableProperty(double duration) => new() { Duration = duration };
    public static implicit operator AnimatableProperty(int duration) => new() { Duration = duration };
}
