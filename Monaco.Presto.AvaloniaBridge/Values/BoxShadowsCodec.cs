using Avalonia.Media;
using Monaco.Presto.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Values;

/// <summary>
/// Tweens <see cref="BoxShadows"/> as one <c>(inset, offsetX, offsetY, blur, spread, r, g, b, a)</c> run per shadow, parsing
/// strings such as <c>"0 4 16 0 #40000000"</c> through <see cref="BoxShadows.Parse"/>. The shorter list is padded as CSS
/// pads it, with zero shadows, but in the other end's color at alpha 0 so a shadow fades in or out instead of flashing black.
/// </summary>
/// <remarks>Whether a shadow is inset cannot be interpolated, so it comes from the end value.</remarks>
internal sealed class BoxShadowsCodec : IReconcilingValueCodec
{
    private const int Size = 9;
    private const int Alpha = 8;

    public bool TryDecode(object value, DecomposedValue into)
    {
        if (value is not BoxShadows shadows) return false;
        ValueParser.Decompose(Decompose(shadows), into);
        return true;
    }

    public bool TryParse(string text, DecomposedValue into)
    {
        BoxShadows shadows;
        try
        {
            shadows = text.Trim() == "none" ? default : BoxShadows.Parse(text);
        }
        catch (Exception e) when (e is FormatException or ArgumentException)
        {
            return false;
        }
        ValueParser.Decompose(Decompose(shadows), into);
        return true;
    }

    public void Reconcile(DecomposedValue from, DecomposedValue to)
    {
        var fromNumbers = Numbers(from);
        var toNumbers = Numbers(to);
        var length = Math.Max(fromNumbers.Length, toNumbers.Length);
        ValueParser.Decompose(Pad(fromNumbers, toNumbers, length), from);
        ValueParser.Decompose(Pad(toNumbers, fromNumbers, length), to);
    }

    public object Encode(double number, Tween tween)
    {
        var numbers = tween._numbers;
        var count = numbers.Length / Size;
        if (count == 0) return default(BoxShadows);
        var rest = count > 1 ? new BoxShadow[count - 1] : null;
        for (var i = 1; i < count; i++) rest![i - 1] = Shadow(numbers, tween._toNumbers, i);
        return rest == null ? new BoxShadows(Shadow(numbers, tween._toNumbers, 0)) : new BoxShadows(Shadow(numbers, tween._toNumbers, 0), rest);
    }

    private static BoxShadow Shadow(double[] numbers, double[] to, int index)
    {
        var at = index * Size;
        return new BoxShadow
        {
            IsInset = to[at] != 0,
            OffsetX = numbers[at + 1],
            OffsetY = numbers[at + 2],
            Blur = Math.Max(0, numbers[at + 3]),
            Spread = numbers[at + 4],
            Color = PrestoColor.FromChannels(numbers.AsSpan(at + 5)).ToAvalonia(),
        };
    }

    private static double[] Decompose(BoxShadows shadows)
    {
        var numbers = new double[shadows.Count * Size];
        for (var i = 0; i < shadows.Count; i++)
        {
            var shadow = shadows[i];
            var at = i * Size;
            numbers[at] = shadow.IsInset ? 1 : 0;
            numbers[at + 1] = shadow.OffsetX;
            numbers[at + 2] = shadow.OffsetY;
            numbers[at + 3] = shadow.Blur;
            numbers[at + 4] = shadow.Spread;
            shadow.Color.ToPresto().ToChannels().CopyTo(numbers, at + 5);
        }
        return numbers;
    }

    /// <summary>A missing value (the number 0 the core decomposes it into) is an empty list.</summary>
    private static double[] Numbers(DecomposedValue value) => value.Kind == ValueKind.Complex ? value.Numbers ?? [] : [];

    /// <summary><paramref name="numbers"/> grown to <paramref name="length"/> with the shadows <paramref name="other"/> has there, zeroed and faded out.</summary>
    private static double[] Pad(double[] numbers, double[] other, int length)
    {
        var padded = new double[length];
        numbers.CopyTo(padded, 0);
        for (var at = numbers.Length; at < length; at += Size)
        {
            padded[at] = other[at];
            Array.Copy(other, at + 5, padded, at + 5, 3);
            padded[at + Alpha] = 0;
        }
        return padded;
    }
}
