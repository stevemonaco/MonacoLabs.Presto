using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Extensions.Time.Testing;
using MonacoLabs.Presto.Compositor;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Connected"/> (CONNECTED-ANIMATIONS-PLAN §6): keyed elements prepare on detach and claim on attach, and
/// anchored ones enter with the flight. Leaving elements are blue, arriving ones red and anchored ones lime, measured in pixels.
/// </summary>
public sealed class ConnectedTests : CompositorTestBase
{
    [AvaloniaFact]
    public void AContentSwapFliesTheArrivingElementFromWhereTheLeavingOneWasDrawn()
    {
        var host = ShowHost(View(Keyed(nameof(AContentSwapFliesTheArrivingElementFromWhereTheLeavingOneWasDrawn), Brushes.Blue), 0, 50));
        var drawn = Extent(host, Colors.Blue)!.Value;

        var arriving = Keyed(nameof(AContentSwapFliesTheArrivingElementFromWhereTheLeavingOneWasDrawn), Brushes.Red, ConnectedRecipes.Held);
        host.Content = View(arriving, 80, 130);
        Tick();

        Assert.Equal(drawn, Box(arriving));
        Assert.Null(LayoutTransition.TakePrepared(nameof(AContentSwapFliesTheArrivingElementFromWhereTheLeavingOneWasDrawn)));
    }

    [AvaloniaFact]
    public void AContentSwapFliesOutOfAScrolledList()
    {
        var scroller = new ScrollViewer
        {
            Height = 200,
            Content = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Children = { new Border { Height = 150 }, Keyed(nameof(AContentSwapFliesOutOfAScrolledList), Brushes.Blue), new Border { Height = 400 } },
            },
        };
        var host = ShowHost(scroller);
        scroller.Offset = new Vector(0, 100);
        Tick();
        var drawn = Extent(host, Colors.Blue)!.Value;

        var arriving = Keyed(nameof(AContentSwapFliesOutOfAScrolledList), Brushes.Red, ConnectedRecipes.Held);
        host.Content = View(arriving, 80, 130);
        Tick();

