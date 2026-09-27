namespace Monaco.Presto.Values;

/// <summary>
/// Mutable scratch object holding a parsed tween value (upstream <c>TweenDecomposedValue</c>): a number with optional unit
/// and relative operator, color channels, or the numbers and surrounding strings of a complex value.
/// </summary>
public sealed class DecomposedValue
{
    /// <summary>Which of the members below carry the value.</summary>
    public ValueKind Kind { get; set; }

    /// <summary>The scalar of a <see cref="ValueKind.Number"/> or <see cref="ValueKind.Unit"/> value.</summary>
    public double Number { get; set; }

    /// <summary>The unit of a <see cref="ValueKind.Unit"/> value, such as <c>"%"</c>, <c>"px"</c> or <c>"deg"</c>.</summary>
    public string? Unit { get; set; }

    /// <summary>The operator of a relative value (<c>'+'</c>, <c>'-'</c> or <c>'*'</c>), or <c>'\0'</c> when the value is absolute.</summary>
    public char Operator { get; set; }

    /// <summary>The RGBA channels of a <see cref="ValueKind.Color"/> value, or every number embedded in a <see cref="ValueKind.Complex"/> one.</summary>
    public double[]? Numbers { get; set; }

    /// <summary>The literal text around each of a complex value's <see cref="Numbers"/>, one entry longer than that array.</summary>
    public string[]? Strings { get; set; }

    /// <summary>Clears every member back to the number 0, which is where <see cref="ValueParser.Decompose"/> starts from.</summary>
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
