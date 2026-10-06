using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>The <see cref="Animate"/> triggers: OnAttached plays on attach, OnTapped on each tap and OnChanged on each later Watch change.</summary>
public sealed class AnimateAttachedTests : BridgeTestBase
{
    private static StackPanel ShowPanel()
    {
        var panel = new StackPanel();
        new Window { Content = panel, Width = 100, Height = 100 }.Show();
        Dispatcher.UIThread.RunJobs();
        return panel;
    }

    private static Border AddBorder(Panel panel)
    {
        var border = new Border();
        panel.Children.Add(border);
        return border;
    }

    private static AnimationBuilder SlideX(double to) => Presto.Build().X(to).Duration(100).Ease(Eases.None);

    [AvaloniaFact]
    public void SettingOnAnAttachedControlPlaysOneAnimationToItsEnd()
    {
        var border = AddBorder(ShowPanel());

        Animate.SetOnAttached(border, SlideX(100));

        Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(200, 16);
        Assert.Equal(100d, PrestoAvalonia.Get(border, "X"));
    }

    [AvaloniaFact]
    public void SettingBeforeAttachmentWaitsForTheAttach()
    {
        var panel = ShowPanel();
        var border = new Border();

        Animate.SetOnAttached(border, SlideX(100));
        Assert.Empty(Presto.GetAnimations(border));

        panel.Children.Add(border);
        Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(200, 16);
        Assert.Equal(100d, PrestoAvalonia.Get(border, "X"));
    }

