using System.Globalization;

namespace Monaco.Presto;

/// <summary>
/// A point of a piecewise <see cref="Eases.Linear"/> ease: the eased value and, optionally, the progress in [0, 1] at
/// which it is reached. Converts from a bare number or from anime.js's <c>"value percent"</c> string, e.g. <c>"0.25 75%"</c>.
/// </summary>
public readonly record struct LinearStop(double Value, double? Position = null)
{
    public static implicit operator LinearStop(double value) => new(value);

    public static implicit operator LinearStop(string s)
    {
        var parts = s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (1 or 2)) throw new FormatException($"Invalid linear stop \"{s}\".");
        var value = double.Parse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture);
        return parts.Length == 1
            ? new(value)
            : new(value, double.Parse(parts[1].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture) / 100);
    }
}
