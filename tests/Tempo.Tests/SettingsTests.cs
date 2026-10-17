using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Controls;
using Tempo.Model;
using Tempo.ViewModels;
using Tempo.Views;

namespace Tempo.Tests;

public sealed class SettingsTests : TempoTestBase
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheSettingsButtonHoldsAQuarterTurnOnlyWhileSettingsAreOpen(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var button = Find<Button>(window, "SettingsButton");

        vm.OpenSettingsCommand.Execute(null);
        TestFrames.Tick(40);
        Assert.Contains("open", button.Classes);
        Assert.Equal(reduced ? 0 : 1, button.RenderTransform?.Value.M12 ?? 0, 3);

        FlyoutBase.GetAttachedFlyout(button)!.Hide();
        TestFrames.Tick(40);
        Assert.DoesNotContain("open", button.Classes);
        Assert.Equal(0, button.RenderTransform?.Value.M12 ?? 0, 3);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaFact]
    public async Task OpenSettingsShowsTheFlyoutFromItsPopoverStart()
    {
        var (window, vm) = await ShowAsync(reduced: false);

        vm.OpenSettingsCommand.Execute(null);
        TestFrames.Tick();
        var presenter = Assert.Single(window.GetVisualDescendants().OfType<FlyoutPresenter>());
        Assert.True(presenter.Opacity < 1);
        TestFrames.Tick(30);
        Assert.Equal(1, presenter.Opacity);
        Assert.Single(presenter.GetVisualDescendants().OfType<SettingsFlyout>());
        Assert.False(Find<Sheet>(window, "SettingsSheet").IsOpen);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheSettingsPanelIsGlassAndItsRowsArriveAndSettleAtFullOpacity(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);

        vm.OpenSettingsCommand.Execute(null);
        TestFrames.Tick();
        var presenter = Assert.Single(window.GetVisualDescendants().OfType<FlyoutPresenter>());
        var rows = presenter.GetVisualDescendants().OfType<StackPanel>().First(p => p.Classes.Contains("settings")).Children;
        Assert.Contains("glass", presenter.Classes);
        Assert.All(rows, row => Assert.True(row.Opacity < 1));
        TestFrames.Tick(50);
        Assert.All(rows, row => Assert.Equal(1, row.Opacity));
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SteppersClampToTheirLimitsAndPersistTheSetting(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        vm.WorkMinutes = 119;
        vm.SessionsBeforeLongBreak = 1;
        vm.OpenSettingsCommand.Execute(null);
        TestFrames.Tick(50);

        var work = Find<Stepper>(window, "WorkStepper");
        Click(work, "Increase");
        Click(work, "Increase");
        Click(Find<Stepper>(window, "SessionsStepper"), "Decrease");
        var number = work.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "Number");
        TestFrames.Tick(4);
        if (reduced)
            Assert.Equal(("120", 1d, true), (number.Text, number.Opacity, number.Inlines is null or { Count: 0 }));
        TestFrames.Tick(36);

        Assert.Equal(120, vm.WorkMinutes);
        Assert.Equal(1, vm.SessionsBeforeLongBreak);
        Assert.Equal("120", number.Text);
        window.Close();
        await vm.FlushAsync();
        var settings = (await new TempoStore(Root).LoadAsync()).Settings;
        Assert.Equal(120, settings.WorkMinutes);
        Assert.Equal(1, settings.SessionsBeforeLongBreak);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosingSettingsLeavesNoMotionPreviewRunning(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        vm.OpenSettingsCommand.Execute(null);
        TestFrames.Tick(50);
        var dot = Find<Ellipse>(window, "MotionPreview");
        var lowest = 0.0;
        for (var i = 0; i < 30; i++)
        {
            TestFrames.Tick();
            lowest = Math.Min(lowest, dot.RenderTransform?.Value.M32 ?? 0);
        }
        Assert.Equal(reduced ? 0 : -7, lowest, 0.5);

        FlyoutBase.GetAttachedFlyout(Find<Button>(window, "SettingsButton"))!.Hide();
        TestFrames.Tick(30);

        Assert.False(dot.IsAttachedToVisualTree());
        Assert.Empty(Presto.GetAnimations(dot));
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaFact]
    public async Task TheMotionPreviewFadesOnceTheChoiceIsReduced()
    {
        var (window, vm) = await ShowAsync(reduced: false);
        vm.OpenSettingsCommand.Execute(null);
        TestFrames.Tick(20);
        var dot = Find<Ellipse>(window, "MotionPreview");

        vm.Motion = MotionPreference.Reduced;
        TestFrames.Tick();
        Assert.True(dot.Opacity < 1);
        Assert.Equal(0, dot.RenderTransform?.Value.M32 ?? 0, 3);
        TestFrames.Tick(40);
        Assert.Equal(1, dot.Opacity);
        Assert.Empty(Presto.GetAnimations(dot));
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaFact]
    public async Task TheMotionPreviewLoopsAgainOnceTheChoiceIsFull()
    {
        var (window, vm) = await ShowAsync(reduced: true);
        vm.OpenSettingsCommand.Execute(null);
        TestFrames.Tick(60);
        var dot = Find<Ellipse>(window, "MotionPreview");
        Assert.Empty(Presto.GetAnimations(dot));

        vm.Motion = MotionPreference.Full;
        var lowest = 0.0;
        for (var i = 0; i < 30; i++)
        {
            TestFrames.Tick();
            lowest = Math.Min(lowest, dot.RenderTransform?.Value.M32 ?? 0);
        }
        Assert.Equal(-7, lowest, 0.5);
        Assert.Equal(1, dot.Opacity);
        Assert.NotEmpty(Presto.GetAnimations(dot));
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HoldingToConfirmClearEmptiesTheListsAndTheLogAndSaves(bool reduced)
    {
        var (window, vm) = await ShowSeededAsync(reduced);
        Assert.NotEmpty(vm.DoneTasks);
        OpenConfirm(vm, vm.RequestClearCommand);
        Assert.True(Find<Modal>(window, "ConfirmModal").IsOpen);

        HoldToConfirm(window);
        TestFrames.Tick(120);

        Assert.False(Find<Modal>(window, "ConfirmModal").IsOpen);
        Assert.Empty(vm.Tasks);
        Assert.Empty(vm.DoneTasks);
        Assert.Empty(vm.Sessions);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("Nothing planned") == true && t.IsEffectivelyVisible);
        window.Close();
        await vm.FlushAsync();
        var saved = await new TempoStore(Root).LoadAsync();
        Assert.Empty(saved.Tasks);
        Assert.Empty(saved.Sessions);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelOrAnEscapeBeforeAnyPressClosesTheDialogAndChangesNothing(bool reduced)
    {
        var (window, vm) = await ShowSeededAsync(reduced);
        var before = (vm.Tasks.Count, vm.DoneTasks.Count, vm.Sessions.Count);

        OpenConfirm(vm, vm.RequestClearCommand);
        vm.CancelConfirmCommand.Execute(null);
        TestFrames.Tick(40);
        Assert.False(Find<Modal>(window, "ConfirmModal").IsOpen);

        OpenConfirm(vm, vm.RequestClearCommand);
        Find<ProgressRing>(window, "Hold").RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        TestFrames.Tick(40);

        Assert.False(Find<Modal>(window, "ConfirmModal").IsOpen);
        Assert.False(vm.IsConfirmOpen);
        Assert.Equal(before, (vm.Tasks.Count, vm.DoneTasks.Count, vm.Sessions.Count));
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnEscapeAfterAnEarlyReleaseShakesTheDialogAndKeepsItOpen(bool reduced)
    {
        var (window, vm) = await ShowSeededAsync(reduced);
        OpenConfirm(vm, vm.RequestClearCommand);
        var ring = Find<ProgressRing>(window, "Hold");
        ring.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        TestFrames.Tick(10);
        ring.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        TestFrames.Tick(20);

        ring.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        var card = Find<Border>(window, "Card");
        var (travel, faintest) = (0.0, 1.0);
        for (var i = 0; i < 40; i++)
        {
            TestFrames.Tick();
            travel = Math.Max(travel, Math.Abs(card.RenderTransform?.Value.M31 ?? 0));
            faintest = Math.Min(faintest, card.Opacity);
        }

        Assert.True(Find<Modal>(window, "ConfirmModal").IsOpen);
        Assert.Equal(reduced ? 0 : 10, travel, 1);
        Assert.Equal(reduced, faintest < 1);
        Assert.NotEmpty(vm.Tasks);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaFact]
    public async Task CrossingTheBreakpointMovesTheSteppersWithoutMorphingAndAStepStillMorphs()
    {
        var (window, vm) = await ShowAsync(reduced: false);
        vm.OpenSettingsCommand.Execute(null);
        TestFrames.Tick(50);
        var steppers = window.GetVisualDescendants().OfType<Stepper>().ToList();
        Assert.Equal(4, steppers.Count);

        window.Width = 400;
        TestFrames.Tick(30);
        window.Width = 1100;
        TestFrames.Tick(30);
        Assert.All(steppers, stepper => Assert.Null(Morph(stepper)));

        vm.OpenSettingsCommand.Execute(null);
        TestFrames.Tick(50);
        var work = Find<Stepper>(window, "WorkStepper");
        Assert.Equal(vm.WorkMinutes.ToString(), work.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "Number").Text);
        Click(work, "Increase");
        Assert.NotNull(Morph(work));
        window.Close();
        await vm.FlushAsync();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_morph")]
    private static extern ref Timeline? Morph(Stepper stepper);

    private static void Click(Stepper stepper, string button) =>
        stepper.GetVisualDescendants().OfType<RepeatButton>().Single(b => b.Name == button).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public async Task ADurationChangePersistsAndAppliesFromTheNextSession()
    {
        var vm = await LoadAsync();
        vm.Focus.StartPauseCommand.Execute(null);

        vm.WorkMinutes = 10;
        Time.Advance(TimeSpan.FromMinutes(1));
        Ticks.Raise();
        Assert.Equal("24:00", vm.Focus.RemainingText);

        vm.Focus.AbandonCommand.Execute(null);
        Assert.Equal("10:00", vm.Focus.RemainingText);
        await vm.FlushAsync();
        Assert.Equal(10, (await new TempoStore(Root).LoadAsync()).Settings.WorkMinutes);
    }

    [AvaloniaFact]
    public async Task TheWindowPlacementRoundTripsThroughSettings()
    {
        var vm = await LoadAsync();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        window.Width = 900;
        window.Height = 600;
        window.Position = new PixelPoint(120, 80);
        window.Close();
        await vm.FlushAsync();

        var restored = new MainWindow { DataContext = await LoadAsync() };

        Assert.Equal(900, restored.Width);
        Assert.Equal(600, restored.Height);
        Assert.Equal(new PixelPoint(120, 80), restored.Position);
    }

    [AvaloniaFact]
    public async Task APlacementOffEveryScreenIsNotRestored()
    {
        var vm = await LoadAsync();
        vm.SaveWindowPlacement(900, 600, -50_000, -50_000, isMaximized: false);
        await vm.FlushAsync();

        var restored = new MainWindow { DataContext = await LoadAsync() };

        Assert.NotEqual(new PixelPoint(-50_000, -50_000), restored.Position);
        Assert.NotEqual(900, restored.Width);
    }
}
