namespace Monaco.Presto;

/// <summary>Number of additional iterations a timer plays; <c>true</c>, a negative value or <see cref="Infinite"/> loops forever.</summary>
public readonly struct LoopCount
{
    private LoopCount(double value) => Value = value;

    public static LoopCount Infinite => new(double.PositiveInfinity);

    public double Value { get; }

    public bool IsInfinite => double.IsPositiveInfinity(Value) || Value < 0;

    public static implicit operator LoopCount(int count) => new(count);

    public static implicit operator LoopCount(bool infinite) => infinite ? Infinite : new(0);
}