        Assert.Equal(drawn, Box(arriving));
    }

    [AvaloniaFact]
    public void CoexistingViewsFlyFromTheLiveElementAndLeaveNothingPrepared()
    {
        const string key = nameof(CoexistingViewsFlyFromTheLiveElementAndLeaveNothingPrepared);
        var root = ShowRoot();
        var leaving = View(Keyed(key, Brushes.Blue), 0, 50);
        root.Children.Add(leaving);
        Tick();
        var drawn = Extent(root, Colors.Blue)!.Value;

        var arriving = Keyed(key, Brushes.Red, ConnectedRecipes.Held);
        root.Children.Add(View(arriving, 80, 130));
        Tick();
        root.Children.Remove(leaving);
        Tick();

        Assert.Equal(drawn, Box(arriving));
        Assert.Null(LayoutTransition.TakePrepared(key));
    }

    [AvaloniaFact]
    public void APreparedRectIsTakenWithinThreeSecondsAndExpiresAfter()
    {
        const string key = nameof(APreparedRectIsTakenWithinThreeSecondsAndExpiresAfter);
        var drawn = new Rect(10, 20, 30, 40);
        var time = new FakeTimeProvider();
        LayoutTransition.TimeProvider = time;
        try
        {
            LayoutTransition.Prepare(key, drawn);
            time.Advance(TimeSpan.FromMilliseconds(2999));
            Assert.Equal(drawn, LayoutTransition.TakePrepared(key));

            LayoutTransition.Prepare(key, drawn);
            time.Advance(TimeSpan.FromMilliseconds(3001));
            Assert.Null(LayoutTransition.TakePrepared(key));
        }
        finally
        {
            LayoutTransition.TimeProvider = TimeProvider.System;
        }
    }

    [AvaloniaFact]
    public void AKeyThatNeverReturnsDoesNotAccumulate()
    {
        const string gone = nameof(AKeyThatNeverReturnsDoesNotAccumulate) + "-gone";
        const string fresh = nameof(AKeyThatNeverReturnsDoesNotAccumulate) + "-fresh";
        const string next = nameof(AKeyThatNeverReturnsDoesNotAccumulate) + "-next";
        var drawn = new Rect(10, 20, 30, 40);
        var time = new FakeTimeProvider();
        LayoutTransition.TimeProvider = time;
        try
        {
            LayoutTransition.Prepare(gone, drawn);
            time.Advance(TimeSpan.FromMilliseconds(2000));
            LayoutTransition.Prepare(fresh, drawn);
            time.Advance(TimeSpan.FromMilliseconds(1001));
            LayoutTransition.Prepare(next, drawn);

            Assert.False(LayoutTransition.IsPrepared(gone));
            Assert.Equal(drawn, LayoutTransition.TakePrepared(fresh));
            Assert.Equal(drawn, LayoutTransition.TakePrepared(next));
        }
        finally
        {
            LayoutTransition.TimeProvider = TimeProvider.System;
        }
    }

    [AvaloniaFact]
    public void AFirstLoadWithNothingPreparedAnimatesNothing()
    {
        var arriving = Keyed(nameof(AFirstLoadWithNothingPreparedAnimatesNothing), Brushes.Red, ConnectedRecipes.Held);
        ShowHost(View(arriving, 80, 130));

        Assert.Equal(SlotOf(arriving), Box(arriving));
    }

    [AvaloniaFact]
    public void ChangingTheKeyOfAnAttachedElementDoesNotFly()
    {
        const string key = nameof(ChangingTheKeyOfAnAttachedElementDoesNotFly);
        var root = ShowRoot();
        root.Children.Add(View(Keyed(key, Brushes.Blue), 0, 50));
        var arriving = Keyed(null, Brushes.Red, ConnectedRecipes.Held);
        root.Children.Add(View(arriving, 80, 130));
        Tick();

        Connected.SetKey(arriving, key);
        Tick();

        Assert.Equal(SlotOf(arriving), Box(arriving));
    }

    [AvaloniaFact]
    public void ClearingTheKeyStopsPreparing()
    {
        const string key = nameof(ClearingTheKeyStopsPreparing);
        var leaving = Keyed(key, Brushes.Blue);
        var host = ShowHost(View(leaving, 0, 50));

        Connected.SetKey(leaving, null);
        var arriving = Keyed(key, Brushes.Red, ConnectedRecipes.Held);
        host.Content = View(arriving, 80, 130);
        Tick();

        Assert.Equal(SlotOf(arriving), Box(arriving));
    }

    /// <summary>Halfway through an 80-each-way linear flight the middle element is 40 short of its slot, and the next one flies from there.</summary>
    [AvaloniaFact]
    public void LeavingMidFlightFliesTheNextArrivalFromWhereTheLeavingElementWasDrawn()
    {
        const string key = nameof(LeavingMidFlightFliesTheNextArrivalFromWhereTheLeavingElementWasDrawn);
        var host = ShowHost(View(Keyed(key, Brushes.Blue), 0, 50));
        host.Content = View(Keyed(key, Brushes.Green, Presto.Build().Duration(800).Ease(Eases.None)), 80, 130);
        Tick();
        Frames.Advance(400);

        var arriving = Keyed(key, Brushes.Red, ConnectedRecipes.Held);
        host.Content = View(arriving, 0, 150);
        Tick();

        var box = Box(arriving);
        Assert.InRange(box.Left, 37, 43);
        Assert.InRange(box.Top, 87, 93);
    }

    [AvaloniaFact]
    public void TheArrivingElementsMotionSetsTheFlight()
    {
        const string key = nameof(TheArrivingElementsMotionSetsTheFlight);
        var host = ShowHost(View(Keyed(key, Brushes.Blue), 0, 50));
        var drawn = Extent(host, Colors.Blue)!.Value;

        var arriving = Keyed(key, Brushes.Red, ConnectedRecipes.Held);
        host.Content = View(arriving, 80, 130);
        Pump(() => false, 80);

        AssertNear(drawn, Box(arriving));
    }

    [AvaloniaFact]
    public void TheLeavingElementsMotionIsIgnoredForTheDefault()
    {
        const string key = nameof(TheLeavingElementsMotionIsIgnoredForTheDefault);
        var defaultMotion = Connected.DefaultMotion;
        Connected.DefaultMotion = Presto.Build().Duration(50);
        try
        {
            var host = ShowHost(View(Keyed(key, Brushes.Blue, ConnectedRecipes.Held), 0, 50));
            var arriving = Keyed(key, Brushes.Red);
            host.Content = View(arriving, 80, 130);
            Tick();
            Frames.Advance(100);

            Assert.True(Pump(() => Box(arriving) == SlotOf(arriving)), "the arrival took the leaving element's motion");
        }
        finally
        {
            Connected.DefaultMotion = defaultMotion;
        }
    }

    [AvaloniaFact]
    public void ChainingOnADefaultLeavesTheDefaultUnchanged()
    {
        var motion = Connected.DefaultMotion.ToParams();
        var anchorMotion = Connected.DefaultAnchorMotion.ToParams();

        Connected.DefaultMotion.Duration(9999);
        Connected.DefaultAnchorMotion.Duration(9999);

        Assert.Equal(motion.Duration, Connected.DefaultMotion.ToParams().Duration);
        Assert.Equal(anchorMotion.Duration, Connected.DefaultAnchorMotion.ToParams().Duration);
    }

    [AvaloniaFact]
    public void DefaultMotionAppliesWhenNeitherElementSetsOne()
    {
        const string key = nameof(DefaultMotionAppliesWhenNeitherElementSetsOne);
        var defaultMotion = Connected.DefaultMotion;
        Connected.DefaultMotion = ConnectedRecipes.Held;
        try
        {
            var host = ShowHost(View(Keyed(key, Brushes.Blue), 0, 50));
            var drawn = Extent(host, Colors.Blue)!.Value;
            var arriving = Keyed(key, Brushes.Red);
            host.Content = View(arriving, 80, 130);
            Pump(() => false, 80);

            AssertNear(drawn, Box(arriving));
        }
        finally
        {
            Connected.DefaultMotion = defaultMotion;
        }
    }

    [AvaloniaFact]
    public void AnAnchoredElementAttachedBeforeItsKeyedElementEntersWithTheFlight()
    {
        const string key = nameof(AnAnchoredElementAttachedBeforeItsKeyedElementEntersWithTheFlight);
        var host = ShowHost(View(Keyed(key, Brushes.Blue), 0, 50));
        var anchored = Anchored(key);
        host.Content = new Panel { Children = { anchored, View(Keyed(key, Brushes.Red, ConnectedRecipes.Held), 80, 130) } };

        AssertEnters(host, anchored);
    }

    [AvaloniaFact]
    public void AnAnchoredElementAttachedAfterItsKeyedElementEntersWithTheFlight()
    {
        const string key = nameof(AnAnchoredElementAttachedAfterItsKeyedElementEntersWithTheFlight);
        var host = ShowHost(View(Keyed(key, Brushes.Blue), 0, 50));
        var anchored = Anchored(key);
        host.Content = new Panel { Children = { View(Keyed(key, Brushes.Red, ConnectedRecipes.Held), 80, 130), anchored } };

        AssertEnters(host, anchored);
    }

    [AvaloniaFact]
    public void AnAnchoredElementAttachedWithNoLiveFlightShowsNormally()
    {
        const string key = nameof(AnAnchoredElementAttachedWithNoLiveFlightShowsNormally);
        var host = ShowHost(View(Keyed(key, Brushes.Blue), 0, 50));
        var view = View(Keyed(key, Brushes.Red), 80, 130);
        host.Content = view;
        Tick();
        Frames.Advance(600);

        var anchored = Anchored(key);
        view.Children.Add(anchored);
        Tick();

        Assert.Equal(SlotOf(anchored), Extent(host, Colors.Lime));
    }

    [AvaloniaFact]
    public void AnAnchoredElementAttachedAfterAFlightThatDidNotMoveShowsNormally()
    {
        const string key = nameof(AnAnchoredElementAttachedAfterAFlightThatDidNotMoveShowsNormally);
        var host = ShowHost(View(Keyed(key, Brushes.Blue), 0, 50));
        var view = View(Keyed(key, Brushes.Red, ConnectedRecipes.Held), 0, 50);
        host.Content = view;
        Tick();

        var anchored = Anchored(key);
        view.Children.Add(anchored);
        Tick();

        Assert.Equal(SlotOf(anchored), Extent(host, Colors.Lime));
    }

    [AvaloniaFact]
    public void DetachingAnAnchoredElementRevertsItsEntrance()
    {
        const string key = nameof(DetachingAnAnchoredElementRevertsItsEntrance);
        var completed = false;
        var host = ShowHost(View(Keyed(key, Brushes.Blue), 0, 50));
        var anchored = Anchored(key, Presto.Build().Y(from: 24, to: 0).Duration(100).Ease(Eases.None).OnComplete(_ => completed = true));
        var view = new Panel { Children = { View(Keyed(key, Brushes.Red, ConnectedRecipes.Held), 80, 130), anchored } };
        host.Content = view;
        Tick();
        Assert.True(Extent(host, Colors.Lime)!.Value.Top > SlotOf(anchored).Top + 10, "the entrance did not start");

        view.Children.Remove(anchored);
        Frames.Advance(200);

        Assert.False(completed);
    }

    [AvaloniaFact]
    public void MotionOnAnAnchoredElementOverridesTheDefaultEntrance()
    {
        const string key = nameof(MotionOnAnAnchoredElementOverridesTheDefaultEntrance);
        var host = ShowHost(View(Keyed(key, Brushes.Blue), 0, 50));
        var anchored = Anchored(key, Presto.Build().Y(from: 24, to: 0).Duration(600000).Ease(Eases.None));
        host.Content = new Panel { Children = { View(Keyed(key, Brushes.Red, ConnectedRecipes.Held), 80, 130), anchored } };
        Tick();

        AssertNear(SlotOf(anchored).Translate(new Vector(0, 24)), Extent(host, Colors.Lime)!.Value);
    }

    [AvaloniaFact]
    public void ACompiledXamlViewFliesItsKeyedElementAndEntersItsAnchoredOne()
    {
        const int id = 17017;
        var leaving = new ConnectedXamlView { DataContext = new ConnectedXamlViewModel(id, new Thickness(0, 50, 0, 0)) };
        var host = ShowHost(leaving);
        Tick();
        var drawn = Extent(host, Colors.Red)!.Value;
        Assert.Equal(SlotOf(leaving.Caption), Extent(host, Colors.Lime));

        var arriving = new ConnectedXamlView { DataContext = new ConnectedXamlViewModel(id, new Thickness(80, 130, 0, 0)) };
        host.Content = arriving;
        Tick();

        Assert.Equal(drawn, Box(arriving.Photo));
        AssertEnters(host, arriving.Caption);
    }

    [AvaloniaFact]
    public void DoesNothingInDesignMode()
    {
        const string key = nameof(DoesNothingInDesignMode);
        var isDesignMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!; // The setter is internal.
        Border leaving, arriving, anchored;
        isDesignMode.SetValue(null, true);
        try
        {
            leaving = Keyed(key, Brushes.Blue);
            arriving = Keyed(key, Brushes.Red, ConnectedRecipes.Held);
            anchored = Anchored(key);
        }
        finally
        {
            isDesignMode.SetValue(null, false);
        }

        var host = ShowHost(View(leaving, 0, 50));
        host.Content = new Panel { Children = { View(arriving, 80, 130), anchored } };
        Tick();

        Assert.Equal(SlotOf(arriving), Box(arriving));
        Assert.Equal(SlotOf(anchored), Extent(host, Colors.Lime));
    }

    [AvaloniaFact]
    public void AMotionTheElementsRoleCannotCarryThrowsWhereItIsSet()
    {
        const string key = nameof(AMotionTheElementsRoleCannotCarryThrowsWhereItIsSet);
        Assert.Throws<NotSupportedException>(() => Keyed(key, Brushes.Red, Presto.Build().Opacity(to: 0)));
        Assert.Throws<NotSupportedException>(() => Anchored(key, Presto.Build().Opacity(from: 0).OnUpdate(_ => { })));

        var later = new Border();
        Connected.SetMotion(later, Presto.Build().Loop(2));
        Assert.Throws<NotSupportedException>(() => Connected.SetKey(later, key));
    }

    [AvaloniaFact]
    public void AnAnchorLeftUnderTheKeyDoesNotKeepTheFlownElementAlive()
    {
        const string key = nameof(AnAnchorLeftUnderTheKeyDoesNotKeepTheFlownElementAlive);
        var host = new ContentControl { Content = View(Keyed(key, Brushes.Blue), 0, 50) };
        ShowRoot().Children.AddRange([host, Anchored(key)]);

        var flown = FlyAndLeave(host, key);
        Pump(() => false, 10);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(flown.TryGetTarget(out _), "the registry kept the flight's control alive");
    }

    [AvaloniaFact]
    public void AKeyedElementDetachedBeforeItsFirstLayoutPassPreparesNothing()
    {
        const string key = nameof(AKeyedElementDetachedBeforeItsFirstLayoutPassPreparesNothing);
        var root = ShowRoot();
        var view = View(Keyed(key, Brushes.Blue), 0, 50);

        root.Children.Add(view);
        root.Children.Remove(view);

        Assert.Null(LayoutTransition.TakePrepared(key));
    }

    [AvaloniaFact]
    public void FlightsAndEntrancesRegisterWithTheAmbientScope()
    {
        const string key = nameof(FlightsAndEntrancesRegisterWithTheAmbientScope);
        var host = ShowHost(View(Keyed(key, Brushes.Blue), 0, 50));
        var arriving = Keyed(key, Brushes.Red, ConnectedRecipes.Held);
        var anchored = Anchored(key, Presto.Build().Y(from: 24, to: 0).Duration(600000).Ease(Eases.None));
        var scope = new Scope();
        scope.Execute(_ => host.Content = new Panel { Children = { View(arriving, 80, 130), anchored } });
        Tick();
        Assert.NotEqual(SlotOf(arriving), Box(arriving));
        Assert.NotEqual(SlotOf(anchored), Extent(host, Colors.Lime));

        scope.Revert();
        Tick();

        Assert.Equal(SlotOf(arriving), Box(arriving));
        Assert.Equal(SlotOf(anchored), Extent(host, Colors.Lime));
    }

    [AvaloniaFact]
    public void AnUnsetDefaultMotionLasts500MsWithOutQuint()
    {
        var border = new Border();
        var animation = Presto.Animate(border, Connected.DefaultMotion.X(100).ToParams() with { Autoplay = false });

        animation.Seek(150);

        Assert.Equal(500, animation.Duration);
        Assert.Equal(0, animation._delay);
        Assert.Equal(100 * Eases.OutQuint(.3), X(border), 1e-6);
    }

    [AvaloniaFact]
    public void AnUnsetDefaultAnchorMotionRisesAndFadesInAfterADelayWithOutQuart()
    {
        var border = new Border();
        var animation = Presto.Animate(border, Connected.DefaultAnchorMotion.ToParams() with { Autoplay = false });

        animation.Seek(0);
        Assert.Equal(24d, PrestoAvalonia.Get(border, "Y"));
        Assert.Equal(0, border.Opacity);

        animation.Seek(200);

        Assert.Equal(400, animation.Duration);
        Assert.Equal(150, animation._delay);
        Assert.Equal(24 * (1 - Eases.OutQuart(.5)), (double)PrestoAvalonia.Get(border, "Y")!, 1e-6);
        Assert.Equal(Eases.OutQuart(.5), border.Opacity, 1e-6);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference<Border> FlyAndLeave(ContentControl host, string key)
    {
        var arriving = Keyed(key, Brushes.Red, Presto.Build().Duration(50));
        host.Content = View(arriving, 80, 130);
        Tick();
        Frames.Advance(100);
        host.Content = null;
        Tick();
        return new(arriving);
    }

    /// <summary>The default entrance holds the element transparent through its delay, then brings it to rest in its slot.</summary>
    private static void AssertEnters(Visual host, Control anchored)
    {
        Tick();
        Assert.Null(Extent(host, Colors.Lime));
        Assert.True(Pump(() => Extent(host, Colors.Lime) == SlotOf(anchored)), "the anchored element did not come to rest");
    }

    private static Border Anchored(string key, AnimationBuilder? motion = null)
    {
        var border = new Border
        {
            Background = Brushes.Lime,
            Width = 40,
            Height = 20,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(100, 20, 0, 0),
        };
        Connected.SetAnchor(border, key);
        Connected.SetMotion(border, motion);
        return border;
    }

    private static Border Keyed(string? key, IBrush brush, AnimationBuilder? motion = null)
    {
        var border = new Border { Background = brush, Width = 40, Height = 20 };
        Connected.SetKey(border, key);
        Connected.SetMotion(border, motion);
        return border;
    }

    private static Panel View(Border element, double x, double y)
    {
        element.HorizontalAlignment = HorizontalAlignment.Left;
        element.VerticalAlignment = VerticalAlignment.Top;
        element.Margin = new Thickness(x, y, 0, 0);
        return new Panel { Children = { element } };
    }

    private static ContentControl ShowHost(Control view)
    {
        var host = new ContentControl { Content = view };
        Show(host);
        return host;
    }

    private static Panel ShowRoot()
    {
        var root = new Panel();
        Show(root);
        return root;
    }

    private static new void Show(Control content)
    {
        new Window { Content = content, Background = Brushes.White, Width = 160, Height = 220 }.Show();
        Tick();
    }

    /// <summary>Held flights still creep a fraction of a pixel per second of compositor time, which antialiasing shows at the edges.</summary>
    private static void AssertNear(Rect expected, Rect actual)
    {
        Assert.InRange(actual.Left, expected.Left - 1, expected.Left + 1);
        Assert.InRange(actual.Top, expected.Top - 1, expected.Top + 1);
    }
}
