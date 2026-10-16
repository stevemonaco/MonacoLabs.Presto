using PrestoGallery.Showcase;
using PrestoGallery.SnippetGenerator;

namespace PrestoGallery.Tests;

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
    public void CrlfInputIsNormalizedToLf()
    {
        var (snippets, _) = SnippetExtractor.Extract("<!-- snippet: Lines -->\r\n  <A />\r\n  <B />\r\n<!-- /snippet -->\r\n", SnippetSyntax.Xaml);

        Assert.Equal("<A />\n<B />", snippets[0].Text);
    }

    [Fact]
    public void EachSyntaxRecognizesOnlyItsOwnMarkers()
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
    public void AXamlSkipRegionIsPrintedAsOneEllipsisLineIndentedLikeItsMarker()
    {
        var (snippets, errors) = SnippetExtractor.Extract("""
            <Root>
                <!-- snippet: Card -->
                <Border>
                    <Border.Tag>1</Border.Tag>
                    <!-- snippet-skip -->
                    <StackPanel>
                        <TextBlock />
                    </StackPanel>
                    <!-- /snippet-skip -->
                </Border>
                <!-- /snippet -->
            </Root>
            """, SnippetSyntax.Xaml);

        Assert.Empty(errors);
        Assert.Equal("<Border>\n    <Border.Tag>1</Border.Tag>\n    …\n</Border>", Assert.Single(snippets).Text);
    }

    [Fact]
    public void ASnippetInsideASkipRegionIsStillExtractedAndItsSkipsApplyAtEveryLevel()
    {
        var (snippets, errors) = SnippetExtractor.Extract("""
            <!-- snippet: Outer -->
            <A>
                <!-- snippet-skip -->
                <!-- snippet: Middle -->
                <B>
                    <!-- snippet: Inner -->
                    <C>
                        <!-- snippet-skip -->
                        <D />
                        <!-- /snippet-skip -->
                    </C>
                    <!-- /snippet -->
                </B>
                <!-- /snippet -->
                <!-- /snippet-skip -->
            </A>
            <!-- /snippet -->
            """, SnippetSyntax.Xaml);

        Assert.Empty(errors);
        Assert.Equal("<A>\n    …\n</A>", snippets.Single(s => s.Name == "Outer").Text);
        Assert.Equal("<B>\n    <C>\n        …\n    </C>\n</B>", snippets.Single(s => s.Name == "Middle").Text);
        Assert.Equal("<C>\n    …\n</C>", snippets.Single(s => s.Name == "Inner").Text);
    }

    [Fact]
    public void ASkipRegionOutsideASnippetProducesNothing()
    {
        var (snippets, errors) = SnippetExtractor.Extract("<!-- snippet-skip -->\n<A />\n<!-- /snippet-skip -->", SnippetSyntax.Xaml);

        Assert.Empty(snippets);
        Assert.Empty(errors);
    }

    [Fact]
    public void AnUnclosedSkipRegionIsAnError() =>
        Assert.Equal(1, Assert.Single(SnippetExtractor.Extract("<!-- snippet-skip -->\n<A />", SnippetSyntax.Xaml).Errors).Line);

    [Fact]
    public void ASampleRegionIsGeneratedAsASnippetsConstant() =>
        Assert.Equal("""<Border Classes="swatch" Width="64" Height="64" presto:Presence.Arrive="{x:Static local:XamlPage.FadeUp}" />""", Snippets.XamlStatic);
}
