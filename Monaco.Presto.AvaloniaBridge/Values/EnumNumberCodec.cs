using Monaco.Presto.Core;
using Monaco.Presto.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Values;

/// <summary>
/// Tweens a numbered enum such as <see cref="Avalonia.Media.FontWeight"/> as its number, written rounded and clamped to
/// 1–<paramref name="max"/>; an in-between number is a valid value the font system resolves to the nearest face.
/// </summary>
internal sealed class EnumNumberCodec<T>(int max) : IReconcilingValueCodec where T : struct, Enum
{
    public bool TryDecode(object value, DecomposedValue into)
    {
        if (value is not T) return false;
        into.Number = Convert.ToDouble(value);
        return true;
    }

    public bool TryParse(string text, DecomposedValue into)
    {
        if (!Enum.TryParse<T>(text, ignoreCase: true, out var member)) return false;
        into.Number = Convert.ToDouble(member);
        return true;
    }

    public void Reconcile(DecomposedValue from, DecomposedValue to)
    {
    }

    public object Encode(double number, Tween tween) => Enum.ToObject(typeof(T), (int)Math.Clamp(Helpers.Round(number, 0), 1, max));
}
