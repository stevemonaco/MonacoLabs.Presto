namespace MonacoLabs.Presto;

/// <summary>A value over a particle's life: <see cref="From"/> at birth to <see cref="To"/> at death, shaped by <see cref="Ease"/>.</summary>
public sealed record ParticleCurve
{
    private Ease _ease = Eases.None;

    /// <summary>Creates a curve; the ease is resolved once here, and a <see cref="Spring"/>'s settling duration is ignored.</summary>
    public ParticleCurve(double from, double to, EaseValue ease = default)
    {
        From = from;
        To = to;
        Ease = ease;
    }

    /// <summary>The value at age 0.</summary>
    public double From { get; init; }

    /// <summary>The value at age 1.</summary>
    public double To { get; init; }

    /// <summary>The ease applied to the normalized age; linear when unset.</summary>
    public EaseValue Ease
    {
        get;
        init
        {
            field = value;
            _ease = value.Resolve(this, 0, 1, out _);
        }
    }

    /// <summary>The value at <paramref name="age"/>, from 0 at birth to 1 at death.</summary>
    public double At(double age) => From + (To - From) * _ease(age);
}
