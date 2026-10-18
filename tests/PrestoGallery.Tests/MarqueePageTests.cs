using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Controls;
using PrestoGallery.Showcase;
using MarqueePage = PrestoGallery.Showcase.Pages.MarqueePage;

namespace PrestoGallery.Tests;

public sealed class MarqueePageTests
{
    [AvaloniaFact]
    public void TheLogoWallScrolls()
    {
        var page = new MarqueePage();
        var window = SceneHarness.Show(page, 1100, 700);
        var before = Offset(page, "Logos");

        TestFrames.Tick(30);

        Assert.True(Offset(page, "Logos") < before - 10, $"the wall moved from {before} to {Offset(page, "Logos")}");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ThePauseToggleSetsIsPausedAndTheWallSettles()
    {
        var page = new MarqueePage();
        var window = SceneHarness.Show(page, 1100, 700);

        SceneHarness.Click(window, page.GetControl<ToggleButton>("Pause"));
        TestFrames.Tick(60);
        var settled = Offset(page, "Logos");
        TestFrames.Tick(30);

        Assert.True(page.GetControl<Marquee>("Logos").IsPaused);
        Assert.Equal(settled, Offset(page, "Logos"));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ClickingANameInACopyUpdatesTheReadout()
    {
        var page = new MarqueePage();
        var window = SceneHarness.Show(page, 1100, 700);
        var wall = page.GetControl<Marquee>("Logos");
        var buttons = Strip(page, "Logos").Children[1].GetVisualDescendants().OfType<Button>();
        page.GetControl<Slider>("SpeedSlider").Value = 240;
        Button? shown = null;
        for (var i = 0; i < 600 && shown is null; i++)
        {
            TestFrames.Tick();
            shown = buttons.FirstOrDefault(button => SceneHarness.Center(button, window).X is var x && x > 100 && x < wall.Bounds.Width - 100);
        }

        SceneHarness.Click(window, shown!);

        Assert.Equal($"Picked {((Datasets.Album)shown!.DataContext!).Artist}", page.GetControl<TextBlock>("Picked").Text);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheOverflowTitleRestsWhileShortAndScrollsOnceLong()
    {
        var page = new MarqueePage();
        var window = SceneHarness.Show(page, 1100, 700);

        TestFrames.Tick(30);
        Assert.Equal(0, Offset(page, "NowPlaying"));
        Assert.Single(Strip(page, "NowPlaying").Children);

        SceneHarness.Click(window, page.GetControl<ToggleButton>("LongTitle"));
        TestFrames.Tick(30);

        Assert.True(Offset(page, "NowPlaying") < -10);
        Assert.True(Strip(page, "NowPlaying").Children.Count > 1);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionNothingMoves()
    {
        using var _ = ReducedMotion.On();
        var page = new MarqueePage();
        var window = SceneHarness.Show(page, 1100, 700);
        SceneHarness.Click(window, page.GetControl<ToggleButton>("LongTitle"));

        TestFrames.Tick(60);

        Assert.All(["Logos", "Ticker", "NowPlaying"], name =>
        {
            Assert.Equal(0, Offset(page, name));
            Assert.Single(Strip(page, name).Children);
        });
        SceneHarness.Close(window);
    }

    private static Panel Strip(MarqueePage page, string name) =>
        (Panel)page.GetControl<Marquee>(name).GetVisualChildren().Single();

    private static double Offset(MarqueePage page, string name)
    {
        var marquee = page.GetControl<Marquee>(name);
        var shorthand = marquee.Direction is MarqueeDirection.Up or MarqueeDirection.Down ? "Y" : "X";
        return Convert.ToDouble(PrestoAvalonia.Get(Strip(page, name), shorthand));
    }
}
