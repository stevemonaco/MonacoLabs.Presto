using System.Globalization;

namespace Monaco.Presto;

/// <summary>
/// A point of a piecewise <see cref="Eases.Linear"/> ease: the eased value and, optionally, the progress in [0, 1] at
/// which it is reached. Converts from a bare number or from anime.js's <c>"value percent"</c> string, e.g. <c>"0.25 75%"</c>.
/// </summary>
public readonly record struct LinearStop(double Value, double? Position = null)
{
    /// <summary>A stop at this value, reached at its evenly spaced position among the other stops.</summary>
    public static implicit operator LinearStop(double value) => new(value);

    /// <summary>A stop written as <c>"value"</c> or <c>"value percent"</c>, the percent placing it at its own progress.</summary>
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
