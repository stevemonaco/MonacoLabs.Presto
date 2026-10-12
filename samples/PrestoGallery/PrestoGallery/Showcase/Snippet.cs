using System.Runtime.CompilerServices;

namespace PrestoGallery.Showcase;

/// <summary>Pairs a value with the source text that produced it, so a page prints exactly the code it ran.</summary>
public static class Snippet
{
    /// <summary>
    /// Returns <paramref name="value"/> with its source expression, continuation lines re-indented one level under
    /// the first while keeping their relative indentation.
    /// </summary>
    public static (T Value, string Code) Of<T>(T value, [CallerArgumentExpression(nameof(value))] string code = "")
    {
        var lines = code.ReplaceLineEndings("\n").Split('\n');
        var indent = lines.Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Length - line.TrimStart().Length)
            .DefaultIfEmpty(0)
            .Min();
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                lines[i] = "";
            else
                lines[i] = "    " + lines[i][indent..];
        }
        return (value, string.Join('\n', lines));
    }
}
