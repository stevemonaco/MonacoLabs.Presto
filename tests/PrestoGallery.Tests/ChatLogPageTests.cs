using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using MonacoLabs.Presto;
using PrestoGallery.Showcase.Pages;

namespace PrestoGallery.Tests;

public sealed class ChatLogPageTests
{
    [AvaloniaFact]
    public void AtTheEndNewMessagesKeepTheNewestInView()
    {
        var (window, page) = Loaded();
        var scroll = Scroller(page);
        Assert.Equal(scroll.ScrollBarMaximum.Y, scroll.Offset.Y, 1);

        for (var i = 0; i < 3; i++)
        {
            NextMessage(page);
            TestFrames.Tick(EaseFrames);

            Assert.Equal(scroll.ScrollBarMaximum.Y, scroll.Offset.Y, 1);
            var newest = Log(page).ContainerFromIndex(page.ViewModel.Messages.Count - 1)!;
            Assert.Equal(scroll.Viewport.Height, newest.TranslatePoint(new Point(0, newest.Bounds.Height), scroll)!.Value.Y, 1);
        }
        Assert.False(Pill(page).IsVisible);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AfterAWheelUpNewMessagesLeaveTheOffsetAloneAndThePillShows()
    {
        var (window, page) = Loaded();
        var scroll = WheelUp(window, page);
        var offset = scroll.Offset;

        for (var i = 0; i < 3; i++) page.ViewModel.Receive();
        TestFrames.Tick(EaseFrames);

        Assert.Equal(offset, scroll.Offset);
        Assert.True(scroll.Offset.Y < scroll.ScrollBarMaximum.Y - 100);
        Assert.True(Pill(page).IsVisible);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ThePillLandsAtTheEndAndReArms()
    {
        var (window, page) = Loaded();
        var scroll = WheelUp(window, page);
        // The pill aims at the last message when clicked, so click between two timer messages.
        NextMessage(page);
        TestFrames.Tick(30);

        SceneHarness.Click(window, Pill(page));
        TestFrames.Tick(EaseFrames);
        Assert.Equal(scroll.ScrollBarMaximum.Y, scroll.Offset.Y, 1);
        Assert.False(page.ViewModel.Behind);

        NextMessage(page);
        TestFrames.Tick(EaseFrames);
        Assert.Equal(scroll.ScrollBarMaximum.Y, scroll.Offset.Y, 1);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AMessageLandingMidJumpRetargetsThePillToTheNewEnd()
    {
        var (window, page) = Loaded();
        var scroll = WheelUp(window, page);
        NextMessage(page);
        TestFrames.Tick(30);

        SceneHarness.Click(window, Pill(page));
        TestFrames.Tick(3);
        Assert.True(scroll.Offset.Y < scroll.ScrollBarMaximum.Y - 100);
        page.ViewModel.Receive();
        TestFrames.Tick(EaseFrames);

        Assert.Equal(scroll.ScrollBarMaximum.Y, scroll.Offset.Y, 1);
        Assert.False(page.ViewModel.Behind);
        SceneHarness.Close(window);
    }

    private static int EaseFrames => (int)Math.Ceiling(Recipes.Tokens.Long / 16) + 2;

    private static void NextMessage(ChatLogPage page)
    {
        var count = page.ViewModel.Messages.Count;
        for (var i = 0; i < 400 && page.ViewModel.Messages.Count == count; i++) TestFrames.Tick();
        Assert.Equal(count + 1, page.ViewModel.Messages.Count);
    }

    private static ScrollViewer WheelUp(Window window, ChatLogPage page)
    {
        for (var i = 0; i < 5; i++)
        {
            window.MouseWheel(SceneHarness.Center(Log(page), window), new Vector(0, 3));
            TestFrames.Tick();
        }
        TestFrames.Tick(30);
        return Scroller(page);
    }

    private static (Window Window, ChatLogPage Page) Loaded()
    {
        var page = new ChatLogPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(EaseFrames);
        return (window, page);
    }

    private static ListBox Log(ChatLogPage page) => page.GetControl<ListBox>("Log");

    private static ScrollViewer Scroller(ChatLogPage page) => (ScrollViewer)Log(page).Scroll!;

    private static Button Pill(ChatLogPage page) => page.GetControl<Button>("JumpPill");
}
