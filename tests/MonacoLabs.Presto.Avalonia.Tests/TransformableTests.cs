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
}
