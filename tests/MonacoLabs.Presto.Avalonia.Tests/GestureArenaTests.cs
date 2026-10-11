using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using MonacoLabs.Presto.Gestures;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// How <see cref="Swipe"/>, <see cref="Reorder"/> and a scroller share a press on a Tempo-shaped list: a 100-high
/// <see cref="ScrollViewer"/> around a reorderable <see cref="ListBox"/> of eight 40-high swipe rows with a 400 ms touch hold;
/// and how <see cref="GestureArena"/> decides between stub members on nested elements.
/// </summary>
public sealed class GestureArenaTests : BridgeTestBase
{
    private static readonly Point OnSecond = new(100, 60);
    private static readonly Point Middle = new(100, 100);

    [AvaloniaFact]
    public void ATouchScrollScrollsWithoutLiftingOrOpeningARow()
    {
        var stage = TaskList();
        var touch = new Touch(stage.Window);

        touch.Press(1, OnSecond);
        for (var dy = 12; dy <= 48; dy += 12)
        {
            Frames.Advance(20);
            touch.Move(1, OnSecond - new Point(0, dy));
        }
        Assert.True(stage.Scroller.Offset.Y > 0);
        Frames.Advance(500);
        touch.Up(1, OnSecond - new Point(0, 48));
        Frames.Advance(1000);

        Assert.False(Lifted(stage, "1"));
        Assert.Empty(stage.Moves);
        Assert.All(stage.Rows.Values, row => Assert.Equal(0, X(row), 3));
    }

    [AvaloniaFact]
    public void ATouchSwipeOpensTheRowsActionsAndMovesNothing()
    {
        var stage = TaskList();
        var touch = new Touch(stage.Window);

        touch.Press(1, OnSecond);
        for (var dx = 10; dx <= 50; dx += 10)
        {
            Frames.Advance(20);
            touch.Move(1, OnSecond - new Point(dx, 1));
        }

        Assert.True(X(stage.Rows["1"]) < 0);
        Assert.True(((Button)Swipe.GetTrailing(stage.Rows["1"])!).IsEffectivelyVisible);
        Assert.Equal(0, stage.Scroller.Offset.Y);
        Assert.False(Lifted(stage, "1"));
        touch.Up(1, OnSecond - new Point(50, 1));
        Assert.Empty(stage.Moves);
    }

    [AvaloniaFact]
    public void ATouchHeldStillPastTheHoldLiftsTheRow()
    {
        var stage = TaskList();
        var touch = new Touch(stage.Window);

        touch.Press(1, OnSecond);
        Frames.Advance(450);

        Assert.True(Lifted(stage, "1"));
        Assert.Equal(0, X(stage.Rows["1"]), 3);
    }

    [AvaloniaFact]
    public void AHorizontalMouseDragSwipesAndAVerticalOneReorders()
    {
        var stage = TaskList();

        stage.Window.MouseDown(OnSecond, MouseButton.Left);
        stage.Window.MouseMove(OnSecond - new Point(40, 0));
        Assert.True(X(stage.Rows["1"]) < 0);
        stage.Window.MouseUp(OnSecond - new Point(40, 0), MouseButton.Left);
        Frames.Advance(1000);
        Assert.Empty(stage.Moves);

        stage.Window.MouseDown(OnSecond, MouseButton.Left);
        stage.Window.MouseMove(OnSecond + new Point(0, 50));

        Assert.Equal([(1, 2)], stage.Moves);
        Assert.All(stage.Rows.Values, row => Assert.Equal(0, X(row), 3));
    }

    [AvaloniaFact]
    public void ATouchThatScrollsWithinTheHoldSlopAndRestsDoesNotLift()
    {
        var stage = TaskList();
        var touch = new Touch(stage.Window);

        touch.Press(1, OnSecond);
        touch.Move(1, OnSecond - new Point(0, 3));
        touch.Move(1, OnSecond - new Point(0, 6));
        Assert.True(stage.Scroller.Offset.Y > 0);
        Frames.Advance(450);

        Assert.False(Lifted(stage, "1"));
    }

