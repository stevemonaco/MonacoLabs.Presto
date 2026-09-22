using System.Globalization;

namespace Monaco.Presto;

/// <summary>
/// Parses anime.js ease strings such as <c>"inOutQuint"</c>, <c>"out(2)"</c> or <c>"outElastic(1.5, .5)"</c> into
/// <see cref="Ease"/> delegates, caching every result by input string like upstream's <c>easesLookups</c>.
/// </summary>
public static class EaseParser
{
    private static readonly Dictionary<string, Ease> _cache = new();

    private static readonly string[] _deprecated = ["steps(", "irregular(", "linear(", "cubicBezier("];

    private static readonly Dictionary<string, Ease> _fixed = new()
    {
        ["linear"] = Eases.None, ["none"] = Eases.None,
        ["inQuad"] = Eases.InQuad, ["outQuad"] = Eases.OutQuad, ["inOutQuad"] = Eases.InOutQuad, ["outInQuad"] = Eases.OutInQuad,
        ["inCubic"] = Eases.InCubic, ["outCubic"] = Eases.OutCubic, ["inOutCubic"] = Eases.InOutCubic, ["outInCubic"] = Eases.OutInCubic,
        ["inQuart"] = Eases.InQuart, ["outQuart"] = Eases.OutQuart, ["inOutQuart"] = Eases.InOutQuart, ["outInQuart"] = Eases.OutInQuart,
        ["inQuint"] = Eases.InQuint, ["outQuint"] = Eases.OutQuint, ["inOutQuint"] = Eases.InOutQuint, ["outInQuint"] = Eases.OutInQuint,
        ["inSine"] = Eases.InSine, ["outSine"] = Eases.OutSine, ["inOutSine"] = Eases.InOutSine, ["outInSine"] = Eases.OutInSine,
        ["inCirc"] = Eases.InCirc, ["outCirc"] = Eases.OutCirc, ["inOutCirc"] = Eases.InOutCirc, ["outInCirc"] = Eases.OutInCirc,
        ["inExpo"] = Eases.InExpo, ["outExpo"] = Eases.OutExpo, ["inOutExpo"] = Eases.InOutExpo, ["outInExpo"] = Eases.OutInExpo,
        ["inBounce"] = Eases.InBounce, ["outBounce"] = Eases.OutBounce, ["inOutBounce"] = Eases.InOutBounce, ["outInBounce"] = Eases.OutInBounce,
    };

    private static readonly Dictionary<string, Func<double[], Ease>> _parameterised = new()
    {
        ["in"] = a => a.Length == 0 ? Eases.In() : Eases.In(a[0]),
        ["out"] = a => a.Length == 0 ? Eases.Out() : Eases.Out(a[0]),
        ["inOut"] = a => a.Length == 0 ? Eases.InOut() : Eases.InOut(a[0]),
        ["outIn"] = a => a.Length == 0 ? Eases.OutIn() : Eases.OutIn(a[0]),
        ["inBack"] = a => a.Length == 0 ? Eases.InBack() : Eases.InBack(a[0]),
        ["outBack"] = a => a.Length == 0 ? Eases.OutBack() : Eases.OutBack(a[0]),
        ["inOutBack"] = a => a.Length == 0 ? Eases.InOutBack() : Eases.InOutBack(a[0]),
        ["outInBack"] = a => a.Length == 0 ? Eases.OutInBack() : Eases.OutInBack(a[0]),
        ["inElastic"] = a => a.Length switch { 0 => Eases.InElastic(), 1 => Eases.InElastic(a[0]), _ => Eases.InElastic(a[0], a[1]) },
        ["outElastic"] = a => a.Length switch { 0 => Eases.OutElastic(), 1 => Eases.OutElastic(a[0]), _ => Eases.OutElastic(a[0], a[1]) },
        ["inOutElastic"] = a => a.Length switch { 0 => Eases.InOutElastic(), 1 => Eases.InOutElastic(a[0]), _ => Eases.InOutElastic(a[0], a[1]) },
        ["outInElastic"] = a => a.Length switch { 0 => Eases.OutInElastic(), 1 => Eases.OutInElastic(a[0]), _ => Eases.OutInElastic(a[0], a[1]) },
    };

    /// <summary>
    /// Resolves an anime.js ease name (<c>linear</c>, <c>none</c>, <c>in</c>, <c>outQuad</c>, <c>inOutElastic</c>, ...) or its
    /// <see cref="Eases"/> PascalCase spelling, optionally with arguments in parentheses, to an <see cref="Ease"/>.
    /// </summary>
    /// <remarks>
    /// Unknown names and the removed <c>steps(</c>, <c>irregular(</c>, <c>linear(</c> and <c>cubicBezier(</c> forms resolve to
    /// <see cref="Eases.None"/>, as upstream. Unlike upstream, an empty argument list such as <c>"in()"</c> uses the factory
    /// defaults rather than coercing to 0, and arguments on a fixed ease, unparsable arguments or unbalanced parentheses throw
    /// <see cref="FormatException"/> instead of caching a broken ease.
    /// </remarks>
    public static Ease Parse(string ease)
    {
        var key = ease.Trim();
        return _cache.TryGetValue(key, out var cached) ? cached : _cache[key] = Create(key);
    }

    private static Ease Create(string input)
    {
        foreach (var prefix in _deprecated)
        {
            if (input.StartsWith(prefix, StringComparison.Ordinal)) return Eases.None;
        }

        var open = input.IndexOf('(');
        var name = open < 0 ? input : input[..open].TrimEnd();
        if (name.Length > 0 && char.IsUpper(name[0])) name = char.ToLowerInvariant(name[0]) + name[1..];
        double[] args = open < 0 ? [] : ParseArgs(input, open);

        if (_fixed.TryGetValue(name, out var ease))
        {
            return args.Length == 0 ? ease : throw new FormatException($"Ease \"{name}\" takes no arguments.");
        }
        return _parameterised.TryGetValue(name, out var factory) ? factory(args) : Eases.None;
    }

    private static double[] ParseArgs(string input, int open)
    {
        if (input[^1] != ')') throw new FormatException($"Unbalanced parentheses in ease \"{input}\".");
        var list = input[(open + 1)..^1];
        return string.IsNullOrWhiteSpace(list) ? [] : Array.ConvertAll(list.Split(','), s => double.Parse(s, CultureInfo.InvariantCulture));
    }
}
