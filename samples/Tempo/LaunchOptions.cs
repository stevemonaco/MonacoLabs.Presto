using System.Globalization;
using Avalonia;

namespace Tempo;

/// <summary>Command-line options that start Tempo in a known state.</summary>
public sealed record LaunchOptions(bool SkipIntro = false, Size? Size = null)
{
    public static string Usage =>
        """
        Options:
          --skip-intro               Start on the main view without playing the intro.
          --size <width>x<height>    Set the window size in DIPs for this run, e.g. 360x780.
        """;

    /// <summary>Parses <paramref name="args"/>, accepting <c>--size value</c> and <c>--size=value</c>.</summary>
    /// <returns>False with <paramref name="error"/> set when an argument is unknown or malformed.</returns>
    public static bool TryParse(IReadOnlyList<string> args, out LaunchOptions options, out string? error)
    {
        options = new LaunchOptions();
        error = null;
        for (var i = 0; i < args.Count; i++)
        {
            var (name, value) = args[i].Split('=', 2) is [var n, var v] ? (n, (string?)v) : (args[i], null);
            if (name == "--skip-intro" && value is null)
            {
                options = options with { SkipIntro = true };
                continue;
            }
            if (name != "--size")
            {
                error = $"Unknown argument '{args[i]}'.";
                return false;
            }
            value ??= i + 1 < args.Count ? args[++i] : null;
            if (value?.Split('x', 'X') is not [var w, var h]
                || !int.TryParse(w, NumberStyles.None, CultureInfo.InvariantCulture, out var width)
                || !int.TryParse(h, NumberStyles.None, CultureInfo.InvariantCulture, out var height)
                || width <= 0 || height <= 0)
            {
                error = $"--size must be <width>x<height>, not '{value}'.";
                return false;
            }
            options = options with { Size = new Size(width, height) };
        }
        return true;
    }
}
