using PrestoSample.Showcase;

namespace PrestoSample.Tests;

public sealed class CodeHighlighterTests
{
    private const string CSharpSample = """"
        // a comment
        public static readonly var x = new Foo(.5, 1e3, 2.5f, 0xFF);
        /* block */ var s = "a\"b" + @"c""d" + $"{x}" + 'c' + $$"""raw "quoted" {{x}}""";
        """";

    private const string XamlSample = """
        <!-- note -->
        <Border xmlns:presto="using:Presto" Classes="swatch" presto:Motion.Enter="{presto:Fade Direction=Up}">
            <Grid.Margin>{Binding Text}</Grid.Margin>
            <TextBlock Text="{}{0}" Width='{x:Static Sizes.Wide}' />
        </Border>
        """;

    [Theory]
    [InlineData(CSharpSample, CodeLanguage.CSharp)]
    [InlineData(XamlSample, CodeLanguage.Xaml)]
    [InlineData("", CodeLanguage.CSharp)]
    [InlineData("<Unclosed attr=\"x\"", CodeLanguage.Xaml)]
    public void TokensAreContiguousAndCoverTheWholeInput(string code, CodeLanguage language)
    {
        var position = 0;
        foreach (var (start, length, _) in CodeHighlighter.Tokenize(code, language))
        {
            Assert.Equal(position, start);
            Assert.True(length > 0);
            position += length;
        }
        Assert.Equal(code.Length, position);
    }

    [Fact]
    public void CSharpClassifiesKeywordsStringsNumbersAndComments()
    {
        var tokens = Classified(CSharpSample, CodeLanguage.CSharp);

        Assert.Equal(["public", "static", "readonly", "var", "new", "var"], Of(tokens, CodeTokenKind.Keyword));
        Assert.Equal([".5", "1e3", "2.5f", "0xFF"], Of(tokens, CodeTokenKind.Number));
        Assert.Equal(["// a comment", "/* block */"], Of(tokens, CodeTokenKind.Comment));
        Assert.Equal(["\"a\\\"b\"", "@\"c\"\"d\"", "$\"{x}\"", "'c'", "$$\"\"\"raw \"quoted\" {{x}}\"\"\""], Of(tokens, CodeTokenKind.String));
        Assert.DoesNotContain("Foo", Of(tokens, CodeTokenKind.Keyword));
    }

    [Fact]
    public void ALineCommentStopsBeforeACrlfLineBreak() =>
        Assert.Equal(["// note"], Of(Classified("x = 1; // note\r\ny = 2;", CodeLanguage.CSharp), CodeTokenKind.Comment));

    [Fact]
    public void XamlClassifiesTagsAttributesValuesMarkupExtensionsAndComments()
    {
        var tokens = Classified(XamlSample, CodeLanguage.Xaml);

        Assert.Equal(["<!-- note -->"], Of(tokens, CodeTokenKind.Comment));
        Assert.Equal(["Border", "Grid.Margin", "Grid.Margin", "TextBlock", "Border"], Of(tokens, CodeTokenKind.Tag));
        Assert.Equal(["xmlns:presto", "Classes", "presto:Motion.Enter", "Text", "Width"], Of(tokens, CodeTokenKind.Attribute));
        Assert.Equal(["{presto:Fade Direction=Up}", "{x:Static Sizes.Wide}"], Of(tokens, CodeTokenKind.MarkupExtension));
        Assert.Contains("\"swatch\"", Of(tokens, CodeTokenKind.AttributeValue));
        Assert.Contains("\"{}{0}\"", Of(tokens, CodeTokenKind.AttributeValue));
    }

    private static List<(string Text, CodeTokenKind Kind)> Classified(string code, CodeLanguage language) =>
        [.. CodeHighlighter.Tokenize(code, language).Select(token => (code.Substring(token.Start, token.Length), token.Kind))];

    private static List<string> Of(List<(string Text, CodeTokenKind Kind)> tokens, CodeTokenKind kind) =>
        [.. tokens.Where(token => token.Kind == kind).Select(token => token.Text)];
}
