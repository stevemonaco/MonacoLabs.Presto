using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MonacoLabs.Presto.Compositor;
using MonacoLabs.Presto.Gestures;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Swipe"/> (Phase 63b): rows 200 wide with 60-wide actions on each side, swiped by synthetic touch, so an
/// action opens at 60 and a swipe commits past 100.
/// </summary>
public sealed class SwipeTests : CompositorTestBase
{
    private const double Open = 60;

    [AvaloniaFact]
    public void APartialRevealSnapsOpen()
    {
        var (window, rows) = Stage();

        new Touch(window).Drag(Frames, Center(rows[0], window), new Vector(-40, 0));
        Frames.Advance(3000, 20);

        Assert.Equal(-Open, X(rows[0]), 3);
        var action = (Button)Swipe.GetTrailing(rows[0])!;
        Assert.True(action.IsEffectivelyVisible);
        Assert.Equal(200 - Open, action.TranslatePoint(default, window)!.Value.X);
    }

    [AvaloniaFact]
    public void ActionsStayPutWhileARowIsDraggedByFractionalOffsets()
    {
        var (window, rows) = Stage();
        var action = (Button)Swipe.GetTrailing(rows[0])!;
        var touch = new Touch(window);
        var from = Center(rows[0], window);

        touch.Press(1, from);
        Frames.Advance(20);
        touch.Move(1, from + new Vector(-12, 0));
        foreach (var dx in new[] { -16.0, -20.3, -20.7, -31.45, -44.5, -52.25 })
        {
            touch.Move(1, from + new Vector(dx, 0));
            Assert.Equal(dx, X(rows[0]), 6);
            Assert.Equal(200 - Open, action.TranslatePoint(default, window)!.Value.X, 6);
        }
        touch.Up(1, from + new Vector(-52.25, 0));
    }

