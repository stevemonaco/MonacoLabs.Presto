namespace MonacoLabs.Presto;

/// <summary>
/// A to-value that flies from a launch velocity under constant acceleration and friction for the tween's duration. Its
/// end value and ease are resolved once the tween's start value is known.
/// </summary>
public sealed record Projectile
{
    /// <summary>Creates a projectile launched at <paramref name="velocity"/> units per engine-time unit.</summary>
    public Projectile(double velocity) => Velocity = velocity;

    /// <summary>The launch velocity along this property's axis, in units per engine-time unit.</summary>
    public double Velocity { get; init; }

    /// <summary>The constant acceleration, such as gravity, in units per engine-time unit squared.</summary>
    public double Acceleration { get; init; }

    /// <summary>The fraction of velocity lost per millisecond, from 0 up to but excluding 1.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative, or 1 or more.</exception>
    public double Friction
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, 1);
            field = value;
        }
    }

    /// <summary>The flight time in engine-time units; the tween's own duration when null.</summary>
    public double? Duration { get; init; }

    /// <summary>
    /// Splits a 2D launch into per-axis projectiles, with gravity on Y only and positive Y pointing down; the angle runs
    /// clockwise from the positive X axis, so -90 throws straight up.
    /// </summary>
    public static (Projectile X, Projectile Y) Launch(double speed, double angleDegrees, double gravity, double friction = 0) =>
    (
        new(speed * double.CosPi(angleDegrees / 180)) { Friction = friction },
        new(speed * double.SinPi(angleDegrees / 180)) { Acceleration = gravity, Friction = friction }
    );
}
