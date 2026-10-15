using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace MonacoLabs.Presto;

/// <summary>
/// Parses ease strings such as <c>"inOutQuint"</c>, <c>"out(2)"</c> or <c>"outElastic(1.5, .5)"</c> into
/// <see cref="Ease"/> delegates, caching every result by input string.
/// </summary>
public static class EaseParser
{
    private static readonly ConcurrentDictionary<string, Ease> _cache = new();

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
        ["steps"] = a => a.Length switch { 0 => Eases.Steps(), 1 => Eases.Steps(StepCount(a)), _ => Eases.Steps(StepCount(a), a.Flag(1)) },
        ["cubicBezier"] = a => a.Length == 4 ? Eases.CubicBezier(a[0], a[1], a[2], a[3]) : throw new FormatException("cubicBezier needs four numbers: x1, y1, x2, y2."),
        ["expoScale"] = a => a.Length < 2
            ? throw new FormatException("expoScale needs a start and an end scale.")
            : Eases.ExpoScale(a[0], a[1], a.Length == 2 ? null : Parse(string.Join(',', a.Tokens[2..]))),
    };

    private const int Seed = 1;

    /// <summary>
    /// Resolves an ease name (<c>linear</c>, <c>none</c>, <c>in</c>, <c>outQuad</c>, <c>inOutElastic</c>, ...) or its
    /// <see cref="Eases"/> PascalCase spelling, optionally with arguments in parentheses, to an <see cref="Ease"/>.
    /// <c>fromPath(...)</c> takes SVG path data for <see cref="Eases.FromPath"/>, <c>wiggle(10, anticipate)</c> a
    /// <see cref="WiggleType"/> name, <c>slowMo(.5, .7, true)</c> a yoyo flag, <c>expoScale(1, 10, outQuad)</c> an ease string,
    /// <c>steps(5, true)</c> a count and a from-start flag, and <c>cubicBezier(.17, .67, .83, .67)</c> four control values.
    /// </summary>
    /// <remarks>
    /// An empty argument list such as <c>"in()"</c> uses the factory defaults rather than 0. Unknown names, <c>irregular(</c>
    /// and <c>linear(</c> (built in code with <see cref="Eases.Irregular"/> and <see cref="Eases.Linear"/>), arguments on a
    /// fixed ease, unparsable arguments and unbalanced parentheses throw <see cref="FormatException"/> instead of caching a broken ease. <c>rough</c> and <c>wiggle(n, random)</c> draw from a
    /// <see cref="Random"/> seeded with 1, so a string always parses to the same curve.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="ease"/> is null.</exception>
    /// <exception cref="FormatException"><paramref name="ease"/> is not a valid ease string.</exception>
    public static Ease Parse(string ease)
    {
        ArgumentNullException.ThrowIfNull(ease);
        return _cache.GetOrAdd(ease.Trim(), Create);
    }

    /// <summary>Resolves <paramref name="ease"/> as <see cref="Parse"/> does, returning false where it would throw <see cref="FormatException"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="ease"/> is null.</exception>
    public static bool TryParse(string ease, [NotNullWhen(true)] out Ease? result)
    {
        try
        {
            result = Parse(ease);
            return true;
        }
        catch (FormatException)
        {
            result = null;
            return false;
        }
    }

    // Strings parsed while diagnostics were inactive got no name from their factory; the cache key is that name.
    internal static void NameCached()
    {
        foreach (var (text, ease) in _cache) Eases.NameIfUnnamed(ease, text);
    }

    private static Ease Create(string input)
    {
        if (input.Length == 0) return Eases.None;
        var open = input.IndexOf('(');
        var name = open < 0 ? input : input[..open].TrimEnd();
        if (name.Length > 0 && char.IsUpper(name[0])) name = char.ToLowerInvariant(name[0]) + name[1..];
        var args = open < 0 ? EaseArgs.Empty : ParseArgs(input, open);

        if (open >= 0 && name is "irregular" or "linear")
        {
            throw new FormatException($"Ease \"{name}(...)\" has no string form; build it with Eases.{char.ToUpperInvariant(name[0])}{name[1..]}.");
        }
        if (Eases.Fixed.TryGetValue(name, out var ease))
        {
            return args.Length == 0 ? ease : throw new FormatException($"Ease \"{name}\" takes no arguments.");
        }
        return _parameterized.TryGetValue(name, out var factory) ? factory(args) : throw new FormatException($"Unknown ease \"{name}\".");
    }

    private static int StepCount(EaseArgs args)
        => (int)args[0] is var count and >= 1 ? count : throw new FormatException("steps needs a count of at least 1.");

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
