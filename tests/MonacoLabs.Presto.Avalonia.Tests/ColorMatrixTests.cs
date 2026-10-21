using Avalonia.Media;

namespace MonacoLabs.Presto.Tests;

/// <summary>Covers <see cref="ColorMatrix"/> against the Filter Effects Level 1 filter matrices (§13, filter functions).</summary>
public sealed class ColorMatrixTests
{
    private static void AssertMatrix(ReadOnlySpan<float> expected, ColorMatrix actual, float tolerance = 1e-5f)
    {
        for (var i = 0; i < 20; i++)
            Assert.True(Math.Abs(expected[i] - actual[i]) <= tolerance, $"element {i}: expected {expected[i]}, got {actual[i]}");
    }

    private static void AssertMatrix(ColorMatrix expected, ColorMatrix actual) => AssertMatrix(expected.AsSpan(), actual);

    private static (float R, float G, float B, float A) Apply(ColorMatrix m, float r, float g, float b, float a = 1)
    {
        float Row(int i) => m[i * 5] * r + m[i * 5 + 1] * g + m[i * 5 + 2] * b + m[i * 5 + 3] * a + m[i * 5 + 4];
        return (Row(0), Row(1), Row(2), Row(3));
    }

    [Fact]
    public void EachFactoryAtItsIdentityValueIsTheIdentity()
    {
        Assert.True(ColorMatrix.Brightness(1).IsIdentity);
        Assert.True(ColorMatrix.Contrast(1).IsIdentity);
        Assert.True(ColorMatrix.Saturate(1).IsIdentity);
        Assert.True(ColorMatrix.Grayscale(0).IsIdentity);
        Assert.True(ColorMatrix.Sepia(0).IsIdentity);
        Assert.True(ColorMatrix.HueRotate(0).IsIdentity);
        Assert.True(ColorMatrix.Invert(0).IsIdentity);
        Assert.True(ColorMatrix.Tint(0, Colors.Blue).IsIdentity);
        Assert.True(ColorMatrix.Duotone(0, Colors.Navy, Colors.Gold).IsIdentity);
        Assert.True(ColorMatrix.Compose(1, 1, 1, 0, 0, 0, 0, Colors.Blue, 0, Colors.Navy, Colors.Gold, 0).IsIdentity);
        Assert.False(ColorMatrix.Brightness(0.5).IsIdentity);
    }

    [Fact]
    public void FullStrengthFactoriesMatchTheSpecMatrices()
    {
        AssertMatrix([0.5f, 0, 0, 0, 0, 0, 0.5f, 0, 0, 0, 0, 0, 0.5f, 0, 0, 0, 0, 0, 1, 0], ColorMatrix.Brightness(0.5));
        AssertMatrix([2, 0, 0, 0, -0.5f, 0, 2, 0, 0, -0.5f, 0, 0, 2, 0, -0.5f, 0, 0, 0, 1, 0], ColorMatrix.Contrast(2));
        AssertMatrix([
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0, 0, 0, 1, 0], ColorMatrix.Grayscale(1));
        AssertMatrix([
            0.393f, 0.769f, 0.189f, 0, 0,
            0.349f, 0.686f, 0.168f, 0, 0,
            0.272f, 0.534f, 0.131f, 0, 0,
            0, 0, 0, 1, 0], ColorMatrix.Sepia(1));
        AssertMatrix([
            -0.574f, 1.430f, 0.144f, 0, 0,
            0.426f, 0.430f, 0.144f, 0, 0,
            0.426f, 1.430f, -0.856f, 0, 0,
            0, 0, 0, 1, 0], ColorMatrix.HueRotate(180));
        AssertMatrix([-1, 0, 0, 0, 1, 0, -1, 0, 0, 1, 0, 0, -1, 0, 1, 0, 0, 0, 1, 0], ColorMatrix.Invert(1));
    }

    [Fact]
    public void AFullHueTurnIsTheIdentity() => Assert.True(ColorMatrix.HueRotate(360).IsIdentity);

