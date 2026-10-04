using Avalonia.Headless.XUnit;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class DevToolsPageTests
{
    [AvaloniaFact]
    public void TheSubjectsRunExceptTheParkedOneAndRevertOnDetach()
    {
        var page = new DevToolsPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(100);

        Assert.True(page.Sequence!.CurrentTime > 0);
        Assert.True(page.Wave!.CurrentTime > 0);
        Assert.NotEqual(0, page.Bounce!.Get("X"));
        Assert.True(page.Parked!.Paused);
        Assert.Equal(0, page.Parked.CurrentTime);

        SceneHarness.Close(window);

        Assert.True(page.Sequence.Cancelled);
        Assert.True(page.Wave.Cancelled);
    }
}