    [AvaloniaFact]
    public void AShortDragSnapsClosed()
    {
        var (window, rows) = Stage();

        new Touch(window).Drag(Frames, Center(rows[0], window), new Vector(-20, 0));
        Frames.Advance(3000, 20);

        Assert.Equal(0, X(rows[0]), 3);
        Assert.False(((Button)Swipe.GetTrailing(rows[0])!).IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void AShortReleaseIsBackAtRestWithinFourHundredMilliseconds()
    {
        var (window, rows) = Stage();

        new Touch(window).Drag(Frames, Center(rows[0], window), new Vector(-20, 0));
        Frames.Advance(400, 16);

        Assert.InRange(X(rows[0]), -1, 0);
    }

    [AvaloniaFact]
    public void AFlingOpens()
    {
        var (window, rows) = Stage();

        new Touch(window).Drag(Frames, Center(rows[0], window), new Vector(24, 0), steps: 2, hold: false);
        Frames.Advance(3000, 20);

        Assert.Equal(Open, X(rows[0]), 3);
    }

    [AvaloniaFact]
    public void WithoutRevealAPartialDragOrAFlingSpringsClosedAndAFullSwipeStillCommits()
    {
        var command = new Recorder();
        var (window, rows) = Stage(row =>
        {
            Swipe.SetCommand(row, command);
            Swipe.SetReveal(row, false);
        });

        new Touch(window).Drag(Frames, Center(rows[0], window), new Vector(-40, 0));
        Frames.Advance(3000, 20);
        Assert.Equal(0, X(rows[0]), 3);

        new Touch(window).Drag(Frames, Center(rows[0], window), new Vector(24, 0), steps: 2, hold: false);
        Frames.Advance(3000, 20);
        Assert.Equal(0, X(rows[0]), 3);
        Assert.Empty(command.Runs);

        new Touch(window).Drag(Frames, Center(rows[0], window), new Vector(-130, 0));
        Frames.Advance(3000, 20);
        Assert.Equal(new object?[] { Edge.Left }, command.Runs);
    }

    [AvaloniaFact]
    public void AFullSwipeCommitsAndRunsTheCommand()
    {
        var command = new Recorder();
        var (window, rows) = Stage(row => Swipe.SetCommand(row, command));

        new Touch(window).Drag(Frames, Center(rows[0], window), new Vector(-130, 0));
        Frames.Advance(3000, 20);

        Assert.Equal(new object?[] { Edge.Left }, command.Runs);
        Assert.Equal(0, X(rows[0]), 3);
    }

    [AvaloniaFact]
    public void ADismissPlaysTheExitAndRemovesTheRowWhileItemLayoutClosesTheGap()
    {
        var items = new ObservableCollection<string> { "a", "red", "c" };
        var remove = new Recorder(edge => items.Remove("red"));
        var list = new ItemsControl
        {
            ItemsSource = items,
            VerticalAlignment = VerticalAlignment.Top,
            ItemTemplate = new FuncDataTemplate<string>((item, _) =>
            {
                var row = new Border { Height = 40, Background = item == "red" ? Brushes.Red : Brushes.Blue };
                Swipe.SetTrailing(row, new FuncDataTemplate<string>((_, _) => new Button { Width = Open }));
                Swipe.SetDismiss(row, true);
                Swipe.SetCommand(row, remove);
                Presence.SetExit(row, Presto.Build().Opacity(to: 0).Duration(200).Ease("linear"));
                return row;
            }),
        };
        Animate.SetItemLayout(list, HeldMove);
        var window = ShowWindow(list);
        var below = list.ContainerFromIndex(2)!;

        new Touch(window).Drag(Frames, Center(RowOf(list, "red"), window), new Vector(-130, 0));
        Run(20);

        Assert.Equal(["a", "c"], items);
        Assert.Single(Ghosts(window));
        Assert.True(LayoutTransition.Live(below) is { Completed: false });
    }

    [AvaloniaTheory]
    [InlineData(-9, 3)]
    [InlineData(-30, -16)]
    public void ATouchWithinItsSlopOrNotClearlyHorizontalNeverOpens(double dx, double dy)
    {
        var (window, rows) = Stage();

        new Touch(window).Drag(Frames, Center(rows[0], window), new Vector(dx, dy));
        Frames.Advance(3000, 20);

        Assert.Equal(0, X(rows[0]), 3);
    }

    [AvaloniaFact]
    public void AVerticalDragScrollsTheParentAndNeverOpens()
    {
        var rows = Enumerable.Range(0, 10).Select(_ => SwipeRow()).ToArray();
        var scroller = new ScrollViewer { Content = Stack(rows) };
        var window = ShowWindow(scroller);
        var touch = new Touch(window);

        touch.Down(1, new Point(100, 100));
        foreach (var (x, y) in new[] { (101, 98), (104, 70), (104, 40), (104, 10) })
        {
            Frames.Advance(20);
            touch.Move(1, new Point(x, y));
        }
        touch.Up(1, new Point(104, 10));
        Frames.Advance(1000, 20);

        Assert.True(scroller.Offset.Y > 0);
        Assert.All(rows, row => Assert.Equal(0, X(row), 3));
    }

    [AvaloniaFact]
    public void ASecondRowOpeningClosesTheFirst()
    {
        var (window, rows) = Stage();
        new Touch(window).Drag(Frames, Center(rows[0], window), new Vector(-40, 0));
        Frames.Advance(3000, 20);

        new Touch(window).Drag(Frames, Center(rows[1], window), new Vector(-40, 0));
        Frames.Advance(3000, 20);

        Assert.Equal(0, X(rows[0]), 3);
        Assert.Equal(-Open, X(rows[1]), 3);
    }

    [AvaloniaTheory]
    [InlineData("escape")]
    [InlineData("tap outside")]
    [InlineData("tap the row")]
    public void EscapeOrATapClosesTheOpenRow(string how)
    {
        var (window, rows) = Stage();
        new Touch(window).Drag(Frames, Center(rows[0], window), new Vector(-40, 0));
        Frames.Advance(3000, 20);

        switch (how)
        {
            case "escape":
                rows[2].RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = rows[2] });
                break;
            case "tap outside":
                Press(window, new Point(100, 160));
                break;
            default:
                Press(window, new Point(60, 20));
                break;
        }
        Frames.Advance(3000, 20);

        Assert.Equal(0, X(rows[0]), 3);
    }

    [AvaloniaFact]
    public void ALongPressContextRequestDuringOrJustAfterATouchLeavesTheRowClosed()
    {
        var (window, rows) = Stage();
        var touch = new Touch(window);
        var at = Center(rows[0], window);

        touch.Down(1, at);
        var during = new ContextRequestedEventArgs();
        rows[0].RaiseEvent(during);
        touch.Up(1, at);
        rows[0].RaiseEvent(new ContextRequestedEventArgs());
        Frames.Advance(3000, 20);

        Assert.False(during.Handled);
        Assert.Equal(0, X(rows[0]), 3);
    }

