using System.Text.RegularExpressions;

namespace PrestoSample.SnippetGenerator;

/// <summary>
/// Finds snippet regions (<c>&lt;!-- snippet: Name --&gt;</c> … <c>&lt;!-- /snippet --&gt;</c> in XAML,
/// <c>#region snippet Name</c> … <c>#endregion</c> in C#) and returns each one's dedented text. A skip region
/// (<c>&lt;!-- snippet-skip --&gt;</c> … <c>&lt;!-- /snippet-skip --&gt;</c>, XAML only) inside a snippet is printed as a single <c>…</c> line.
/// </summary>
public static class SnippetExtractor
{
    private static readonly Regex XamlOpen = new(@"^<!--\s*snippet:\s*(\S+)\s*-->$");
    private static readonly Regex XamlClose = new(@"^<!--\s*/snippet(?:-skip)?\s*-->$");
    private static readonly Regex XamlSkip = new(@"^<!--\s*snippet-skip\s*-->$");
    private static readonly Regex CSharpOpen = new(@"^#region\s+snippet\s+(\S+)$");
    private static readonly Regex CSharpRegion = new(@"^#region\b");
    private static readonly Regex CSharpClose = new(@"^#endregion\b");
    private static readonly Regex Identifier = new(@"^[A-Za-z_][A-Za-z0-9_]*$");

    public static (List<(string Name, string Text, int Line)> Snippets, List<(int Line, string Message)> Errors) Extract(string text, SnippetSyntax syntax)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var isMarker = new bool[lines.Length];
        var skipEnd = new int[lines.Length];
        var open = new Stack<(string? Name, int Start, bool Skip)>();
        var snippets = new List<(string Name, string Text, int Line)>();
        var errors = new List<(int Line, string Message)>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var opening = (syntax == SnippetSyntax.Xaml ? XamlOpen : CSharpOpen).Match(line);
            if (opening.Success)
            {
                var name = opening.Groups[1].Value;
                if (!Identifier.IsMatch(name))
                    errors.Add((i + 1, $"Snippet name '{name}' is not a valid identifier"));
                isMarker[i] = true;
                open.Push((name, i, false));
            }
            else if (syntax == SnippetSyntax.Xaml && XamlSkip.IsMatch(line))
            {
                open.Push((null, i, true));
            }
            else if (syntax == SnippetSyntax.CSharp && CSharpRegion.IsMatch(line))
            {
                open.Push((null, i, false));
            }
            else if ((syntax == SnippetSyntax.Xaml ? XamlClose : CSharpClose).IsMatch(line))
            {
                if (open.Count == 0)
                {
                    if (syntax == SnippetSyntax.Xaml)
                        errors.Add((i + 1, "'<!-- /snippet -->' has no matching snippet"));
                    continue;
                }

                var (name, start, skip) = open.Pop();
                if (skip)
                    skipEnd[start] = i;
                if (name is null)
                    continue;
                isMarker[i] = true;
                if (Identifier.IsMatch(name))
                    snippets.Add((name, Dedent(lines, start + 1, i, isMarker, skipEnd), start + 1));
            }
        }

        foreach (var (name, start, skip) in open)
        {
            if (skip)
                errors.Add((start + 1, "Snippet skip region is never closed"));
            else if (name is not null)
                errors.Add((start + 1, $"Snippet '{name}' is never closed"));
        }

        return (snippets, errors);
    }

    private static string Dedent(string[] lines, int from, int to, bool[] isMarker, int[] skipEnd)
    {
        var body = new List<string>();
        for (var i = from; i < to; i++)
        {
            if (skipEnd[i] > 0)
            {
                body.Add(lines[i].Substring(0, lines[i].Length - lines[i].TrimStart().Length) + "…");
                i = skipEnd[i];
            }
            else if (!isMarker[i])
                body.Add(string.IsNullOrWhiteSpace(lines[i]) ? "" : lines[i]);
        }

        var indent = body.Where(line => line.Length > 0).Select(line => line.Length - line.TrimStart().Length).DefaultIfEmpty(0).Min();
        var first = body.FindIndex(line => line.Length > 0);
        var last = body.FindLastIndex(line => line.Length > 0);
        return first < 0 ? "" : string.Join("\n", body.Skip(first).Take(last - first + 1).Select(line => line.Length > 0 ? line.Substring(indent) : line));
    }
}
