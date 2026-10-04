using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class GesturesPageTests
{
    [AvaloniaFact]
    public void APullPastTheThresholdRefreshesTheFeedWithThreeRows()
    {
        var page = new GesturesPage();
        var window = SceneHarness.Show(page, 712, 440);
        var feed = page.GetControl<StackPanel>("Feed");

        Drag(window, SceneHarness.Centre(feed.Children[0], window), new Vector(0, 200));
        TestFrames.Tick(150);

        Assert.Equal(11, feed.Children.Count);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ASwipedRowIsDismissedAndUndoBringsItBack()
    {
        var page = new GesturesPage();
        var window = SceneHarness.Show(page, 712, 440);
        var inbox = page.GetControl<StackPanel>("Inbox");
        var first = inbox.Children[0];

        Drag(window, SceneHarness.Centre(first, window), new Vector(first.Bounds.Width * .6, 0));
        TestFrames.Tick(90);
        Assert.Equal(7, inbox.Children.Count);

        SceneHarness.Click(window, page.GetControl<Button>("UndoButton"));
        TestFrames.Tick(60);
        Assert.Equal(8, inbox.Children.Count);
        Assert.Same(first, inbox.Children[0]);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TappingTheHandleStepsTheCurtainToHalfThenFullWithTheContentLinked()
    {
        var page = new GesturesPage();
        var window = SceneHarness.Show(page, 712, 440);
        TestFrames.Tick(30);
        var shade = page.GetControl<Border>("Shade");
        var home = page.GetControl<StackPanel>("Home");
        var travel = page.GetControl<Panel>("CurtainStage").Bounds.Height - 28;
        Assert.Equal(-travel, TranslateY(shade), 1);

        SceneHarness.Click(window, page.GetControl<Border>("Handle"));
        TestFrames.Tick(60);
        Assert.Equal(-travel / 2, TranslateY(shade), 1);
        Assert.Equal(.3 * travel / 2, TranslateY(home), 1);

        SceneHarness.Click(window, page.GetControl<Border>("Handle"));
        TestFrames.Tick(60);
        Assert.Equal(0, TranslateY(shade), 1);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void PullingTheCurtainHalfwaySinksShrinksAndDimsTheContentByHalf()
    {
        var page = new GesturesPage();
        var window = SceneHarness.Show(page, 712, 440);
        TestFrames.Tick(30);
        var home = page.GetControl<StackPanel>("Home");
        var travel = page.GetControl<Panel>("CurtainStage").Bounds.Height - 28;

        Drag(window, SceneHarness.Centre(page.GetControl<Border>("Handle"), window), new Vector(0, travel / 2));
        TestFrames.Tick(60);

        Assert.Equal(-travel / 2, TranslateY(page.GetControl<Border>("Shade")), 1);
        Assert.Equal(.3 * travel / 2, TranslateY(home), 1);
        Assert.Equal(.97, Convert.ToDouble(PrestoAvalonia.Get(home, "Scale")), 3);
        Assert.Equal(.3, page.GetControl<Border>("Scrim").Opacity, 3);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AFlickUpCoastsTheSheetOpenPastItsStopAndBouncesBackOntoIt()
    {
        var page = new GesturesPage();
        var window = SceneHarness.Show(page, 712, 440);
        TestFrames.Tick(30);
        var sheet = page.GetControl<Border>("Sheet");
        Assert.Equal(sheet.Bounds.Height - 28, TranslateY(sheet), 1);
        var grab = sheet.TranslatePoint(new Point(sheet.Bounds.Width / 2, 14), window)!.Value;

        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 4; i++)
        {
            window.MouseMove(grab - new Vector(0, 40 * i));
            TestFrames.Tick();
        }
        window.MouseUp(grab - new Vector(0, 160), MouseButton.Left);
        var peak = double.MaxValue;
        for (var i = 0; i < 120; i++)
        {
            TestFrames.Tick();
            peak = Math.Min(peak, TranslateY(sheet));
        }

        Assert.True(peak < -1, $"the sheet peaked at {peak}");
        Assert.Equal(0, TranslateY(sheet), 1);
        SceneHarness.Close(window);
    }

    private static double TranslateY(Control control) => Convert.ToDouble(PrestoAvalonia.Get(control, "Y"));

    // Held still before letting go, so the release carries no throw and the snap alone decides where it lands.
    private static void Drag(Window window, Point grab, Vector by)
    {
        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 10; i++)
        {
            window.MouseMove(grab + (by * i / 10));
            TestFrames.Tick();
        }
        for (var i = 0; i < 8; i++)
        {
            window.MouseMove(grab + by);
            TestFrames.Tick();
        }
        window.MouseUp(grab + by, MouseButton.Left);
        TestFrames.Tick();
    }
}