    [AvaloniaTheory]
    [InlineData(PointerType.Mouse, false, 3, 0, null)]
    [InlineData(PointerType.Mouse, false, 4, 3, "won")]
    [InlineData(PointerType.Mouse, false, 4, 4, "lost")]
    [InlineData(PointerType.Touch, false, 10, 0, null)]
    [InlineData(PointerType.Touch, false, 11, 5, "won")]
    [InlineData(PointerType.Touch, false, 11, 5.5, "lost")]
    [InlineData(PointerType.Pen, false, 10, 0, null)]
    [InlineData(PointerType.Pen, false, 11, 5, "won")]
    [InlineData(PointerType.Touch, true, 3, 0, null)]
    [InlineData(PointerType.Touch, true, 4, 3, "won")]
    [InlineData(PointerType.Touch, true, 4, 4, "lost")]
    public void AGateClaimsPastItsSlopAlongItsAxisByItsDominance(PointerType type, bool sameAxis, double dx, double dy, string? expected)
    {
        var nest = Nested();
        nest.Joins("inner", nest.Inner, sameAxis ? GestureGate.SameAxis(Orientation.Horizontal) : GestureGate.Crosswise(Orientation.Horizontal));
        var to = Middle + new Point(dx, dy);

        if (type == PointerType.Mouse)
        {
            nest.Window.MouseDown(Middle, MouseButton.Left);
            nest.Window.MouseMove(to);
            Assert.Equal(expected is null ? [] : [$"inner {expected}"], nest.Log);
            nest.Window.MouseUp(to, MouseButton.Left);
            return;
        }
        var touch = new Touch(nest.Window);
        touch.Press(1, Middle, type);
        touch.Move(1, to);
        Assert.Equal(expected is null ? [] : [$"inner {expected}"], nest.Log);
        touch.Up(1, to);
    }

    [AvaloniaFact]
    public void AMoveInADirectionTheGateDoesNotTakeDropsItsMemberForTheRestOfThePress()
    {
        var nest = Nested();
        nest.Joins("inner", nest.Inner, GestureGate.Crosswise(Orientation.Horizontal));

        nest.Window.MouseDown(Middle, MouseButton.Left);
        nest.Window.MouseMove(Middle + new Point(1, 10));
        nest.Window.MouseMove(Middle + new Point(40, 10));
        nest.Window.MouseUp(Middle + new Point(40, 10), MouseButton.Left);

        Assert.Equal(["inner lost"], nest.Log);
    }

    [AvaloniaFact]
    public void AGateWhoseAcceptsRefusesTheTravelDropsItsMember()
    {
        var nest = Nested();
        nest.Joins("inner", nest.Inner, GestureGate.Crosswise(Orientation.Horizontal) with { Accepts = travel => travel.X > 0 });

        nest.Window.MouseDown(Middle, MouseButton.Left);
        nest.Window.MouseMove(Middle - new Point(10, 0));
        nest.Window.MouseUp(Middle - new Point(10, 0), MouseButton.Left);

        Assert.Equal(["inner lost"], nest.Log);
    }

    [AvaloniaFact]
    public void WhenTwoGatesPassOnOneMoveTheDeeperElementWinsThoughItJoinedLast()
    {
        var nest = Nested();
        nest.Joins("outer", nest.Outer, GestureGate.SameAxis(Orientation.Horizontal));
        nest.Joins("inner", nest.Inner, GestureGate.SameAxis(Orientation.Horizontal));

        nest.Window.MouseDown(Middle, MouseButton.Left);
        nest.Window.MouseMove(Middle + new Point(10, 0));
        nest.Window.MouseUp(Middle + new Point(10, 0), MouseButton.Left);

        Assert.Equal(["inner won", "outer lost"], nest.Log);
    }

