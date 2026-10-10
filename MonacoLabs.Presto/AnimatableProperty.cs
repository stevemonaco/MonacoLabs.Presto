namespace MonacoLabs.Presto;

/// <summary>
/// Settings for one property of an <see cref="Animatable"/>. A bare number is its duration (<c>["X"] = 20</c>), the object
/// form adds the unit and the rest (<c>["Rotate"] = new() { Unit = "rad", Duration = 0 }</c>).
/// </summary>
public sealed record AnimatableProperty
{
    /// <summary>The unit the property is animated in, converted from whatever unit the target currently holds.</summary>
    public string? Unit { get; init; }

    /// <summary>How long this property takes to reach a value set on it; 0 follows the value immediately.</summary>
    public TweenValue? Duration { get; init; }

    /// <summary>The ease this property follows to each new value.</summary>
    public EaseValue? Ease { get; init; }

    /// <summary>Post-processes this property's value, including the one <see cref="Animatable.Get"/> reads back.</summary>
    public Func<double, double>? Modifier { get; init; }

    /// <summary>How this property composes with other animations of it; an animatable otherwise replaces them.</summary>
    public Composition? Composition { get; init; }

    /// <inheritdoc cref="AnimatableParams.PreserveVelocity"/>
    public bool? PreserveVelocity { get; init; }

    /// <summary>A bare number is the property's <see cref="Duration"/>.</summary>
    public static implicit operator AnimatableProperty(double duration) => new() { Duration = duration };

    /// <inheritdoc cref="op_Implicit(double)"/>
    public static implicit operator AnimatableProperty(int duration) => new() { Duration = duration };
}
