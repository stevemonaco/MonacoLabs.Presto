using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace PrestoSample.Showcase;

/// <summary>A regex tokenizer for the gallery's printed C# and XAML, good enough to colour a snippet, not to parse one.</summary>
public static class CodeHighlighter
{
    private const RegexOptions Options = RegexOptions.Compiled | RegexOptions.ExplicitCapture;

    private static readonly Regex CSharpToken = new(
        @"(?<comment>//[^\r\n]*|/\*[\s\S]*?\*/)" +
        @"|(?<string>\$*""{3,}[\s\S]*?""{3,}|(?:\$@|@\$?)""(?:[^""]|"""")*""|\$?""(?:[^""\\\n]|\\.)*""|'(?:[^'\\\n]|\\.)+')" +
        @"|(?<number>(?:0[xX][0-9a-fA-F_]+|0[bB][01_]+|(?:\d[\d_]*(?:\.\d[\d_]*)?|\.\d[\d_]*)(?:[eE][+-]?\d+)?)(?:[uU][lL]?|[lL][uU]?|[fFdDmM])?)" +
        @"|(?<word>@?[A-Za-z_]\w*)",
        Options);

    private static readonly Regex XamlMarkup = new(
        @"(?<comment><!--[\s\S]*?-->)|<[/?]?(?<tag>[\w:.\-]+)(?<body>(?:""[^""]*""|'[^']*'|[^'"">])*)>",
        Options);

    private static readonly Regex XamlAttribute = new(@"(?<name>[\w:.\-]+)\s*=\s*(?<value>""[^""]*""|'[^']*')", Options);

    private static readonly FrozenSet<string> Keywords = FrozenSet.ToFrozenSet(
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
        "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
        "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
        "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
        "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
        "and", "async", "await", "dynamic", "get", "init", "nameof", "not", "or", "partial", "record", "required",
        "set", "var", "when", "with", "yield",
    ]);

    /// <summary>Splits <paramref name="code"/> into contiguous tokens that together cover all of it, untokenized text as <see cref="CodeTokenKind.Plain"/>.</summary>
    public static IReadOnlyList<(int Start, int Length, CodeTokenKind Kind)> Tokenize(string code, CodeLanguage language)
    {
        var tokens = new List<(int Start, int Length, CodeTokenKind Kind)>();
        var position = 0;

        void Add(int start, int length, CodeTokenKind kind)
        {
            if (start > position) tokens.Add((position, start - position, CodeTokenKind.Plain));
            tokens.Add((start, length, kind));
            position = start + length;
        }

        void AddGroup(Group group, CodeTokenKind kind)
        {
            if (group.Success) Add(group.Index, group.Length, kind);
        }

        if (language == CodeLanguage.CSharp)
        {
            foreach (Match match in CSharpToken.Matches(code))
            {
                AddGroup(match.Groups["comment"], CodeTokenKind.Comment);
                AddGroup(match.Groups["string"], CodeTokenKind.String);
                AddGroup(match.Groups["number"], CodeTokenKind.Number);
                if (match.Groups["word"].Success && Keywords.Contains(match.Value)) Add(match.Index, match.Length, CodeTokenKind.Keyword);
            }
        }
        else
        {
            foreach (Match match in XamlMarkup.Matches(code))
            {
                AddGroup(match.Groups["comment"], CodeTokenKind.Comment);
                AddGroup(match.Groups["tag"], CodeTokenKind.Tag);
                var body = match.Groups["body"];
                if (!body.Success) continue;
                foreach (Match attribute in XamlAttribute.Matches(code, body.Index).TakeWhile(a => a.Index < body.Index + body.Length))
                {
                    AddGroup(attribute.Groups["name"], CodeTokenKind.Attribute);
                    var value = attribute.Groups["value"];
                    if (value.ValueSpan[1..].StartsWith("{") && !value.ValueSpan[1..].StartsWith("{}"))
                    {
                        Add(value.Index, 1, CodeTokenKind.AttributeValue);
                        Add(value.Index + 1, value.Length - 2, CodeTokenKind.MarkupExtension);
                        Add(value.Index + value.Length - 1, 1, CodeTokenKind.AttributeValue);
                    }
                    else
                    {
                        Add(value.Index, value.Length, CodeTokenKind.AttributeValue);
                    }
                }
            }
        }

        if (position < code.Length) tokens.Add((position, code.Length - position, CodeTokenKind.Plain));
        return tokens;
    }
}
