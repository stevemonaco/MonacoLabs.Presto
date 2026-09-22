namespace Monaco.Presto.Values;

/// <summary>
/// Mutable scratch object holding a parsed tween value (upstream <c>TweenDecomposedValue</c>): a number with optional unit
/// and relative operator, color channels, or the numbers and surrounding strings of a complex value.
/// </summary>
public sealed class DecomposedValue
{
    public ValueKind Kind { get; set; }
    public double Number { get; set; }
    public string? Unit { get; set; }
    public char Operator { get; set; }
    public double[]? Numbers { get; set; }
    public string[]? Strings { get; set; }

    public void Reset()
    {
        Kind = ValueKind.Number;
        Number = 0;
        Unit = null;
        Operator = '\0';
        Numbers = null;
        Strings = null;
    }
}
