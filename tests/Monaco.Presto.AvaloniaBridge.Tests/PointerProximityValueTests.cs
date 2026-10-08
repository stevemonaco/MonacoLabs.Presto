using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Events;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary><see cref="PrestoAvalonia.PointerProximityValue"/> and <see cref="PrestoAvalonia.PointerOffsetValue"/> over their shared <see cref="PointerTracker"/>.</summary>
public sealed class PointerProximityValueTests : BridgeTestBase
{
    // The box's layout slot spans 50 to 100 on both axes.
    private static Border MarginBox()
    {
        var box = Box();
        box.Margin = new Thickness(50, 50, 0, 0);
        return box;
    }

    [AvaloniaFact]
    public void ProximityFallsOffLinearlyFromTheEdgeOrTheCenter()
    {
        var box = MarginBox();
        var window = Show(box);
        var edge = PrestoAvalonia.PointerProximityValue(box, 40);
        var center = PrestoAvalonia.PointerProximityValue(box, 40, fromCenter: true);

        window.MouseMove(new Point(75, 75));
        Assert.Equal((1d, 1d), (edge.Value, center.Value));

        window.MouseMove(new Point(75, 95));
        Assert.Equal((1d, .5), (edge.Value, center.Value));

        window.MouseMove(new Point(120, 75));
        Assert.Equal((.5, 0d), (edge.Value, center.Value));

        window.MouseMove(new Point(110, 110));
        Assert.Equal(1 - (Math.Sqrt(200) / 40), edge.Value, 9);

        window.MouseMove(new Point(75, 150));
        Assert.Equal(0d, edge.Value);
    }

    [AvaloniaFact]
    public void OffsetsAreThePointerMinusTheCenterOnEachAxis()
    {
        var box = MarginBox();
        var window = Show(box);
        var x = PrestoAvalonia.PointerOffsetValue(box, Orientation.Horizontal);
        var y = PrestoAvalonia.PointerOffsetValue(box, Orientation.Vertical);
        Assert.Equal((0d, 0d), (x.Value, y.Value));

        window.MouseMove(new Point(90, 60));
        Assert.Equal((15d, -15d), (x.Value, y.Value));

        window.MouseMove(new Point(10, 190));
        Assert.Equal((-65d, 115d), (x.Value, y.Value));
    }

    [AvaloniaFact]
    public void AValueMadeBeforeAttachTracksItsTopLevelOnceAttached()
    {
        var box = MarginBox();
        var x = PrestoAvalonia.PointerOffsetValue(box, Orientation.Horizontal);
        var window = Show(box);

        window.MouseMove(new Point(90, 60));

        Assert.Equal(15d, x.Value);
    }

