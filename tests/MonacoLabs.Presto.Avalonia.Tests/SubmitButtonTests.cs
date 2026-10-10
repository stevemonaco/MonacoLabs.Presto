using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Prebuilt;
using Path = Avalonia.Controls.Shapes.Path;

namespace MonacoLabs.Presto.Tests;

/// <summary>The <see cref="SubmitButton"/> recipe driven by an awaitable or a state (GAPS-2-PLAN §10.2 65b).</summary>
public sealed class SubmitButtonTests : BridgeTestBase
{
    private static readonly IBrush Green = Brushes.Green;
    private static readonly IBrush Red = Brushes.Red;

    private static Button Submit()
    {
        var button = new Button { Content = "Send", Width = 160, Height = 40, Background = Brushes.Blue };
        SubmitButton.SetSuccessBrush(button, Green);
        SubmitButton.SetFailureBrush(button, Red);
        Show(button);
        return button;
    }

    private static Path[] Marks(Button button) => button.GetVisualChildren().OfType<Panel>().Single().Children.Cast<Path>().ToArray();

    private void Advance(double ms)
    {
        for (var elapsed = 0d; elapsed < ms; elapsed += 16)
        {
            Frames.Advance(16);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void RunShowsBusyThenTheOutcomeThenSpringsBack(bool succeeds)
    {
        var button = Submit();
        async Task<bool> Work()
        {
            await Presto.CreateTimer(new() { Duration = 1000 });
            return succeeds;
        }

        var run = SubmitButton.Run(button, Work());
        Advance(Recipes.Tokens.Medium + 100);
        Assert.Equal(SubmitState.Busy, SubmitButton.GetState(button));
        Assert.Equal(40, button.Width, 1);
        Assert.Equal(0, button.Presenter!.Child!.Opacity, 3);
        var (spinner, mark) = (Marks(button)[0], Marks(button)[1]);
        Assert.Equal(1, spinner.Opacity, 3);

        Advance(1000);
        Assert.Equal(succeeds ? SubmitState.Succeeded : SubmitState.Failed, SubmitButton.GetState(button));
        Assert.Equal(0, spinner.Opacity, 3);
        Assert.Equal([0, 1], (double[])PrestoAvalonia.Get(mark, "Draw")!);
        Assert.Equal((succeeds ? Draws.Check : Draws.Cross).Bounds, mark.Data!.Bounds);
        Assert.Equal(((ISolidColorBrush)(succeeds ? Green : Red)).Color, ((ISolidColorBrush)button.Background!).Color);

        Advance(Recipes.Tokens.Long + 1500);
        Assert.True(run.IsCompleted);
        Assert.Equal(succeeds, run.Result);
        Assert.Equal(SubmitState.Idle, SubmitButton.GetState(button));
        Assert.Equal(160, button.Width);
        Assert.Equal(Brushes.Blue, button.Background);
        Assert.Equal(1, button.Presenter!.Child!.Opacity);
        Assert.Equal([1, 1], (double[])PrestoAvalonia.Get(mark, "Draw")!);
    }

    [AvaloniaFact]
    public void AFaultedRunFailsWithoutThrowing()
    {
        var button = Submit();

        var run = SubmitButton.Run(button, Task.FromException(new InvalidOperationException()));
        Advance(32);

        Assert.Equal(SubmitState.Failed, SubmitButton.GetState(button));
        Advance(Recipes.Tokens.Medium + Recipes.Tokens.Long + 32);
        Assert.False(run.Result);
    }

    [AvaloniaFact]
    public void ReducedStatesSwapWithoutMotion()
    {
        Presto.Defaults.ReducedMotion = true;
        var button = Submit();

        SubmitButton.SetState(button, SubmitState.Busy);
        Advance(16);
        var (spinner, mark) = (Marks(button)[0], Marks(button)[1]);
        Assert.Equal(40, button.Width);
        Assert.Equal(1, spinner.Opacity);
        Advance(400);
        Assert.Null(spinner.RenderTransform);

        SubmitButton.SetState(button, SubmitState.Succeeded);
        Advance(16);
        Assert.Equal(0, spinner.Opacity);
        Assert.Equal([0, 1], (double[])PrestoAvalonia.Get(mark, "Draw")!);
        Assert.Equal(Colors.Green, ((ISolidColorBrush)button.Background!).Color);

        SubmitButton.SetState(button, SubmitState.Idle);
        Advance(16);
        Assert.Equal(160, button.Width);
        Assert.Equal(Brushes.Blue, button.Background);
        Assert.Equal(1, button.Presenter!.Child!.Opacity);
    }
}
