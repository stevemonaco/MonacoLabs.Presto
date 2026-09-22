namespace Monaco.Presto.Values;

/// <summary>
/// Converts a decomposed value to a target unit. Only the angle units <c>deg</c>, <c>rad</c> and <c>turn</c> are scaled;
/// upstream measures every other unit against a DOM clone, and without layout the core adopts the target unit 1:1.
/// </summary>
public static class UnitConverter
{
    public static void Convert(DecomposedValue value, string unit)
    {
        if (value.Kind == ValueKind.Unit && value.Unit == unit) return;
        var from = AngleFactor(value.Unit);
        var to = AngleFactor(unit);
        if (from > 0 && to > 0) value.Number = value.Number * from / to;
        value.Kind = ValueKind.Unit;
        value.Unit = unit;
    }

    private static double AngleFactor(string? unit) => unit switch
    {
        "deg" => 1,
        "rad" => 180 / Math.PI,
        "turn" => 360,
        _ => 0,
    };
}
