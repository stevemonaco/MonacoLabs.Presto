namespace Tempo;

/// <summary>Command-line options that start Tempo in a known state.</summary>
public sealed record LaunchOptions(bool SkipIntro = false)
{
    public static string Usage =>
        """
        Options:
          --skip-intro    Start on the main view without playing the intro.
        """;

    /// <returns>False with <paramref name="error"/> set when an argument is unknown.</returns>
    public static bool TryParse(IReadOnlyList<string> args, out LaunchOptions options, out string? error)
    {
        options = new LaunchOptions();
        error = null;
        foreach (var arg in args)
        {
            if (arg != "--skip-intro")
            {
                error = $"Unknown argument '{arg}'.";
                return false;
            }
            options = options with { SkipIntro = true };
        }
        return true;
    }
}
