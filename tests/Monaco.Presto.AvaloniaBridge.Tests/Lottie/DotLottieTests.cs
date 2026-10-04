using System.IO.Compression;
using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge.Lottie;
using SkiaSharp;

namespace Monaco.Presto.AvaloniaBridge.Tests.Lottie;

/// <summary>A <see cref="LottieComposition"/> read from a dotLottie archive, or from JSON with embedded images.</summary>
public sealed class DotLottieTests : CompositorTestBase
{
    private const string Manifest = """{"version":"1","animations":[{"id":"badge"},{"id":"blank"}]}""";
    private const string Blank = """{"v":"5.7.0","fr":30,"ip":0,"op":60,"w":50,"h":50,"layers":[]}""";

    // A 100x100 composition showing a 40x40 image asset at (10, 10).
    private static string Badge(string asset) => $$$"""
        {"v":"5.7.0","fr":30,"ip":0,"op":30,"w":100,"h":100,"assets":[{{{asset}}}],"layers":[
        {"ty":2,"ind":1,"refId":"img","ip":0,"op":30,"st":0,
        "ks":{"o":{"a":0,"k":100},"r":{"a":0,"k":0},"p":{"a":0,"k":[10,10]},"a":{"a":0,"k":[0,0]},"s":{"a":0,"k":[100,100]}}
        }]}
        """;

    private static byte[] RedPng()
    {
        using var bitmap = new SKBitmap(40, 40);
        bitmap.Erase(SKColors.Red);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static MemoryStream Archive(string? manifest = Manifest)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, byte[] content)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(content);
            }

            if (manifest is not null) Add("manifest.json", Encoding.UTF8.GetBytes(manifest));
            Add("animations/badge.json", Encoding.UTF8.GetBytes(Badge("""{"id":"img","w":40,"h":40,"u":"/images/","p":"red.png","e":0}""")));
            Add("animations/blank.json", Encoding.UTF8.GetBytes(Blank));
            Add("images/red.png", RedPng());
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void TheFirstAnimationIsReadByDefaultAndTheManifestIdsAreListed()
    {
        var composition = LottieComposition.FromDotLottie(Archive());

        Assert.Equal(100, composition.Width);
        Assert.Equal(["badge", "blank"], composition.AnimationIds);
        Assert.Empty(LottieComposition.FromJson(Blank).AnimationIds);
    }

    [Fact]
    public void TheManifestsInitialAnimationIsReadByDefault()
    {
        var manifest = """{"animations":[{"id":"badge"},{"id":"blank"}],"initial":{"animation":"blank"}}""";

        Assert.Equal(50, LottieComposition.FromDotLottie(Archive(manifest)).Width);
    }

    [Fact]
    public void AnAnimationIsReadById() =>
        Assert.Equal(60, LottieComposition.FromDotLottie(Archive(), "blank").OutPoint);

    [Fact]
    public void FromStreamDetectsTheZipSignature() =>
        Assert.Equal(["badge", "blank"], LottieComposition.FromStream(Archive()).AnimationIds);

    [AvaloniaFact]
    public void AnArchiveImageIsDrawnWhereItsLayerSits()
    {
        var view = LottieViewTests.Place(new LottieView { Composition = LottieComposition.FromDotLottie(Archive()) });

        Assert.Equal(Colors.Black, Row(view, 30)[5]);
        Assert.Equal(Colors.Red, Row(view, 30)[10]);
        Assert.Equal(Colors.Red, Row(view, 30)[49]);
        Assert.Equal(Colors.Black, Row(view, 30)[50]);
        Assert.Equal(Colors.Black, Row(view, 60)[30]);
    }

    [AvaloniaFact]
    public void ADataUriImageInPlainJsonIsDrawn()
    {
        var uri = "data:image/png;base64," + Convert.ToBase64String(RedPng());
        var composition = LottieComposition.FromJson(Badge($$"""{"id":"img","w":40,"h":40,"u":"","p":"{{uri}}","e":1}"""));
        var view = LottieViewTests.Place(new LottieView { Composition = composition });

        Assert.Equal(Colors.Red, Row(view, 30)[30]);
        Assert.Equal(Colors.Black, Row(view, 60)[30]);
    }

    [Fact]
    public void AMissingManifestThrowsFormatException()
    {
        var error = Assert.Throws<FormatException>(() => LottieComposition.FromDotLottie(Archive(manifest: null)));

        Assert.Contains("manifest.json", error.Message);
    }

    [Fact]
    public void AnUnknownAnimationIdThrowsListingTheAvailableIds()
    {
        var error = Assert.Throws<ArgumentException>(() => LottieComposition.FromDotLottie(Archive(), "missing"));

        Assert.Contains("'missing'", error.Message);
        Assert.Contains("badge, blank", error.Message);
    }
}
