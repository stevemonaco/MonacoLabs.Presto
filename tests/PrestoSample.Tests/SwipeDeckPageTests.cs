using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class SwipeDeckPageTests
{
    [AvaloniaFact]
    public void LikeDismissesTheTopCardAndPromotesTheNextOneToRest()
    {
        var page = new SwipeDeckPage();
        var window = SceneHarness.Show(page, 712, 440);
        var deck = page.GetControl<Panel>("Deck");

        SceneHarness.Click(window, page.GetControl<Button>("LikeButton"));
        TestFrames.Tick(90);

        Assert.Equal(3, deck.Children.Count);
        Assert.Same(Datasets.Places[1], Top(deck).Tag);
        Assert.Equal(1, Get(Top(deck), "Scale"), 3);
        Assert.Equal(0, Get(Top(deck), "Y"), 1);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UndoBringsTheLastCardBackToTheTop()
    {
        var page = new SwipeDeckPage();
        var window = SceneHarness.Show(page, 712, 440);
        var deck = page.GetControl<Panel>("Deck");
        SceneHarness.Click(window, page.GetControl<Button>("LikeButton"));
        TestFrames.Tick(90);

        SceneHarness.Click(window, page.GetControl<Button>("UndoButton"));
        TestFrames.Tick(90);

        Assert.Equal(3, deck.Children.Count);
        Assert.Same(Datasets.Places[0], Top(deck).Tag);
        Assert.Equal(0, Get(Top(deck), "X"), 1);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void DraggingTheTopCardSixtyPixelsRightHalfStampsIt()
    {
        var page = new SwipeDeckPage();
        var window = SceneHarness.Show(page, 712, 440);
        var top = Top(page.GetControl<Panel>("Deck"));
        var grab = SceneHarness.Centre(top, window);

        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 6; i++)
        {
            window.MouseMove(grab.WithX(grab.X + (i * 10)));
            TestFrames.Tick();
        }
        TestFrames.Tick(4);

        var like = top.GetLogicalDescendants().OfType<Border>().Single(border => border.Classes.Contains("like"));
        Assert.Equal(.5, like.Opacity, 3);
        window.MouseUp(grab.WithX(grab.X + 60), MouseButton.Left);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void DraggingTheTopCardLeansItAndReleaseSpringsTheLeanBack()
    {
        var page = new SwipeDeckPage();
        var window = SceneHarness.Show(page, 712, 440);
        var top = Top(page.GetControl<Panel>("Deck"));
        var grab = SceneHarness.Centre(top, window);

        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 6; i++)
        {
            window.MouseMove(grab.WithX(grab.X + (i * 10)));
            TestFrames.Tick();
        }
        TestFrames.Tick(4);

        Assert.InRange(Lean(top), 1, 8);
        window.MouseUp(grab.WithX(grab.X + 60), MouseButton.Left);
        TestFrames.Tick(120);

        Assert.Equal(0, Lean(top), 2);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ManyDismissesLeaveExactlyThreeCards()
    {
        var page = new SwipeDeckPage();
        var window = SceneHarness.Show(page, 712, 440);
        var deck = page.GetControl<Panel>("Deck");

        for (var i = 0; i < 25; i++)
        {
            SceneHarness.Click(window, page.GetControl<Button>(i % 2 == 0 ? "LikeButton" : "NopeButton"));
            TestFrames.Tick(3);
        }
        TestFrames.Tick(90);

        Assert.Equal(3, deck.Children.Count);
        Assert.Same(Datasets.Places[5], Top(deck).Tag);
        SceneHarness.Close(window);
    }

    private static Border Top(Panel deck) => (Border)deck.Children[^1];

    private static double Get(Border card, string property) => Convert.ToDouble(PrestoAvalonia.Get(card, property));

    private static double Lean(Border card) => ((TransformGroup)card.RenderTransform!).Children.OfType<Rotate3DTransform>().Single().AngleY;
}