    [AvaloniaFact]
    public void TheWinnerIsToldBeforeTheLosersAndThePointerEndsCapturedToItsCaptureElement()
    {
        var nest = Nested();
        nest.Joins("outer", nest.Outer, GestureGate.Crosswise(Orientation.Vertical));
        nest.Joins("inner", nest.Inner, GestureGate.SameAxis(Orientation.Horizontal), capture: nest.Outer);
        var touch = new Touch(nest.Window);

        var pointer = touch.Press(1, Middle);
        touch.Move(1, Middle + new Point(5, 0));

        Assert.Equal(["inner won", "outer lost"], nest.Log);
        Assert.Same(nest.Outer, pointer.Captured);
        touch.Move(1, Middle + new Point(5, 40));
        touch.Up(1, Middle + new Point(5, 40));
        Assert.Equal(["inner won", "outer lost"], nest.Log);
    }

    [AvaloniaFact]
    public void ATouchHeldStillPastAMembersHoldWinsForIt()
    {
        var nest = Nested();
        nest.Joins("inner", nest.Inner, GestureGate.Crosswise(Orientation.Horizontal) with { Hold = 400 });
        var touch = new Touch(nest.Window);

        var pointer = touch.Press(1, Middle);
        Frames.Advance(350);
        Assert.Empty(nest.Log);
        Frames.Advance(100);

        Assert.Equal(["inner won"], nest.Log);
        Assert.Same(nest.Inner, pointer.Captured);
        touch.Up(1, Middle);
    }

    [AvaloniaFact]
    public void AHoldStopsWhenAnotherMemberWins()
    {
        var nest = Nested();
        nest.Joins("outer", nest.Outer, GestureGate.Crosswise(Orientation.Vertical) with { Hold = 400 });
        nest.Joins("inner", nest.Inner, GestureGate.SameAxis(Orientation.Horizontal));
        var touch = new Touch(nest.Window);

        touch.Press(1, Middle);
        touch.Move(1, Middle + new Point(5, 0));
        Frames.Advance(500);
        touch.Up(1, Middle + new Point(5, 0));

        Assert.Equal(["inner won", "outer lost"], nest.Log);
    }

    [AvaloniaFact]
    public void AMovePastTheHoldSlopStopsTheHoldAndLeavesTheMemberPending()
    {
        var nest = Nested();
        nest.Joins("inner", nest.Inner, GestureGate.Crosswise(Orientation.Vertical) with { Hold = 400 });
        var touch = new Touch(nest.Window);

        touch.Press(1, Middle);
        touch.Move(1, Middle + new Point(8, 8));
        Frames.Advance(500);
        touch.Up(1, Middle + new Point(8, 8));

        Assert.Equal(["inner tap"], nest.Log);
    }

    [AvaloniaFact]
    public void ACaptureElsewhereTellsEveryPendingMemberItLostAndStopsTheHold()
    {
        var nest = Nested();
        nest.Joins("outer", nest.Outer, GestureGate.SameAxis(Orientation.Vertical));
        nest.Joins("inner", nest.Inner, GestureGate.Crosswise(Orientation.Horizontal) with { Hold = 400 });
        var touch = new Touch(nest.Window);

        var pointer = touch.Press(1, Middle);
        pointer.Capture(nest.Sibling);
        Frames.Advance(500);
        touch.Move(1, Middle + new Point(40, 0));
        touch.Up(1, Middle + new Point(40, 0));

        Assert.Equal(["outer lost", "inner lost"], nest.Log);
    }

    [AvaloniaFact]
    public void AScrollRecognizerCaptureTellsThePendingMemberItLost()
    {
        var log = new List<string>();
        var content = new Border { Height = 400, Background = Brushes.Red };
        var scroller = new ScrollViewer { Height = 100, VerticalAlignment = VerticalAlignment.Top, Content = content };
        var window = Show(scroller);
        Joins(content, "content", GestureGate.Crosswise(Orientation.Horizontal) with { Hold = 400 }, log);
        var touch = new Touch(window);

        touch.Press(1, new Point(100, 50));
        touch.Move(1, new Point(100, 44));
        Assert.True(scroller.Offset.Y > 0);
        Frames.Advance(500);
        touch.Up(1, new Point(100, 44));

        Assert.Equal(["content lost"], log);
    }

