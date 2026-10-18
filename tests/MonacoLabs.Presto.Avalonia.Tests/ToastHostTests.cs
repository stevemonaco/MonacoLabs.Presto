using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Compositor;
using MonacoLabs.Presto.Controls;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="ToastHost"/> (Phase 64b): toasts 200 wide that slide in from the right and fade out, each with a countdown
/// bar on <c>ScaleX</c> and a button to focus, above a button outside them.
/// </summary>
public sealed class ToastHostTests : CompositorTestBase
{
    [AvaloniaFact]
    public void ShowPlaysTheEntrance()
    {
        var (_, host) = Stage();

        host.Show("a");
        Run(2);
        Assert.InRange(X(Root(host, "a")), 1, 99);

        Run(10);
        Assert.Equal(0, X(Root(host, "a")), 3);
    }

    [AvaloniaFact]
    public void TheCountdownRunsTheBarDownAndDismissesTheToastWithItsExit()
    {
        var (window, host) = Stage();

        host.Show("a", 500);
        Run(20);
        Assert.Single(host.Items);
        Assert.InRange(ScaleX(Bar(host, "a")), .2, .8);

        Run(20);
        Assert.Empty(host.Items);
        Assert.Single(Ghosts(window));
    }

    [AvaloniaTheory]
    [InlineData("hover")]
    [InlineData("focus")]
    public void HoverOrFocusHoldsTheCountdownUntilItLeaves(string how)
    {
        var (window, host) = Stage();
        host.Show("a", 500);
        Run(10);
        var root = Root(host, "a");
        var button = root.GetVisualDescendants().OfType<Button>().Single();

        if (how == "hover") window.MouseMove(Center(root, window));
        else button.Focus();
        var held = ScaleX(Bar(host, "a"));
        Run(60);
        Assert.Equal(held, ScaleX(Bar(host, "a")), 6);
        Assert.Single(host.Items);

        if (how == "hover") window.MouseMove(new Point(1, 199));
        else ((StackPanel)window.Content!).Children[1].Focus();
        Run(40);
        Assert.Empty(host.Items);
    }

    [AvaloniaFact]
    public void ASwipeSlidesTheToastOutAndDismissesItWithoutItsExit()
    {
        var (window, host) = Stage();
        host.Show("a");
        Run(12);
        var container = host.ContainerFromIndex(0)!;

        new Touch(window).Drag(Frames, Center(container, window), new Vector(-130, 0));
        Run(2);
        Assert.True(X(container) < -130);

        Run(30);
        Assert.Empty(host.Items);
        Assert.Empty(Ghosts(window));
    }

    [AvaloniaFact]
    public void PastMaxCountTheOldestLeavesAndTheRestRestack()
    {
        var (window, host) = Stage();
        Animate.SetItemLayout(host, HeldMove);
        host.MaxCount = 2;
        host.Show("a");
        host.Show("b");
        Run(12);

        host.Show("c");
        Run(2);

        Assert.Equal(["b", "c"], host.Items.Cast<string>());
        Assert.Single(Ghosts(window));
        Assert.True(LayoutTransition.Live(host.ContainerFromIndex(0)!) is { Completed: false });
    }

    [AvaloniaFact]
    public void DismissRemovesTheToastAndToastFindsItFromInside()
    {
        var (window, host) = Stage();
        var toast = host.Show("a");
        Run(12);

        var button = Root(host, "a").GetVisualDescendants().OfType<Button>().Single();
        Assert.Same(toast, ToastHost.GetToast(button));
        toast.Dismiss();

        Assert.Empty(host.Items);
        Assert.Single(Ghosts(window));
    }

    [AvaloniaTheory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("source")]
    public void ChangingItemsOrSettingItemsSourceThrows(string how)
    {
        var (_, host) = Stage();
        if (how == "remove") host.Show("a");
        Run(2);

        Assert.Throws<InvalidOperationException>(() =>
        {
            if (how == "add") host.Items.Add("b");
            else if (how == "remove") host.Items.RemoveAt(0);
            else host.ItemsSource = new[] { "b" };
        });
    }

