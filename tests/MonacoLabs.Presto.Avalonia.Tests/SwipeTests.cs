using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
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
    public void AShortDragSnapsClosed()
    {
        var (window, rows) = Stage();

        new Touch(window).Drag(Frames, Center(rows[0], window), new Vector(-20, 0));
        Frames.Advance(3000, 20);

        Assert.Equal(0, X(rows[0]), 3);
        Assert.False(((Button)Swipe.GetTrailing(rows[0])!).IsEffectivelyVisible);
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
