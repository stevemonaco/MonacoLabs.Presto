using PrestoGallery;

namespace PrestoGallery.Tests;

public sealed class LaunchOptionsTests
{
    [Theory]
    [InlineData(new[] { "--swapchain", "composition" }, true)]
    [InlineData(new[] { "--swapchain=default" }, false)]
    [InlineData(new string[0], false)]
    public void SwapChainSelectsTheCompositionSwapChainOrLeavesTheDefault(string[] args, bool composition)
    {
        Assert.True(LaunchOptions.TryParse(args, out var options, out _, out var error), error);

        Assert.Equal(composition, options.CompositionSwapChain);
    }

    [Fact]
    public void AnUnknownSwapChainIsAnError()
    {
        Assert.False(LaunchOptions.TryParse(["--swapchain", "flip"], out _, out _, out var error));

        Assert.Contains("composition or default", error);
    }
}
