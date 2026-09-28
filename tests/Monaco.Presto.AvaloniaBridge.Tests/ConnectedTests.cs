using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge.Tests;

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
    public async Task TheArrivingElementsMotionSetsTheFlight()
    {
        const string key = nameof(TheArrivingElementsMotionSetsTheFlight);
        var host = ShowHost(View(Keyed(key, Brushes.Blue), 0, 50));
        var drawn = Extent(host, Colors.Blue)!.Value;

        var arriving = Keyed(key, Brushes.Red, ConnectedRecipes.Held);
        host.Content = View(arriving, 80, 130);
        await Pump(() => false, 80);

        AssertNear(drawn, Box(arriving));
    }

    [AvaloniaFact]
    public async Task TheLeavingElementsMotionIsIgnoredForTheDefault()
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

            Assert.True(await Pump(() => Box(arriving) == SlotOf(arriving)), "the arrival took the leaving element's motion");
        }
        finally
        {
            Connected.DefaultMotion = defaultMotion;
        }
    }

    [AvaloniaFact]
    public async Task DefaultMotionAppliesWhenNeitherElementSetsOne()
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
            await Pump(() => false, 80);

            AssertNear(drawn, Box(arriving));
        }
        finally
        {
            Connected.DefaultMotion = defaultMotion;
        }
    }

    [AvaloniaFact]
    public async Task AnAnchoredElementAttachedBeforeItsKeyedElementEntersWithTheFlight()
    {
        const string key = nameof(AnAnchoredElementAttachedBeforeItsKeyedElementEntersWithTheFlight);
        var host = ShowHost(View(Keyed(key, Brushes.Blue), 0, 50));
        var anchored = Anchored(key);
        host.Content = new Panel { Children = { anchored, View(Keyed(key, Brushes.Red, ConnectedRecipes.Held), 80, 130) } };

        await AssertEnters(host, anchored);
    }

    [AvaloniaFact]
    public async Task AnAnchoredElementAttachedAfterItsKeyedElementEntersWithTheFlight()
    {
        const string key = nameof(AnAnchoredElementAttachedAfterItsKeyedElementEntersWithTheFlight);
        var host = ShowHost(View(Keyed(key, Brushes.Blue), 0, 50));
        var anchored = Anchored(key);
        host.Content = new Panel { Children = { View(Keyed(key, Brushes.Red, ConnectedRecipes.Held), 80, 130), anchored } };

        await AssertEnters(host, anchored);
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
    public async Task ACompiledXamlViewFliesItsKeyedElementAndEntersItsAnchoredOne()
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
        await AssertEnters(host, arriving.Caption);
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

    /// <summary>The default entrance holds the element transparent through its delay, then brings it to rest in its slot.</summary>
    private static async Task AssertEnters(Visual host, Control anchored)
    {
        Tick();
        Assert.Null(Extent(host, Colors.Lime));
        Assert.True(await Pump(() => Extent(host, Colors.Lime) == SlotOf(anchored)), "the anchored element did not come to rest");
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

    private static void Show(Control content)
    {
        new Window { Content = content, Background = Brushes.White, Width = 160, Height = 220 }.Show();
        Tick();
    }

    /// <summary>Held flights still creep a fraction of a pixel per second of wall time, which antialiasing shows at the edges.</summary>
    private static void AssertNear(Rect expected, Rect actual)
    {
        Assert.InRange(actual.Left, expected.Left - 1, expected.Left + 1);
        Assert.InRange(actual.Top, expected.Top - 1, expected.Top + 1);
    }
}
