using System.Globalization;

namespace MonacoLabs.Presto.Core;

/// <summary>Flattens the M, L, H, V and C commands of SVG path data (absolute or relative) into a polyline.</summary>
internal static class EasePathParser
{
    private const int CurveSegments = 64;

    public static (List<double> Xs, List<double> Ys) Flatten(string pathData)
    {
        var xs = new List<double>();
        var ys = new List<double>();
        double x = 0, y = 0;
        var command = '\0';
        var i = 0;
        while (true)
        {
            SkipSeparators(pathData, ref i);
            if (i >= pathData.Length) break;
            if (char.IsLetter(pathData[i]))
            {
                command = pathData[i++];
                if ("MmLlHhVvCc".IndexOf(command) < 0) throw new FormatException($"Unsupported path command '{command}'.");
            }
            else if (command == '\0')
            {
                throw new FormatException("Path data must start with a command.");
            }

            var relative = char.IsLower(command);
            double ox = relative ? x : 0, oy = relative ? y : 0;
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                case 'L':
                    x = ox + ReadNumber(pathData, ref i);
                    y = oy + ReadNumber(pathData, ref i);
                    Add(xs, ys, x, y);
                    // Pairs after a moveto are implicit linetos.
                    if (command is 'M' or 'm') command = relative ? 'l' : 'L';
                    break;
                case 'H':
                    x = ox + ReadNumber(pathData, ref i);
                    Add(xs, ys, x, y);
                    break;
                case 'V':
                    y = oy + ReadNumber(pathData, ref i);
                    Add(xs, ys, x, y);
                    break;
                default:
                    double x1 = ox + ReadNumber(pathData, ref i), y1 = oy + ReadNumber(pathData, ref i);
                    double x2 = ox + ReadNumber(pathData, ref i), y2 = oy + ReadNumber(pathData, ref i);
                    double x3 = ox + ReadNumber(pathData, ref i), y3 = oy + ReadNumber(pathData, ref i);
                    if (xs.Count == 0) Add(xs, ys, x, y);
                    for (var s = 1; s <= CurveSegments; s++)
                    {
                        var t = (double)s / CurveSegments;
                        var u = 1 - t;
                        double a = u * u * u, b = 3 * u * u * t, c = 3 * u * t * t, d = t * t * t;
                        Add(xs, ys, a * x + b * x1 + c * x2 + d * x3, a * y + b * y1 + c * y2 + d * y3);
                    }
                    (x, y) = (x3, y3);
                    break;
            }
        }
        return (xs, ys);
    }

    private static void Add(List<double> xs, List<double> ys, double x, double y)
    {
        if (xs.Count > 0 && x < xs[^1])
        {
            if (x < xs[^1] - 1e-9) throw new ArgumentException("Ease path x must not decrease along the path.", "pathData");
            x = xs[^1];
        }
        xs.Add(x);
        ys.Add(y);
    }

    private static void SkipSeparators(string s, ref int i)
    {
        while (i < s.Length && (s[i] == ',' || char.IsWhiteSpace(s[i]))) i++;
    }

    private static double ReadNumber(string s, ref int i)
    {
        SkipSeparators(s, ref i);
        var start = i;
        if (i < s.Length && s[i] is '+' or '-') i++;
        var dot = false;
        while (i < s.Length && (char.IsAsciiDigit(s[i]) || (s[i] == '.' && !dot)))
        {
            dot |= s[i] == '.';
            i++;
        }
        if (i < s.Length && s[i] is 'e' or 'E' && i + 1 < s.Length && (char.IsAsciiDigit(s[i + 1]) || s[i + 1] is '+' or '-'))
        {
            i += 2;
            while (i < s.Length && char.IsAsciiDigit(s[i])) i++;
        }
        return double.TryParse(s.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"Expected a number at position {start} of path \"{s}\".");
    }
}
