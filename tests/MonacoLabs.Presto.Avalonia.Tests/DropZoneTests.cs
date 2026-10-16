using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using MonacoLabs.Presto.Compositor;
using MonacoLabs.Presto.Draggables;

namespace MonacoLabs.Presto.Tests;

/// <summary>Covers the <see cref="Drop"/> attached properties, the per-<see cref="TopLevel"/> zone registry, a draggable's hover over the zones and how its drop resolves.</summary>
public sealed class DropZoneTests : BridgeTestBase
{
    private static (Window Window, StackPanel Panel) ShowPanel(params Control[] children)
    {
        var panel = new StackPanel();
        panel.Children.AddRange(children);
        var window = new Window { Content = panel, Width = 300, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, panel);
    }

    private static Border Zone(string? group = "lanes")
    {
        var zone = new Border { Width = 50, Height = 50 };
        Drop.SetGroup(zone, group);
        return zone;
    }

    private static List<Control> Zones(Window window) => DropZones.Of(window) ?? [];

    [AvaloniaFact]
    public void AZoneRegistersOnAttachAndLeavesOnDetach()
    {
        var a = Zone();
        var b = Zone();
        var plain = Zone(null);
        var (window, panel) = ShowPanel(a, b, plain);
        var left = new List<Control>();
        void OnLeft(Control zone) => left.Add(zone);
        DropZones.Left += OnLeft;
        try
        {
            Assert.Equal([a, b], Zones(window));

            panel.Children.Remove(a);
            Assert.Equal([b], Zones(window));
            Assert.Equal([a], left);

            panel.Children.Add(a);
            Assert.Equal([b, a], Zones(window));

            Drop.SetGroup(b, null);
            Assert.Equal([a], Zones(window));
            Assert.Equal([a, b], left);

            Drop.SetGroup(plain, "lanes");
            Assert.Equal([a, plain], Zones(window));

            panel.Children.Remove(b);
            panel.Children.Add(b);
            Assert.Equal([a, plain], Zones(window));
        }
        finally
        {
            DropZones.Left -= OnLeft;
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ADisabledOrHiddenZoneIsIgnored()
    {
        var enabled = Zone();
        var disabled = Zone();
        var hidden = Zone();
        var nested = Zone();
        var host = new Border { Child = nested };
        var (window, _) = ShowPanel(enabled, disabled, hidden, host);
        var left = new List<Control>();
        void OnLeft(Control zone) => left.Add(zone);
        DropZones.Left += OnLeft;
        try
        {
            Assert.True(Drop.GetIsEnabled(enabled));
            Drop.SetIsEnabled(disabled, false);
            hidden.IsVisible = false;
            host.IsVisible = false;

            var usable = new List<Control>();
            var zones = Zones(window);
            for (var i = 0; i < zones.Count; i++)
            {
                if (DropZones.IsUsable(zones[i], "lanes")) usable.Add(zones[i]);
            }

            Assert.Equal(4, zones.Count);
            Assert.Equal([enabled], usable);
            Assert.False(DropZones.IsUsable(enabled, "bin"));
            Assert.Equal([disabled], left);
        }
        finally
        {
            DropZones.Left -= OnLeft;
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CenterHitsTheZoneUnderTheTargetsCenter()
    {
        var (window, target, zones) = ShowBoard(new Rect(100, 0, 100, 100), new Rect(200, 0, 100, 100));
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams { DropGroup = "lanes" });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        DragTo(window, new Point(90, 25));
        Assert.Null(draggable.Zone);
        DragTo(window, new Point(110, 25));
        Assert.Same(zones[0], draggable.Zone);
        DragTo(window, new Point(210, 25));
        Assert.Same(zones[1], draggable.Zone);
    }

    [AvaloniaFact]
    public void PointerHitsTheZoneUnderThePointer()
    {
        var (window, target, zones) = ShowBoard(new Rect(100, 0, 100, 100), new Rect(200, 0, 100, 100));
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams { DropGroup = "lanes", DropHit = DropHit.Pointer });

        window.MouseDown(new Point(5, 25), MouseButton.Left);
        // The target's center sits 20 to the right of the pointer.
        DragTo(window, new Point(90, 25));
        Assert.Null(draggable.Zone);
        DragTo(window, new Point(190, 25));
        Assert.Same(zones[0], draggable.Zone);
    }

    [AvaloniaFact]
    public void OverlapPicksTheLargestIntersection()
    {
        var (window, target, zones) = ShowBoard(new Rect(100, 0, 80, 100), new Rect(200, 0, 100, 100));
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams { DropGroup = "lanes", DropHit = DropHit.Overlap });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        // The target spans 160 to 210, its center in the gap: 20 over the first zone, 10 over the second.
        DragTo(window, new Point(185, 25));
        Assert.Same(zones[0], draggable.Zone);
        DragTo(window, new Point(200, 25));
        Assert.Same(zones[1], draggable.Zone);
        DragTo(window, new Point(350, 25));
        Assert.Null(draggable.Zone);
    }

    [AvaloniaFact]
    public void TheLastRegisteredZoneWinsATie()
    {
        var (window, target, zones) = ShowBoard(new Rect(100, 0, 100, 100), new Rect(150, 0, 100, 100));
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams { DropGroup = "lanes" });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        DragTo(window, new Point(175, 25));
        Assert.Same(zones[1], draggable.Zone);

        var canvas = (Canvas)zones[0].Parent!;
        canvas.Children.Remove(zones[0]);
        canvas.Children.Insert(0, zones[0]);
        DragTo(window, new Point(176, 25));
        Assert.Same(zones[0], draggable.Zone);
    }

