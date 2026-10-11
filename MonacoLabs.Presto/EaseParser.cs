using System.Collections.Concurrent;
using System.Globalization;

namespace MonacoLabs.Presto;

/// <summary>
/// Parses ease strings such as <c>"inOutQuint"</c>, <c>"out(2)"</c> or <c>"outElastic(1.5, .5)"</c> into
/// <see cref="Ease"/> delegates, caching every result by input string.
/// </summary>
public static class EaseParser
{
    private static readonly ConcurrentDictionary<string, Ease> _cache = new();

    private static readonly string[] _deprecated = ["steps(", "irregular(", "linear(", "cubicBezier("];

    private static readonly Dictionary<string, Func<EaseArgs, Ease>> _parameterized = new()
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
        ["fromPath"] = a => Eases.FromPath(a.Raw),
        ["wiggle"] = a => a.Length switch
        {
            0 => Eases.Wiggle(random: new(Seed)),
            1 => Eases.Wiggle((int)a[0], random: new(Seed)),
            _ => Eases.Wiggle((int)a[0], a.Identifier<WiggleType>(1), new(Seed)),
        },
        ["rough"] = a => a.Length switch
        {
            0 => Eases.Rough(random: new(Seed)),
            1 => Eases.Rough((int)a[0], random: new(Seed)),
            _ => Eases.Rough((int)a[0], a[1], random: new(Seed)),
        },
        ["slowMo"] = a => a.Length switch
        {
            0 => Eases.SlowMo(),
            1 => Eases.SlowMo(a[0]),
            2 => Eases.SlowMo(a[0], a[1]),
            _ => Eases.SlowMo(a[0], a[1], a.Flag(2)),
        },
        ["expoScale"] = a => a.Length < 2
            ? throw new FormatException("expoScale needs a start and an end scale.")
            : Eases.ExpoScale(a[0], a[1], a.Length == 2 ? null : Parse(string.Join(',', a.Tokens[2..]))),
    };

    private const int Seed = 1;

    /// <summary>
    /// Resolves an ease name (<c>linear</c>, <c>none</c>, <c>in</c>, <c>outQuad</c>, <c>inOutElastic</c>, ...) or its
    /// <see cref="Eases"/> PascalCase spelling, optionally with arguments in parentheses, to an <see cref="Ease"/>.
    /// <c>fromPath(...)</c> takes SVG path data for <see cref="Eases.FromPath"/>, <c>wiggle(10, anticipate)</c> a
    /// <see cref="WiggleType"/> name, <c>slowMo(.5, .7, true)</c> a yoyo flag and <c>expoScale(1, 10, outQuad)</c> an ease string.
    /// </summary>
    /// <remarks>
    /// Unknown names and the removed <c>steps(</c>, <c>irregular(</c>, <c>linear(</c> and <c>cubicBezier(</c> forms resolve to
    /// <see cref="Eases.None"/>. An empty argument list such as <c>"in()"</c> uses the factory
    /// defaults rather than 0, and arguments on a fixed ease, unparsable arguments or unbalanced parentheses throw
    /// <see cref="FormatException"/> instead of caching a broken ease. <c>rough</c> and <c>wiggle(n, random)</c> draw from a
    /// <see cref="Random"/> seeded with 1, so a string always parses to the same curve.
    /// </remarks>
    public static Ease Parse(string ease) => _cache.GetOrAdd(ease.Trim(), Create);

    // Strings parsed while diagnostics were inactive got no name from their factory; the cache key is that name.
    internal static void NameCached()
    {
        foreach (var (text, ease) in _cache) Eases.NameIfUnnamed(ease, text);
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
        var args = open < 0 ? EaseArgs.Empty : ParseArgs(input, open);

        if (Eases.Fixed.TryGetValue(name, out var ease))
        {
            return args.Length == 0 ? ease : throw new FormatException($"Ease \"{name}\" takes no arguments.");
        }
        return _parameterized.TryGetValue(name, out var factory) ? factory(args) : Eases.None;
    }

    private static EaseArgs ParseArgs(string input, int open)
    {
        if (input[^1] != ')') throw new FormatException($"Unbalanced parentheses in ease \"{input}\".");
        var list = input[(open + 1)..^1].Trim();
        return list.Length == 0 ? EaseArgs.Empty : new(list, list.Split(',', StringSplitOptions.TrimEntries));
    }

    private sealed record EaseArgs(string Raw, string[] Tokens)
    {
        public static readonly EaseArgs Empty = new("", []);

        public int Length => Tokens.Length;

        public double this[int index] => double.Parse(Tokens[index], CultureInfo.InvariantCulture);

        public T Identifier<T>(int index) where T : struct, Enum
            => Enum.TryParse<T>(Tokens[index], true, out var value) && Enum.IsDefined(value)
                ? value
                : throw new FormatException($"Unknown {typeof(T).Name} \"{Tokens[index]}\".");

        public bool Flag(int index)
            => bool.TryParse(Tokens[index], out var value) ? value : throw new FormatException($"Expected true or false, got \"{Tokens[index]}\".");
    }
}