    [Fact]
    public void ThenAppliesTheReceiverFirstAndComposeRunsBrightnessBeforeInvert()
    {
        var brightnessThenInvert = ColorMatrix.Brightness(0.5).Then(ColorMatrix.Invert(1));
        var invertThenBrightness = ColorMatrix.Invert(1).Then(ColorMatrix.Brightness(0.5));

        Assert.Equal(0.5f, Apply(brightnessThenInvert, 1, 1, 1).R, 5);
        Assert.Equal(0f, Apply(invertThenBrightness, 1, 1, 1).R, 5);
        AssertMatrix(brightnessThenInvert, ColorMatrix.Compose(0.5, 1, 1, 0, 0, 0, 0, Colors.Blue, 0, Colors.Black, Colors.White, 1));
    }

    [Fact]
    public void FullTintMapsLuminanceOntoTheColor()
    {
        var (r, g, b, a) = Apply(ColorMatrix.Tint(1, Colors.Blue), 1, 0.5f, 0.25f);
        var luminance = 0.2126f * 1 + 0.7152f * 0.5f + 0.0722f * 0.25f;

        Assert.Equal(0f, r, 5);
        Assert.Equal(0f, g, 5);
        Assert.Equal(luminance, b, 5);
        Assert.Equal(1f, a, 5);
    }

    [Fact]
    public void FullDuotoneMapsBlackToTheShadowAndWhiteToTheHighlight()
    {
        var shadow = Color.FromRgb(20, 40, 120);
        var highlight = Color.FromRgb(250, 200, 60);
        var duotone = ColorMatrix.Duotone(1, shadow, highlight);

        AssertColor(shadow, Apply(duotone, 0, 0, 0));
        AssertColor(highlight, Apply(duotone, 1, 1, 1));
    }

    [Fact]
    public void TheMixingAmountsClampToZeroThroughOneAndTheMultipliersDoNot()
    {
        AssertMatrix(ColorMatrix.Grayscale(1), ColorMatrix.Grayscale(3));
        AssertMatrix(ColorMatrix.Sepia(1), ColorMatrix.Sepia(3));
        AssertMatrix(ColorMatrix.Tint(1, Colors.Blue), ColorMatrix.Tint(3, Colors.Blue));
        AssertMatrix(ColorMatrix.Duotone(1, Colors.Navy, Colors.Gold), ColorMatrix.Duotone(3, Colors.Navy, Colors.Gold));
        AssertMatrix(ColorMatrix.Invert(1), ColorMatrix.Invert(3));
        Assert.True(ColorMatrix.Grayscale(-1).IsIdentity);
        Assert.True(ColorMatrix.Sepia(-1).IsIdentity);
        Assert.True(ColorMatrix.Tint(-1, Colors.Blue).IsIdentity);
        Assert.True(ColorMatrix.Duotone(-1, Colors.Navy, Colors.Gold).IsIdentity);
        Assert.True(ColorMatrix.Invert(-1).IsIdentity);

        Assert.Equal(3f, ColorMatrix.Brightness(3)[0], 5);
        Assert.Equal(-0.5f, ColorMatrix.Brightness(-0.5)[0], 5);
        Assert.Equal(3f, ColorMatrix.Contrast(3)[0], 5);
        Assert.Equal(-1f, ColorMatrix.Contrast(3)[4], 5);
        Assert.Equal(0.2126f + 0.7874f * 3, ColorMatrix.Saturate(3)[0], 5);
        Assert.Equal(0.2126f + 0.7874f * -1, ColorMatrix.Saturate(-1)[0], 5);
    }

    private static void AssertColor(Color expected, (float R, float G, float B, float A) actual)
    {
        Assert.Equal(expected.R / 255f, actual.R, 5);
        Assert.Equal(expected.G / 255f, actual.G, 5);
        Assert.Equal(expected.B / 255f, actual.B, 5);
        Assert.Equal(1f, actual.A, 5);
    }
}