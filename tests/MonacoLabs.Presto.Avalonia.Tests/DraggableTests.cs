using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MonacoLabs.Presto.Draggables;
using MonacoLabs.Presto.Core;

namespace MonacoLabs.Presto.Tests;

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

    private static (Window Window, ScrollViewer Scroller, Border Target, Draggable Draggable) ShowInScroller(DraggableParams? parameters = null)
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
        return (window, scroller, target, PrestoAvalonia.CreateDraggable(target, parameters));
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
    public void EscapeMidDragSpringsTheTargetBackToWhereItWasGrabbed()
    {
        var (window, _, target) = Show();
        var released = 0;
        var settled = 0;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            OnRelease = _ => released++,
            OnSettle = _ => settled++,
        });
        draggable.X = 10;

        Drag(window, 20, 3);
        Assert.Equal(70d, draggable.X);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Frames.Advance(2000, 16);
        window.MouseMove(new Point(150, 25));
        window.MouseUp(new Point(150, 25), MouseButton.Left);
        Frames.Advance(100, 16);

        Assert.Equal(10d, draggable.X);
        Assert.Equal(0d, draggable.Y);
        Assert.Equal(1, released);
        Assert.Equal(1, settled);
    }

    [AvaloniaFact]
    public void AReleaseThatOnlyMovesOneAxisSettlesOnceBothAreWritten()
    {
        var (window, _, target) = Show();
        var settles = new List<double>();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Snap = Utils.Snap(40),
            OnSettle = d =>
            {
                settles.Add(d.Y);
                d.Reset();
            },
        });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        for (var i = 1; i <= 3; i++)
        {
            window.MouseMove(new Point(25, 25 + (i * 15)));
            Frames.Advance(16);
        }
        window.MouseUp(new Point(25, 70), MouseButton.Left);
        Frames.Advance(2000, 16);

        Assert.Single(settles);
        Assert.NotEqual(0d, settles[0]);
        Assert.Equal(0d, draggable.Y);
    }

    [AvaloniaFact]
    public void EscapeIsIgnoredWithoutCancelOnEscape()
    {
        var (window, _, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams { CancelOnEscape = false });

        Drag(window, 20, 3);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        window.MouseMove(new Point(105, 25));
        Frames.Advance(16);

        Assert.Equal(80d, draggable.X);
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
    public void PositionAnimatesByNameThroughItsTable()
    {
        var (_, container, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams { Container = container });

        Presto.Animate(draggable, new AnimationParams { ["X"] = 30, ["Y"] = 40, Duration = 100 });
        Frames.Advance(120);

        Assert.Equal(30d, draggable.X);
        Assert.Equal(40d, draggable.Y);

        Presto.Set(draggable, new AnimationParams { ["ProgressX"] = 0.5, ["ProgressY"] = 1 });

        Assert.Equal((ContainerSize - TargetSize) / 2, draggable.X);
        Assert.Equal(ContainerSize - TargetSize, draggable.Y);
        Assert.Throws<ArgumentException>(() => Presto.Set(draggable, new AnimationParams { ["Velocity"] = 1 }));
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
    public void VelocityIsMeasuredInDilatedEngineTime()
    {
        var (window, _, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams { Y = new DraggableAxisParams { Enabled = false } });
        Presto.Engine.TimeDilation = .5;

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        for (var i = 1; i <= 4; i++)
        {
            window.MouseMove(new Point(25 + (i * 20), 25));
            Frames.Advance(40);
        }

        // 20 units per 40 ms frame, which is 20 ms of engine time.
        Assert.Equal(1d, draggable.Velocity);
    }

    [AvaloniaFact]
    public void ANegativeMultiplierReportsANegativeVelocityAtOnce()
    {
        var (window, _, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams { VelocityMultiplier = -1, MinVelocity = -50 });

        Drag(window, 20, 2);

        Assert.True(draggable.Velocity < 0, $"velocity {draggable.Velocity}");
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

    [AvaloniaTheory]
    [InlineData(10)] // lands inside the old bounds
    [InlineData(20)] // thrown past them, so the additive overshoot is in flight
    public void AContainerResizedMidThrowRetargetsTheReleaseAndSettlesOnce(double step)
    {
        var (window, container, target) = Show();
        var settles = 0;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Container = container,
            Y = new DraggableAxisParams { Enabled = false },
            OnSettle = _ => settles++,
        });

        Drag(window, step, 4);
        window.MouseUp(new Point(25 + (4 * step), 25), MouseButton.Left);
        Frames.Advance(60, 20);

        container.Width = 100;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(3000, 20);

        Assert.Equal(50d, draggable.X);
        Assert.Equal(1, settles);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)] // mid-frame, with the additive overshoot tweens next in the engine's list
    public void ResetMidOvershootLeavesAnAnimationOnAnotherControlRunning(bool fromItsCallback)
    {
        var (window, container, target) = Show();
        var other = new Border { Width = 10, Height = 10 };
        container.Children.Add(other);
        Dispatcher.UIThread.RunJobs();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Container = container,
            Y = new DraggableAxisParams { Enabled = false },
        });
        var completed = 0;
        var reset = false;

        Drag(window, 20, 10);
        other.Animate(new AvaloniaAnimationParams
        {
            ["Opacity"] = 0,
            Duration = 1000,
            OnUpdate = _ =>
            {
                if (!fromItsCallback || reset) return;
                reset = true;
                draggable.Reset();
            },
            OnComplete = _ => completed++,
        });
        window.MouseUp(new Point(225, 25), MouseButton.Left);
        Frames.Advance(100, 20);
        if (!fromItsCallback) draggable.Reset();
        Frames.Advance(3000, 20);

        Assert.Equal(0d, draggable.X);
        Assert.Equal(0d, other.Opacity);
        Assert.Equal(1, completed);
    }

    /// <summary>PLAN Phase 51: a grab canceling an overshoot whose out leg had completed corrupted the additive lists into a cycle.</summary>
    [AvaloniaFact]
    public void RegrabbingAnOvershootingThrowRepeatedlyKeepsTheTweenListsAcyclic()
    {
        var (window, container, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Container = container,
            Y = new DraggableAxisParams { Enabled = false },
        });
        var timers = new List<Timer>();

        // Some grabs land after the out leg completed, so canceling it removes its tweens from their siblings a second time.
        for (var wait = 100; wait <= 2000; wait += 150)
        {
            var x = draggable.X + 25;
            window.MouseDown(new Point(x, 25), MouseButton.Left);
            for (var step = 1; step <= 4; step++)
            {
                window.MouseMove(new Point(x + (step * 20), 25));
                Frames.Advance(20);
            }
            window.MouseUp(new Point(x + 80, 25), MouseButton.Left);
            Frames.Advance(wait, 20);
        }
        Assert.NotNull(Presto.Engine.Additive.Animation);
        for (var (timer, steps) = (Presto.Engine._head, 0); timer != null; timer = timer._next, steps++)
        {
            Assert.True(steps < 10_000, "The engine's timer list has a cycle.");
            timers.Add(timer);
        }
        Presto.Remove(target);
        Presto.Remove(draggable);

        timers.Add(Presto.Engine.Additive.Animation!);
        foreach (var timer in timers) TweenList.CheckIntact(timer);
        foreach (var properties in Presto.Engine.Additive.Lookups.Values)
        {
            foreach (var siblings in properties.Values) TweenList.CheckIntact(siblings);
        }
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

    [AvaloniaFact]
    public void ResizeRaisesOnResizeWithinTheDebounceInSecondsMode()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        var (_, container, target) = Show();
        var resizes = 0;
        PrestoAvalonia.CreateDraggable(target, new DraggableParams { Container = container, OnResize = _ => resizes++ });

        container.Width = 150;
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(200, 16);

        Assert.Equal(1, resizes);
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

    [AvaloniaFact]
    public void ADraggableCreatedInAScopeIsRevertedWithIt()
    {
        var (window, _, target) = Show();
        var scope = Presto.CreateScope();
        scope.Add(_ => { PrestoAvalonia.CreateDraggable(target); });
        window.MouseDown(new Point(25, 25), MouseButton.Left);
        window.MouseMove(new Point(55, 25));
        Frames.Advance(16);
        window.MouseUp(new Point(55, 25), MouseButton.Left);
        Assert.Equal(30d, PrestoAvalonia.Get(target, "X"));

        scope.Revert();

        Assert.Equal(0d, PrestoAvalonia.Get(target, "X"));
        PrestoAvalonia.CreateDraggable(target).Revert();
    }

    [AvaloniaFact]
    public void ASecondPointerIsIgnoredMidDrag()
    {
        var (window, _, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target);
        var touch = new Touch(window);

        touch.Press(1, new Point(25, 25));
        touch.Move(1, new Point(55, 25));
        Frames.Advance(16);
        var second = touch.Down(2, new Point(60, 30));
        second.Pointer.Capture(target);
        touch.Move(2, new Point(150, 150));
        Frames.Advance(16);

        Assert.False(second.Handled);
        Assert.Equal(30d, draggable.X);
        Assert.Equal(0d, draggable.Y);
    }

    [AvaloniaFact]
    public void DisabledCursorsLeaveTheTriggersCursorAlone()
    {
        var (window, _, target) = Show();
        var own = new Cursor(StandardCursorType.Cross);
        target.Cursor = own;
        PrestoAvalonia.CreateDraggable(target, new DraggableParams { Cursor = new() { Enabled = false } });

        window.MouseMove(new Point(25, 25));
        Assert.Same(own, target.Cursor);
        window.MouseDown(new Point(25, 25), MouseButton.Left);
        Assert.Same(own, target.Cursor);
        window.MouseUp(new Point(25, 25), MouseButton.Left);
        Assert.Same(own, target.Cursor);
    }

    [AvaloniaFact]
    public void TheDefaultCursorsAreHandOnHoverAndSizeAllWhileGrabbed()
    {
        var (window, _, target) = Show();
        PrestoAvalonia.CreateDraggable(target);

        window.MouseMove(new Point(25, 25));
        Assert.Equal(nameof(StandardCursorType.Hand), target.Cursor!.ToString());
        window.MouseDown(new Point(25, 25), MouseButton.Left);
        Assert.Equal(nameof(StandardCursorType.SizeAll), target.Cursor!.ToString());
    }

    [AvaloniaFact]
    public void AGrabScrollsTheTargetIntoViewOverAHundredMilliseconds()
    {
        var (window, scroller, target, _) = ShowInScroller();
        Canvas.SetTop(target, 180);
        Dispatcher.UIThread.RunJobs();

        window.MouseDown(new Point(25, 190), MouseButton.Left);
        Frames.Advance(60, 20);
        Assert.InRange(scroller.Offset.Y, 21, 29.9);
        Frames.Advance(100, 20);

        Assert.Equal(30, scroller.Offset.Y, 3);
    }

    [AvaloniaFact]
    public void ContainerBoundsAreInsetByThePaddingReReadOnRefreshAndOutrankTheContainer()
    {
        var (_, container, target) = Show();
        var bounds = new Rect(0, 0, 100, 40);
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Container = container,
            ContainerBounds = _ => bounds,
            ContainerPadding = new Thickness(10),
        });
        Assert.Equal(10d, draggable.Y);

        draggable.X = 500;
        draggable.Refresh();
        Assert.Equal(90d, draggable.X);

        bounds = new Rect(-50, 0, 300, 100);
        draggable.X = -500;
        draggable.Refresh();
        Assert.Equal(-40d, draggable.X);
    }

    [AvaloniaFact]
    public void NoPaddingBoundsTheTargetByTheContainersBox()
    {
        var (_, container, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams { Container = container });

        draggable.X = 500;
        draggable.Y = -500;
        draggable.Refresh();

        Assert.Equal(ContainerSize - TargetSize, draggable.X);
        Assert.Equal(0d, draggable.Y);
    }

    [AvaloniaFact]
    public void AGrabPastABoundPicksUpFromTheCushionedPosition()
    {
        var (window, container, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Container = container,
            Y = new DraggableAxisParams { Enabled = false },
        });
        draggable.X = 160;

        window.MouseDown(new Point(185, 25), MouseButton.Left);
        window.MouseMove(new Point(195, 25));
        Frames.Advance(16);

        Assert.Equal(162d, draggable.X);
    }

    [AvaloniaFact]
    public void AnAxisModifierReplacesTheSharedOne()
    {
        var (window, _, target) = Show();
        PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            X = new DraggableAxisParams { Modifier = v => Math.Round(v / 10) * 10 },
            Modifier = v => Math.Floor(v / 25) * 25,
        });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        window.MouseMove(new Point(59, 52));
        Frames.Advance(16);

        Assert.Equal(30d, PrestoAvalonia.Get(target, "X"));
        Assert.Equal(25d, PrestoAvalonia.Get(target, "Y"));
    }

    [AvaloniaFact]
    public void AnAxisComposesByReplaceByDefault()
    {
        double WrittenOverAnotherAnimation(Composition? composition)
        {
            var (_, _, target) = Show();
            var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams { X = new DraggableAxisParams { Composition = composition } });
            Presto.Animate(target, new AvaloniaAnimationParams { ["X"] = 100, Duration = 1000, Ease = "linear" });
            Frames.Advance(100, 20);
            draggable.X = 30;
            Frames.Advance(500, 20);
            var x = Convert.ToDouble(PrestoAvalonia.Get(target, "X"));
            draggable.Revert();
            return x;
        }

        var unset = WrittenOverAnotherAnimation(null);

        Assert.Equal(WrittenOverAnotherAnimation(Composition.Replace), unset);
        Assert.NotEqual(WrittenOverAnotherAnimation(Composition.Additive), unset);
        Assert.NotEqual(WrittenOverAnotherAnimation(Composition.Overlap), unset);
    }

    [AvaloniaTheory]
    [InlineData("speed")]
    [InlineData("threshold")]
    public void AZeroScrollSpeedOrThresholdTurnsAutoScrollOff(string zero)
    {
        var (window, scroller, _, draggable) = ShowInScroller(new DraggableParams
        {
            ScrollSpeed = zero == "speed" ? 0 : null,
            ScrollThreshold = zero == "threshold" ? 0 : null,
        });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        window.MouseMove(new Point(25, ContainerSize - 5));
        Frames.Advance(200, 20);

        Assert.Equal(0, scroller.Offset.Y);
        Assert.Equal(ContainerSize - 30, draggable.Y);
    }

    [AvaloniaFact]
    public void AutoScrollWaitsForTheGrabsScrollIntoView()
    {
        var (window, scroller, target, draggable) = ShowInScroller();
        Canvas.SetTop(target, 180);
        Dispatcher.UIThread.RunJobs();

        window.MouseDown(new Point(25, 190), MouseButton.Left);
        window.MouseMove(new Point(25, 195));
        Frames.Advance(60, 20);

        Assert.Equal(5d, draggable.Y);
        Assert.True(scroller.Offset.Y <= 30, $"auto-scroll ran past the scroll into view to {scroller.Offset.Y}");
    }

    [AvaloniaTheory]
    [InlineData(195, 11.25)]
    [InlineData(185, 3.75)]
    [InlineData(175, 0)]
    public void AutoScrollDefaultsToSpeedOnePointFiveInATwentyWideBand(double pointerY, double perFrame)
    {
        var (window, scroller, _, _) = ShowInScroller();

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        window.MouseMove(new Point(25, pointerY));
        Frames.Advance(20);
        var first = scroller.Offset.Y;
        Frames.Advance(20);

        Assert.Equal(perFrame, scroller.Offset.Y - first, 6);
    }

    [AvaloniaTheory]
    [InlineData(600, 0, 450)]
    [InlineData(600, 20, 470)]
    [InlineData(960, 0, 800)]
    [InlineData(100, 0, 0)]
    [InlineData(100, 60, 10)]
    public void ScrollInViewScrollsOnlyWhenTheTargetIsLessThanTheGapInside(double top, double gap, double offset)
    {
        var (_, scroller, target, draggable) = ShowInScroller();
        Canvas.SetTop(target, top);
        Dispatcher.UIThread.RunJobs();

        draggable.ScrollInView(200, gap);
        Frames.Advance(400, 20);

        Assert.Equal(offset, scroller.Offset.Y, 3);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void BringingIntoViewDefaultsToThreeHundredFiftyMillisecondsOfInOutQuadWithNoGap(bool moveTarget)
    {
        var (_, scroller, target, draggable) = ShowInScroller();
        Canvas.SetTop(target, 600);
        Dispatcher.UIThread.RunJobs();
        double Travel() => moveTarget ? -draggable.Y : scroller.Offset.Y;

        if (moveTarget) draggable.AnimateInView();
        else draggable.ScrollInView();
        Frames.Advance(100, 20);
        Assert.InRange(Travel(), 30, 100);
        Frames.Advance(200, 20);
        Assert.InRange(Travel(), 400, 449);
        Frames.Advance(100, 20);

        Assert.Equal(450, Travel(), 3);
    }

    [AvaloniaFact]
    public void ProgressMapsEachAxisAcrossItsBounds()
    {
        var (_, container, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Container = container,
            ContainerPadding = new Thickness(10, 0, 0, 0),
        });

        draggable.X = 80;
        Assert.Equal(.5, draggable.ProgressX, 6);

        draggable.ProgressY = .2;
        Assert.Equal(30d, draggable.Y);
    }

    [AvaloniaFact]
    public void DisablingDropsTheDragUntilEnabledAgain()
    {
        var (window, _, target) = Show();
        IPointer? pointer = null;
        target.PointerPressed += (_, e) => pointer = e.Pointer;
        var drags = 0;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams { OnDrag = _ => drags++ });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        window.MouseMove(new Point(55, 25));
        Frames.Advance(16);
        draggable.Disable();
        window.MouseMove(new Point(95, 25));
        Frames.Advance(16);
        window.MouseUp(new Point(95, 25), MouseButton.Left);

        Assert.Equal(30d, draggable.X);
        Assert.Null(pointer!.Captured);
        Assert.Null(target.Cursor);

        draggable.Enable().Enable();
        drags = 0;
        window.MouseDown(new Point(55, 25), MouseButton.Left);
        window.MouseMove(new Point(75, 25));
        Frames.Advance(16);

        Assert.Equal(50d, draggable.X);
        Assert.Equal(1, drags);
    }

    [AvaloniaFact]
    public void TheReleaseCushionsPastABoundWithTheDragFrictionByDefault()
    {
        var (window, container, target) = Show();
        var parameters = new DraggableParams { Container = container, ContainerFriction = .5, Y = new DraggableAxisParams { Enabled = false } };

        var unset = Release(window, target, parameters);

        Assert.Contains(unset, x => x > 150);
        Assert.Equal(Release(window, target, parameters with { ReleaseContainerFriction = .5 }), unset);
        Assert.NotEqual(Release(window, target, parameters with { ReleaseContainerFriction = .8 }), unset);
        Assert.DoesNotContain(Release(window, target, parameters with { ContainerFriction = 1 }), x => x > 150);
    }

    [AvaloniaFact]
    public void TheDefaultReleaseEasesOutQuintOverAMassOneStiffnessEightyDampingTwentySpring()
    {
        var (window, _, target) = Show();
        var defaults = Release(window, target, new DraggableParams { Y = new DraggableAxisParams { Enabled = false } });
        var spelled = Release(window, target, new DraggableParams
        {
            Y = new DraggableAxisParams { Enabled = false },
            ReleaseEase = Eases.OutQuint,
            ReleaseMass = 1,
            ReleaseStiffness = 80,
            ReleaseDamping = 20,
        });
        var stiffer = Release(window, target, new DraggableParams { Y = new DraggableAxisParams { Enabled = false }, ReleaseStiffness = 200 });

        Assert.Equal(spelled, defaults);
        Assert.NotEqual(spelled, stiffer);
    }

    [AvaloniaTheory]
    [InlineData(4, .2)]
    [InlineData(20, 1)]
    [InlineData(1500, 50)]
    public void VelocityDefaultsToUnscaledAndAtMostFifty(double step, double velocity)
    {
        var (window, _, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams { Y = new DraggableAxisParams { Enabled = false } });

        Drag(window, step, 3);

        Assert.Equal(velocity, draggable.Velocity, 6);
    }

    private List<double> Release(Window window, Border target, DraggableParams parameters)
    {
        var draggable = PrestoAvalonia.CreateDraggable(target, parameters);
        Drag(window, 20, 4);
        window.MouseUp(new Point(105, 25), MouseButton.Left);
        var path = new List<double>();
        for (var t = 0; t < 1000; t += 20)
        {
            Frames.Advance(20);
            path.Add(draggable.X);
        }
        draggable.Revert();
        return path;
    }

    // ReleaseInertia is not upstream, so these pin the opt-in release on its own.

    private void Flick(Window window, double stepX, double stepY, int steps)
    {
        window.MouseDown(new Point(25, 25), MouseButton.Left);
        for (var i = 1; i <= steps; i++)
        {
            window.MouseMove(new Point(25 + (i * stepX), 25 + (i * stepY)));
            Frames.Advance(20);
        }
        window.MouseUp(new Point(25 + (steps * stepX), 25 + (steps * stepY)), MouseButton.Left);
    }

    [AvaloniaFact]
    public void AnInertiaReleaseDecaysOntoEachAxisSnap()
    {
        var (window, _, target) = Show();
        var settles = 0;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            X = new DraggableAxisParams { Snap = Utils.Snap(100) },
            Y = new DraggableAxisParams { Snap = Utils.Snap(30) },
            ReleaseInertia = new Inertia(),
            OnSettle = _ => settles++,
        });

        // 20 and 10 units per 20 ms frame: velocities of 1 and .5, coasting 350 and 175 on the default time constant.
        Flick(window, 20, 10, 4);
        Frames.Advance(100, 20);
        Assert.InRange(draggable.X, 81, 399);
        Assert.InRange(draggable.Y, 41, 209);

        Frames.Advance(5000, 20);
        Assert.Equal(400d, draggable.X);
        Assert.Equal(210d, draggable.Y);
        Assert.Equal(1, settles);
    }

    [AvaloniaFact]
    public void AnInertiaReleaseBouncesOffTheContainerAndSettlesOnItsBound()
    {
        var (window, container, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Container = container,
            Y = new DraggableAxisParams { Enabled = false },
            ReleaseInertia = new Inertia(),
        });
        var peak = 0d;

        Flick(window, 20, 0, 4);
        for (var t = 0; t < 5000; t += 20)
        {
            Frames.Advance(20);
            peak = Math.Max(peak, draggable.X);
        }

        Assert.True(peak > 150, $"the release never bounced past the bound, peaking at {peak}");
        Assert.Equal(150d, draggable.X);
    }

    [AvaloniaFact]
    public void ARefreshMidInertiaReleaseResolvesItAgainAndSettlesOnce()
    {
        var (window, container, target) = Show();
        var settles = 0;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Container = container,
            Y = new DraggableAxisParams { Enabled = false },
            ReleaseInertia = new Inertia() { TimeConstant = 200 },
            OnSettle = _ => settles++,
        });

        // Released at 40 with a velocity of .5, bound for 140 inside the 150 bound.
        Flick(window, 10, 0, 4);
        Frames.Advance(60, 20);
        container.Width = 100;
        Dispatcher.UIThread.RunJobs();
        draggable.Refresh();
        var refreshed = draggable.X;
        var peak = refreshed;
        for (var t = 0; t < 5000; t += 20)
        {
            Frames.Advance(20);
            peak = Math.Max(peak, draggable.X);
        }

        Assert.True(refreshed > 50, $"the release was already inside the new bound at {refreshed}");
        Assert.True(peak > refreshed, "the resolved release did not carry the measured velocity on");
        Assert.Equal(50d, draggable.X);
        Assert.Equal(1, settles);
    }

    [AvaloniaFact]
    public void AnInertiaReleaseOfATargetWiderThanItsContainerSettlesBetweenItsBounds()
    {
        var (window, container, target) = Show();
        target.Width = ContainerSize + 100;
        Dispatcher.UIThread.RunJobs();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Container = container,
            Y = new DraggableAxisParams { Enabled = false },
            ReleaseInertia = new Inertia(),
        });

        Flick(window, 20, 0, 4);
        Frames.Advance(5000, 20);

        Assert.InRange(draggable.X, -100, 0);
    }

    [AvaloniaFact]
    public void AnInertiaReleaseLeavesADisabledAxisAlone()
    {
        var (window, _, target) = Show();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            Y = new DraggableAxisParams { Enabled = false },
            ReleaseInertia = new Inertia(),
        });

        Flick(window, 20, 20, 4);
        Frames.Advance(5000, 20);

        Assert.True(draggable.X > 80, $"the enabled axis did not coast on from 80, resting at {draggable.X}");
        Assert.Equal(0d, draggable.Y);
        Assert.Equal(0d, PrestoAvalonia.Get(target, "Y"));
    }
}
