using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Draggables;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// Covers <c>PrestoAvalonia.CreateDraggable</c>, porting the pointer half of tests/suites/draggables.test.js — where
/// upstream drives the DOM with synthetic mouse events, these drive a headless window.
/// </summary>
public sealed class DraggableTests : BridgeTestBase
{
    private const double ContainerSize = 200;
    private const double TargetSize = 50;

    private static (Window Window, Canvas Container, Border Target) Show()
    {
        var target = new Border { Width = TargetSize, Height = TargetSize, Background = Brushes.Red };
        var container = new Canvas
        {
            Width = ContainerSize,
            Height = ContainerSize,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { target },
        };
        var window = new Window { Content = container, Width = 300, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, container, target);
    }

    private static (Window Window, ScrollViewer Scroller, Border Target, Draggable Draggable) ShowInScroller()
    {
        var target = new Border { Width = TargetSize, Height = TargetSize, Background = Brushes.Red };
        var scroller = new ScrollViewer
        {
            Width = ContainerSize,
            Height = ContainerSize,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Content = new Canvas { Width = ContainerSize, Height = 1000, Children = { target } },
        };
        var window = new Window { Content = scroller, Width = 300, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, scroller, target, PrestoAvalonia.CreateDraggable(target));
    }

    private void Drag(Window window, double step, int steps)
    {
        window.MouseDown(new Point(25, 25), MouseButton.Left);
        for (var i = 1; i <= steps; i++)
        {
            window.MouseMove(new Point(25 + (i * step), 25));
            Frames.Advance(20);
        }
    }

    [AvaloniaFact]
    public void DraggingMovesTheTargetByThePointerDelta()
    {
        var (window, _, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target);

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        window.MouseMove(new Point(55, 45));
        Frames.Advance(16);

        Assert.Equal(30d, draggable.X);
        Assert.Equal(20d, draggable.Y);
        Assert.Equal(30d, PrestoAvalonia.Get(target, "X"));
        Assert.Equal(20d, PrestoAvalonia.Get(target, "Y"));
    }

    [AvaloniaFact]
    public void MovementBelowTheDragThresholdIsSuppressed()
    {
        var (window, _, target) = Show();
        var drags = 0;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams { OnDrag = _ => drags++ });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        window.MouseMove(new Point(28, 27));
        Frames.Advance(48, 16);

        Assert.Equal(0, drags);
        Assert.Equal(0d, draggable.X);
        Assert.Equal(0d, draggable.Y);

        window.MouseMove(new Point(35, 25));
        Frames.Advance(16);

