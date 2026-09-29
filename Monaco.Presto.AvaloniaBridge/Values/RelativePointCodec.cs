using Avalonia;
using Monaco.Presto.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Values;

/// <summary>
/// Decomposes a <see cref="RelativePoint"/> into its two coordinates. The unit rides in the separator strings, the way a unit
/// does in any complex value, so the unit of the value a tween ends on is the unit its coordinates are written in; mixing an
/// absolute end value with a relative start value tweens the raw coordinates, without converting between the two.
/// </summary>
internal sealed class RelativePointCodec : IValueCodec
{
    private const string AbsoluteUnit = "px";

    public bool TryDecode(object value, DecomposedValue into)
    {
        if (value is not RelativePoint point) return false;
        var unit = point.Unit == RelativeUnit.Absolute ? AbsoluteUnit : "";
        into.Kind = ValueKind.Complex;
        into.Numbers = [point.Point.X, point.Point.Y];
        into.Strings = ["", unit + " ", unit];
        return true;
    }

    public object Encode(double number, Tween tween) => Compose(tween);

    internal static RelativePoint Compose(Tween tween) => new(
        tween._numbers[0],
        tween._numbers[1],
        tween._strings[^1] == AbsoluteUnit ? RelativeUnit.Absolute : RelativeUnit.Relative);
}