    [AvaloniaFact]
    public void APressAnotherGestureCapturesIsLostAndTheNextSwipeOpens()
    {
        var (window, rows) = Stage();
        var touch = new Touch(window);
        var at = Center(rows[0], window);
        var pointer = touch.Press(1, at);
        pointer.Capture(window);
        touch.Move(1, at - new Point(40, 0));
        touch.Up(1, at - new Point(40, 0));
        Assert.Equal(0, X(rows[0]), 3);

        new Touch(window).Drag(Frames, at, new Vector(-40, 0));
        Frames.Advance(3000, 20);

        Assert.Equal(-Open, X(rows[0]), 3);
    }

    [AvaloniaTheory]
    [InlineData(PointerType.Mouse, -6, true)]
    [InlineData(PointerType.Pen, -6, false)]
    [InlineData(PointerType.Pen, -40, true)]
    public void APenSwipesWithTouchsSlop(PointerType type, double dx, bool opens)
    {
        var (window, rows) = Stage(row => Swipe.SetTrailing(row, new Button { Width = 4 }));
        var at = Center(rows[0], window);

        if (type == PointerType.Mouse)
        {
            window.MouseDown(at, MouseButton.Left);
            window.MouseMove(at + new Point(dx, 0));
            window.MouseUp(at + new Point(dx, 0), MouseButton.Left);
        }
        else
        {
            var pen = new Touch(window);
            pen.Press(1, at, type);
            pen.Move(1, at + new Point(dx, 0));
            pen.Up(1, at + new Point(dx, 0));
        }
        Frames.Advance(3000, 20);

        Assert.Equal(opens, X(rows[0]) < 0);
    }

    [AvaloniaFact]
    public void AContextRequestOpensTheTrailingThenTheLeadingActionsAndTheyRun()
    {
        var command = new Recorder();
        var (window, rows) = Stage();
        var action = (Button)Swipe.GetTrailing(rows[0])!;
        action.Command = command;

        rows[0].RaiseEvent(new ContextRequestedEventArgs());
        Assert.True(action.IsFocused);
        Frames.Advance(3000, 20);
        Assert.Equal(-Open, X(rows[0]), 3);

        Press(window, Center(action, window));
        Assert.Single(command.Runs);

        rows[0].RaiseEvent(new ContextRequestedEventArgs());
        Frames.Advance(3000, 20);
        Assert.Equal(Open, X(rows[0]), 3);

        rows[0].RaiseEvent(new ContextRequestedEventArgs());
        Frames.Advance(3000, 20);
        Assert.Equal(0, X(rows[0]), 3);
    }

    [AvaloniaFact]
    public void UnderReducedMotionAReleaseLandsAndADismissCommitsAtOnce()
    {
        var command = new Recorder();
        var (window, rows) = Stage(row =>
        {
            Swipe.SetCommand(row, command);
            Swipe.SetDismiss(row, true);
        });
        Presto.Defaults.ReducedMotion = true;

        new Touch(window).Drag(Frames, Center(rows[0], window), new Vector(-40, 0));
        Assert.Equal(-Open, X(rows[0]), 3);

        new Touch(window).Drag(Frames, Center(rows[1], window), new Vector(130, 0));
        Assert.Equal(new object?[] { Edge.Right }, command.Runs);
        Assert.False(rows[1].IsVisible);
    }

    private static Border SwipeRow()
    {
        var row = new Border { Height = 40, Background = Brushes.Red };
        Swipe.SetLeading(row, new Button { Width = Open });
        Swipe.SetTrailing(row, new Button { Width = Open });
        return row;
    }

    private static (Window Window, Border[] Rows) Stage(Action<Border>? decorate = null)
    {
        var rows = Enumerable.Range(0, 3).Select(_ => SwipeRow()).ToArray();
        foreach (var row in rows) decorate?.Invoke(row);
        return (ShowWindow(Stack(rows)), rows);
    }

    private static StackPanel Stack(Border[] rows)
    {
        var panel = new StackPanel();
        panel.Children.AddRange(rows);
        return panel;
    }

    private static Window ShowWindow(Control content)
    {
        var window = new Window { Content = content, Background = Brushes.White, Width = 200, Height = 200 };
        window.Show();
        Tick();
        return window;
    }

    private static Border RowOf(ItemsControl list, string item) => (Border)((ContentPresenter)list.ContainerFromItem(item)!).Child!;

    private sealed class Recorder(Action<object?>? execute = null) : ICommand
    {
        public List<object?> Runs { get; } = [];

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            Runs.Add(parameter);
            execute?.Invoke(parameter);
        }
    }
}
