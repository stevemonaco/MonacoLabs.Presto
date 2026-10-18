using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Controls;
using Path = Avalonia.Controls.Shapes.Path;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="PullToRefresh"/> (Phase 65c) filling a 200x200 window, pulled by synthetic touch from (100, 20).</summary>
public sealed class PullToRefreshTests : BridgeTestBase
{
    private static readonly Point Start = new(100, 20);

    [AvaloniaFact]
    public void AShortPullSpringsBackWithoutRefreshing()
    {
        var (window, pull) = Stage();
        var refreshed = false;
        pull.Refresh = () =>
        {
            refreshed = true;
            return Task.CompletedTask;
        };

        new Touch(window).Drag(Frames, Start, new Vector(0, 40));
        Assert.InRange(pull.Pull.Value, 1, pull.Threshold - 1);
        Frames.Advance(2000, 20);

        Assert.False(refreshed);
        Assert.Equal(0, pull.Pull.Value, 3);
    }

    [AvaloniaFact]
    public void APullPastTheThresholdHoldsThereUntilTheRefreshEndsThenSpringsBack()
    {
        var (window, pull) = Stage();
        var work = new TaskCompletionSource();
        var calls = 0;
        pull.Refresh = () =>
        {
            calls++;
            return work.Task;
        };

        new Touch(window).Drag(Frames, Start, new Vector(0, 150));
        Frames.Advance(2000, 20);

        Assert.Equal(1, calls);
        Assert.Equal(pull.Threshold, pull.Pull.Value);
        Assert.NotEqual(0, Angle(Ring(pull)));

        work.SetResult();
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(2000, 20);

        Assert.Equal(0, pull.Pull.Value, 3);
        Assert.Equal(0, Angle(Ring(pull)));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFaultedOrCanceledRefreshIsLoggedAndTheContentStillSpringsBack(bool canceled)
    {
        var (window, pull) = Stage();
        var work = new TaskCompletionSource();
        pull.Refresh = () => work.Task;
        using var log = new LogSink(LogArea.Control, LogEventLevel.Warning);

        new Touch(window).Drag(Frames, Start, new Vector(0, 150));
        Frames.Advance(2000, 20);
        if (canceled) work.SetCanceled();
        else work.SetException(new InvalidOperationException("offline"));
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(2000, 20);

        Assert.Equal(canceled ? LogEventLevel.Warning : LogEventLevel.Error, Assert.Single(log.Entries).Level);
        Assert.Equal(0, pull.Pull.Value, 3);
        Assert.Equal(0, Angle(Ring(pull)));
    }

    [AvaloniaFact]
    public void TheArrowAndTheRingFollowThePull()
    {
        var (window, pull) = Stage();
        var touch = new Touch(window);
        touch.Down(1, Start);
        foreach (var by in (double[])[20, 50])
        {
            touch.Move(1, Start + new Vector(0, by));
            Frames.Advance(20);
            var progress = pull.Pull.Value / pull.Threshold;

            Assert.Equal(progress * 180, Angle(Arrow(pull)), 3);
            Assert.Equal(progress * .8, Drawn(Ring(pull)), 3);
        }
        touch.Up(1, Start + new Vector(0, 50));
    }

    [AvaloniaFact]
    public void TheResistanceRisesSteadilyAndTheReleaseStartsWhereItWasDrawn()
    {
        var (window, pull) = Stage();
        var touch = new Touch(window);
        touch.Down(1, Start);
        var pulled = new List<double>();
        for (var by = 10; by <= 160; by += 10)
        {
            touch.Move(1, Start + new Vector(0, by));
            Frames.Advance(20);
            pulled.Add(pull.Pull.Value);
        }

        Assert.All(pulled.Zip(pulled.Skip(1)), pair => Assert.True(pair.Second > pair.First));
        Assert.All(pulled.Zip(pulled.Skip(1)).Skip(1), pair => Assert.True(pair.Second - pair.First < 10));
        Assert.True(pulled[^1] < pull.Threshold * 2.2);

        touch.Up(1, Start + new Vector(0, 160));
        Frames.Advance(20);
        Assert.InRange(pull.Pull.Value, pull.Threshold, pulled[^1]);
    }

    [AvaloniaFact]
    public void APullIsLeftToAListScrolledAwayFromItsTop()
    {
        var scroller = new ScrollViewer { Content = new Border { Height = 1000, Background = Brushes.Blue } };
        var (window, pull) = Stage(scroller);
        scroller.Offset = new Vector(0, 50);
        Dispatcher.UIThread.RunJobs();

        new Touch(window).Drag(Frames, Start, new Vector(0, 150));
        Frames.Advance(200, 20);

        Assert.Equal(0, pull.Pull.Value);
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheContentLandsAtOnceAndTheRingHoldsStill()
    {
        var (window, pull) = Stage();
        var work = new TaskCompletionSource();
        pull.Refresh = () => work.Task;
        Presto.Defaults.ReducedMotion = true;

        new Touch(window).Drag(Frames, Start, new Vector(0, 150));
        Frames.Advance(20);
        Assert.Equal(pull.Threshold, pull.Pull.Value);
        Frames.Advance(400, 20);
        Assert.Equal(0, Angle(Ring(pull)));

        work.SetResult();
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(20);
        Assert.Equal(0, pull.Pull.Value);
    }

    [AvaloniaFact]
    public void SettingReducedMotionMidRefreshStopsTheSpin()
    {
        var (window, pull) = Stage();
        pull.Refresh = () => new TaskCompletionSource().Task;
        new Touch(window).Drag(Frames, Start, new Vector(0, 150));
        Frames.Advance(400, 20);
        Assert.NotEqual(0, Angle(Ring(pull)));

        Presto.Defaults.ReducedMotion = true;
        Assert.Equal(0, Angle(Ring(pull)));
        Frames.Advance(400, 20);

        Assert.Equal(0, Angle(Ring(pull)));
    }

    [AvaloniaFact]
    public void ClearingItMidRefreshStartsTheSpin()
    {
        var (window, pull) = Stage();
        pull.Refresh = () => new TaskCompletionSource().Task;
        Presto.Defaults.ReducedMotion = true;
        new Touch(window).Drag(Frames, Start, new Vector(0, 150));
        Frames.Advance(400, 20);
        Assert.Equal(0, Angle(Ring(pull)));

        Presto.Defaults.ReducedMotion = false;
        Frames.Advance(400, 20);

        Assert.NotEqual(0, Angle(Ring(pull)));
    }

    [AvaloniaFact]
    public void ItClipsToItsBoundsAndHasASixtyFourDipThresholdByDefault()
    {
        var pull = new PullToRefresh();

        Assert.True(pull.ClipToBounds);
        Assert.Equal(64, pull.Threshold);
    }

    [AvaloniaFact]
    public void TheIndicatorSitsInAThresholdTallStripAboveTheContentAndFollowsAThresholdChange()
    {
        var (window, pull) = Stage();
        var ring = Ring(pull);
        var arrow = Arrow(pull);
        var strip = (Panel)ring.GetVisualParent()!;
        var touch = new Touch(window);
        touch.Press(1, Start);
        Frames.Advance(20);
        touch.Move(1, Start + new Vector(0, 30));
        Frames.Advance(20);

        Assert.Equal((28d, 28d, 3d, PenLineCap.Round), (ring.Bounds.Width, ring.Bounds.Height, ring.StrokeThickness, ring.StrokeLineCap));
        Assert.Equal(2, arrow.StrokeThickness);
        touch.Up(1, Start + new Vector(0, 30));
        Frames.Advance(2000, 20);
        Assert.Same(strip, arrow.GetVisualParent());
        Assert.False(strip.IsHitTestVisible);
        AssertStripAboveContent(pull, strip);

        pull.Threshold = 100;
        Dispatcher.UIThread.RunJobs();

        AssertStripAboveContent(pull, strip);
    }

    [AvaloniaFact]
    public void TheArrowAndTheRingAreStrokedWithTheForegroundAndFollowItsChanges()
    {
        var (_, pull) = Stage();
        pull.Foreground = Brushes.Green;

        Assert.Same(Brushes.Green, Ring(pull).Stroke);
        Assert.Same(Brushes.Green, Arrow(pull).Stroke);

        pull.Foreground = Brushes.Orange;

        Assert.Same(Brushes.Orange, Ring(pull).Stroke);
        Assert.Same(Brushes.Orange, Arrow(pull).Stroke);
    }

    [AvaloniaFact]
    public void AnUnsetForegroundIsInheritedFromTheAncestorsTextForeground()
    {
        var pull = new PullToRefresh { Content = new Border() };
        var host = new Border { Child = pull };
        TextElement.SetForeground(host, Brushes.Purple);
        Show(host);

        Assert.Same(Brushes.Purple, pull.Foreground);
        Assert.Same(Brushes.Purple, Ring(pull).Stroke);
    }

    [AvaloniaFact]
    public void AWonPullHandlesEachMoveAndPreventsGestureRecognition()
    {
        var (window, pull) = Stage();
        var touch = new Touch(window);
        touch.Press(1, Start);
        Frames.Advance(20);
        touch.Move(1, Start + new Vector(0, 10));

        var moved = touch.Move(1, Start + new Vector(0, 20));

        Assert.True(moved.Handled);
        Assert.True(GestureRecognitionSkipped(moved));
        touch.Up(1, Start + new Vector(0, 20));
    }

    [AvaloniaFact]
    public void APullStartedWhileTheContentSpringsBackPicksItUpWhereItIsDrawn()
    {
        var (window, pull) = Stage();
        new Touch(window).Drag(Frames, Start, new Vector(0, 50));
        Frames.Advance(60, 20);
        var drawn = pull.Pull.Value;
        Assert.InRange(drawn, 10, pull.Threshold);

        var onContent = new Point(100, 100);
        var touch = new Touch(window);
        touch.Press(1, onContent);
        touch.Move(1, onContent + new Vector(0, 4));
        touch.Move(1, onContent + new Vector(0, 5));
        Frames.Advance(20);

        Assert.True(pull.Pull.Value > drawn);
        touch.Up(1, onContent + new Vector(0, 5));
    }

    [AvaloniaFact]
    public void LosingTheCaptureMidPullSettlesAsOnARelease()
    {
        var (window, pull) = Stage();
        var touch = new Touch(window);
        var pointer = touch.Press(1, Start);
        for (var by = 10; by <= 40; by += 10)
        {
            Frames.Advance(20);
            touch.Move(1, Start + new Vector(0, by));
        }
        Assert.True(pull.Pull.Value > 10);

        pointer.Capture(null);
        Frames.Advance(2000, 20);

        Assert.Equal(0, pull.Pull.Value, 3);
    }

    [AvaloniaFact]
    public void TheReleaseIsHandledOnlyWhenAPullTookPlace()
    {
        var (window, pull) = Stage();
        var touch = new Touch(window);
        touch.Press(1, Start);
        Assert.False(touch.Up(1, Start).Handled);

        touch.Press(1, Start);
        Frames.Advance(20);
        touch.Move(1, Start + new Vector(0, 20));
        Assert.True(touch.Up(1, Start + new Vector(0, 20)).Handled);
    }

    [AvaloniaFact]
    public void WithoutARefreshAPullPastTheThresholdHoldsAndReturnsAndTheNextPullStillWorks()
    {
        var (window, pull) = Stage();

        new Touch(window).Drag(Frames, Start, new Vector(0, 150));
        Frames.Advance(2000, 20);

        Assert.Equal(0, pull.Pull.Value, 3);
        Assert.Equal(0, Angle(Ring(pull)));
        Assert.Equal(1, Arrow(pull).Opacity, 3);

        var touch = new Touch(window);
        touch.Press(1, Start);
        Frames.Advance(20);
        touch.Move(1, Start + new Vector(0, 30));
        Frames.Advance(20);
        Assert.True(pull.Pull.Value > 5);
        touch.Up(1, Start + new Vector(0, 30));
    }

    [AvaloniaFact]
    public void APressWhileTheRefreshRunsDoesNotArmANewPull()
    {
        var (window, pull) = Stage();
        var work = new TaskCompletionSource();
        var calls = 0;
        pull.Refresh = () =>
        {
            calls++;
            return work.Task;
        };
        new Touch(window).Drag(Frames, Start, new Vector(0, 150));
        Frames.Advance(2000, 20);

        var onContent = new Point(100, 100);
        var touch = new Touch(window);
        touch.Press(1, onContent);
        Frames.Advance(20);
        touch.Move(1, onContent + new Vector(0, 80));
        Frames.Advance(20);
        Assert.Equal(pull.Threshold, pull.Pull.Value);
        Assert.False(touch.Up(1, onContent + new Vector(0, 80)).Handled);
        Frames.Advance(2000, 20);

        Assert.Equal(1, calls);
        Assert.Equal(pull.Threshold, pull.Pull.Value);
        work.SetResult();
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertStripAboveContent(PullToRefresh pull, Panel strip)
    {
        var content = pull.Content!;
        var top = strip.TranslatePoint(default, content)!.Value.Y;

        Assert.Equal(pull.Threshold, strip.Bounds.Height);
        Assert.Equal(-pull.Threshold, top, 3);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_IsGestureRecognitionSkipped")]
    private static extern bool GestureRecognitionSkipped(PointerEventArgs args);

    private static (Window Window, PullToRefresh Pull) Stage(Control? content = null)
    {
        var pull = new PullToRefresh { Content = content ?? new Border { Background = Brushes.Red } };
        var window = new Window { Content = pull, Width = 200, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, pull);
    }

    private static Ellipse Ring(PullToRefresh pull) => pull.GetVisualDescendants().OfType<Ellipse>().Single();

    private static Path Arrow(PullToRefresh pull) => pull.GetVisualDescendants().OfType<Path>().Single();

    private static double Angle(Visual visual) =>
        double.Parse(Convert.ToString(PrestoAvalonia.Get(visual, "Rotate"), CultureInfo.InvariantCulture)!.Replace("deg", ""), CultureInfo.InvariantCulture);

    private static double Drawn(Shape shape) => ((double[])PrestoAvalonia.Get(shape, "Draw")!)[1];
}
