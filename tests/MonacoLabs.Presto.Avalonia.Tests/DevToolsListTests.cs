using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MonacoLabs.Presto.DevTools;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto.Tests;

public sealed class DevToolsListTests : BridgeTestBase, IDisposable
{
    private readonly Border _box = new() { Name = "Hero", Width = 50, Height = 50 };
    private readonly Window _window = new() { Width = 900, Height = 500 };
    private readonly TickableList _list;

    public DevToolsListTests()
    {
        _window.Content = new Panel { Children = { _box } };
        _window.Show();
        PrestoDevTools.Open(_window);
        Dispatcher.UIThread.RunJobs();
        _list = OverlayLayer.GetOverlayLayer(_window)!.Children.OfType<DevToolsOverlay>().Single().List;
    }

    public void Dispose()
    {
        PrestoDevTools.Close(_window);
        _window.Close();
    }

    [AvaloniaFact]
    public void ListsRunningPausedAndUnplayedAnimations()
    {
        var running = Animate(new() { ["Opacity"] = 0, Duration = 1000 });
        var paused = Animate(new() { ["Width"] = 100, Duration = 1000 }).Pause();
        var unplayed = Animate(new() { ["Height"] = 100, Duration = 1000, Autoplay = false });
        Frames.Advance(100);

        _list.Refresh();

        Assert.Equal(TickableState.Running, RowOf(running).State);
        Assert.Equal(TickableState.Paused, RowOf(paused).State);
        Assert.Equal(TickableState.Paused, RowOf(unplayed).State);
        Assert.Equal("Hero", RowOf(running).Target);
        Assert.Equal("Visual.Opacity", RowOf(running).Detail);
        Assert.True(RowOf(running).Progress > 0);
    }

    [AvaloniaFact]
    public void ATimelineExpandsToItsChildrenAndTheirTweens()
    {
        var timeline = Presto.CreateTimeline(new TimelineParams())
            .Add(_box, new AvaloniaAnimationParams { ["Opacity"] = 0, Duration = 100 })
            .Add(_box, new AvaloniaAnimationParams { ["Width"] = 100, Duration = 100 });
        _list.Refresh();
        Assert.Single(_list.Items);

        RowOf(timeline).IsExpanded = true;
        _list.Refresh();
        var children = _list.Items.Skip(1).Where(r => r.Timer != null).ToList();
        Assert.Equal(2, children.Count);

        children[1].IsExpanded = true;
        _list.Refresh();
        var tween = Assert.Single(_list.Items, r => r.Tween != null);
        Assert.Equal("Layoutable.Width", tween.Label);
        Assert.Same(children[1], _list.Items[^2]);
    }

    [AvaloniaFact]
    public void BridgeOriginsAreHiddenUntilTheFilterIsOff()
    {
        Animation tagged;
        using (PrestoDiagnostics.Origin("Draggable")) tagged = Animate(new() { ["Opacity"] = 0, Duration = 1000 });
        _list.Refresh();
        Assert.DoesNotContain(_list.Items, r => r.Timer == tagged);

        _list.HideInternal.IsChecked = false;
        _list.Refresh();
        Assert.Equal("Draggable", RowOf(tagged).Origin);
    }

    [AvaloniaFact]
    public void TheSearchFiltersByPropertyName()
    {
        var opacity = Animate(new() { ["Opacity"] = 0, Duration = 1000 });
        Animate(new() { ["Width"] = 100, Duration = 1000 });

        _list.Search.Text = "opac";
        _list.Refresh();

        Assert.Same(opacity, Assert.Single(_list.Items).Timer);
    }

    [AvaloniaFact]
    public void ACompletedRootMovesToTheDimmedRecentlyEndedSection()
    {
        var shortOne = Animate(new() { ["Opacity"] = 0, Duration = 50 });
        var longOne = Animate(new() { ["Width"] = 100, Duration = 1000 });
        _list.Refresh();
        Assert.Same(shortOne, _list.Items[0].Timer);

        Frames.Advance(100, 16);
        _list.Refresh();

        Assert.Same(longOne, _list.Items[0].Timer);
        Assert.Same(shortOne, _list.Items[1].Timer);
        Assert.True(_list.Items[1].IsEnded);
        Assert.Equal(TickableState.Completed, _list.Items[1].State);
        Assert.False(_list.Items[0].IsEnded);
    }

