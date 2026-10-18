using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;

namespace MonacoLabs.Presto.Tests;

/// <summary><c>{presto:StateTokens}</c> composes <see cref="Motion.StateProperty"/> from the tokens of several bindings.</summary>
public sealed class MotionStateTokensTests : BridgeTestBase
{
    private static double Get(Visual visual, string property) => (double)PrestoAvalonia.Get(visual, property)!;

    private static (StateTokensXamlView View, TokensViewModel Model) ShowView()
    {
        var model = new TokensViewModel();
        var view = new StateTokensXamlView { DataContext = model };
        Show(view);
        return (view, model);
    }

    [AvaloniaFact]
    public void AnEnumAndATrueFlagHoldBothStatesAndTheFlagGoingFalseLeavesOnlyItsState()
    {
        var (view, model) = ShowView();
        var border = view.FindControl<Border>("Compiled")!;

        model.IsRunning = true;
        Frames.Advance(200, 10);
        Assert.Equal("Focus IsRunning", Motion.GetState(border));
        Assert.Equal("Focus IsRunning", Motion.GetState(view.FindControl<Border>("Reflected")!));
        Assert.Equal(0.4, border.Opacity, 6);
        Assert.Equal(10,Get(border, "X"), 6);

        model.IsRunning = false;
        Frames.Advance(200, 10);
        Assert.Equal("Focus", Motion.GetState(border));
        Assert.Equal(0.4, border.Opacity, 6);
        Assert.Equal(0, Get(border, "X"), 6);
    }

    [AvaloniaFact]
    public void ABoolBoundWithAnEmptyPathContributesTrue()
    {
        var (view, model) = ShowView();
        var flag = view.FindControl<Border>("Flag")!;
        Assert.Equal("False", Motion.GetState(flag));

        model.IsRunning = true;
        Frames.Advance(200, 10);

        Assert.Equal("True", Motion.GetState(flag));
        Assert.Equal(0.4, flag.Opacity, 6);
    }

    [AvaloniaFact]
    public void OneSourceChangePlaysOneTransition()
    {
        var (view, model) = ShowView();
        var border = view.FindControl<Border>("Compiled")!;
        var changes = 0;
        border.PropertyChanged += (_, e) => changes += e.Property == Motion.StateProperty ? 1 : 0;

        model.Mode = TokenMode.Break;

        Assert.Equal(1, changes);
        Assert.Single(Presto.GetAnimations(border));
    }

    [AvaloniaFact]
    public void SettingItOnAnotherPropertyThrowsNamingMotionState()
    {
        var border = new Border();
        var extension = new StateTokensExtension(new Binding("Mode"), new Binding("IsRunning"));

        var error = Assert.Throws<InvalidOperationException>(() => extension.ProvideValue(new ProvidedTarget(border, Visual.OpacityProperty)));

        Assert.Contains("Motion.State", error.Message);
    }
}
