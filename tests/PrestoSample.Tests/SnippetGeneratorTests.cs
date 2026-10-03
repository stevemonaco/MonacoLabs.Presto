using PrestoSample.Showcase;
using PrestoSample.SnippetGenerator;

namespace PrestoSample.Tests;

public sealed class SnippetGeneratorTests
{
    [Fact]
    public void ARegionIsDedentedByItsSmallestIndentationIgnoringBlankLines()
    {
        var (snippets, errors) = SnippetExtractor.Extract("""
            <Root>
                <!-- snippet: Sample -->

                    <Grid>
                  <Border />
                    </Grid>

                <!-- /snippet -->
            </Root>
            """, SnippetSyntax.Xaml);

        Assert.Empty(errors);
        Assert.Equal(("Sample", "  <Grid>\n<Border />\n  </Grid>"), (snippets[0].Name, snippets[0].Text));
    }

    [Fact]
    public void BlankLinesInsideARegionArePreservedAsEmptyLines()
    {
        var (snippets, _) = SnippetExtractor.Extract("#region snippet Gap\n    var a = 1;\n      \n\n    var b = 2;\n#endregion", SnippetSyntax.CSharp);

        Assert.Equal("var a = 1;\n\n\nvar b = 2;", snippets[0].Text);
    }

    [Fact]
    public void CrlfInputIsNormalisedToLf()
    {
        var (snippets, _) = SnippetExtractor.Extract("<!-- snippet: Lines -->\r\n  <A />\r\n  <B />\r\n<!-- /snippet -->\r\n", SnippetSyntax.Xaml);

        Assert.Equal("<A />\n<B />", snippets[0].Text);
    }

    [Fact]
    public void EachSyntaxRecognisesOnlyItsOwnMarkers()
    {
        const string text = "<!-- snippet: Xaml -->\n<A />\n<!-- /snippet -->\n#region snippet CSharp\nvar a = 1;\n#endregion";

        Assert.Equal(["Xaml"], SnippetExtractor.Extract(text, SnippetSyntax.Xaml).Snippets.Select(s => s.Name));
        Assert.Equal(["CSharp"], SnippetExtractor.Extract(text, SnippetSyntax.CSharp).Snippets.Select(s => s.Name));
    }

    [Fact]
    public void ANestedSnippetIsEmittedAndItsMarkersAreStrippedFromTheOuterRegion()
    {
        var (snippets, errors) = SnippetExtractor.Extract("""
            <!-- snippet: Outer -->
            <Panel>
                <!-- snippet: Inner -->
                <Border />
                <!-- /snippet -->
            </Panel>
            <!-- /snippet -->
            """, SnippetSyntax.Xaml);

        Assert.Empty(errors);
        Assert.Equal("<Border />", snippets.Single(s => s.Name == "Inner").Text);
        Assert.Equal("<Panel>\n    <Border />\n</Panel>", snippets.Single(s => s.Name == "Outer").Text);
    }

    [Fact]
    public void AnOrdinaryRegionInsideASnippetIsKeptAndPairsWithItsOwnEndregion()
    {
        var (snippets, errors) = SnippetExtractor.Extract("""
            #region snippet Setup
            #region Fields
            int a;
            #endregion
            int b;
            #endregion
            int c;
            """, SnippetSyntax.CSharp);

        Assert.Empty(errors);
        Assert.Equal("#region Fields\nint a;\n#endregion\nint b;", snippets[0].Text);
    }

    [Fact]
    public void AnUnterminatedRegionIsAnError()
    {
        var (snippets, errors) = SnippetExtractor.Extract("int a;\n#region snippet Open\nint b;", SnippetSyntax.CSharp);

        Assert.Empty(snippets);
        Assert.Equal(2, Assert.Single(errors).Line);
    }

    [Fact]
    public void AnInvalidIdentifierIsAnError()
    {
        var (snippets, errors) = SnippetExtractor.Extract("<!-- snippet: 2Fast -->\n<A />\n<!-- /snippet -->", SnippetSyntax.Xaml);

        Assert.Empty(snippets);
        Assert.Single(errors);
    }

    [Fact]
    public void ASampleRegionIsGeneratedAsASnippetsConstant() =>
        Assert.Equal("""<Border Classes="swatch" Width="64" Height="64" presto:Presence.Arrive="{x:Static local:XamlPage.FadeUp}" />""", Snippets.XamlStatic);
}
