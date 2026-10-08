using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Controls;
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

        Drag(window, SceneHarness.Center(feed.Children[0], window), new Vector(0, 200));
        TestFrames.Tick(150);

        Assert.Equal(11, feed.Children.Count);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ASwipedRowIsDismissedAndUndoBringsItBack()
    {
        var page = new GesturesPage();
        var window = SceneHarness.Show(page, 712, 440);
        var inbox = page.GetControl<ItemsControl>("Inbox");
        var first = inbox.Items[0];
        var row = inbox.ContainerFromIndex(0)!;

        Drag(window, SceneHarness.Center(row, window), new Vector(row.Bounds.Width * .6, 0));
        TestFrames.Tick(90);
        Assert.Equal(7, inbox.ItemCount);

        SceneHarness.Click(window, page.GetControl<Button>("UndoButton"));
        TestFrames.Tick(60);
        Assert.Equal(8, inbox.ItemCount);
        Assert.Same(first, inbox.Items[0]);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TappingTheHandleStepsTheCurtainToHalfThenFullWithTheContentLinked()
    {
        var page = new GesturesPage();
        var window = SceneHarness.Show(page, 712, 440);
        TestFrames.Tick(30);
        var curtain = page.GetControl<Sheet>("Curtain");
        var home = page.GetControl<StackPanel>("Home");
        var height = page.GetControl<Panel>("CurtainStage").Bounds.Height;
        Assert.Equal(28, curtain.Position.Value, 1);

        SceneHarness.Click(window, page.GetControl<Border>("Handle"));
        TestFrames.Tick(150);
        Assert.Equal(height / 2, curtain.Position.Value, 1);
        Assert.Equal(.3 * (height / 2 - 28), TranslateY(home), 1);

        SceneHarness.Click(window, page.GetControl<Border>("Handle"));
        TestFrames.Tick(150);
        Assert.Equal(height, curtain.Position.Value, 1);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void PullingTheCurtainHalfwaySinksShrinksAndDimsTheContentWithIt()
    {
        var page = new GesturesPage();
        var window = SceneHarness.Show(page, 712, 440);
        TestFrames.Tick(30);
        var curtain = page.GetControl<Sheet>("Curtain");
        var home = page.GetControl<StackPanel>("Home");
        var height = page.GetControl<Panel>("CurtainStage").Bounds.Height;
        var pulled = (height / 2 - 28) / (height - 28);

        Drag(window, SceneHarness.Center(page.GetControl<Border>("Handle"), window), new Vector(0, (height - 28) / 2));
        TestFrames.Tick(150);

        Assert.Equal(height / 2, curtain.Position.Value, 1);
        Assert.Equal(.3 * (height / 2 - 28), TranslateY(home), 1);
        Assert.Equal(1 - (.06 * pulled), Convert.ToDouble(PrestoAvalonia.Get(home, "Scale")), 3);
        Assert.Equal(pulled, curtain.GetVisualChildren().OfType<Border>().First().Opacity, 3);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AFlickUpCoastsTheSheetOpenPastItsStopAndBouncesBackOntoIt()
    {
        var page = new GesturesPage();
        var window = SceneHarness.Show(page, 712, 440);
        TestFrames.Tick(30);
        var sheet = page.GetControl<Sheet>("QueueSheet");
        Assert.Equal(52, sheet.Position.Value, 1);
        var content = sheet.Content!;
        var grab = content.TranslatePoint(new Point(content.Bounds.Width / 2, 14), window)!.Value;

        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 4; i++)
        {
            window.MouseMove(grab - new Vector(0, 40 * i));
            TestFrames.Tick();
        }
        window.MouseUp(grab - new Vector(0, 160), MouseButton.Left);
        var peak = 0d;
        for (var i = 0; i < 120; i++)
        {
            TestFrames.Tick();
            peak = Math.Max(peak, sheet.Position.Value);
        }

        Assert.True(peak > sheet.Bounds.Height + 1, $"the sheet peaked at {peak}");
        Assert.Equal(sheet.Bounds.Height, sheet.Position.Value, 1);
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