    [AvaloniaFact]
    public void RefreshReusesRowObjects()
    {
        var animation = Animate(new() { ["Opacity"] = 0, Duration = 1000 });
        _list.Refresh();
        var row = RowOf(animation);
        row.IsExpanded = true;
        _list.Refresh();
        var tween = _list.Items[1];
        _list.Rows.SelectedItem = tween;

        Frames.Advance(100);
        _list.Refresh();

        Assert.Same(row, _list.Items[0]);
        Assert.Same(tween, _list.Items[1]);
        Assert.Same(animation, _list.SelectedTimer);
        Assert.Same(tween.Tween, _list.SelectedTween);
    }

    [AvaloniaFact]
    public void ALoopOnARemovedControlShowsDetachedUntilItIsReattached()
    {
        var loop = Animate(new() { ["Opacity"] = 0, Duration = 200, Loop = true });
        Frames.Advance(100, 16);
        var panel = (Panel)_box.Parent!;

        panel.Children.Remove(_box);
        _list.Refresh();
        Assert.Equal(TickableState.Detached, RowOf(loop).State);
        Assert.Equal("detached", RowOf(loop).StateText);

        panel.Children.Add(_box);
        Frames.Advance(100, 16);
        _list.Refresh();
        Assert.Equal(TickableState.Running, RowOf(loop).State);
    }

    [AvaloniaFact]
    public void AUserPausedRowStaysPausedWhenItsControlIsRemoved()
    {
        var paused = Animate(new() { ["Opacity"] = 0, Duration = 200, Loop = true }).Pause();

        ((Panel)_box.Parent!).Children.Remove(_box);
        _list.Refresh();

        Assert.Equal(TickableState.Paused, RowOf(paused).State);
    }

    [AvaloniaFact]
    public void ATimelineChildRowShowsItsParentsStateWhileTheParentIsNotRunning()
    {
        var timeline = Presto.CreateTimeline(new TimelineParams())
            .Add(_box, new AvaloniaAnimationParams { ["Opacity"] = 0, Duration = 100 })
            .Add(_box, new AvaloniaAnimationParams { ["Width"] = 100, Duration = 100 });
        Frames.Advance(50, 16);
        _list.Refresh();
        RowOf(timeline).IsExpanded = true;
        _list.Refresh();
        var second = _list.Items[2];
        Assert.NotEqual(TickableState.Paused, second.State);

        timeline.Pause();
        _list.Refresh();

        Assert.Equal(TickableState.Paused, _list.Items[1].State);
        Assert.Equal(TickableState.Paused, second.State);
    }

    [AvaloniaFact]
    public void TheStateFilterShowsOnlyRootsInTheChosenState()
    {
        var running = Animate(new() { ["Opacity"] = 0, Duration = 1000 });
        var paused = Animate(new() { ["Width"] = 100, Duration = 1000 }).Pause();
        Frames.Advance(100);

        _list.StateFilter.SelectedIndex = 1 + (int)TickableState.Paused;
        _list.Refresh();
        Assert.Same(paused, Assert.Single(_list.Items).Timer);

        _list.StateFilter.SelectedIndex = 1 + (int)TickableState.Running;
        _list.Refresh();
        Assert.Same(running, Assert.Single(_list.Items).Timer);

        _list.StateFilter.SelectedIndex = 0;
        _list.Refresh();
        Assert.Equal(2, _list.Items.Count);
    }

    private Animation Animate(AvaloniaAnimationParams parameters) => Presto.Animate(_box, parameters);

    private TickableRow RowOf(Timer timer) => Assert.Single(_list.Items, r => r.Timer == timer);
}
