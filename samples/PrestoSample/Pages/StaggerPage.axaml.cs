using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Pages;

public partial class StaggerPage : UserControl
{
    private const int StripDots = 12;
    private const int GridColumns = 7;
    private const int GridRows = 4;

    private readonly List<Animation> _strips = [];
    private readonly List<Animation> _grids = [];
    private Timeline? _timeline;

    public StaggerPage()
    {
        InitializeComponent();

        foreach (var strip in new[] { ValueStrip, TimeStrip, RangeStrip, EaseStrip, StartStrip, TotalStrip, JitterStrip, ModifierStrip, TimelineStrip })
        {
            for (var i = 0; i < StripDots; i++) strip.Children.Add(new Border { Classes = { "dot" } });
        }
        for (var i = 0; i < GridColumns * GridRows; i++)
        {
            ExplicitTiles.Children.Add(new Border { Classes = { "tile" } });
            AutoTiles.Children.Add(new Border { Classes = { "tile" } });
        }
    }

    private StaggerFrom SelectedFrom => FromBox.SelectedIndex switch
    {
        1 => StaggerFrom.Center,
        2 => StaggerFrom.Last,
        3 => 10,
        4 => StaggerFrom.Random,
        5 => StaggerFrom.At(1, 0),
        _ => StaggerFrom.First,
    };

    private StaggerAxis? SelectedAxis => AxisBox.SelectedIndex switch
    {
        1 => StaggerAxis.X,
        2 => StaggerAxis.Y,
        _ => null,
    };

    // AutoGrid measures the tiles where they landed, so the animations wait for the first layout pass.
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _strips.Add(Presto.Animate(ValueStrip.Children, Ripple(new() { ["X"] = Presto.Stagger(6) })));
        Hop(TimeStrip, Presto.Stagger(60));
        Hop(RangeStrip, Presto.Stagger(new double[] { 0, 700 }));
        Hop(EaseStrip, Presto.Stagger(new double[] { 0, 700 }, new() { Ease = "inOutQuad" }));
        Hop(StartStrip, Presto.Stagger(50, new() { Start = 400 }));
        Hop(TotalStrip, Presto.Stagger(50, new() { From = StaggerFrom.Center, Total = 24 }));
        Hop(JitterStrip, Presto.Stagger(50, new() { Jitter = [70], Seed = 7 }));
        _strips.Add(Presto.Animate(ModifierStrip.Children, Ripple(new()
        {
            ["X"] = Presto.Stagger(1.6, new() { Modifier = x => x * x }),
        })));

        _timeline = Presto.CreateTimeline(new() { Loop = true })
            .Add(TimelineStrip.Children, new() { ["Scale"] = 1.9, Duration = 350, Ease = Eases.OutQuad, Alternate = true, Loop = 1 }, Presto.Stagger(60));

        RebuildGrids();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var animation in _strips) animation.Revert();
        _strips.Clear();
        foreach (var animation in _grids) animation.Revert();
        _grids.Clear();
        _timeline?.Revert();
        _timeline = null;
    }

    private void OriginChanged(object? sender, SelectionChangedEventArgs e) => RebuildGrids();

    private void ToggleChanged(object? sender, RoutedEventArgs e) => RebuildGrids();

    private void RebuildGrids()
    {
        if (!IsLoaded) return;

        foreach (var animation in _grids) animation.Revert();
        _grids.Clear();

        var from = SelectedFrom;
        var axis = SelectedAxis;
        var reversed = ReversedCheck.IsChecked == true;

        _grids.Add(Presto.Animate(ExplicitTiles.Children,
            Tiles(Presto.Stagger(40, new() { Grid = [GridColumns, GridRows], From = from, Axis = axis, Reversed = reversed }))));
        _grids.Add(Presto.Animate(AutoTiles.Children,
            Tiles(PrestoAvalonia.Stagger(40, new() { From = from, Axis = axis, Reversed = reversed }))));
    }

    private void Hop(Panel strip, Stagger delay) =>
        _strips.Add(Presto.Animate(strip.Children, Ripple(new() { ["Y"] = -14, Delay = delay })));

    private static AnimationParams Ripple(AnimationParams values) =>
        values with { Duration = 450, Ease = Eases.OutQuad, Loop = true, Alternate = true };

    private static AnimationParams Tiles(Stagger delay) =>
        Ripple(new() { ["Scale"] = 1.35, ["Background"] = "#ff6b6b", Delay = delay });
}
