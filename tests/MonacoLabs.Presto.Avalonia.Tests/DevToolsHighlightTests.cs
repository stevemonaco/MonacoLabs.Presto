using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using MonacoLabs.Presto.DevTools;

namespace MonacoLabs.Presto.Tests;

public sealed class DevToolsHighlightTests : BridgeTestBase, IDisposable
{
    private readonly Border _box = Box();
    private readonly Border _other = Box();
    private readonly Window _window = new() { Width = 900, Height = 500 };
    private readonly DevToolsOverlay _overlay;

    public DevToolsHighlightTests()
    {
        _box.Name = "Hero";
        _other.Margin = new Thickness(100, 0, 0, 0);
        _window.Content = new Panel { Children = { _box, _other } };
        _window.Show();
        PrestoDevTools.Open(_window);
        Dispatcher.UIThread.RunJobs();
        _overlay = OverlayLayer.GetOverlayLayer(_window)!.Children.OfType<DevToolsOverlay>().Single();
    }

    private TickableList List => _overlay.List;

    public void Dispose()
    {
        PrestoDevTools.Close(_window);
        _window.Close();
    }

    [AvaloniaFact]
    public void TheOutlineMatchesTheTargetsTransformedBoundsInTopLevelCoordinates()
    {
        var animation = Seeked(_box, new AvaloniaAnimationParams { ["X"] = 30, ["Y"] = 20, ["Scale"] = 2 }, 0.5);
        _window.UpdateLayout();
        List.Refresh();
        List.Rows.SelectedItem = RowOf(animation);

        _overlay.Highlighter.Update(_window, null, List.SelectedRow);

        Assert.Equal(new Rect(2.5, -2.5, 75, 75), Assert.Single(_overlay.Highlighter.Outlines));
    }

    [AvaloniaFact]
    public void HoveringARowAddsTheAdornerAndLeavingRemovesIt()
    {
        var animation = Presto.Animate(_box, new AvaloniaAnimationParams { ["Opacity"] = 0, Duration = 1000 });
        List.Refresh();
        _window.UpdateLayout();
        var layer = AdornerLayer.GetAdornerLayer(_box)!;

        _window.MouseMove(Center((Control)List.Rows.ContainerFromIndex(0)!, _window));
        Assert.Same(animation, List.HoveredRow?.Timer);
        _overlay.Highlighter.Update(_window, List.HoveredRow, List.SelectedRow);
        Assert.Contains(_overlay.Highlighter, layer.Children);

        _window.MouseMove(new Point(450, 10));
        Assert.Null(List.HoveredRow);
        _overlay.Highlighter.Update(_window, List.HoveredRow, List.SelectedRow);
        Assert.DoesNotContain(_overlay.Highlighter, layer.Children);
    }

    [AvaloniaFact]
    public void PickModeFiltersTheListToTheClickedControlsAnimationsIncludingPausedOnes()
    {
        var running = Presto.Animate(_box, new AvaloniaAnimationParams { ["Opacity"] = 0, Duration = 1000 });
        var paused = Presto.Animate(_box, new AvaloniaAnimationParams { ["X"] = 100, Duration = 1000 }).Pause();
        Presto.Animate(_other, new AvaloniaAnimationParams { ["Opacity"] = 0, Duration = 1000 });
        var reached = false;
        _box.PointerPressed += (_, _) => reached = true;

        List.Pick.Toggle.IsChecked = true;
        _window.MouseDown(Center(_box, _window), MouseButton.Left);
        _window.MouseUp(Center(_box, _window), MouseButton.Left);
        List.Refresh();

        Assert.False(reached);
        Assert.False(List.Pick.Toggle.IsChecked);
        Assert.Same(_box, List.Pick.Picked);
        Assert.Equal(new HashSet<Timer?> { running, paused }, List.Items.Select(row => row.Timer).ToHashSet());
    }

    [AvaloniaFact]
    public void TheSpringsPanelListsAnActiveSpring()
    {
        var source = Presto.CreateMotionValue();
        var follower = source.Spring(new Spring(stiffness: 100, damping: 10));
        source.Set(100);
        Frames.Advance(16);

        _overlay.Springs.Refresh();

        var row = Assert.IsType<Grid>(Assert.Single(_overlay.Springs.Children));
        var texts = row.Children.OfType<TextBlock>().Select(text => text.Text).ToList();
        Assert.Equal("spring", texts[0]);
        Assert.Equal("100", texts[2]);
        Assert.True(follower.IsAnimating);
    }

    [AvaloniaFact]
    public void TargetsThatDoNotResolveToAVisualGetNoOutline()
    {
        var brush = new SolidColorBrush(Colors.Red);
        var timeline = Presto.CreateTimeline(new TimelineParams())
            .Add(brush, new AvaloniaAnimationParams { ["Opacity"] = 0, Duration = 100 })
            .Add(_box, new AvaloniaAnimationParams { ["Opacity"] = 0, Duration = 100 });
        _window.UpdateLayout();
        List.Refresh();
        List.Rows.SelectedItem = RowOf(timeline);

        _overlay.Highlighter.Update(_window, null, List.SelectedRow);

        Assert.Equal(new Rect(0, 0, 50, 50), Assert.Single(_overlay.Highlighter.Outlines));
    }

    [AvaloniaFact]
    public void TheSpringsPanelDropsSettledRowsAndHidesWhenEmpty()
    {
        var source = Presto.CreateMotionValue();
        var follower = source.Spring(new Spring(stiffness: 100, damping: 10));
        source.Set(100);
        Frames.Advance(16);
        _overlay.Springs.Refresh();
        Assert.Single(_overlay.Springs.Children);
        Assert.True(_overlay.Springs.IsVisible);

        RunUntil(() => !follower.IsAnimating, max: 20000);
        _overlay.Springs.Refresh();

        Assert.False(follower.IsAnimating);
        Assert.Empty(_overlay.Springs.Children);
        Assert.False(_overlay.Springs.IsVisible);
    }

    private TickableRow RowOf(Timer timer) => Assert.Single(List.Items, r => r.Timer == timer);
}