    [AvaloniaFact]
    public void ARepreparedToastFollowsItsNewContainer()
    {
        var (window, host) = Stage();
        host.Show("a", 500);
        Run(10);
        var old = host.ContainerFromIndex(0)!;

        host.ItemTemplate = Template();
        Run(2);
        var container = host.ContainerFromIndex(0)!;
        Assert.NotSame(old, container);
        window.MouseMove(Center(container, window));
        Run(60);
        Assert.Single(host.Items);

        window.MouseMove(new Point(1, 199));
        Run(40);
        Assert.Empty(host.Items);
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheToastArrivesInPlaceAndTheBarFadesInsteadOfShrinking()
    {
        Presto.Defaults.ReducedMotion = true;
        var (_, host) = Stage();

        host.Show("a", 500);
        Run(2);
        Assert.Equal(0, X(Root(host, "a")), 3);

        Run(18);
        var bar = Bar(host, "a");
        Assert.Equal(1, ScaleX(bar), 3);
        Assert.InRange(bar.Opacity, .4, .99);

        Run(20);
        Assert.Empty(host.Items);
    }

    [AvaloniaFact]
    public void ShowToastShowsTheRaisedValueOrItsContent()
    {
        var (_, host) = Stage();
        var cue = new Cue<string>();
        Animate.SetCue(host, cue);
        Animate.SetDoOnCue(host, (CueAction)new ShowToastExtension().ProvideValue(null!));

        cue.Raise("a");
        Assert.Equal(["a"], host.Items.Cast<string>());

        Animate.SetDoOnCue(host, (CueAction)new ShowToastExtension { Content = "b" }.ProvideValue(null!));
        cue.Raise("a");
        Assert.Equal(["a", "b"], host.Items.Cast<string>());
    }

    [AvaloniaFact]
    public void ShowToastSkipsAFirstValueAndThrowsOffAHostOrWithNothingToShow()
    {
        var (_, host) = Stage();
        var action = (CueAction)new ShowToastExtension().ProvideValue(null!);

        action.Invoke(host, "a", first: true);
        Assert.Empty(host.Items);

        Assert.Throws<InvalidOperationException>(() => action.Invoke(new Border(), "a", first: false));
        Assert.Throws<InvalidOperationException>(() => action.Invoke(host, null, first: false));
    }

    [AvaloniaFact]
    public void DismissOnClickDismissesTheToastAndTheButtonsCommandStillRuns()
    {
        var (window, host) = Stage();
        object? undone = null;
        host.DataContext = new TestCommand(parameter => undone = parameter);
        host.ItemTemplate = new FuncDataTemplate<string>((_, _) =>
        {
            var button = new Button { Height = 30 };
            ToastHost.SetDismissOnClick(button, true);
            button.Bind(Button.CommandProperty, new Binding("DataContext") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ToastHost) } });
            button.Bind(Button.CommandParameterProperty, new Binding());
            return button;
        });
        host.Show("a");
        Run(12);

        var button = host.ContainerFromIndex(0)!.GetVisualDescendants().OfType<Button>().Single();
        var center = Center(button, window);
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);

        Assert.Equal("a", undone);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(host.Items);
    }

    [AvaloniaFact]
    public void ItKeepsTheItemsControlTheme()
    {
        var (_, host) = Stage();

        Assert.NotNull(host.Template);
        Assert.Single(host.GetVisualDescendants().OfType<ItemsPresenter>());
    }

    [AvaloniaFact]
    public void WithoutAMarkedElementAPlainTimerDismissesTheToast()
    {
        var (window, host) = Stage(countdown: false);

        host.Show("a", 500);
        Run(20);
        Assert.Single(host.Items);

        Run(20);
        Assert.Empty(host.Items);
        Assert.Single(Ghosts(window));
    }

    [AvaloniaFact]
    public void ACountdownStartingWithFocusWithinStartsPaused()
    {
        var (window, host) = Stage();

        host.Show("a", 500);
        window.UpdateLayout();
        Assert.True(Root(host, "a").GetVisualDescendants().OfType<Button>().Single().Focus());
        Run(60);
        Assert.Single(host.Items);
        Assert.Equal(1, ScaleX(Bar(host, "a")), 6);

        ((StackPanel)window.Content!).Children[1].Focus();
        Run(40);
        Assert.Empty(host.Items);
    }

    [AvaloniaFact]
    public void ASecondDismissDoesNothing()
    {
        var (window, host) = Stage();
        var toast = host.Show("a");
        host.Show("b");
        Run(12);
        toast.Dismiss();

        toast.Dismiss();

        Assert.Equal(["b"], host.Items.Cast<string>());
        Assert.Single(Ghosts(window));
    }

    [AvaloniaTheory]
    [InlineData(null, new[] { "b", "c", "d" })]
    [InlineData(0, new[] { "d" })]
    [InlineData(-2, new[] { "d" })]
    public void MaxCountDefaultsToThreeAndAtMostZeroKeepsOne(int? maxCount, string[] kept)
    {
        var (_, host) = Stage();
        if (maxCount is { } max) host.MaxCount = max;

        foreach (var item in new[] { "a", "b", "c", "d" }) host.Show(item);
        Run(2);

        Assert.Equal(kept, host.Items.Cast<string>());
    }

    [AvaloniaFact]
    public void TheCountdownDefaultsToFourSeconds()
    {
        var (_, host) = Stage(countdown: false);

        host.Show("a");
        Run(240);
        Assert.Single(host.Items);

        Run(20);
        Assert.Empty(host.Items);
    }

    private static (Window Window, ToastHost Host) Stage(bool countdown = true)
    {
        var host = new ToastHost
        {
            VerticalAlignment = VerticalAlignment.Top,
            ItemTemplate = Template(countdown),
        };
        var window = new Window { Content = new StackPanel { Children = { host, new Button() } }, Background = Brushes.White, Width = 200, Height = 200 };
        window.Show();
        Tick();
        return (window, host);
    }

    private static FuncDataTemplate<string> Template(bool countdown = true) => new((_, _) =>
    {
        var bar = new Border { Height = 4, Background = Brushes.Blue };
        if (countdown) ToastHost.SetCountdown(bar, "ScaleX");
        var root = new Border
        {
            Background = Brushes.Red,
            Child = new StackPanel { Children = { new Button { Height = 30 }, bar } },
        };
        Presence.SetArrive(root, Recipes.SlideIn(Edge.Right, distance: 100, duration: 100, ease: Eases.None));
        Presence.SetExit(root, Presto.Build().Opacity(to: 0).Duration(200).Ease("linear"));
        return root;
    });

    private static Border Root(ToastHost host, string item) => (Border)((ContentPresenter)host.ContainerFromItem(item)!).Child!;

    private static Border Bar(ToastHost host, string item) =>
        Root(host, item).GetVisualDescendants().OfType<Border>().Single(border => ToastHost.GetCountdown(border) is not null);

    private static double ScaleX(Visual visual) => Convert.ToDouble(PrestoAvalonia.Get(visual, "ScaleX"));
}