        Assert.Equal(1, drags);
        Assert.Equal(10d, draggable.X);
        Assert.Equal(0d, draggable.Y);
    }

    [AvaloniaFact]
    public void AContainerClampsToItsBoundsMinusPadding()
    {
        var (window, container, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Container = container,
            ContainerPadding = new Thickness(10),
            ContainerFriction = 1,
        });

        Assert.Equal(10d, draggable.X);
        Assert.Equal(10d, draggable.Y);

        window.MouseDown(new Point(35, 35), MouseButton.Left);
        window.MouseMove(new Point(500, 500));
        Frames.Advance(16);

        Assert.Equal(ContainerSize - 10 - TargetSize, draggable.X);
        Assert.Equal(ContainerSize - 10 - TargetSize, draggable.Y);

        window.MouseMove(new Point(-500, -500));
        Frames.Advance(16);

        Assert.Equal(10d, draggable.X);
        Assert.Equal(10d, draggable.Y);
    }

    [AvaloniaFact]
    public void ADisabledAxisStaysPut()
    {
        var (window, _, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Y = new DraggableAxisParams { Enabled = false },
        });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        window.MouseMove(new Point(55, 55));
        Frames.Advance(16);

        Assert.Equal(30d, draggable.X);
        Assert.Equal(0d, draggable.Y);
        Assert.Equal(0d, PrestoAvalonia.Get(target, "Y"));
    }

    [AvaloniaFact]
    public void AnAxisCanBeMappedToALayoutProperty()
    {
        var (window, container, target) = Show();
        Canvas.SetLeft(target, 20);
        Canvas.SetTop(target, 10);
        Dispatcher.UIThread.RunJobs();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            X = new DraggableAxisParams { MapTo = "Canvas.Left" },
            Y = new DraggableAxisParams { MapTo = "Canvas.Top" },
            Container = container,
        });

        Assert.Equal(20d, draggable.X);

        window.MouseDown(new Point(45, 35), MouseButton.Left);
        window.MouseMove(new Point(75, 35));
        Frames.Advance(16);

        Assert.Equal(50d, draggable.X);
        Assert.Equal(50d, Canvas.GetLeft(target));
        Assert.Equal(10d, Canvas.GetTop(target));
    }

    [AvaloniaFact]
    public void SnapQuantizesTheReleaseAndReportsEveryStep()
    {
        var (window, _, target) = Show();
        var snaps = 0;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Y = new DraggableAxisParams { Enabled = false },
            Snap = Utils.Snap(25),
            OnSnap = _ => snaps++,
            MaxVelocity = 0, // No release throw, so the settle is the snap alone.
        });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        window.MouseMove(new Point(35, 25));
        Frames.Advance(16);
        Assert.Equal(0, snaps);

        window.MouseMove(new Point(45, 25));
        Frames.Advance(16);
        Assert.Equal(1, snaps);

        window.MouseMove(new Point(85, 25));
        Frames.Advance(16);
        Assert.Equal(2, snaps);
        Assert.Equal(60d, draggable.X);

        window.MouseUp(new Point(85, 25), MouseButton.Left);
        Frames.Advance(2000, 20);

        Assert.Equal(50d, draggable.X);
        Assert.Equal(2, snaps);
    }

    [AvaloniaFact]
    public void ReleasingWithVelocityThrowsTheTargetOnAndSettles()
    {
        var (window, _, target) = Show();
        var log = new List<string>();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Y = new DraggableAxisParams { Enabled = false },
            OnRelease = _ => log.Add("release"),
            OnSettle = _ => log.Add("settle"),
        });

        Drag(window, 20, 4);
        window.MouseUp(new Point(105, 25), MouseButton.Left);

        // 20 units per 20 ms frame is a velocity of 1, and the throw carries 150 units of it.
        Assert.Equal(80d, draggable.X);
        Assert.Equal(1d, draggable.Velocity);
        Assert.Equal(["release"], log);

        Frames.Advance(100, 20);
        Assert.InRange(draggable.X, 81, 229);

        Frames.Advance(3000, 20);
        Assert.Equal(230d, draggable.X);
        Assert.Equal(["release", "settle"], log);
    }

    [AvaloniaFact]
    public void AnOutOfBoundsReleaseEasesBackIntoTheContainer()
    {
        var (window, container, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Container = container,
            Y = new DraggableAxisParams { Enabled = false },
        });

        Drag(window, 20, 10);

        // 200 dragged past a 150 bound, of which the .8 container friction swallows four fifths.
        Assert.Equal(160d, draggable.X);

        window.MouseUp(new Point(225, 25), MouseButton.Left);
        Assert.Equal(160d, draggable.X);

        Frames.Advance(300, 20);
        Assert.InRange(draggable.X, 151, 159);

        Frames.Advance(3000, 20);
        Assert.Equal(150d, draggable.X);
    }

    [AvaloniaFact]
    public void AReleaseEaseAndAReleaseSpringSettleAtTheSameCoordinates()
    {
        var (window, _, target) = Show();
        var eased = PrestoAvalonia.CreateDraggable(target, new DraggableParams { ReleaseEase = Eases.OutQuad });

        Drag(window, 20, 4);
        window.MouseUp(new Point(105, 25), MouseButton.Left);
        Frames.Advance(3000, 20);
        var easedX = eased.X;
        eased.Revert();

        var sprung = PrestoAvalonia.CreateDraggable(target, new DraggableParams { ReleaseEase = new Spring(stiffness: 120, damping: 14) });

        Drag(window, 20, 4);
        window.MouseUp(new Point(105, 25), MouseButton.Left);
        Frames.Advance(3000, 20);

        Assert.Equal(230d, easedX);
        Assert.Equal(easedX, sprung.X);
    }

    [AvaloniaFact]
    public void AnimateInViewMovesTheTargetBackIntoTheScrollViewersViewport()
    {
        var (_, _, target, draggable) = ShowInScroller();
        Canvas.SetTop(target, 600);
        Dispatcher.UIThread.RunJobs();

        draggable.AnimateInView(200);
        Frames.Advance(400, 20);

        Assert.Equal(-450d, draggable.Y);
    }

    [AvaloniaFact]
    public void AutoScrollAdvancesTheScrollViewerWhileThePointerSitsInTheEdgeBand()
    {
        var (window, scroller, _, _) = ShowInScroller();

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        window.MouseMove(new Point(25, ContainerSize - 5));
        Frames.Advance(20);
        var firstFrame = scroller.Offset.Y;

        Assert.True(firstFrame > 0, $"the pointer sat in the bottom band but the offset stayed at {firstFrame}");

        Frames.Advance(60, 20);
        Assert.True(scroller.Offset.Y > firstFrame, "a still pointer inside the band should keep scrolling");

        window.MouseUp(new Point(25, ContainerSize - 5), MouseButton.Left);
        Frames.Advance(2000, 20);
        var settled = scroller.Offset.Y;
        Frames.Advance(2000, 20);

        Assert.Equal(settled, scroller.Offset.Y);
        Assert.True(settled < 800, $"auto-scroll should have stopped on release, not run to the end at {settled}");
    }

    [AvaloniaFact]
    public void TheCursorFollowsTheDragAndRevertPutsItBack()
    {
        var (window, _, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target);

        Assert.Null(target.Cursor);

        window.MouseMove(new Point(25, 25));
        var hover = target.Cursor;
        Assert.NotNull(hover);

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        Assert.NotEqual(hover, target.Cursor);

        window.MouseUp(new Point(25, 25), MouseButton.Left);
        Assert.Equal(hover, target.Cursor);

        draggable.Revert();
        Assert.Null(target.Cursor);
    }

    /// <summary>Upstream "onUpdate should only trigger when the dragged element actually moves".</summary>
    [AvaloniaFact]
    public void OnUpdateOnlyFiresWhenTheTargetMoves()
    {
        var (window, container, target) = Show();
        var updates = 0;
        var grabs = 0;
        var drags = 0;
        var releases = 0;
        PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Container = container,
            OnUpdate = _ => updates++,
            OnGrab = _ => grabs++,
            OnDrag = _ => drags++,
            OnRelease = _ => releases++,
        });

        Assert.Equal(0, updates);

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        for (var i = 0; i < 3; i++)
        {
            window.MouseMove(new Point(35, 35));
            Frames.Advance(16);
        }
        window.MouseUp(new Point(35, 35), MouseButton.Left);
        Frames.Advance(48, 16);

        Assert.Equal(1, grabs);
        Assert.Equal(3, drags);
        Assert.Equal(1, releases);
        // One per axis write of the only frame that moved, as upstream's two updates for a single diagonal move.
        Assert.Equal(2, updates);
    }

    /// <summary>Upstream "onUpdate should properly trigger when the dragged element only moves horizontally".</summary>
    [AvaloniaFact]
    public void OnUpdateFiresForEveryHorizontalStep()
    {
        var (window, container, target) = Show();
        var updates = 0;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Container = container,
            OnUpdate = _ => updates++,
        });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        for (var i = 1; i <= 4; i++)
        {
            window.MouseMove(new Point(25 + (i * 4), 25));
            Frames.Advance(16);
        }

        Assert.Equal(4, updates);
        Assert.Equal(16d, draggable.X);
        Assert.Equal(0d, draggable.Y);
    }

    /// <summary>Upstream "Triggering a reset in the onSettle callback should correctly set the values".</summary>
    [AvaloniaFact]
    public void ResettingInsideOnSettleLeavesBothAxesAtZero()
    {
        var (_, _, target) = Show();
        double? settledX = null;
        double? settledY = null;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            OnSettle = self =>
            {
                if (settledX != null) return;
                self.Reset();
                settledX = self.X;
                settledY = self.Y;
            },
        });

        draggable.Animate.Set("X", 100, 10);
        draggable.Animate.Set("Y", 100, 10);
        RunUntil(() => settledX != null);

        Assert.Equal(0d, settledX);
        Assert.Equal(0d, settledY);
    }

    /// <summary>Upstream "Removing the parent should not throw an error".</summary>
    [AvaloniaFact]
    public void RemovingTheParentMidDragDoesNotThrow()
    {
        var (window, container, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams { Container = container });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        window.MouseMove(new Point(60, 60));
        Frames.Advance(16);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        window.MouseMove(new Point(80, 80));
        Frames.Advance(48, 16);
        window.MouseUp(new Point(80, 80), MouseButton.Left);

        draggable.Refresh().Revert();
    }

    /// <summary>Upstream "Reverting a draggable should properly disconect the ResizeObserver attached to it".</summary>
    [AvaloniaFact]
    public void RevertingDetachesTheResizeAndPointerHandlers()
    {
        var (window, container, target) = Show();
        var resizes = 0;
        var afterResizes = 0;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Container = container,
            OnResize = _ => resizes++,
            OnAfterResize = _ => afterResizes++,
        });

        container.Width = 150;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(200, 16);

        Assert.Equal(1, resizes);
        Assert.Equal(1, afterResizes);

        draggable.Revert();
        container.Width = 100;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(200, 16);

        Assert.Equal(1, resizes);

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        window.MouseMove(new Point(55, 55));
        Frames.Advance(48, 16);

        Assert.Equal(0d, PrestoAvalonia.Get(target, "X"));
        Assert.Equal(0d, PrestoAvalonia.Get(target, "Y"));
    }
}
