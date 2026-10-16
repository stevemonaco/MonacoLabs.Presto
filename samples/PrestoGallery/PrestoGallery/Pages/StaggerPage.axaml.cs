using Avalonia.Controls;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;

namespace PrestoGallery.Pages;

public partial class StaggerPage : ShowcasePage
{
    private const int StripDots = 12;
    private const int GridTiles = 7 * 4;

    private Animation? _explicitGrid;
    private Animation? _autoGrid;

    public StaggerPage()
    {
        InitializeComponent();

        foreach (var strip in new[] { ValueStrip, TimeStrip, RangeStrip, EaseStrip, StartStrip, TotalStrip, JitterStrip, ModifierStrip, TimelineStrip })
        {
            for (var i = 0; i < StripDots; i++) strip.Children.Add(new Border { Classes = { "dot" } });
        }
        for (var i = 0; i < GridTiles; i++)
        {
            ExplicitTiles.Children.Add(new Border { Classes = { "tile" } });
            AutoTiles.Children.Add(new Border { Classes = { "tile" } });
        }
    }

    private (StaggerFrom Value, string Code) SelectedFrom => FromBox.SelectedIndex switch
    {
        1 => Snippet.Of<StaggerFrom>(StaggerFrom.Center),
        2 => Snippet.Of<StaggerFrom>(StaggerFrom.Last),
        3 => Snippet.Of<StaggerFrom>(10),
        4 => Snippet.Of<StaggerFrom>(StaggerFrom.Random),
        5 => Snippet.Of<StaggerFrom>(StaggerFrom.At(1, 0)),
        _ => Snippet.Of<StaggerFrom>(StaggerFrom.First),
    };

    private (StaggerAxis? Value, string Code) SelectedAxis => AxisBox.SelectedIndex switch
    {
        1 => Snippet.Of<StaggerAxis?>(StaggerAxis.X),
        2 => Snippet.Of<StaggerAxis?>(StaggerAxis.Y),
        _ => Snippet.Of<StaggerAxis?>(null),
    };

    protected override void OnAttached()
    {
        ValueCode.Code = Snippet.Of(Presto.Animate(ValueStrip.Children, new()
        {
            ["X"] = Presto.Stagger(6),
            Duration = 450,
            Ease = Eases.OutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        TimeCode.Code = Snippet.Of(Presto.Animate(TimeStrip.Children, new()
        {
            ["Y"] = -14,
            Delay = Presto.Stagger(60),
            Duration = 450,
            Ease = Eases.OutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        RangeCode.Code = Snippet.Of(Presto.Animate(RangeStrip.Children, new()
        {
            ["Y"] = -14,
            Delay = Presto.Stagger(new double[] { 0, 700 }),
            Duration = 450,
            Ease = Eases.OutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        EaseCode.Code = Snippet.Of(Presto.Animate(EaseStrip.Children, new()
        {
            ["Y"] = -14,
            Delay = Presto.Stagger(new double[] { 0, 700 }, new() { Ease = "inOutQuad" }),
            Duration = 450,
            Ease = Eases.OutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        StartCode.Code = Snippet.Of(Presto.Animate(StartStrip.Children, new()
        {
            ["Y"] = -14,
            Delay = Presto.Stagger(50, new() { Start = 400 }),
            Duration = 450,
            Ease = Eases.OutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        TotalCode.Code = Snippet.Of(Presto.Animate(TotalStrip.Children, new()
        {
            ["Y"] = -14,
            Delay = Presto.Stagger(50, new() { From = StaggerFrom.Center, Total = 24 }),
            Duration = 450,
            Ease = Eases.OutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        JitterCode.Code = Snippet.Of(Presto.Animate(JitterStrip.Children, new()
        {
            ["Y"] = -14,
            Delay = Presto.Stagger(50, new() { Jitter = [70], Seed = 7 }),
            Duration = 450,
            Ease = Eases.OutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        ModifierCode.Code = Snippet.Of(Presto.Animate(ModifierStrip.Children, new()
        {
            ["X"] = Presto.Stagger(1.6, new() { Modifier = x => x * x }),
            Duration = 450,
            Ease = Eases.OutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        TimelineCode.Code = Snippet.Of(Presto.CreateTimeline(new() { Loop = true })
            .Add(TimelineStrip.Children, new()
            {
                ["Scale"] = 1.9,
                Duration = 350,
                Ease = Eases.OutQuad,
                Alternate = true,
                Loop = 1,
            }, Presto.Stagger(60))).Code;
    }

    // AutoGrid measures where the tiles were laid out, and OnAttached runs before the first layout pass.
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Scope.Execute(_ => RebuildGrids());
    }

    private void GridOptionChanged(object? sender, RoutedEventArgs e)
    {
        if (IsLoaded) Scope.Execute(_ => RebuildGrids());
    }

    private void RebuildGrids()
    {
        _explicitGrid?.Revert();
        _autoGrid?.Revert();

        var (from, fromCode) = SelectedFrom;
        var (axis, axisCode) = SelectedAxis;
        var reversed = ReversedCheck.IsChecked == true;
        var locals = $"StaggerFrom from = {fromCode};\nStaggerAxis? axis = {axisCode};\nbool reversed = {(reversed ? "true" : "false")};\n\n";

        (_explicitGrid, var explicitCode) = Snippet.Of(Presto.Animate(ExplicitTiles.Children, new()
        {
            ["Scale"] = 1.35,
            ["Background"] = "#ff6b6b",
            Delay = Presto.Stagger(40, new()
            {
                Grid = [7, 4],
                From = from,
                Axis = axis,
                Reversed = reversed,
            }),
            Duration = 450,
            Ease = Eases.OutQuad,
            Loop = true,
            Alternate = true,
        }));
        (_autoGrid, var autoCode) = Snippet.Of(Presto.Animate(AutoTiles.Children, new()
        {
            ["Scale"] = 1.35,
            ["Background"] = "#ff6b6b",
            Delay = PrestoAvalonia.Stagger(40, new()
            {
                From = from,
                Axis = axis,
                Reversed = reversed,
            }),
            Duration = 450,
            Ease = Eases.OutQuad,
            Loop = true,
            Alternate = true,
        }));
        ExplicitCode.Code = locals + explicitCode;
        AutoCode.Code = locals + autoCode;
    }
}