    [AvaloniaFact]
    public void AReleaseBeforeAnyClaimEndsEveryMemberAsATap()
    {
        var nest = Nested();
        nest.Joins("outer", nest.Outer, GestureGate.SameAxis(Orientation.Vertical));
        nest.Joins("inner", nest.Inner, GestureGate.Crosswise(Orientation.Horizontal));
        var touch = new Touch(nest.Window);

        touch.Press(1, Middle);
        touch.Move(1, Middle + new Point(2, 0));
        touch.Up(1, Middle + new Point(2, 0));

        Assert.Equal(["outer tap", "inner tap"], nest.Log);
    }

    [AvaloniaFact]
    public void AClosedArenaHearsNoLaterMoveOrRelease()
    {
        var nest = Nested();
        nest.Joins("inner", nest.Inner, GestureGate.SameAxis(Orientation.Horizontal));

        nest.Window.MouseDown(Middle, MouseButton.Left);
        nest.Window.MouseUp(Middle, MouseButton.Left);
        nest.Window.MouseDown(new Point(10, 10), MouseButton.Left);
        nest.Window.MouseMove(new Point(60, 10));
        nest.Window.MouseUp(new Point(60, 10), MouseButton.Left);

        Assert.Equal(["inner tap"], nest.Log);
    }

    // Joined in the tunnel, so an outer member joins before the inner one.
    private static void Joins(Control element, string name, GestureGate gate, List<string> log, IInputElement? capture = null) =>
        element.AddHandler(InputElement.PointerPressedEvent,
            (_, e) => GestureArena.Join(e, new Member(name, element, capture ?? element, log), gate), RoutingStrategies.Tunnel);

    private static Nest Nested()
    {
        var inner = new Border { Width = 100, Height = 100, Background = Brushes.Red };
        var sibling = new Border
        {
            Width = 20, Height = 20, Background = Brushes.Green,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        var outer = new Border { Background = Brushes.Blue, Child = new Panel { Children = { inner, sibling } } };
        return new(Show(outer), outer, inner, sibling, []);
    }

    private static Stage TaskList()
    {
        var rows = new Dictionary<string, Border>();
        ObservableCollection<string> items = ["0", "1", "2", "3", "4", "5", "6", "7"];
        var list = new ListBox
        {
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<string>((item, _) =>
            {
                var row = new Border { Height = 40, Background = Brushes.Blue };
                Swipe.SetLeading(row, new Button { Width = 60 });
                Swipe.SetTrailing(row, new Button { Width = 60 });
                Swipe.SetReveal(row, false);
                if (item is not null) rows[item] = row;
                return row;
            }, supportsRecycling: false),
            Styles =
            {
                new Style(x => x.OfType<ListBoxItem>())
                {
                    Setters = { new Setter(Layoutable.MinHeightProperty, 0d), new Setter(TemplatedControl.PaddingProperty, new Thickness(0)) },
                },
            },
        };
        Reorder.SetEnabled(list, true);
        Reorder.SetTouchHold(list, 400);
        var moves = new List<(int, int)>();
        items.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Move) moves.Add((e.OldStartingIndex, e.NewStartingIndex));
        };
        var scroller = new ScrollViewer { Height = 100, VerticalAlignment = VerticalAlignment.Top, Content = list };
        return new(Show(scroller), scroller, list, rows, moves);
    }

    private static bool Lifted(Stage stage, string item) => stage.List.ContainerFromItem(item)!.ZIndex == int.MaxValue;

    private static double X(Visual visual) => Convert.ToDouble(PrestoAvalonia.Get(visual, "X"));

    private sealed record Stage(
        Window Window, ScrollViewer Scroller, ListBox List, Dictionary<string, Border> Rows, List<(int, int)> Moves);

    private sealed record Nest(Window Window, Border Outer, Border Inner, Border Sibling, List<string> Log)
    {
        public void Joins(string name, Control element, GestureGate gate, IInputElement? capture = null) =>
            GestureArenaTests.Joins(element, name, gate, Log, capture);
    }

    private sealed class Member(string name, Visual element, IInputElement capture, List<string> log) : IGestureMember
    {
        public Visual Element => element;

        public IInputElement CaptureTarget => capture;

        public void Won() => log.Add($"{name} won");

        public void Lost() => log.Add($"{name} lost");

        public void EndedWithoutClaim() => log.Add($"{name} tap");
    }
}
