using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Headless.XUnit;
using MonacoLabs.Presto.Lottie;

namespace MonacoLabs.Presto.Tests.Lottie;

/// <summary>A <see cref="LottieComposition"/> reading size, timing and markers from Lottie JSON.</summary>
public sealed class LottieCompositionTests
{
    private const string ToggleUri = "avares://MonacoLabs.Presto.Avalonia.Tests/Lottie/toggle.json";

    private static string Json(string markers = "") =>
        $$"""{"v":"5.7.0","fr":30,"ip":5,"op":65,"w":120,"h":80,"layers":[]{{markers}}}""";

    [Fact]
    public void MetadataComesFromTheTopLevelFields()
    {
        var composition = LottieComposition.FromJson(Json());

        Assert.Equal(120, composition.Width);
        Assert.Equal(80, composition.Height);
        Assert.Equal(new Size(120, 80), composition.Size);
        Assert.Equal(30, composition.FrameRate);
        Assert.Equal(5, composition.InPoint);
        Assert.Equal(65, composition.OutPoint);
        Assert.Equal(2000, composition.Duration);
    }

    [AvaloniaFact]
    public void LoadsAnAvaloniaAsset()
    {
        var composition = LottieComposition.Load(new Uri(ToggleUri));

        Assert.Equal(new Size(100, 100), composition.Size);
        Assert.Equal(["hover", "on", "off"], composition.Segments.Select(s => s.Name));
    }

    [Fact]
    public void LoadsAFileUriOrPathWithAByteOrderMark()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, Json(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        try
        {
            Assert.Equal(120, LottieComposition.Load(new Uri(path)).Width);
            var relative = Path.GetRelativePath(Environment.CurrentDirectory, path);
            Assert.Equal(120, LottieComposition.Load(new Uri(relative, UriKind.Relative)).Width);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void TheTypeConverterLoadsAUriString()
    {
        var converter = TypeDescriptor.GetConverter(typeof(LottieComposition));

        Assert.True(converter.CanConvertFrom(typeof(string)));
        var composition = Assert.IsType<LottieComposition>(converter.ConvertFrom(ToggleUri));
        Assert.Equal(3, composition.Segments.Count);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"w":"wide","h":10,"fr":30,"ip":0,"op":10,"layers":[]}""")]
    public void InvalidInputThrowsFormatException(string json) =>
        Assert.Throws<FormatException>(() => LottieComposition.FromJson(json));
}
