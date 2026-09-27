using Monaco.Presto.Core;

namespace Monaco.Presto;

public static partial class Eases
{
    /// <summary>
    /// Piecewise linear ease through <paramref name="length"/> randomly spaced steps; <paramref name="randomness"/> 0 is
    /// evenly spaced and 1 fully random within each segment.
    /// </summary>
    public static Ease Irregular(int length = 10, double randomness = 1, Random? random = null)
    {
        random ??= Random.Shared;
        var total = length - 1;
        var values = new LinearStop[Math.Max(length, 2)];
        for (var i = 1; i < total; i++)
        {
            var previousValue = values[i - 1].Value;
            var spacing = (double)i / total;
            var segmentEnd = (double)(i + 1) / total;
            var randomVariation = spacing + (segmentEnd - spacing) * random.NextDouble();
            values[i] = Helpers.Clamp(spacing * (1 - randomness) + randomVariation * randomness, previousValue, 1);
        }
        values[^1] = 1;
        return Linear(values);
    }
}
