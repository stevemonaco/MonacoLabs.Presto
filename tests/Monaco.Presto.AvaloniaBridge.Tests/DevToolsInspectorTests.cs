using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.DevTools;

namespace Monaco.Presto.AvaloniaBridge.Tests;

public sealed class DevToolsInspectorTests : BridgeTestBase, IDisposable
{
    private readonly Border _box = new() { Name = "Hero", Width = 50, Height = 50 };
    private readonly Window _window = new() { Width = 900, Height = 500 };
    private readonly RootInspector _inspector;

    public DevToolsInspectorTests()
    {
        _window.Content = new Panel { Children = { _box } };
        _window.Show();
        PrestoDevTools.Open(_window);
        Dispatcher.UIThread.RunJobs();
        _inspector = OverlayLayer.GetOverlayLayer(_window)!.Children.OfType<DevToolsOverlay>().Single().Inspector;
    }

    public void Dispose()
    {
        PrestoDevTools.Close(_window);
        _window.Close();
    }

    [AvaloniaFact]
    public void ScrubbingAPlayingRootSeeksItAndResumesOnRelease()
    {
        var animation = Animate(new() { ["Opacity"] = 0, Duration = 1000, Ease = Eases.None });
        Frames.Advance(100);
        _inspector.Refresh(animation, null);

        Assert.True(_inspector.BeginScrub());
        _inspector.ScrubTo(500);
        Assert.True(animation.Paused);
        Assert.Equal(500, animation.CurrentTime);
        Assert.Equal(0.5, _box.Opacity, 3);

        _inspector.EndScrub();
        Assert.False(animation.Paused);
    }

    [AvaloniaFact]
    public void ScrubbingAPausedRootLeavesItPaused()
    {
        var animation = Animate(new() { ["Opacity"] = 0, Duration = 1000 }).Pause();
        _inspector.Refresh(animation, null);

        Assert.True(_inspector.BeginScrub());
        _inspector.ScrubTo(300);
        _inspector.EndScrub();

        Assert.True(animation.Paused);
        Assert.Equal(300, animation.CurrentTime);
    }

    [AvaloniaFact]
    public void SelectingATimelineChildSeeksOnlyTheRoot()
    {
        var timeline = Presto.CreateTimeline(new TimelineParams())
            .Add(_box, new AvaloniaAnimationParams { ["Opacity"] = 0, Duration = 100 })
            .Add(_box, new AvaloniaAnimationParams { ["Width"] = 100, Duration = 100, Ease = Eases.None });
        Timer second = null!;
        foreach (var child in timeline.Children) second = child;
        _inspector.Refresh(second, null);

        Assert.Same(timeline, _inspector.Root);
        Assert.Equal((100d, 200d), _inspector.Track.Highlight);

        _inspector.BeginScrub();
        _inspector.ScrubTo(150);
        _inspector.EndScrub();

        Assert.Equal(150, timeline.CurrentTime);
        Assert.Equal(50, second.CurrentTime);
        Assert.Equal(75, _box.Width, 3);
    }

    [AvaloniaFact]
    public void ScrubbingACompletedRootIsBlockedUntilReviveIsConfirmed()
    {
        var ended = Animate(new() { ["Opacity"] = 0, Duration = 50 });
        Frames.Advance(100, 16);
        var newer = Animate(new() { ["Opacity"] = 1, Duration = 1000 });
        _inspector.Refresh(ended, null);

        Assert.True(ended.Completed);
        Assert.True(_inspector.Revive.IsVisible);
        Assert.False(_inspector.BeginScrub());

        Click(_inspector.Revive);
        Assert.True(_inspector.Warning.IsVisible);
        Assert.Contains($"#{newer.Id}", _inspector.Warning.Text);
        Assert.False(_inspector.BeginScrub());

        Click(_inspector.Revive);
        _inspector.Refresh(ended, null);
        Assert.False(_inspector.Revive.IsVisible);
        Assert.True(_inspector.BeginScrub());
        _inspector.ScrubTo(25);
        Assert.Equal(25, ended.CurrentTime);
    }

    [AvaloniaTheory]
    [InlineData("98.9795918367347", "98.98")]
    [InlineData("-12.34567px", "-12.346px")]
    [InlineData("rgba(255, 0, 0, 1)", "rgba(255, 0, 0, 1)")]
    [InlineData("0.5 12.3456789 3", "0.5 12.346 3")]
    public void DisplayedValuesKeepAtMostThreeDecimals(string text, string expected) =>
        Assert.Equal(expected, TickableRow.Display(new TweenSnapshot(0, [], text)));

    [AvaloniaFact]
    public void TheTweenTableShowsLiveValuesAndTheEaseName()
    {
        var animation = Animate(new() { ["Opacity"] = 0, Duration = 1000, Ease = Eases.OutQuad });
        Frames.Advance(300);
        _inspector.Refresh(animation, null);

        var row = Assert.Single(_inspector.Tweens.Rows);
        var current = Convert.ToDouble(Presto.Get(_box, "Opacity"), CultureInfo.InvariantCulture);
        Assert.Equal($"1 → {current.ToString("0.###", CultureInfo.InvariantCulture)} → 0", row.Values.Text);
        Assert.Equal("outQuad", row.EaseName.Text);
        Assert.False(row.Marker.IsVisible);
    }

    [AvaloniaFact]
    public void TheOverriddenMarkerNamesTheNewerRoot()
    {
        var older = Animate(new() { ["Opacity"] = 0, ["Width"] = 100, Duration = 1000 });
        _inspector.Refresh(older, null);
        Assert.DoesNotContain(_inspector.Tweens.Rows, r => r.Marker.IsVisible);

        var newer = Animate(new() { ["Opacity"] = 1, Duration = 1000 });
        _inspector.Refresh(older, null);

        var row = Assert.Single(_inspector.Tweens.Rows, r => r.Marker.IsVisible);
        Assert.Equal("Visual.Opacity", row.Tween.Property);
        Assert.Equal($"overridden by #{newer.Id}", row.Marker.Text);
    }

    private Animation Animate(AvaloniaAnimationParams parameters) => Presto.Animate(_box, parameters);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
