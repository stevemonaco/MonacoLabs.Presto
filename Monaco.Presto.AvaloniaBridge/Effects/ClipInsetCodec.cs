using Avalonia;
using Monaco.Presto.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Effects;

/// <summary>
/// Decomposes a <c>ClipInset</c> into its left, top, right and bottom channels, each in DIPs or <c>%</c>, from a
/// <see cref="Thickness"/> or a string in <see cref="Thickness"/> order (<c>"10%"</c>, <c>"0 20"</c>, <c>"0 0 100% 0"</c>).
/// A bare number is that many DIPs on every side. Each channel's unit rides in the separator after it, so the end value's
/// units are the ones the channels are written in, without converting a start value written in the other unit.
/// </summary>
internal sealed class ClipInsetCodec : IReconcilingValueCodec
{
    private static readonly char[] s_separators = [' ', ',', '\t'];

    public static bool IsPercent(string[] strings, int channel) => strings[channel + 1].StartsWith('%');

    public bool TryDecode(object value, DecomposedValue into)
    {
        if (value is not Thickness thickness) return false;
        Fill(into, [thickness.Left, thickness.Top, thickness.Right, thickness.Bottom], new bool[4]);
        return true;
    }

    public bool TryParse(string text, DecomposedValue into)
    {
        var tokens = text.Split(s_separators, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length is not (1 or 2 or 4)) return false;
        var numbers = new double[4];
        var percent = new bool[4];
        for (var side = 0; side < 4; side++)
        {
            var token = tokens[side % tokens.Length];
            if (ValueParser.TryParseUnit(token, out var number, out var unit) && unit is "%" or "px")
            {
                numbers[side] = number;
                percent[side] = unit == "%";
            }
            else if (double.IsNaN(numbers[side] = JsNumber.ToNumber(token)))
            {
                return false;
            }
        }
        Fill(into, numbers, percent);
        return true;
    }

    public void Reconcile(DecomposedValue from, DecomposedValue to)
    {
        if (from.Kind != ValueKind.Complex) Fill(from, [from.Number, from.Number, from.Number, from.Number], new bool[4]);
        if (to.Kind != ValueKind.Complex) Fill(to, [to.Number, to.Number, to.Number, to.Number], new bool[4]);
    }

    public object Encode(double number, Tween tween) => tween.ComposeString(number);

    private static void Fill(DecomposedValue into, double[] numbers, bool[] percent)
    {
        into.Reset();
        into.Kind = ValueKind.Complex;
        into.Numbers = numbers;
        into.Strings = ["", Unit(percent[0]) + " ", Unit(percent[1]) + " ", Unit(percent[2]) + " ", Unit(percent[3])];
    }

    private static string Unit(bool percent) => percent ? "%" : "";
}