    [AvaloniaFact]
    public void ZoneChangesRaiseOnceAndMoveThePseudoClass()
    {
        var (window, target, zones) = ShowBoard(new Rect(100, 0, 100, 100), new Rect(200, 0, 100, 100));
        var changes = new List<Control?>();
        PrestoAvalonia.CreateDraggable(target, new DraggableParams { DropGroup = "lanes", OnZoneChange = d => changes.Add(d.Zone) });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        DragTo(window, new Point(125, 25));
        DragTo(window, new Point(135, 25));
        Assert.Contains(":dragover", zones[0].Classes);
        DragTo(window, new Point(225, 25));
        Assert.DoesNotContain(":dragover", zones[0].Classes);
        Assert.Contains(":dragover", zones[1].Classes);
        DragTo(window, new Point(325, 25));
        Assert.DoesNotContain(":dragover", zones[1].Classes);

        Assert.Equal([zones[0], zones[1], null], changes);
    }

    [AvaloniaFact]
    public void ARejectedZoneNeverBecomesZone()
    {
        var (window, target, zones) = ShowBoard(new Rect(100, 0, 100, 100), new Rect(150, 0, 100, 100));
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            DropGroup = "lanes",
            CanDrop = (_, zone) => zone != zones[1],
        });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        DragTo(window, new Point(175, 25));
        Assert.Same(zones[0], draggable.Zone);
        DragTo(window, new Point(225, 25));
        Assert.Null(draggable.Zone);
        Assert.DoesNotContain(":dragover", zones[1].Classes);
    }

    [AvaloniaFact]
    public void AFlungCardTracksZonesThroughTheThrow()
    {
        var (window, target, zones) = ShowBoard(new Rect(100, 0, 100, 100), new Rect(200, 0, 100, 100));
        var released = false;
        var changesAfterRelease = new List<Control?>();
        Control? dropped = null;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            DropGroup = "lanes",
            Y = new DraggableAxisParams { Enabled = false },
            OnRelease = _ => released = true,
            OnDrop = d => dropped = d.Zone,
            OnZoneChange = d =>
            {
                if (released) changesAfterRelease.Add(d.Zone);
            },
        });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        for (var x = 45; x <= 125; x += 20) DragTo(window, new Point(x, 25));
        Assert.Same(zones[0], draggable.Zone);
        // 20 units per 20 ms frame throws the target 150 further, its center from 125 to 275.
        window.MouseUp(new Point(125, 25), MouseButton.Left);
        Frames.Advance(3000, 20);

        Assert.Equal(250d, draggable.X);
        Assert.Equal([zones[1]], changesAfterRelease);
        Assert.Same(zones[1], dropped);
    }

    [AvaloniaFact]
    public void SettlingOverAZoneDropsAfterOnSettle()
    {
        var (window, target, zones) = ShowBoard(new Rect(100, 0, 100, 100));
        var log = new List<string>();
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            DropGroup = "lanes",
            OnSettle = _ => log.Add("settle"),
            OnDrop = d => log.Add(d.Zone == zones[0] ? "drop" : "drop elsewhere"),
            OnDropMiss = _ => log.Add("miss"),
        });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        DragTo(window, new Point(125, 25));
        // Held still until the velocity samples are 0, so the release settles where it is.
        Frames.Advance(100, 20);
        window.MouseUp(new Point(125, 25), MouseButton.Left);
        Frames.Advance(2000, 20);

        Assert.Equal(["settle", "drop"], log);
        Assert.Null(draggable.Zone);
        Assert.DoesNotContain(":dragover", zones[0].Classes);
    }

    [AvaloniaFact]
    public void AResetInOnSettleStillDropsOnTheZone()
    {
        var (window, target, zones) = ShowBoard(new Rect(100, 0, 100, 100));
        Control? dropped = null;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            DropGroup = "lanes",
            OnSettle = d => d.Reset(),
            OnDrop = d => dropped = d.Zone,
        });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        DragTo(window, new Point(125, 25));
        Frames.Advance(100, 20);
        window.MouseUp(new Point(125, 25), MouseButton.Left);
        Frames.Advance(2000, 20);

        Assert.Same(zones[0], dropped);
        Assert.Null(draggable.Zone);
        Assert.DoesNotContain(":dragover", zones[0].Classes);
    }

    [AvaloniaFact]
    public void ARegrabMidThrowKeepsZoneAndDropsOnTheNextSettle()
    {
        var (window, target, zones) = ShowBoard(new Rect(100, 0, 100, 100), new Rect(200, 0, 100, 100));
        var drops = new List<Control?>();
        var misses = 0;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            DropGroup = "lanes",
            Y = new DraggableAxisParams { Enabled = false },
            OnDrop = d => drops.Add(d.Zone),
            OnDropMiss = _ => misses++,
        });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        for (var x = 45; x <= 125; x += 20) DragTo(window, new Point(x, 25));
        window.MouseUp(new Point(125, 25), MouseButton.Left);
        Frames.Advance(60, 20);
        var zone = draggable.Zone;
        Assert.NotNull(zone);

        var grab = new Point(draggable.X + 25, 25);
        window.MouseDown(grab, MouseButton.Left);
        Frames.Advance(100, 20);
        Assert.Same(zone, draggable.Zone);
        Assert.Empty(drops);
        Assert.Equal(0, misses);

        window.MouseUp(grab, MouseButton.Left);
        Frames.Advance(2000, 20);
        Assert.Equal([zone], drops);
        Assert.Equal(0, misses);
    }

    [AvaloniaFact]
    public void SettlingOverNothingMissesAndReturnsHome()
    {
        var (window, target, _) = ShowBoard(new Rect(100, 0, 100, 100));
        var misses = 0;
        var drops = 0;
        var settles = 0;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            DropGroup = "lanes",
            DropMiss = DropMiss.ReturnHome,
            OnSettle = _ => settles++,
            OnDrop = _ => drops++,
            OnDropMiss = _ => misses++,
        });
        draggable.X = 10;

        window.MouseDown(new Point(35, 25), MouseButton.Left);
        DragTo(window, new Point(60, 25));
        Frames.Advance(100, 20);
        window.MouseUp(new Point(60, 25), MouseButton.Left);
        Assert.Equal(35d, draggable.X);
        Frames.Advance(2000, 20);

        Assert.Equal(10d, draggable.X);
        Assert.Equal(1, misses);
        Assert.Equal(0, drops);
        // The release's settle, then the return home's.
        Assert.Equal(2, settles);
    }

    [AvaloniaFact]
    public void EscapeIsAMiss()
    {
        var (window, target, zones) = ShowBoard(new Rect(100, 0, 100, 100));
        var misses = 0;
        var drops = 0;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            DropGroup = "lanes",
            OnDrop = _ => drops++,
            OnDropMiss = _ => misses++,
        });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        DragTo(window, new Point(125, 25));
        Assert.Same(zones[0], draggable.Zone);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

        Assert.Null(draggable.Zone);
        Assert.DoesNotContain(":dragover", zones[0].Classes);
        Assert.Equal(1, misses);

        Frames.Advance(2000, 20);
        window.MouseUp(new Point(125, 25), MouseButton.Left);
        Frames.Advance(100, 20);

        Assert.Equal(0d, draggable.X);
        Assert.Null(draggable.Zone);
        Assert.Equal(1, misses);
        Assert.Equal(0, drops);
    }

    [AvaloniaFact]
    public void AZoneDetachingMidDragClearsZone()
    {
        var (window, target, zones) = ShowBoard(new Rect(100, 0, 100, 100));
        var changes = new List<Control?>();
        var misses = 0;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            DropGroup = "lanes",
            OnZoneChange = d => changes.Add(d.Zone),
            OnDropMiss = _ => misses++,
        });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        DragTo(window, new Point(125, 25));
        ((Canvas)zones[0].Parent!).Children.Remove(zones[0]);

        Assert.Null(draggable.Zone);
        Assert.DoesNotContain(":dragover", zones[0].Classes);
        Assert.Equal([zones[0], null], changes);

        Frames.Advance(100, 20);
        window.MouseUp(new Point(125, 25), MouseButton.Left);
        Frames.Advance(2000, 20);
        Assert.Equal(1, misses);
        Assert.Equal(100d, draggable.X);
    }

    [AvaloniaTheory]
    [InlineData(nameof(Draggable.Stop))]
    [InlineData(nameof(Draggable.Reset))]
    [InlineData(nameof(Draggable.Disable))]
    [InlineData(nameof(Draggable.Revert))]
    public void RevertClearsZoneWithoutDropping(string method)
    {
        var (window, target, zones) = ShowBoard(new Rect(100, 0, 100, 100));
        var resolved = 0;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            DropGroup = "lanes",
            OnDrop = _ => resolved++,
            OnDropMiss = _ => resolved++,
        });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        DragTo(window, new Point(125, 25));
        Assert.Same(zones[0], draggable.Zone);
        typeof(Draggable).GetMethod(method)!.Invoke(draggable, null);

        Assert.Null(draggable.Zone);
        Assert.DoesNotContain(":dragover", zones[0].Classes);
        window.MouseUp(new Point(125, 25), MouseButton.Left);
        Frames.Advance(2000, 20);
        Assert.Equal(0, resolved);
    }

    [AvaloniaFact]
    public void ReparentMovesIntoThePanelAndFliesFromWhereTheDragLeftIt()
    {
        var target = new Border { Width = 50, Height = 50, Background = Brushes.Red };
        var first = new Canvas { Width = 100, Height = 100, Children = { target } };
        var filler = new Border { Height = 20 };
        var second = new StackPanel { Width = 100, Height = 100, Children = { filler } };
        Drop.SetGroup(first, "lanes");
        Drop.SetGroup(second, "lanes");
        Canvas.SetLeft(second, 200);
        Canvas.SetTop(second, 100);
        var window = new Window { Content = new Canvas { Width = 500, Height = 200, Children = { first, second } }, Width = 500, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var layout = target.AnimateLayout(400);
        Rect? released = null;
        var draggable = PrestoAvalonia.CreateDraggable(target, new DraggableParams
        {
            DropGroup = "lanes",
            OnDrop = d =>
            {
                released = LayoutTransition.MeasureDrawn(target);
                d.Reparent(second, 0);
            },
        });

        window.MouseDown(new Point(25, 25), MouseButton.Left);
        DragTo(window, new Point(240, 150));
        Frames.Advance(100, 20);
        window.MouseUp(new Point(240, 150), MouseButton.Left);
        for (var i = 0; i < 200 && released is null; i++) Frames.Advance(20);
        window.UpdateLayout();

        Assert.Equal(new Rect(215, 125, 50, 50), released);
        Assert.Same(second, target.Parent);
        Assert.Equal(0, second.Children.IndexOf(target));
        Assert.Equal(0d, draggable.X);
        Assert.Equal(0d, draggable.Y);
        Assert.NotEqual(released!.Value.Position, LayoutTransition.MeasureOnScreen(target)!.Value.Position);
        Assert.Equal(released, LayoutTransition.MeasureDrawn(target));

        Frames.Advance(3000, 20);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(layout, LayoutTransition.Live(target));
    }

    [AvaloniaFact]
    public void ReparentOnANonPanelParentThrows()
    {
        var target = new Border { Width = 50, Height = 50 };
        var host = new Border { Child = target };
        var panel = new StackPanel();
        var (window, _) = ShowPanel(host, panel);
        var draggable = PrestoAvalonia.CreateDraggable(target);

        Assert.Throws<InvalidOperationException>(() => draggable.Reparent(panel));
        Assert.Same(host, target.Parent);
        Assert.Empty(panel.Children);
        window.Close();
    }

    /// <summary>A 50x50 target at the origin of a canvas, with a "lanes" zone at each of <paramref name="zones"/> beneath it.</summary>
    private static (Window Window, Border Target, Border[] Zones) ShowBoard(params Rect[] zones)
    {
        var canvas = new Canvas { Width = 500, Height = 200 };
        var borders = new Border[zones.Length];
        for (var i = 0; i < zones.Length; i++)
        {
            borders[i] = new Border { Width = zones[i].Width, Height = zones[i].Height };
            Canvas.SetLeft(borders[i], zones[i].X);
            Canvas.SetTop(borders[i], zones[i].Y);
            Drop.SetGroup(borders[i], "lanes");
            canvas.Children.Add(borders[i]);
        }
        var target = new Border { Width = 50, Height = 50, Background = Brushes.Red };
        canvas.Children.Add(target);
        var window = new Window { Content = canvas, Width = 500, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, target, borders);
    }

    private void DragTo(Window window, Point point)
    {
        window.MouseMove(point);
        Frames.Advance(20);
    }
}
