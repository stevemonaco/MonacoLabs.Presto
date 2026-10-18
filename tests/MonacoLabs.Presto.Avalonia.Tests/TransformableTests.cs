using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using MonacoLabs.Presto.Draggables;
using MonacoLabs.Presto.Gestures;

namespace MonacoLabs.Presto.Tests;

/// <summary>Covers the pointer core of <c>PrestoAvalonia.CreateTransformable</c>, driven by synthetic touch in a headless window.</summary>
public sealed class TransformableTests : BridgeTestBase
{
    private static readonly TransformableParams s_free = new() { PanBounds = PanBounds.Free };

    // A 200x200 target filling its parent, so parent and window coordinates coincide.
    private static (Window Window, Panel Parent, Border Target) Scene()
    {
        var target = new Border { Background = Brushes.Red };
        var parent = new Panel { Children = { target } };
        return (Show(parent), parent, target);
    }

    [AvaloniaFact]
    public void SpreadingTwoFingersDoublesTheScaleAndKeepsTheirMidpointFixed()
    {
        var (window, parent, target) = Scene();
        int grabs = 0, updates = 0;
        var transformable = target.CreateTransformable(s_free with { OnGrab = _ => grabs++, OnUpdate = _ => updates++ });
        var touch = new Touch(window);

        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));
        touch.Move(1, new Point(20, 60));
        touch.Move(2, new Point(100, 60));

        Assert.Equal(2, transformable.Scale, 3);
        Assert.Equal(40, transformable.X, 3);
        Assert.Equal(40, transformable.Y, 3);
        var p = target.TranslatePoint(new Point(60, 60), parent)!.Value;
        Assert.Equal(60, p.X, 3);
        Assert.Equal(60, p.Y, 3);
        Assert.True(transformable.IsManipulating);
        Assert.Equal(1, grabs);
        Assert.Equal(2, updates);
    }

    [AvaloniaFact]
    public void MovingBothFingersTogetherPans()
    {
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable(s_free);
        var touch = new Touch(window);

        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));
        touch.Move(1, new Point(70, 80));
        touch.Move(2, new Point(110, 80));

        Assert.Equal(1, transformable.Scale, 3);
        Assert.Equal(30, transformable.X, 3);
        Assert.Equal(20, transformable.Y, 3);
    }

    [AvaloniaTheory]
    [InlineData(true, 45)]
    [InlineData(false, 0)]
    public void TurningTwoFingersRotatesOnlyWhenRotateIsOn(bool rotate, double expected)
    {
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable(s_free with { Rotate = rotate });
        var touch = new Touch(window);
        var arm = 40 / Math.Sqrt(2);

        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));
        touch.Move(2, new Point(40 + arm, 60 + arm));

        Assert.Equal(expected, transformable.Rotation, 3);
        Assert.Equal(1, transformable.Scale, 3);
    }

    [AvaloniaFact]
    public void AThirdFingerChangesNothing()
    {
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable(s_free);
        var touch = new Touch(window);

        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));

        Assert.False(touch.Down(3, new Point(150, 150)).Handled);
        Assert.False(touch.Move(3, new Point(190, 190)).Handled);
        touch.Up(3, new Point(190, 190));

        Assert.Equal(1, transformable.Scale, 3);
        Assert.Equal(0, transformable.X, 3);
        Assert.Equal(0, transformable.Y, 3);
        Assert.True(transformable.IsManipulating);
    }

    [AvaloniaFact]
    public void LiftingAFingerHandsOverToAPanAndASecondFingerPinchesFromThere()
    {
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable(s_free);
        var touch = new Touch(window);

        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));
        touch.Move(2, new Point(120, 60));
        var (x, y, scale) = (transformable.X, transformable.Y, transformable.Scale);

        touch.Up(2, new Point(120, 60));
        Assert.Equal((x, y, scale), (transformable.X, transformable.Y, transformable.Scale));
        Assert.True(transformable.IsManipulating);

        touch.Move(1, new Point(50, 65));
        Assert.Equal(x + 10, transformable.X, 3);
        Assert.Equal(y + 5, transformable.Y, 3);
        Assert.Equal(scale, transformable.Scale, 3);

        (x, y) = (transformable.X, transformable.Y);
        touch.Down(3, new Point(130, 65));
        Assert.Equal((x, y, scale), (transformable.X, transformable.Y, transformable.Scale));

        touch.Move(3, new Point(170, 65));
        Assert.Equal(scale * 1.5, transformable.Scale, 3);
    }

    // Spreads (40,60)-(80,60) to (20,60)-(100,60): twice the scale about a last focal point of (60,60).
    private static void PinchPastMaxScale(Touch touch)
    {
        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));
        touch.Move(1, new Point(20, 60));
        touch.Move(2, new Point(100, 60));
        touch.Up(1, new Point(20, 60));
        touch.Up(2, new Point(100, 60));
    }

    // Zooms to 2 about the center of the 200x200 target, where Contain allows X in [-100, 100].
    private static void ZoomToTwo(Touch touch)
    {
        touch.Down(1, new Point(80, 100));
        touch.Down(2, new Point(120, 100));
        touch.Move(1, new Point(60, 100));
        touch.Move(2, new Point(140, 100));
        touch.Up(1, new Point(60, 100));
        touch.Up(2, new Point(140, 100));
    }

    [AvaloniaFact]
    public void BeyondMaxScaleTheReleaseSpringsBackAboutTheLastFocalPoint()
    {
        var (window, parent, target) = Scene();
        var transformable = target.CreateTransformable(s_free with { MaxScale = 1.5 });
        var touch = new Touch(window);
        var focal = new Point(60, 60);

        PinchPastMaxScale(touch);
        Assert.Equal(1.5 * Math.Pow(2 / 1.5, 0.35), transformable.Scale, 3);
        var content = parent.TranslatePoint(focal, target)!.Value;

        Frames.Advance(1000, 20);

        Assert.Equal(1.5, transformable.Scale, 3);
        var p = target.TranslatePoint(content, parent)!.Value;
        Assert.Equal(focal.X, p.X, 3);
        Assert.Equal(focal.Y, p.Y, 3);
    }

    [AvaloniaFact]
    public void UnderContainThePanClampsAndAFlickCoastsIntoTheEdgeAndSettlesOnIt()
    {
        var (window, _, target) = Scene();
        var peak = double.MinValue;
        Transformable? transformable = null;
        transformable = target.CreateTransformable(new TransformableParams { OnUpdate = _ => peak = Math.Max(peak, transformable!.X) });
        var touch = new Touch(window);
        ZoomToTwo(touch);
        Frames.Advance(100, 20);
        Assert.Equal(0, transformable.X, 3);

        touch.Down(1, new Point(100, 100));
        for (var x = 125; x <= 175; x += 25)
        {
            touch.Move(1, new Point(x, 100));
            Frames.Advance(20);
        }
        touch.Up(1, new Point(175, 100));
        Frames.Advance(3000, 20);

        Assert.True(peak > 100);
        Assert.Equal(100, transformable.X, 3);
        Assert.Equal(0, transformable.Y, 3);

        touch.Down(1, new Point(100, 100));
        touch.Move(1, new Point(150, 100));
        Assert.Equal(100 + 50 * 0.35, transformable.X, 3);
        touch.Up(1, new Point(150, 100));
        Frames.Advance(3000, 20);
        Assert.Equal(100, transformable.X, 3);
    }

    [AvaloniaFact]
    public void ARotationSnapLandsOnAMultiple()
    {
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable(s_free with { Rotate = true, RotationSnap = 90 });
        var touch = new Touch(window);
        var turned = new Point(40 + 40 * Math.Cos(Math.PI / 3), 60 + 40 * Math.Sin(Math.PI / 3));

        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));
        touch.Move(2, turned);
        Assert.Equal(60, transformable.Rotation, 3);
        touch.Up(1, new Point(40, 60));
        touch.Up(2, turned);
        Frames.Advance(3000, 20);

        Assert.Equal(90, transformable.Rotation, 3);
    }

    [AvaloniaFact]
    public void ACustomSettleDurationIsHonored()
    {
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable(s_free with { MaxScale = 1.5, Settle = new AnimationBuilder().Duration(300) });
        var touch = new Touch(window);

        PinchPastMaxScale(touch);
        Frames.Advance(200, 20);
        Assert.True(transformable.Scale > 1.5);

        Frames.Advance(120, 20);
        Assert.Equal(1.5, transformable.Scale, 3);
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheReleaseLandsOnItsClampedTargetAtOnce()
    {
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable(new TransformableParams { MaxScale = 1.5 });
        var touch = new Touch(window);
        Presto.Defaults.ReducedMotion = true;

        PinchPastMaxScale(touch);
        Assert.Equal(1.5, transformable.Scale, 3);

        touch.Down(1, new Point(100, 100));
        touch.Move(1, new Point(150, 100));
        Frames.Advance(20);
        touch.Move(1, new Point(200, 100));
        Assert.True(transformable.X > 50);
        touch.Up(1, new Point(200, 100));

        Assert.Equal(50, transformable.X, 3);
        Frames.Advance(20);
        Assert.Equal(50, transformable.X, 3);
    }

    [AvaloniaFact]
    public void OnReleaseAndOnSettleFireOncePerGesture()
    {
        var (window, _, target) = Scene();
        var log = new List<string>();
        target.CreateTransformable(s_free with
        {
            MaxScale = 1.5,
            OnRelease = _ => log.Add("release"),
            OnSettle = _ => log.Add("settle"),
        });
        var touch = new Touch(window);

        PinchPastMaxScale(touch);
        Assert.Equal(["release"], log);
        Frames.Advance(1000, 20);
        Assert.Equal(["release", "settle"], log);

        PinchPastMaxScale(touch);
        Frames.Advance(1000, 20);
        Assert.Equal(["release", "settle", "release", "settle"], log);
    }

    [AvaloniaFact]
    public void ATouchDuringAReleaseStopsItAndGrabsFromWhereItIs()
    {
        var (window, _, target) = Scene();
        var settles = 0;
        var transformable = target.CreateTransformable(s_free with { MaxScale = 1.5, OnSettle = _ => settles++ });
        var touch = new Touch(window);

        PinchPastMaxScale(touch);
        Frames.Advance(100, 20);
        var (x, scale) = (transformable.X, transformable.Scale);

        Assert.True(touch.Down(1, new Point(100, 100)).Handled);
        Frames.Advance(1000, 20);

        Assert.Equal((x, scale), (transformable.X, transformable.Scale));
        Assert.Equal(0, settles);
    }

    [AvaloniaFact]
    public void AtRestAOneFingerDragIsLeftToAncestors()
    {
        var (window, parent, target) = Scene();
        var transformable = target.CreateTransformable();
        var touch = new Touch(window);
        var parentMoves = 0;
        parent.PointerMoved += (_, _) => parentMoves++;

        var down = touch.Down(1, new Point(100, 100));
        var move = touch.Move(1, new Point(130, 100));

        Assert.False(down.Handled);
        Assert.False(move.Handled);
        Assert.Null(down.Pointer.Captured);
        Assert.Equal(1, parentMoves);
        Assert.Equal(0, transformable.X);
        Assert.False(transformable.IsManipulating);
    }

    [AvaloniaFact]
    public void OnceZoomedInAOneFingerDragPansRatherThanSwipingAnEnclosingCarousel()
    {
        var target = new Border { Background = Brushes.Red };
        var carousel = new Carousel { IsSwipeEnabled = true, ItemsSource = new Control[] { new Panel { Children = { target } }, new Border() } };
        var window = Show(carousel);
        var transformable = target.CreateTransformable(s_free).ZoomTo(2, animate: false);
        var touch = new Touch(window);

        touch.Down(1, new Point(100, 100));
        for (var x = 90; x >= 40; x -= 10) touch.Move(1, new Point(x, 100));

        Assert.Equal(-60, transformable.X, 3);
        Assert.True(transformable.IsManipulating);
        Assert.False(carousel.IsSwiping);
        touch.Up(1, new Point(40, 100));
        Assert.Equal(0, carousel.SelectedIndex);
    }

    [AvaloniaFact]
    public void AMouseDragPansOnceZoomedIn()
    {
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable();
        var touch = new Touch(window);
        ZoomToTwo(touch);
        Assert.Equal(2, transformable.Scale, 3);
        Assert.False(transformable.IsManipulating);

        window.MouseDown(new Point(100, 100), MouseButton.Left);
        window.MouseMove(new Point(130, 110));
        window.MouseUp(new Point(130, 110), MouseButton.Left);

        Assert.Equal(30, transformable.X, 3);
        Assert.Equal(10, transformable.Y, 3);
    }

    private static void DoubleClick(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    [AvaloniaFact]
    public void ADoubleTapZoomsToDoubleTapScaleAboutTheTapAndTogglesBack()
    {
        var (window, parent, target) = Scene();
        var transformable = target.CreateTransformable();
        var tap = new Point(60, 80);
        var content = parent.TranslatePoint(tap, target)!.Value;

        DoubleClick(window, tap);
        Frames.Advance(1000, 20);

        Assert.Equal(2.5, transformable.Scale, 3);
        var p = target.TranslatePoint(content, parent)!.Value;
        Assert.Equal(tap.X, p.X, 3);
        Assert.Equal(tap.Y, p.Y, 3);

        DoubleClick(window, tap);
        Frames.Advance(1000, 20);

        Assert.Equal((0, 0, 1), (transformable.X, transformable.Y, transformable.Scale));
    }

    [AvaloniaFact]
    public void ControlWheelZoomsAboutTheCursorAtOnce()
    {
        var (window, parent, target) = Scene();
        var transformable = target.CreateTransformable();
        var cursor = new Point(60, 80);
        var content = parent.TranslatePoint(cursor, target)!.Value;

        window.MouseWheel(cursor, new Vector(0, 1), RawInputModifiers.Control);

        Assert.Equal(1.15, transformable.Scale, 3);
        var p = target.TranslatePoint(content, parent)!.Value;
        Assert.Equal(cursor.X, p.X, 3);
        Assert.Equal(cursor.Y, p.Y, 3);
    }

    [AvaloniaFact]
    public void WithZoomOffTheWheelAndADoubleTapLeaveTheScale()
    {
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable(new TransformableParams { Zoom = false });
        var wheels = 0;
        window.AddHandler(InputElement.PointerWheelChangedEvent, (_, _) => wheels++, handledEventsToo: false);

        window.MouseWheel(new Point(60, 80), new Vector(0, 1), RawInputModifiers.Control);
        DoubleClick(window, new Point(60, 80));
        Frames.Advance(1000, 20);

        Assert.Equal(1, transformable.Scale);
        Assert.Equal(1, wheels);
    }

    [AvaloniaTheory]
    [InlineData(WheelZoom.WithControl, 1, true)]
    [InlineData(WheelZoom.Always, 1 / 1.15, false)]
    public void APlainWheelZoomsOnlyUnderAlwaysAndOtherwiseScrollsTheAncestor(WheelZoom mode, double scale, bool scrolls)
    {
        var target = new Border { Background = Brushes.Red, Height = 200 };
        var scroller = new ScrollViewer { Content = new StackPanel { Children = { target, new Border { Height = 400 } } } };
        var window = Show(scroller);
        var transformable = target.CreateTransformable(new TransformableParams { WheelZoom = mode, MinScale = 0.5 });

        window.MouseWheel(new Point(100, 100), new Vector(0, -1));

        Assert.Equal(scale, transformable.Scale, 3);
        Assert.Equal(scrolls, scroller.Offset.Y > 0);
    }

    [AvaloniaTheory]
    [InlineData(WheelZoom.WithControl)]
    [InlineData(WheelZoom.Always)]
    public void AHorizontalOnlyWheelUnderAZoomingModeIsLeftToTheAncestor(WheelZoom mode)
    {
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable(new TransformableParams { WheelZoom = mode });
        var wheels = 0;
        window.AddHandler(InputElement.PointerWheelChangedEvent, (_, _) => wheels++, handledEventsToo: false);

        window.MouseWheel(new Point(60, 80), new Vector(1, 0), RawInputModifiers.Control);
        Assert.Equal((1, 1), (transformable.Scale, wheels));

        window.MouseWheel(new Point(60, 80), new Vector(1, 1), RawInputModifiers.Control);
        Assert.Equal((1.15, 1), (Math.Round(transformable.Scale, 3), wheels));
    }

    [AvaloniaFact]
    public void ZoomPanAndRotateToClampAndPlayThroughSettle()
    {
        var (_, _, target) = Scene();
        var transformable = target.CreateTransformable(new TransformableParams { Rotate = true, Settle = new AnimationBuilder().Duration(300) });

        transformable.ZoomTo(10);
        Frames.Advance(150, 20);
        Assert.InRange(transformable.Scale, 1.01, 3.99);
        Frames.Advance(200, 20);
        Assert.Equal((0, 0, 4), (transformable.X, transformable.Y, transformable.Scale));

        transformable.PanTo(1000, -1000);
        Assert.Equal(0, transformable.X);
        Frames.Advance(350, 20);
        Assert.Equal((300, -300), (transformable.X, transformable.Y));

        transformable.RotateTo(90);
        Frames.Advance(350, 20);
        Assert.Equal((300, 300, 90), (transformable.X, transformable.Y, transformable.Rotation));
    }

    [AvaloniaFact]
    public void UnanimatedMovesApplyAtOnceAndResetRestoresIdentity()
    {
        var (_, _, target) = Scene();
        var updates = 0;
        var transformable = target.CreateTransformable(new TransformableParams { Rotate = true, OnUpdate = _ => updates++ });

        transformable.ZoomTo(2, new Point(0, 0), animate: false);
        Assert.Equal((100, 100, 2), (transformable.X, transformable.Y, transformable.Scale));
        transformable.RotateTo(45, animate: false);
        Assert.Equal(45, transformable.Rotation);
        transformable.ZoomTo(0.1, animate: false);
        Assert.Equal(1, transformable.Scale);
        Assert.Equal(3, updates);

        transformable.Reset(animate: false);
        Assert.Equal((0, 0, 1, 0), (transformable.X, transformable.Y, transformable.Scale, transformable.Rotation));
    }

    [AvaloniaFact]
    public void ADraggableAndATransformableCannotShareATargetUntilOneIsReverted()
    {
        var (_, _, target) = Scene();

        var draggable = PrestoAvalonia.CreateDraggable(target);
        var error = Assert.Throws<InvalidOperationException>(() => target.CreateTransformable());
        Assert.Contains(nameof(Draggable), error.Message);
        Assert.Contains(nameof(Transformable), error.Message);

        draggable.Revert();
        var transformable = target.CreateTransformable();
        error = Assert.Throws<InvalidOperationException>(() => PrestoAvalonia.CreateDraggable(target));
        Assert.Contains(nameof(Draggable), error.Message);
        Assert.Contains(nameof(Transformable), error.Message);

        transformable.Revert();
        PrestoAvalonia.CreateDraggable(target);
    }

    [AvaloniaFact]
    public void ASecondGestureOfTheSameTypeCannotShareATargetUntilTheFirstIsReverted()
    {
        var (_, _, target) = Scene();

        var draggable = PrestoAvalonia.CreateDraggable(target);
        var error = Assert.Throws<InvalidOperationException>(() => PrestoAvalonia.CreateDraggable(target));
        Assert.Contains(nameof(Draggable), error.Message);

        draggable.Revert();
        var transformable = target.CreateTransformable();
        Assert.Throws<InvalidOperationException>(() => target.CreateTransformable());

        transformable.Revert();
        target.CreateTransformable();
    }

    // Drags pointer 1 from `from` by `step` per 20 ms frame, then lets go mid-motion so the release carries a throw.
    private void Flick(Touch touch, Point from, Vector step, int steps = 3)
    {
        touch.Down(1, from);
        for (var i = 1; i <= steps; i++)
        {
            Frames.Advance(20);
            touch.Move(1, from + step * i);
        }
        touch.Up(1, from + step * steps);
    }

    private static (double X, double Y, double Scale, double Rotation) State(Transformable transformable) =>
        (transformable.X, transformable.Y, transformable.Scale, transformable.Rotation);

    [AvaloniaFact]
    public void RevertingTheScopeItWasCreatedInRevertsIt()
    {
        var (_, parent, target) = Scene();
        var scope = Presto.CreateScope();
        var transformable = scope.Execute(_ => target.CreateTransformable(s_free));
        transformable.ZoomTo(2, animate: false);
        Assert.Equal(-100, target.TranslatePoint(default, parent)!.Value.X, 3);

        scope.Revert();

        Assert.Equal(new Point(0, 0), target.TranslatePoint(default, parent));
        PrestoAvalonia.CreateDraggable(target);
    }

    [AvaloniaFact]
    public void ItKeepsTheEngineThatWasCurrentAtCreation()
    {
        var (window, _, target) = Scene();
        var frames = new ManualFrameSource();
        var engine = new Engine(frames) { Precision = 0 };
        Transformable transformable;
        using (engine.MakeCurrent()) transformable = target.CreateTransformable(s_free with { PanAtRest = true });
        var touch = new Touch(window);

        touch.Down(1, new Point(100, 100));
        for (var i = 1; i <= 3; i++)
        {
            frames.Advance(20);
            touch.Move(1, new Point(100 + 10.4 * i, 100));
        }
        Assert.Equal(31, transformable.X);
        Assert.Equal(31.2, transformable.Animate.Get("X"), 3);
        Assert.InRange(transformable.Velocity, .45, .6);
        touch.Up(1, new Point(131.2, 100));
        Frames.Advance(500, 20);
        Assert.Equal(31.2, transformable.Animate.Get("X"), 3);
        frames.Advance(3000, 20);
        var coasted = transformable.Animate.Get("X");
        Assert.True(coasted > 35, $"coasted to {coasted}");

        engine.Defaults.ReducedMotion = true;
        transformable.PanTo(0, 0, animate: false);
        touch.Down(1, new Point(195, 100));
        for (var i = 1; i <= 3; i++)
        {
            frames.Advance(20);
            touch.Move(1, new Point(195 - 10 * i, 100));
        }
        touch.Up(1, new Point(165, 100));
        frames.Advance(500, 20);
        Assert.Equal(-30, transformable.Animate.Get("X"), 3);
    }

    [AvaloniaFact]
    public void WithPanOffTheFingersDoNotPanButAZoomStillKeepsItsFocalPointFixed()
    {
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable(s_free with { Pan = false });
        var touch = new Touch(window);

        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));
        touch.Move(1, new Point(20, 60));
        touch.Move(2, new Point(100, 60));

        Assert.Equal(2, transformable.Scale, 3);
        Assert.Equal(140 / 3d, transformable.X, 3);
        Assert.Equal(40, transformable.Y, 3);

        var (still, _, stillTarget) = Scene();
        var panless = stillTarget.CreateTransformable(s_free with { Pan = false, Zoom = false });
        var fingers = new Touch(still);
        fingers.Down(1, new Point(40, 60));
        fingers.Down(2, new Point(80, 60));
        fingers.Move(1, new Point(70, 80));
        fingers.Move(2, new Point(110, 80));

        Assert.True(panless.IsManipulating);
        Assert.Equal((0, 0, 1), (panless.X, panless.Y, panless.Scale));
    }

    [AvaloniaFact]
    public void AGrabOnARubberBandedStatePicksUpWithoutAJump()
    {
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable(s_free with { MaxScale = 1.5 });
        var touch = new Touch(window);
        PinchPastMaxScale(touch);
        Frames.Advance(20);
        var (x, y, scale) = (transformable.X, transformable.Y, transformable.Scale);
        Assert.True(scale > 1.5);

        touch.Down(1, new Point(100, 100));
        touch.Move(1, new Point(101, 100));

        Assert.Equal(scale, transformable.Scale, 3);
        Assert.Equal(x + 1, transformable.X, 3);
        Assert.Equal(y, transformable.Y, 3);

        var (contained, _, containedTarget) = Scene();
        var edge = containedTarget.CreateTransformable();
        var fingers = new Touch(contained);
        ZoomToTwo(fingers);
        Frames.Advance(100, 20);
        fingers.Down(1, new Point(100, 100));
        fingers.Move(1, new Point(250, 100));
        fingers.Up(1, new Point(250, 100));
        var banded = edge.X;
        Assert.True(banded > 100);

        fingers.Down(1, new Point(100, 100));
        fingers.Move(1, new Point(110, 100));

        Assert.Equal(banded + 10 * 0.35, edge.X, 3);
    }

    [AvaloniaFact]
    public void ItsReadingsAreRoundedToTheEnginePrecisionAndVelocityIsInUnitsPerMillisecond()
    {
        Presto.Engine.Precision = 1;
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable(s_free with { Rotate = true, PanAtRest = true });

        transformable.ZoomTo(1.23456, new Point(0, 0), animate: false).RotateTo(12.3456, new Point(0, 0), animate: false);

        foreach (var (name, reported) in new[] { ("X", transformable.X), ("Y", transformable.Y), ("Scale", transformable.Scale), ("Rotate", transformable.Rotation) })
        {
            var raw = transformable.Animate.Get(name);
            Assert.NotEqual(raw, reported);
            Assert.Equal(Math.Round(raw, 1), reported);
        }

        var touch = new Touch(window);
        touch.Down(1, new Point(100, 100));
        for (var i = 1; i <= 4; i++)
        {
            Frames.Advance(20);
            touch.Move(1, new Point(100 + 10 * i, 100 + 5 * i));
        }

        Assert.Equal(new Vector(.5, .25).Length, transformable.Velocity, 2);
    }

    [AvaloniaFact]
    public void AReleasedPinchKeepsItsScale()
    {
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable(s_free);
        var touch = new Touch(window);

        touch.Down(1, new Point(80, 100));
        touch.Down(2, new Point(120, 100));
        for (var i = 1; i <= 4; i++)
        {
            Frames.Advance(20);
            touch.Move(1, new Point(80 - 5 * i, 100));
            touch.Move(2, new Point(120 + 5 * i, 100));
        }
        touch.Up(1, new Point(60, 100));
        touch.Up(2, new Point(140, 100));
        Frames.Advance(1000, 20);

        Assert.Equal(2, transformable.Scale, 3);
    }

    [AvaloniaFact]
    public void WhenTheReleaseLandsAtOnceOnSettleFollowsOnReleaseSynchronouslyUnlessOnReleaseStops()
    {
        Presto.Defaults.ReducedMotion = true;
        var (window, _, target) = Scene();
        var log = new List<string>();
        var stop = false;
        target.CreateTransformable(s_free with
        {
            PanAtRest = true,
            OnRelease = transformable =>
            {
                log.Add("release");
                if (stop) transformable.Stop();
            },
            OnSettle = _ => log.Add("settle"),
        });
        var touch = new Touch(window);

        touch.Down(1, new Point(100, 100));
        touch.Move(1, new Point(130, 100));
        touch.Up(1, new Point(130, 100));
        Assert.Equal(["release", "settle"], log);

        stop = true;
        touch.Down(1, new Point(100, 100));
        touch.Move(1, new Point(130, 100));
        touch.Up(1, new Point(130, 100));
        Frames.Advance(100, 20);

        Assert.Equal(["release", "settle", "release"], log);
    }

    [AvaloniaFact]
    public void WithoutAReleaseInertiaTheReleaseCoastsAsADefaultInertia()
    {
        double Coast(Inertia? inertia)
        {
            var (window, _, target) = Scene();
            var transformable = target.CreateTransformable(s_free with { PanAtRest = true, ReleaseInertia = inertia });
            Flick(new Touch(window), new Point(50, 100), new Vector(10, 0));
            Frames.Advance(3000, 20);
            return transformable.X;
        }

        var unset = Coast(null);

        Assert.True(unset > 31, $"coasted to {unset}");
        Assert.Equal(Coast(new Inertia()), unset, 3);
    }

    private static void MoveTo(Transformable transformable, string verb) => _ = verb switch
    {
        "ZoomTo" => transformable.ZoomTo(1.5, animate: false),
        "PanTo" => transformable.PanTo(10, 10, animate: false),
        "RotateTo" => transformable.RotateTo(30, animate: false),
        _ => transformable.Reset(animate: false),
    };

    [AvaloniaTheory]
    [InlineData("ZoomTo")]
    [InlineData("PanTo")]
    [InlineData("RotateTo")]
    [InlineData("Reset")]
    public void AProgrammaticMoveStopsTheCoastAndDropsTheGestureWithoutARelease(string verb)
    {
        var (window, _, target) = Scene();
        var releases = 0;
        var transformable = target.CreateTransformable(s_free with { PanAtRest = true, OnRelease = _ => releases++ });
        var touch = new Touch(window);
        Flick(touch, new Point(50, 100), new Vector(10, 0));
        Frames.Advance(40, 20);

        MoveTo(transformable, verb);
        var moved = State(transformable);
        Frames.Advance(1000, 20);
        Assert.Equal(moved, State(transformable));

        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));
        touch.Move(2, new Point(100, 60));
        MoveTo(transformable, verb);
        moved = State(transformable);
        Assert.False(transformable.IsManipulating);

        touch.Move(2, new Point(140, 60));
        touch.Up(1, new Point(40, 60));
        touch.Up(2, new Point(140, 60));
        Assert.Equal(moved, State(transformable));
        Assert.Equal(1, releases);
    }

    [AvaloniaFact]
    public void WithoutAVisualParentProgrammaticMovesAndRefreshDoNothing()
    {
        var (_, parent, target) = Scene();
        var updates = 0;
        var transformable = target.CreateTransformable(s_free with { Rotate = true, OnUpdate = _ => updates++ });
        parent.Children.Remove(target);

        transformable.ZoomTo(2, animate: false).PanTo(10, 10, animate: false).RotateTo(30, animate: false).Refresh();
        transformable.ZoomTo(2).PanTo(10, 10).RotateTo(30);
        Frames.Advance(1000, 20);

        Assert.Equal(0, updates);
        Assert.Equal((0, 0, 1, 0), State(transformable));
    }

    [AvaloniaFact]
    public void StopFreezesTheTransformAndItsSettleButKeepsThePointers()
    {
        var (window, _, target) = Scene();
        var settles = 0;
        var transformable = target.CreateTransformable(s_free with { Settle = new AnimationBuilder().Duration(300), OnSettle = _ => settles++ });

        transformable.ZoomTo(3);
        Frames.Advance(100, 20);
        transformable.Stop();
        var scale = transformable.Scale;
        Frames.Advance(1000, 20);

        Assert.InRange(scale, 1.01, 2.6);
        Assert.Equal(scale, transformable.Scale);
        Assert.Equal(0, settles);

        var touch = new Touch(window);
        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));
        transformable.Stop();
        touch.Move(2, new Point(100, 60));

        Assert.True(transformable.IsManipulating);
        Assert.Equal(scale * 1.5, transformable.Scale, 3);
    }

    [AvaloniaFact]
    public void RefreshClampsTheScaleAndTheTranslationAtOnceWhenNotSettling()
    {
        var (window, _, target) = Scene();
        var updates = 0;
        var transformable = target.CreateTransformable(s_free with { MaxScale = 1.5, OnUpdate = _ => updates++ });
        var touch = new Touch(window);
        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));
        touch.Move(1, new Point(20, 60));
        touch.Move(2, new Point(100, 60));
        Assert.True(transformable.Scale > 1.5);
        updates = 0;

        transformable.Refresh();

        Assert.Equal(1.5, transformable.Scale);
        Assert.Equal(1, updates);

        var (_, parent, contained) = Scene();
        var refreshed = contained.CreateTransformable().ZoomTo(2, new Point(0, 0), animate: false);
        Assert.Equal((100, 100), (refreshed.X, refreshed.Y));
        parent.Width = 100;
        parent.UpdateLayout();

        refreshed.Refresh();

        Assert.Equal((50, 100), (refreshed.X, refreshed.Y));
    }

    [AvaloniaFact]
    public void RefreshDuringACoastReAimsItAtTheNewBounds()
    {
        var (window, parent, target) = Scene();
        var transformable = target.CreateTransformable();
        var touch = new Touch(window);
        ZoomToTwo(touch);
        Frames.Advance(100, 20);
        Flick(touch, new Point(100, 100), new Vector(25, 0));
        Frames.Advance(40, 20);
        var x = transformable.X;
        parent.Width = 100;
        parent.UpdateLayout();

        transformable.Refresh();

        Assert.Equal(x, transformable.X);
        Frames.Advance(3000, 20);
        Assert.Equal(50, transformable.X, 3);
    }

    [AvaloniaFact]
    public void DisableDropsTheGestureAndStopsListeningUntilEnabled()
    {
        var (window, _, target) = Scene();
        int grabs = 0, releases = 0;
        var transformable = target.CreateTransformable(s_free with { OnGrab = _ => grabs++, OnRelease = _ => releases++ });
        var touch = new Touch(window);
        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));
        touch.Move(2, new Point(100, 60));
        var scale = transformable.Scale;

        transformable.Disable();

        Assert.False(transformable.IsManipulating);
        touch.Move(2, new Point(140, 60));
        touch.Up(1, new Point(40, 60));
        touch.Up(2, new Point(140, 60));
        Assert.False(touch.Down(1, new Point(40, 60)).Handled);
        Assert.False(touch.Down(2, new Point(80, 60)).Handled);
        touch.Move(2, new Point(120, 60));
        touch.Up(1, new Point(40, 60));
        touch.Up(2, new Point(120, 60));
        Assert.Equal(scale, transformable.Scale);
        Assert.Equal((1, 0), (grabs, releases));

        transformable.Enable().Enable();
        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));
        touch.Move(2, new Point(120, 60));

        Assert.Equal(scale * 2, transformable.Scale, 3);
        Assert.Equal(2, grabs);
    }

    [AvaloniaFact]
    public void WithoutRotateSetTurningTwoFingersDoesNotRotate()
    {
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable(new TransformableParams { PanBounds = PanBounds.Free });
        var touch = new Touch(window);
        var arm = 40 / Math.Sqrt(2);

        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));
        touch.Move(2, new Point(40 + arm, 60 + arm));

        Assert.Equal(0, transformable.Rotation);
    }

    [AvaloniaFact]
    public void WithoutRotationSnapAReleasedTurnIsNotSnapped()
    {
        Presto.Defaults.ReducedMotion = true;
        var (window, _, target) = Scene();
        var transformable = target.CreateTransformable(s_free with { Rotate = true });
        var touch = new Touch(window);
        var turned = new Point(40 + 40 * Math.Cos(Math.PI / 3), 60 + 40 * Math.Sin(Math.PI / 3));

        touch.Down(1, new Point(40, 60));
        touch.Down(2, new Point(80, 60));
        touch.Move(2, turned);
        touch.Up(1, new Point(40, 60));
        touch.Up(2, turned);
        Frames.Advance(1000, 20);

        Assert.Equal(60, transformable.Rotation, 3);
    }
}