    [AvaloniaFact]
    public void TheValuesReadZeroOutsideTheirSurface()
    {
        var box = MarginBox();
        var stage = new Border { Width = 120, Height = 120, Background = Brushes.Blue, Child = box, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var window = Show(stage);
        var proximity = PrestoAvalonia.PointerProximityValue(box, 40, over: stage);
        var x = PrestoAvalonia.PointerOffsetValue(box, Orientation.Horizontal, over: stage);
        window.MouseMove(new Point(110, 75));
        Assert.Equal((.75, 35d), (proximity.Value, x.Value));

        window.MouseMove(new Point(130, 75));

        Assert.Equal((0d, 0d), (proximity.Value, x.Value));
    }

    [AvaloniaFact]
    public void TheValuesReadZeroAfterThePointerLeavesTheWindow()
    {
        var box = MarginBox();
        var window = Show(box);
        var proximity = PrestoAvalonia.PointerProximityValue(box, 40);
        IPointer? mouse = null;
        window.PointerMoved += (_, e) => mouse = e.Pointer;
        window.MouseMove(new Point(75, 75));
        Assert.Equal(1d, proximity.Value);

        window.RaiseEvent(new PointerEventArgs(InputElement.PointerExitedEvent, window, mouse!, window, new Point(-1, 75), 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None));

        Assert.Equal(0d, proximity.Value);
    }

    [AvaloniaFact]
    public void ATouchCountsOnlyWhileItsContactIsDown()
    {
        var box = MarginBox();
        var window = Show(box);
        var proximity = PrestoAvalonia.PointerProximityValue(box, 40);
        var touch = new Touch(window);

        touch.Down(1, new Point(75, 75));
        Assert.Equal(1d, proximity.Value);
        touch.Move(1, new Point(120, 75));
        Assert.Equal(.5, proximity.Value);

        touch.Up(1, new Point(120, 75));

        Assert.Equal(0d, proximity.Value);
    }

    [AvaloniaFact]
    public void ThePointerThatMovedLastWins()
    {
        var box = MarginBox();
        var window = Show(box);
        var proximity = PrestoAvalonia.PointerProximityValue(box, 40);
        var touch = new Touch(window);

        window.MouseMove(new Point(75, 75));
        touch.Down(1, new Point(120, 75));
        Assert.Equal(.5, proximity.Value);

        window.MouseMove(new Point(75, 75));
        Assert.Equal(1d, proximity.Value);

        touch.Up(1, new Point(120, 75));
        Assert.Equal(1d, proximity.Value);
    }

    [AvaloniaFact]
    public void ChildrenThatHandleOrCaptureThePointerDoNotHideIt()
    {
        var button = new Button { Width = 50, Height = 50, Margin = new Thickness(50, 50, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var stage = new Panel { Background = Brushes.Blue, Children = { button } };
        var outer = new Border { Child = stage };
        outer.AddHandler(InputElement.PointerMovedEvent, (_, e) => e.Handled = true, RoutingStrategies.Tunnel);
        var window = Show(outer);
        var x = PrestoAvalonia.PointerOffsetValue(button, Orientation.Horizontal, over: stage);

        window.MouseDown(new Point(75, 75), MouseButton.Left);
        window.MouseMove(new Point(90, 75));

        Assert.Equal(15d, x.Value);
    }

    [AvaloniaFact]
    public void TheControlsOwnRenderTransformDoesNotChangeTheValues()
    {
        var box = MarginBox();
        box.RenderTransform = new TransformGroup { Children = { new ScaleTransform(2, 2), new TranslateTransform(30, 20) } };
        var window = Show(box);
        var proximity = PrestoAvalonia.PointerProximityValue(box, 40);
        var x = PrestoAvalonia.PointerOffsetValue(box, Orientation.Horizontal);

        window.MouseMove(new Point(120, 75));

        Assert.Equal((.5, 45d), (proximity.Value, x.Value));
    }

    [AvaloniaFact]
    public void ScrollingTheControlUnderAStillPointerUpdatesTheValue()
    {
        var box = MarginBox();
        var scroller = new ScrollViewer { Content = new StackPanel { Height = 1000, Children = { box } } };
        var window = Show(scroller);
        var y = PrestoAvalonia.PointerOffsetValue(box, Orientation.Vertical);
        window.MouseMove(new Point(75, 75));
        Assert.Equal(0d, y.Value);

        scroller.Offset = new Vector(0, 40);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(40d, y.Value);
    }

    [AvaloniaFact]
    public void ValuesOverOneSurfaceShareOneTrackerRemovedWithTheLast()
    {
        var box = MarginBox();
        var window = Show(box);
        var first = Presto.CreateScope();
        var second = Presto.CreateScope();
        first.Execute(_ => PrestoAvalonia.PointerProximityValue(box, 40));
        var tracker = window.GetValue(PointerTracker.TrackerProperty);
        second.Execute(_ => PrestoAvalonia.PointerOffsetValue(box, Orientation.Horizontal));

        Assert.NotNull(tracker);
        Assert.Same(tracker, window.GetValue(PointerTracker.TrackerProperty));
        first.Revert();
        Assert.Same(tracker, window.GetValue(PointerTracker.TrackerProperty));
        second.Revert();
        Assert.Null(window.GetValue(PointerTracker.TrackerProperty));
    }

    [AvaloniaFact]
    public void DetachAndScopeRevertStopTheValueForGood()
    {
        var box = MarginBox();
        var panel = new Panel { Children = { box } };
        var window = Show(panel);
        var detached = PrestoAvalonia.PointerOffsetValue(box, Orientation.Horizontal);
        var scope = Presto.CreateScope();
        var reverted = scope.Execute(_ => PrestoAvalonia.PointerOffsetValue(box, Orientation.Vertical));
        window.MouseMove(new Point(90, 60));

        scope.Revert();
        panel.Children.Remove(box);
        panel.Children.Add(box);
        Dispatcher.UIThread.RunJobs();
        window.MouseMove(new Point(10, 10));

        Assert.Equal((15d, -15d), (detached.Value, reverted.Value));
    }

    [AvaloniaFact]
    public void OverASurfaceTheValueOutlivesItsControlBeingReHosted()
    {
        var box = MarginBox();
        var stage = new Border { Background = Brushes.Blue };
        var panel = new Panel { Children = { stage, box } };
        var window = Show(panel);
        var x = PrestoAvalonia.PointerOffsetValue(box, Orientation.Horizontal, over: stage);

        panel.Children.Remove(box);
        panel.Children.Add(box);
        Dispatcher.UIThread.RunJobs();
        window.MouseMove(new Point(90, 10));

        Assert.Equal(15d, x.Value);
    }

    [AvaloniaFact]
    public void RemovingTheSurfaceStopsItsValues()
    {
        var box = MarginBox();
        var stage = new Border { Background = Brushes.Blue };
        var panel = new Panel { Children = { stage, box } };
        var window = Show(panel);
        var x = PrestoAvalonia.PointerOffsetValue(box, Orientation.Horizontal, over: stage);
        window.MouseMove(new Point(90, 10));

        panel.Children.Remove(stage);
        panel.Children.Add(stage);
        Dispatcher.UIThread.RunJobs();
        window.MouseMove(new Point(10, 10));

        Assert.Equal(15d, x.Value);
        Assert.Null(stage.GetValue(PointerTracker.TrackerProperty));
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void ANonPositiveRadiusThrows(double radius) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PrestoAvalonia.PointerProximityValue(Box(), radius));
}
