using PrestoGallery.Showcase;

namespace PrestoGallery.Tests;

public sealed class SnippetTests
{
    [Fact]
    public void ASingleLineExpressionIsCapturedUnchanged()
    {
        var (value, code) = Snippet.Of(Math.Max(1, 2));

        Assert.Equal(2, value);
        Assert.Equal("Math.Max(1, 2)", code);
    }

    [Fact]
    public void AMultiLineExpressionHasItsContinuationLinesIndentedOneLevel()
    {
        var (value, code) = Snippet.Of(new[] { 1, 2 }
                    .Select(n => n * 10)
                        .Sum());

        Assert.Equal(30, value);
        Assert.Equal("new[] { 1, 2 }\n    .Select(n => n * 10)\n        .Sum()", code);
    }

    [Fact]
    public void CommentsAndBlankLinesArePreserved()
    {
        var (_, code) = Snippet.Of(string.Concat(
            "a", // first

            "b"));

        Assert.Equal("string.Concat(\n    \"a\", // first\n\n    \"b\")", code);
    }
}
