namespace MonacoLabs.Presto;

/// <summary>Number of additional iterations a timer plays; <c>true</c>, a negative value or <see cref="Infinite"/> loops forever.</summary>
public readonly struct LoopCount
{
    private LoopCount(double value) => Value = value;

    /// <summary>Repeats forever, as upstream <c>loop: true</c> does.</summary>
    public static LoopCount Infinite => new(double.PositiveInfinity);

    /// <summary>The raw count, <see cref="double.PositiveInfinity"/> when looping forever.</summary>
    public double Value { get; }

    /// <summary>Whether the timer repeats forever.</summary>
    public bool IsInfinite => double.IsPositiveInfinity(Value) || Value < 0;

    /// <summary>Lets a plain count stand in for a <see cref="LoopCount"/>, so <c>Loop = 2</c> reads as it does in JavaScript.</summary>
    public static implicit operator LoopCount(int count) => new(count);

    /// <summary>Lets <c>true</c> stand in for <see cref="Infinite"/> and <c>false</c> for no repeats, as upstream's boolean <c>loop</c> does.</summary>
    public static implicit operator LoopCount(bool infinite) => infinite ? Infinite : new(0);
}
