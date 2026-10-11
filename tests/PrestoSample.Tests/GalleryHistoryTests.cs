using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using PrestoSample.Pages;
using PrestoSample.Showcase.Pages;
using PrestoSample.ViewModels;
using static PrestoSample.Tests.NavScene;

namespace PrestoSample.Tests;

/// <summary>The gallery's back stack in <see cref="MainViewModel"/> and its back keys in both layouts.</summary>
public sealed class GalleryHistoryTests
{
    [AvaloniaFact]
    public void WideHistoryPushesEveryChangeAndKeepsTheLast50()
    {
        var gallery = new MainViewModel { IsCompact = false };
        List<GalleryPage> opened = [gallery.SelectedPage!];
        for (var i = 1; i <= 60; i++)
        {
            gallery.SelectedPage = NavScene.Pages[i % 3];
            opened.Add(NavScene.Pages[i % 3]);
        }

        for (var i = 1; i <= 50; i++)
        {
            gallery.GoBack();
            Assert.Same(opened[^(i + 1)], gallery.SelectedPage);
            Assert.True(gallery.IsBack);
        }
        Assert.False(gallery.CanGoBack);
        var last = gallery.CurrentPage;
        gallery.GoBack();
        Assert.Same(opened[10], gallery.SelectedPage);
        Assert.Same(last, gallery.CurrentPage);
        Assert.IsType<PlaybackSettingsPage>(gallery.CurrentPage);

        gallery.SelectedPage = NavScene.Pages[2];
        Assert.False(gallery.IsBack);
    }

    [AvaloniaFact]
    public void ACompactStartWithAStartPageOpensIt()
    {
        var gallery = new MainViewModel(Page("Eases")) { IsCompact = true };
        Assert.False(gallery.AtHome);
        Assert.IsType<EasesPage>(gallery.CurrentPage);
    }

    [AvaloniaFact]
    public void OpeningFromHomeStartsAFreshStack()
    {
        var gallery = new MainViewModel { IsCompact = true };
        gallery.SelectedPage = Page("Eases");
        gallery.OpenCommand.Execute("Playground");
        gallery.GoBack();
        gallery.GoBack();

        gallery.SelectedPage = Page("Springs");
        Assert.False(gallery.AtHome);
        Assert.False(gallery.IsBack);

        gallery.GoBack();
        Assert.True(gallery.AtHome);
        Assert.Same(Page("Springs"), gallery.SelectedPage);
    }

    [AvaloniaFact]
    public void AltLeftFromTheNavOrThePageAndTheMouseBackButtonGoBackReversed() => With((window, gallery, nav) =>
    {
        var host = window.GetLogicalDescendants().OfType<TransitioningContentControl>().Single(control => control.Name == "PageHost");
        SceneHarness.Click(window, Container(nav, Page("Callbacks")));
        SceneHarness.Click(window, Container(nav, Page("Keyframes")));
        Assert.True(nav.IsKeyboardFocusWithin);

        window.KeyPress(Key.Left, RawInputModifiers.Alt, PhysicalKey.ArrowLeft, null);
        TestFrames.Tick();
        Assert.Same(Page("Callbacks"), gallery.SelectedPage);
        Assert.True(host.IsTransitionReversed);

        gallery.SelectedPage = Page("Springs");
        TestFrames.Tick();
        Assert.False(host.IsTransitionReversed);
        Assert.True(gallery.CurrentPage!.GetLogicalDescendants().OfType<Button>().First().Focus());
        window.KeyPress(Key.Left, RawInputModifiers.Alt, PhysicalKey.ArrowLeft, null);
        TestFrames.Tick();
        Assert.Same(Page("Callbacks"), gallery.SelectedPage);
        Assert.True(host.IsTransitionReversed);

        gallery.SelectedPage = Page("Springs");
        TestFrames.Tick();
        var title = window.GetLogicalDescendants().OfType<TextBlock>().First(block => block.Text == "Springs" && block.FontSize == 22);
        window.MouseDown(SceneHarness.Center(title, window), MouseButton.XButton1);
        window.MouseUp(SceneHarness.Center(title, window), MouseButton.XButton1);
        TestFrames.Tick();
        Assert.Same(Page("Callbacks"), gallery.SelectedPage);
        Assert.True(host.IsTransitionReversed);
    });

    [AvaloniaFact]
    public void InCompactEachAltLeftOrMouseBackPressPopsOneLevel() => With((window, gallery, _) =>
    {
        gallery.OpenFromNav(Page("Eases"));
        gallery.OpenCommand.Execute("Playground");
        gallery.SelectedPage = Page("Springs");
        TestFrames.Tick(30);

        window.KeyPress(Key.Left, RawInputModifiers.Alt, PhysicalKey.ArrowLeft, null);
        TestFrames.Tick();
        Assert.Same(Page("Playground"), gallery.SelectedPage);

        var back = SceneHarness.Center(window.GetLogicalDescendants().OfType<Button>().Single(button => button.Name == "Back"), window);
        window.MouseDown(back, MouseButton.XButton1);
        window.MouseUp(back, MouseButton.XButton1);
        TestFrames.Tick();
        Assert.Same(Page("Eases"), gallery.SelectedPage);
        Assert.False(gallery.AtHome);
    }, width: 400, height: 850);

    [AvaloniaFact]
    public void TheBoardKeepsAltLeftForItsFocusedPriority() => With((window, gallery, _) =>
    {
        gallery.SelectedPage = Page("Board");
        TestFrames.Tick(10);
        var priorities = window.GetLogicalDescendants().OfType<ListBox>().Single(list => list.Name == "Priorities");
        var second = priorities.Items[1];
        priorities.ContainerFromIndex(1)!.Focus();

        window.KeyPress(Key.Left, RawInputModifiers.Alt, PhysicalKey.ArrowLeft, null);
        TestFrames.Tick();

        Assert.Same(Page("Board"), gallery.SelectedPage);
        Assert.Same(second, priorities.Items[0]);
    });
}