    [AvaloniaFact]
    public void DetachingRevertsAndReattachingPlaysFromTheStart()
    {
        var panel = ShowPanel();
        var border = AddBorder(panel);
        Animate.SetOnAttached(border, SlideX(100));
        Frames.Advance(50, 10);
        var midway = (double)PrestoAvalonia.Get(border, "X")!;
        Assert.InRange(midway, 1, 99);

        panel.Children.Remove(border);
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));
        Assert.Empty(Presto.GetAnimations(border));

        panel.Children.Add(border);
        Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(20, 10);
        Assert.InRange((double)PrestoAvalonia.Get(border, "X")!, 1, midway - 1);
    }

    [AvaloniaFact]
    public void ChangingTheValueRevertsTheFirstAnimationAndPlaysTheSecond()
    {
        var border = AddBorder(ShowPanel());
        Animate.SetOnAttached(border, SlideX(100));
        Frames.Advance(50, 10);
        var first = Assert.Single(Presto.GetAnimations(border));

        Animate.SetOnAttached(border, Presto.Build().Opacity(0).Duration(100).Ease(Eases.None));

        Assert.True(first.Canceled);
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));
        Assert.Single(Presto.GetAnimations(border, "Opacity"));
        Frames.Advance(200, 16);
        Assert.Equal(0, border.Opacity);
    }

    [AvaloniaFact]
    public void SettingNullRevertsAndStopsListening()
    {
        var panel = ShowPanel();
        var border = AddBorder(panel);
        Animate.SetOnAttached(border, SlideX(100));
        Frames.Advance(50, 10);

        Animate.SetOnAttached(border, null);
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));

        panel.Children.Remove(border);
        panel.Children.Add(border);
        Assert.Empty(Presto.GetAnimations(border));
    }

    [AvaloniaFact]
    public void OneBuilderOnTwoControlsGivesIndependentAnimations()
    {
        var panel = ShowPanel();
        var builder = SlideX(100);
        var a = AddBorder(panel);
        var b = AddBorder(panel);

        Animate.SetOnAttached(a, builder);
        Animate.SetOnAttached(b, builder);
        Assert.Single(Presto.GetAnimations(a)).Cancel();
        Frames.Advance(200, 16);

        Assert.Empty(Presto.GetAnimations(a));
        Assert.Equal(100d, PrestoAvalonia.Get(b, "X"));
    }

    [AvaloniaFact]
    public void EachXamlSpellingAnimates()
    {
        var view = new AttachedXamlView { DataContext = new AttachedXamlViewModel() };
        new Window { Content = view }.Show(); // A UserControl's content joins the tree when its template applies, on the first layout.

        Frames.Advance(200, 16);

        Assert.Equal(100d, PrestoAvalonia.Get(view.StaticBorder, "X"));
        Assert.Equal(40d, PrestoAvalonia.Get(view.ExtensionBorder, "X"));
        Assert.Equal(60d, PrestoAvalonia.Get(view.BindingBorder, "X"));
    }

    private static Border AddHitBorder(Panel panel)
    {
        var border = new Border { Height = 50, Background = Brushes.Red };
        panel.Children.Add(border);
        Dispatcher.UIThread.RunJobs();
        return border;
    }

    private static void Tap(Border border)
    {
        var window = (Window)TopLevel.GetTopLevel(border)!;
        var point = border.TranslatePoint(new Point(10, 10), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    [AvaloniaFact]
    public void OnTappedPlaysOnATapAndRevertsWhenItCompletes()
    {
        var border = AddHitBorder(ShowPanel());
        Animate.SetOnTapped(border, SlideX(100));
        Assert.Empty(Presto.GetAnimations(border));

        Tap(border);
        Frames.Advance(50, 10);
        Assert.InRange((double)PrestoAvalonia.Get(border, "X")!, 1, 99);

        Frames.Advance(100, 10);
        Assert.Empty(Presto.GetAnimations(border));
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));
    }

    [AvaloniaFact]
    public void OnTappedRestartsOnEachTap()
    {
        var border = AddHitBorder(ShowPanel());
        Animate.SetOnTapped(border, SlideX(100));
        Tap(border);
        Frames.Advance(60, 10);
        var first = Assert.Single(Presto.GetAnimations(border));

        Tap(border);

        Assert.True(first.Canceled);
        Assert.NotSame(first, Assert.Single(Presto.GetAnimations(border)));
        Frames.Advance(20, 10);
        Assert.InRange((double)PrestoAvalonia.Get(border, "X")!, 1, 50);
    }

    [AvaloniaFact]
    public void ClearingOnTappedStopsListening()
    {
        var border = AddHitBorder(ShowPanel());
        Animate.SetOnTapped(border, SlideX(100));
        Tap(border);
        Frames.Advance(50, 10);

        Animate.SetOnTapped(border, null);
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));

        Tap(border);
        Assert.Empty(Presto.GetAnimations(border));
    }

    [AvaloniaFact]
    public void OnChangedSkipsTheInitialBindingAndPlaysOnEachLaterChange()
    {
        var panel = ShowPanel();
        var border = AddBorder(panel);
        var model = new CountModel { Count = 1 };
        Animate.SetOnChanged(border, SlideX(100));
        border.Bind(Animate.WatchProperty, new Binding(nameof(CountModel.Count)));

        panel.DataContext = model;
        Assert.Equal(1, Animate.GetWatch(border));
        Assert.Empty(Presto.GetAnimations(border));

        model.Count = 2;
        var first = Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(50, 10);

        model.Count = 3;
        Assert.True(first.Canceled);
        Assert.Single(Presto.GetAnimations(border));
        Frames.Advance(200, 16);
        Assert.Empty(Presto.GetAnimations(border));
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));
    }

    [AvaloniaFact]
    public void OnChangedSetAfterTheFirstValuePlaysOnTheNextChange()
    {
        var border = AddBorder(ShowPanel());
        Animate.SetWatch(border, 1);

        Animate.SetOnChanged(border, SlideX(100));
        Assert.Empty(Presto.GetAnimations(border));

        Animate.SetWatch(border, 2);
        Assert.Single(Presto.GetAnimations(border));
    }

    [AvaloniaFact]
    public void ClearingOnChangedStopsListening()
    {
        var border = AddBorder(ShowPanel());
        Animate.SetOnChanged(border, SlideX(100));
        Animate.SetWatch(border, 1);
        Animate.SetWatch(border, 2);
        Frames.Advance(50, 10);

        Animate.SetOnChanged(border, null);
        Assert.Equal(0d, PrestoAvalonia.Get(border, "X"));

        Animate.SetWatch(border, 3);
        Assert.Empty(Presto.GetAnimations(border));
    }

    [AvaloniaFact]
    public void DetachingRevertsTheTriggers()
    {
        var panel = ShowPanel();
        var tapped = AddHitBorder(panel);
        var changed = AddBorder(panel);
        Animate.SetOnTapped(tapped, SlideX(100));
        Animate.SetOnChanged(changed, SlideX(100));
        Animate.SetWatch(changed, 1);
        Tap(tapped);
        Animate.SetWatch(changed, 2);
        Frames.Advance(50, 10);

        panel.Children.Clear();

        Assert.Empty(Presto.GetAnimations(tapped));
        Assert.Empty(Presto.GetAnimations(changed));
        Assert.Equal(0d, PrestoAvalonia.Get(tapped, "X"));
        Assert.Equal(0d, PrestoAvalonia.Get(changed, "X"));
    }

    [AvaloniaFact]
    public void DoesNothingInDesignMode()
    {
        var isDesignMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!; // The setter is internal.
        var border = AddBorder(ShowPanel());
        isDesignMode.SetValue(null, true);
        try
        {
            Animate.SetOnAttached(border, SlideX(100));
        }
        finally
        {
            isDesignMode.SetValue(null, false);
        }

        Assert.Empty(Presto.GetAnimations(border));
    }

    private sealed class CountModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Count
        {
            get;
            set
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            }
        }
    }
}
