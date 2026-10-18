using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Popover"/> on popups and tooltips (GAPS-2-PLAN §7.2): a <see cref="Popup"/> is held open while Close plays
/// on its child, and a <see cref="ToolTip"/>, or a popup closed past <c>IsOpen</c>, plays Close on a ghost.
/// </summary>
public sealed class PopoverPopupTests : CompositorTestBase
{
    private static AnimationBuilder ScaleIn => Recipes.ScaleIn(duration: 200, ease: "linear");

    private static AnimationBuilder FadeOut => Recipes.FadeOut(duration: 200, ease: "linear");

    private readonly Func<Action, Action> _dispatcherPolling = PopoverRun.StartPolling;

    // Steps the poll with the engine's frames, so the tests below need no real time.
    public PopoverPopupTests() => PopoverRun.StartPolling = poll =>
    {
        var timer = Presto.CreateTimer(new TimerParams { OnUpdate = _ => poll() });
        return () => timer.Cancel();
    };

    public override void Dispose()
    {
        PopoverRun.StartPolling = _dispatcherPolling;
        base.Dispose();
    }

    [AvaloniaFact]
    public async Task AnOpenHeldPopupLetsTheEngineIdleAndAWriteUnderTheHoldStillStartsClose()
    {
        PopoverRun.StartPolling = _dispatcherPolling;
        var stage = Stage(comboBox: false, close: FadeOut);
        stage.Open();
        Tick();

        Assert.Null(Presto.Engine._head);
        Assert.False(Frames.IsRunning);

        stage.Close();
        Assert.True(await PumpRealTime(() => Presto.Engine._head is not null), "the dispatcher poll never started Close");
        Assert.True(stage.Popup().IsOpen);

        RunUntil(() => !stage.Popup().IsOpen);
        Assert.False(stage.Popup().IsOpen);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ABoundIsOpenFollowsEveryCloseAndReopensFromTheViewModel(bool lightDismiss)
    {
        var stage = Stage(comboBox: false, close: FadeOut);
        var popup = stage.Popup();
        var model = new OpenModel();
        popup.Bind(Popup.IsOpenProperty, new Binding(nameof(OpenModel.IsOpen)) { Source = model, Mode = BindingMode.TwoWay });
        model.IsOpen = true;
        Tick();

        if (lightDismiss) Press(stage.Window, new Point(190, 10));
        else model.IsOpen = false;
        RunUntil(() => !popup.IsOpen && Overlay(stage.Window).Children.Count == 0);
        Tick();

        Assert.False(popup.IsOpen);
        Assert.False(model.IsOpen);

        model.IsOpen = true;
        Tick();

        Assert.True(popup.IsOpen);
        Assert.NotNull(Extent(stage.Window, Colors.Lime));
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void PopupOpenRendersItsStartValuesOnTheFirstFrame(bool comboBox, bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var stage = Stage(comboBox, open: ScaleIn);

        stage.Open();
        var child = stage.Popup().Child!;

        Assert.Equal(0, child.Opacity);
        Assert.Equal(reduced ? 1 : Recipes.Tokens.Scale, Convert.ToDouble(PrestoAvalonia.Get(child, "Scale")));

        Frames.Advance(300, 16);
        Tick();

        Assert.Equal(1, child.Opacity);
        Assert.NotNull(Extent(stage.Window, Colors.Lime));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheHoldKeepsThePopupDrawnUntilCloseEndsThenClosesIt(bool comboBox)
    {
        var stage = Stage(comboBox, close: FadeOut);
        stage.Open();
        Tick();
        var popup = stage.Popup();
        var child = popup.Child!;

        stage.Close();
        Frames.Advance(100, 16);

        Assert.True(popup.IsOpen);
        Assert.False(stage.IsOpen());
        Assert.True(child.IsAttachedToVisualTree());
        Assert.InRange(child.Opacity, .3, .7);

        RunUntil(() => !popup.IsOpen);
        Tick();

        Assert.False(child.IsAttachedToVisualTree());
        Assert.False(stage.IsOpen());
        Assert.Null(Extent(stage.Window, Colors.Lime));
        Assert.Equal(1, child.Opacity);

        stage.Open();
        Tick();

        Assert.True(popup.IsOpen);
        Assert.NotNull(Extent(stage.Window, Colors.Lime));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void LightDismissClosesAtOnceAndPlaysCloseOnAGhost(bool comboBox)
    {
        var stage = Stage(comboBox, close: FadeOut);
        stage.Open();
        Tick();
        var popup = stage.Popup();
        var child = popup.Child!;

        Press(stage.Window, new Point(190, 10));
        Tick();

        Assert.False(popup.IsOpen);
        Assert.False(stage.IsOpen());
        Assert.False(child.IsAttachedToVisualTree());
        Assert.Single(Overlay(stage.Window).Children);
        Assert.NotNull(Extent(stage.Window, Colors.Lime));

        RunUntil(() => Overlay(stage.Window).Children.Count == 0);
        Tick();

        Assert.Null(Extent(stage.Window, Colors.Lime));

        stage.Open();
        Tick();

        Assert.True(popup.IsOpen);
        Assert.NotNull(Extent(stage.Window, Colors.Lime));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReopenedMidClosePlaysBackFromWhereItStands(bool comboBox)
    {
        var stage = Stage(comboBox, close: FadeOut);
        stage.Open();
        Tick();
        var popup = stage.Popup();
        var child = popup.Child!;
        stage.Close();
        Frames.Advance(100, 16);
        var opacity = child.Opacity;

        stage.Open();
        Frames.Advance(16);

        Assert.True(popup.IsOpen);
        Assert.InRange(child.Opacity, opacity, opacity + .15);

        RunUntil(() => child.Opacity == 1);
        Frames.Advance(200, 16);
        Assert.True(popup.IsOpen);

        stage.Close();
        Frames.Advance(100, 16);
        Assert.True(popup.IsOpen);
        RunUntil(() => !popup.IsOpen);
        Assert.Equal(1, child.Opacity);
    }

    [AvaloniaFact]
    public void APopupDetachingMidCloseReleasesTheHoldAtOnce()
    {
        var stage = Stage(comboBox: false, close: FadeOut);
        stage.Open();
        Tick();
        var popup = stage.Popup();
        var child = popup.Child!;
        stage.Close();
        Frames.Advance(100, 16);

        ((Panel)stage.Window.Content!).Children.Remove(popup);
        Tick();

        Assert.False(popup.IsOpen);
        Assert.False(child.IsAttachedToVisualTree());
        Assert.Empty(Overlay(stage.Window).Children);
        Assert.Null(Extent(stage.Window, Colors.Lime));
        Assert.Equal(1, child.Opacity);
    }

    [AvaloniaTheory]
    [InlineData("local")]
    [InlineData("leave")]
    [InlineData("press")]
    public void AToolTipOpensFromItsStartValuesAndClosesAtOnceWhileCloseFadesItsGhost(string path)
    {
        var (window, anchor) = PopupStage();
        window.Styles.Add(new Style(x => x.OfType<ToolTip>()) { Setters = { new Setter(Popover.OpenProperty, ScaleIn), new Setter(Popover.CloseProperty, FadeOut) } });

        var tip = Hover(window, anchor);

        Assert.Equal(0, tip.Opacity);
        Assert.Equal(Recipes.Tokens.Scale, Convert.ToDouble(PrestoAvalonia.Get(tip, "Scale")));
        Assert.Empty(tip.Transitions!);

        RunUntil(() => tip.Opacity == 1);
        Tick();
        var drawn = Extent(window, Colors.Lime);
        Assert.NotNull(drawn);

        CloseTip(window, anchor, path);
        Tick();

        Assert.False(ToolTip.GetIsOpen(anchor));
        Assert.False(tip.IsAttachedToVisualTree());
        Assert.Single(Overlay(window).Children);
        Assert.Equal(drawn, Extent(window, Colors.Lime));

        RunUntil(() => Overlay(window).Children.Count == 0);
        Tick();

        Assert.Null(Extent(window, Colors.Lime));
        Assert.Equal(1, Convert.ToDouble(PrestoAvalonia.Get(tip, "Scale")));
        Assert.Single(tip.Transitions!);
    }

    [AvaloniaFact]
    public void AToolTipWhoseAnchorLeavesClosesWithoutAGhost()
    {
        var (window, anchor) = PopupStage();
        window.Styles.Add(new Style(x => x.OfType<ToolTip>()) { Setters = { new Setter(Popover.CloseProperty, FadeOut) } });
        var tip = Hover(window, anchor);
        Tick();

        ((Panel)window.Content!).Children.Remove(anchor);
        Tick();

        Assert.False(tip.IsAttachedToVisualTree());
        Assert.Empty(Overlay(window).Children);
        Assert.Null(Extent(window, Colors.Lime));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnderReduceAllCloseIsInstantAndOpenPlaysOnlyTheReducedFade(bool reducedMotion)
    {
        Presto.Defaults.ReduceAll = true;
        Presto.Defaults.ReducedMotion = reducedMotion;
        var stage = Stage(comboBox: false, open: ScaleIn, close: FadeOut);
        stage.Window.Styles.Add(new Style(x => x.OfType<ToolTip>()) { Setters = { new Setter(Popover.OpenProperty, ScaleIn), new Setter(Popover.CloseProperty, FadeOut) } });

        stage.Open();
        var child = stage.Popup().Child!;

        Assert.Equal(reducedMotion ? 0 : 1, child.Opacity);
        Assert.Equal(1, Convert.ToDouble(PrestoAvalonia.Get(child, "Scale")));

        stage.Close();
        Frames.Advance(16);
        Tick();

        Assert.False(stage.Popup().IsOpen);
        Assert.Null(Extent(stage.Window, Colors.Lime));

        var tip = Hover(stage.Window, stage.Anchor);

        Assert.Equal(reducedMotion ? 0 : 1, tip.Opacity);
        Assert.Equal(1, Convert.ToDouble(PrestoAvalonia.Get(tip, "Scale")));

        ToolTip.SetIsOpen(stage.Anchor, false);
        Tick();

        Assert.Empty(Overlay(stage.Window).Children);
        Assert.Null(Extent(stage.Window, Colors.Lime));
    }

    [AvaloniaFact]
    public void BuildersOnAPlainPopupsChildOpenAndHoldItAsOnThePopup()
    {
        var stage = Stage(comboBox: false);
        var popup = stage.Popup();
        var child = popup.Child!;
        Popover.SetOpen(child, ScaleIn);
        Popover.SetClose(child, FadeOut);

        stage.Open();

        Assert.Equal(0, child.Opacity);

        RunUntil(() => child.Opacity == 1);
        stage.Close();
        Frames.Advance(100, 16);

        Assert.True(popup.IsOpen);
        Assert.InRange(child.Opacity, .3, .7);

        RunUntil(() => !popup.IsOpen);
        Assert.False(popup.IsOpen);
    }

    [AvaloniaFact]
    public void WithoutPopoverAPopupClosesAtOnce()
    {
        var stage = Stage(comboBox: false);
        stage.Open();
        Tick();

        stage.Close();

        Assert.False(stage.Popup().IsOpen);
        Tick();
        Assert.Null(Extent(stage.Window, Colors.Lime));
    }

    [AvaloniaFact]
    public void APopupWhoseChildIsNotInTheTreeGetsNoRun()
    {
        var child = Lime();
        var popup = new Popup { Child = child };
        Popover.SetOpen(popup, ScaleIn);
        Popover.SetClose(popup, FadeOut);

        popup.IsOpen = true;
        Assert.False(child.IsAttachedToVisualTree());
        popup.IsOpen = false;

        Assert.Equal(1, child.Opacity);
        Assert.Empty(Presto.GetAnimations(child));
        Assert.False(popup.IsOpen);
    }

    [AvaloniaFact]
    public void NoRunStartsInDesignMode()
    {
        var isDesignMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!; // The setter is internal.
        var stage = Stage(comboBox: false, open: ScaleIn, close: FadeOut);
        isDesignMode.SetValue(null, true);
        try
        {
            stage.Open();
            var child = stage.Popup().Child!;
            Assert.Equal(1, child.Opacity);
            Assert.Empty(Presto.GetAnimations(child));

            stage.Close();
            Assert.False(stage.Popup().IsOpen);
        }
        finally
        {
            isDesignMode.SetValue(null, false);
        }
    }

    [AvaloniaFact]
    public void WithoutOpenAHandledPopupOpensWithNoMotion()
    {
        var stage = Stage(comboBox: false, close: FadeOut);

        stage.Open();
        var child = stage.Popup().Child!;

        Assert.Equal(1, child.Opacity);
        Assert.Empty(Presto.GetAnimations(child));
        Assert.True(stage.Popup().IsAnimating(Popup.IsOpenProperty));
    }

    private sealed class OpenModel : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isOpen;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public bool IsOpen
        {
            get => _isOpen;
            set
            {
                if (_isOpen == value) return;
                _isOpen = value;
                PropertyChanged?.Invoke(this, new(nameof(IsOpen)));
            }
        }
    }

    private static async Task<bool> PumpRealTime(Func<bool> until, int rounds = 60)
    {
        for (var i = 0; i < rounds && !until(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(16);
        }
        return until();
    }

    private sealed record Staged(Window Window, Button Anchor, Func<Popup> Popup, Action Open, Action Close, Func<bool> IsOpen);

    private static OverlayLayer Overlay(Window window) => OverlayLayer.GetOverlayLayer(window)!;

    private static Staged Stage(bool comboBox, AnimationBuilder? open = null, AnimationBuilder? close = null)
    {
        var (window, anchor) = PopupStage();
        var panel = (Panel)window.Content!;
        if (comboBox)
        {
            var setters = new Style(x => x.OfType<ComboBox>().Template().OfType<Popup>().Name("PART_Popup"));
            if (open is not null) setters.Setters.Add(new Setter(Popover.OpenProperty, open));
            if (close is not null) setters.Setters.Add(new Setter(Popover.CloseProperty, close));
            window.Styles.Add(setters);
            window.Styles.Add(new Style(x => x.OfType<ComboBoxItem>()) { Setters = { new Setter(TemplatedControl.BackgroundProperty, Brushes.Lime) } });
            var combo = new ComboBox { ItemsSource = new[] { "a", "b" }, Width = 100, VerticalAlignment = VerticalAlignment.Bottom };
            panel.Children.Add(combo);
            Tick();
            return new(window, anchor, () => combo.GetVisualDescendants().OfType<Popup>().Single(), () => combo.IsDropDownOpen = true, () => combo.IsDropDownOpen = false,
                () => combo.IsDropDownOpen);
        }
        var popup = new Popup { Child = Lime(), PlacementTarget = anchor, IsLightDismissEnabled = true };
        Popover.SetOpen(popup, open);
        Popover.SetClose(popup, close);
        panel.Children.Add(popup);
        return new(window, anchor, () => popup, () => popup.IsOpen = true, () => popup.IsOpen = false, () => popup.GetBaseValue(Popup.IsOpenProperty).GetValueOrDefault());
    }
}
