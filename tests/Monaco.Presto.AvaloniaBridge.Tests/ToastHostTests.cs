using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.AvaloniaBridge.Controls;

namespace Monaco.Presto.AvaloniaBridge.Tests;

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

        if (how == "hover") window.MouseMove(Centre(root, window));
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

        new Touch(window).Drag(Frames, Centre(container, window), new Vector(-130, 0));
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

    private static (Window Window, ToastHost Host) Stage()
    {
        var host = new ToastHost
        {
            VerticalAlignment = VerticalAlignment.Top,
            ItemTemplate = new FuncDataTemplate<string>((_, _) =>
            {
                var bar = new Border { Height = 4, Background = Brushes.Blue };
                ToastHost.SetCountdown(bar, "ScaleX");
                var root = new Border
                {
                    Background = Brushes.Red,
                    Child = new StackPanel { Children = { new Button { Height = 30 }, bar } },
                };
                Presence.SetArrive(root, Recipes.SlideIn(Edge.Right, distance: 100, duration: 100, ease: Eases.None));
                Presence.SetExit(root, Presto.Build().Opacity(to: 0).Duration(200).Ease("linear"));
                return root;
            }),
        };
        var window = new Window { Content = new StackPanel { Children = { host, new Button() } }, Background = Brushes.White, Width = 200, Height = 200 };
        window.Show();
        Tick();
        return (window, host);
    }

    private static Border Root(ToastHost host, string item) => (Border)((ContentPresenter)host.ContainerFromItem(item)!).Child!;

    private static Border Bar(ToastHost host, string item) =>
        Root(host, item).GetVisualDescendants().OfType<Border>().Single(border => ToastHost.GetCountdown(border) is not null);

    private static double ScaleX(Visual visual) => Convert.ToDouble(PrestoAvalonia.Get(visual, "ScaleX"));
}
