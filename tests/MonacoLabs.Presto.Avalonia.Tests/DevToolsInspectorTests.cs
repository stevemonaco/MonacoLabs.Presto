using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using MonacoLabs.Presto.DevTools;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto.Tests;

public sealed class DevToolsInspectorTests : BridgeTestBase, IDisposable
{
    private readonly Border _box = new() { Name = "Hero", Width = 50, Height = 50 };
    private readonly Window _window = new() { Width = 900, Height = 500 };
    private readonly RootInspector _inspector;
    private readonly TickableList _list;

    public DevToolsInspectorTests()
    {
        _window.Content = new Panel { Children = { _box } };
        _window.Show();
        PrestoDevTools.Open(_window);
        Dispatcher.UIThread.RunJobs();
        var overlay = OverlayLayer.GetOverlayLayer(_window)!.Children.OfType<DevToolsOverlay>().Single();
        _inspector = overlay.Inspector;
        _list = overlay.List;
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
            .Add(_box, new AvaloniaAnimationParams { ["Width"] = 98, Duration = 100, Ease = Eases.None });
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
        Assert.Equal(74, _box.Width);
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
    public void OnlyTweenRowsInViewTakeValueSnapshots()
    {
        var boxes = Enumerable.Range(0, 40).Select(_ => new Border { Width = 10, Height = 10 }).ToArray();
        ((Panel)_window.Content!).Children.AddRange(boxes);
        var animation = Presto.Animate(boxes, new AvaloniaAnimationParams { ["Opacity"] = 0, Duration = 1000, Ease = Eases.None });
        _inspector.Refresh(animation, null);
        _window.UpdateLayout();
        var first = _inspector.Tweens.Rows.First();
        var last = _inspector.Tweens.Rows.Last();
        var (firstBefore, lastBefore) = (first.Values.Text, last.Values.Text);

        Frames.Advance(500);
        _inspector.Refresh(animation, null);

        Assert.NotEqual(firstBefore, first.Values.Text);
        Assert.Equal(lastBefore, last.Values.Text);
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

    [AvaloniaFact]
    public void TheHeaderShowsTheRootsIdTimeAndDurationWithInfinityForAnEndlessLoop()
    {
        var once = Animate(new() { ["Opacity"] = 0, Duration = 1000, Autoplay = false }).Seek(250);
        var endless = Animate(new() { ["Width"] = 100, Duration = 200, Loop = true, Autoplay = false }).Seek(250);

        _inspector.Refresh(once, null);
        Assert.Equal($"#{once.Id}  250 / 1000 ms", TimeText);

        _inspector.Refresh(endless, null);
        Assert.Equal($"#{endless.Id}  250 / ∞ ms", TimeText);
    }

    [AvaloniaFact]
    public void ThePlaybackButtonsCallTheRootsMethods()
    {
        var animation = Animate(new() { ["Opacity"] = 0, Duration = 1000 });
        Frames.Advance(300);
        _inspector.Refresh(animation, null);

        Click(_inspector.Pause);
        Assert.True(animation.Paused);

        Click(_inspector.Play);
        Assert.False(animation.Paused);

        Click(_inspector.Reverse);
        Assert.True(animation.Reversed);

        Click(_inspector.Restart);
        Assert.Equal(0, animation.CurrentTime);
    }

    [AvaloniaFact]
    public void EnterInTheSeekAndSpeedBoxesSeeksAndSetsAPositiveSpeedInInvariantCulture()
    {
        var animation = Animate(new() { ["Opacity"] = 0, Duration = 1000 }).Pause();
        animation.Speed = 1.5;
        _inspector.Refresh(animation, null);
        Assert.Equal("1.5", _inspector.Speed.Text);
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            Enter(_inspector.Seek, "250.5");
            Assert.Equal(250.5, animation.CurrentTime, 6);

            Enter(_inspector.Speed, "0");
            Enter(_inspector.Speed, "-2");
            Assert.Equal(1.5, animation.Speed);

            Enter(_inspector.Speed, "2.5");
            Assert.Equal(2.5, animation.Speed);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [AvaloniaFact]
    public void RevertAsksForConfirmationInAFlyout()
    {
        var animation = Animate(new() { ["Opacity"] = 0, Duration = 1000 });
        Frames.Advance(500);
        Select(animation);

        var revert = Center(_inspector.Revert, _window);
        _window.MouseDown(revert, MouseButton.Left);
        _window.MouseUp(revert, MouseButton.Left);
        Assert.True(_inspector.Revert.Flyout!.IsOpen);
        Assert.False(animation.Canceled);

        Click(_inspector.ConfirmRevert);
        Assert.True(animation.Canceled);
        Assert.Equal(1, _box.Opacity);
        Assert.False(_inspector.Revert.Flyout.IsOpen);
    }

    [AvaloniaFact]
    public void TheCreationTraceShowsItsFirstAppFrameWithTheWholeTraceAsTooltip()
    {
        var untraced = Animate(new() { ["Width"] = 100, Duration = 1000 });
        _inspector.Refresh(untraced, null);
        Assert.False(_inspector.CreatedAt.IsVisible);

        PrestoDiagnostics.CaptureCreationTrace = true;
        var traced = PrestoTestApp.AppCode.CreateTimer();
        _inspector.Refresh(traced, null);

        Assert.True(_inspector.CreatedAt.IsVisible);
        Assert.Matches(@"^created at AppCode\.CreateTimer \(AppCode\.cs:\d+\)$", _inspector.CreatedAt.Text);
        Assert.Equal(traced.CreationTrace!.ToString(), ToolTip.GetTip(_inspector.CreatedAt));
    }

    [AvaloniaFact]
    public void TheTrackSpansTheDelayThroughTheDuration()
    {
        var animation = Animate(new() { ["Opacity"] = 0, Duration = 1000, Delay = 200 }).Pause();
        Select(animation);

        PressTrackAt(0.5);

        Assert.Equal(400, animation.CurrentTime, 6);
    }

    [AvaloniaFact]
    public void TheTrackOfAnEndlessLoopSpansOnlyTheCurrentIteration()
    {
        var animation = Animate(new() { ["Opacity"] = 0, Duration = 100, Loop = true });
        Frames.Advance(320, 16);
        animation.Pause();
        Select(animation);

        PressTrackAt(0.25);

        Assert.Equal(325, animation.CurrentTime, 6);
    }

    [AvaloniaFact]
    public void TheTrackDrawsTheDelayIterationsGanttSelectionAndPlayhead()
    {
        var timeline = Presto.CreateTimeline(new TimelineParams { Delay = 250, Autoplay = false })
            .Add(_box, new AvaloniaAnimationParams { ["Opacity"] = 0, Duration = 500 })
            .Add(_box, new AvaloniaAnimationParams { ["Width"] = 100, Duration = 250 })
            .Label("mid", 125);
        timeline.Seek(400);
        Timer first = null!;
        foreach (var child in timeline.Children)
        {
            first ??= child;
        }
        Select(first);

        var track = _inspector.Track;
        Assert.Equal(10 + (2 * 7) + 12 + 4, track.Bounds.Height);
        var x = (double fraction) => (int)(track.Bounds.Width * fraction);
        var bar = TrackRow(track, 5);
        Assert.True(IsGray(bar[x(0.1)]), $"{bar[x(0.1)]}");
        Assert.True(IsBlue(bar[x(0.5)]), $"{bar[x(0.5)]}");
        Assert.True(IsOrangeRed(bar[x(0.65)]), $"{bar[x(0.65)]}");
        var firstChild = TrackRow(track, 14);
        Assert.True(IsBlue(firstChild[x(0.5)]));
        Assert.False(IsBlue(firstChild[x(0.9)]));
        var secondChild = TrackRow(track, 21);
        Assert.False(IsBlue(secondChild[x(0.5)]));
        Assert.True(IsBlue(secondChild[x(0.9)]));
        var gap = TrackRow(track, 26);
        Assert.True(IsGray(gap[x(0.375)]), $"{gap[x(0.375)]}");
        Assert.False(IsGray(gap[x(0.45)]));
        Assert.True(IsGold(TrackRow(track, 0)[x(0.5)]), $"{TrackRow(track, 0)[x(0.5)]}");
    }

    [AvaloniaFact]
    public void ReviveWithoutAConflictRevivesAtOnce()
    {
        var ended = Animate(new() { ["Opacity"] = 0, Duration = 50 });
        Frames.Advance(100, 16);
        _inspector.Refresh(ended, null);
        Assert.False(_inspector.BeginScrub());

        Click(_inspector.Revive);
        _inspector.Refresh(ended, null);

        Assert.False(_inspector.Warning.IsVisible);
        Assert.False(_inspector.Revive.IsVisible);
        Assert.True(_inspector.BeginScrub());
    }

    private string? TimeText => ((TextBlock)((WrapPanel)_inspector.Children[0]).Children[0]).Text;

    private void Render()
    {
        _window.UpdateLayout();
        for (var i = 0; i < 2; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private void Select(Timer timer)
    {
        _list.Refresh();
        if (timer.Parent is { } parent)
        {
            Assert.Single(_list.Items, row => row.Timer == parent).IsExpanded = true;
            _list.Refresh();
        }
        _list.Rows.SelectedItem = Assert.Single(_list.Items, row => row.Timer == timer);
        Render();
    }

    private void PressTrackAt(double fraction)
    {
        var track = _inspector.Track;
        var at = track.TranslatePoint(new Point(track.Bounds.Width * fraction, 5), _window)!.Value;
        _window.MouseDown(at, MouseButton.Left);
        _window.MouseUp(at, MouseButton.Left);
    }

    private Color[] TrackRow(Control track, int y)
    {
        var origin = track.TranslatePoint(new Point(0, y), _window)!.Value;
        using var frame = _window.GetLastRenderedFrame()!;
        using var locked = frame.Lock();
        var row = new Color[(int)track.Bounds.Width];
        for (var x = 0; x < row.Length; x++)
        {
            var at = locked.Address + ((int)origin.Y * locked.RowBytes) + (((int)origin.X + x) * 4);
            row[x] = Color.FromRgb(Marshal.ReadByte(at), Marshal.ReadByte(at, 1), Marshal.ReadByte(at, 2));
        }
        return row;
    }

    private static bool IsGray(Color c) => Math.Abs(c.R - c.G) < 8 && Math.Abs(c.G - c.B) < 8 && c.R < 230;

    private static bool IsBlue(Color c) => c.B > c.R + 40;

    private static bool IsOrangeRed(Color c) => c.R > c.B + 80 && c.G < 150;

    private static bool IsGold(Color c) => c.R > c.B + 80 && c.G > 150;

    private static void Enter(TextBox box, string text)
    {
        box.Text = text;
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
    }

    private Animation Animate(AvaloniaAnimationParams parameters) => Presto.Animate(_box, parameters);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
