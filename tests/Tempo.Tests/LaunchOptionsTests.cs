using Avalonia;

namespace Tempo.Tests;

public sealed class LaunchOptionsTests
{
    [Theory]
    [InlineData("--size", "360x780")]
    [InlineData("--size=360x780")]
    public void SizeParsesAsWidthByHeight(params string[] args)
    {
        Assert.True(LaunchOptions.TryParse(args, out var options, out var error));
        Assert.Null(error);
        Assert.Equal(new Size(360, 780), options.Size);
    }

    [Theory]
    [InlineData("--size", "360")]
    [InlineData("--size", "0x780")]
    [InlineData("--size")]
    [InlineData("--skip-intro=yes")]
    public void AMalformedArgumentFailsWithAnError(params string[] args)
    {
        Assert.False(LaunchOptions.TryParse(args, out _, out var error));
        Assert.NotNull(error);
    }
}
