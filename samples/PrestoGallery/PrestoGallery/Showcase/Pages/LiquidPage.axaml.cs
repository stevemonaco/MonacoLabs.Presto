using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MonacoLabs.Presto;
using static MonacoLabs.Presto.PrestoKeys;

namespace PrestoGallery.Showcase.Pages;

public partial class LiquidPage : ShowcasePage
{
    private const double PillInset = 6;
    private const double LabelDrop = 6;
    private const double ParentRadius = 28;
    private const double ChildRadius = 20;
    private const double Fan = 120;
    private const double Reach = 36;

    private static readonly string[] Names = ["Home", "Search", "Saved", "Alerts", "Profile"];

    private static readonly Geometry[] Icons =
    [
        Geometry.Parse("M10,20V14H14V20H19V12H22L12,3L2,12H5V20H10Z"),
        Geometry.Parse("M9.5,3A6.5,6.5 0 0,1 16,9.5C16,11.11 15.41,12.59 14.44,13.73L14.71,14H15.5L20.5,19L19,20.5L14,15.5V14.71L13.73,14.44C12.59,15.41 11.11,16 9.5,16A6.5,6.5 0 0,1 3,9.5A6.5,6.5 0 0,1 9.5,3M9.5,5C7,5 5,7 5,9.5C5,12 7,14 9.5,14C12,14 14,12 14,9.5C14,7 12,5 9.5,5Z"),
        Geometry.Parse("M12,21.35L10.55,20.03C5.4,15.36 2,12.27 2,8.5C2,5.41 4.42,3 7.5,3C9.24,3 10.91,3.81 12,5.08C13.09,3.81 14.76,3 16.5,3C19.58,3 22,5.41 22,8.5C22,12.27 18.6,15.36 13.45,20.03L12,21.35Z"),
        Geometry.Parse("M21,19V20H3V19L5,17V11C5,7.9 7.03,5.17 10,4.29C10,4.19 10,4.1 10,4A2,2 0 0,1 12,2A2,2 0 0,1 14,4C14,4.1 14,4.19 14,4.29C16.97,5.17 19,7.9 19,11V17L21,19M14,21A2,2 0 0,1 12,23A2,2 0 0,1 10,21"),
        Geometry.Parse("M12,4A4,4 0 0,1 16,8A4,4 0 0,1 12,12A4,4 0 0,1 8,8A4,4 0 0,1 12,4M12,14C16.42,14 20,15.79 20,18V20H4V18C4,15.79 7.58,14 12,14Z"),
    ];

    private static readonly double[] FanAngles = [-135, -90, -45];

    private readonly Edges _edges;
    private readonly ScaleTransform _squash = new();
    private readonly PathIcon[] _icons;
    private readonly TextBlock[] _labels;
    private readonly Blob _parent;
    private readonly Blob[] _children;
    private int _selected;
    private bool _open;

    static LiquidPage()
    {
        Presto.RegisterTarget<Edges>();
        Presto.RegisterTarget<Blob>();
    }

    public LiquidPage()
    {
        InitializeComponent();
        _icons = [.. Icons.Select((icon, i) => new PathIcon { Data = icon, Foreground = i == 0 ? Palette.Ink : Palette.InkDim })];
        _labels = [.. Names.Select(name => new TextBlock { Text = name })];
        for (var i = 0; i < Names.Length; i++)
        {
            var tab = i;
            var button = new Button { Classes = { "bare" }, Content = new StackPanel { Children = { _icons[i], _labels[i] } } };
            button.Click += (_, _) => Select(tab);
            Tabs.Children.Add(button);
        }
        _edges = new Edges { ScaleY = 1 };
        Pill.RenderTransform = _squash;

        var home = new Point(Canvas.GetLeft(Toggle) + ParentRadius, Canvas.GetTop(Toggle) + ParentRadius);
        _parent = new Blob { X = home.X, Y = home.Y, R = ParentRadius };
        _children = [.. FanAngles.Select(_ => new Blob { X = home.X, Y = home.Y, R = ChildRadius })];
        Toggle.Click += (_, _) => Scope.Execute(_ => ToggleFan());
        Redraw();
        HowCode.Code = $"{Snippets.LiquidPill}\n\n{Snippets.LiquidEdge}\n\n{Snippets.LiquidMetaball}";
    }

    protected override void OnAttached()
    {
        for (var i = 0; i < _labels.Length; i++)
        {
            if (i != _selected) PrestoAvalonia.Set(_labels[i], new AvaloniaAnimationParams { [Y] = LabelDrop, ["Opacity"] = 0 });
        }
        AfterFirstArrange(Tabs, () =>
        {
            var slot = Slot(_selected);
            (_edges.Left, _edges.Right) = (slot.Left, slot.Right);
            PlacePill();
        });
    }

    // Measured from the parent so a pressed tab's render transform does not shift the slot.
    private Rect Slot(int tab)
    {
        var button = Tabs.Children[tab];
        var at = Tabs.TranslatePoint(button.Bounds.Position, Rail)!.Value;
        return new Rect(at.X + PillInset, 0, button.Bounds.Width - (2 * PillInset), 0);
    }

    private void Select(int tab)
    {
        if (tab == _selected) return;
        var old = _selected;
        _selected = tab;
        var slot = Slot(tab);
        var forward = slot.Left > _edges.Left;

        Scope.Execute(_ =>
        {
            #region snippet LiquidPill
            Presto.Animate(_edges, new AnimationParams
            {
                ["Left"] = Edge(slot.Left, leads: !forward),
                ["Right"] = Edge(slot.Right, leads: forward),
                [ScaleY] = new TweenParams[]
                {
                    new() { To = .82, Duration = 160, Ease = Eases.OutQuad },
                    new() { To = 1, Duration = 380, Ease = Eases.InOutQuad },
                },
                OnUpdate = _ => PlacePill(),
            });
            #endregion

            _icons[tab].Animate(new()
            {
                [Y] = new TweenParams[]
                {
                    new() { To = -8, Duration = 120, Ease = Eases.OutQuad },
                    new() { To = 0, Ease = Eases.Spring(bounce: .5, duration: 420) },
                },
                ["Foreground"] = TweenValue.Of(Palette.Ink),
                Duration = 300,
            });
            _icons[old].Animate(new() { ["Foreground"] = TweenValue.Of(Palette.InkDim), Duration = 300 });
            _labels[tab].Animate(new() { [Y] = 0, ["Opacity"] = 1, Duration = 320, Delay = 80, Ease = Eases.OutQuart });
            _labels[old].Animate(new() { [Y] = LabelDrop, ["Opacity"] = 0, Duration = 200, Ease = Eases.OutQuad });
        });
    }

    #region snippet LiquidEdge
    private static TweenParams Edge(double to, bool leads) => leads
        ? new() { To = to, Duration = 300, Ease = Eases.OutExpo }
        : new() { To = to, Duration = 520, Delay = 70, Ease = Eases.OutQuart };
    #endregion

    private void PlacePill()
    {
        Canvas.SetLeft(Pill, _edges.Left);
        Pill.Width = _edges.Right - _edges.Left;
        _squash.ScaleY = _edges.ScaleY;
    }

    private void ToggleFan()
    {
        _open = !_open;
        Plus.Animate(new() { [Rotate] = _open ? 45 : 0, Duration = 320, Ease = Eases.OutBack() });
        #region snippet LiquidMetaball
        Presto.Animate(_children, new AnimationParams
        {
            [X] = (FunctionValue)((_, i, _) => _parent.X + (_open ? Fan * Math.Cos(FanAngles[i] * Math.PI / 180) : 0)),
            [Y] = (FunctionValue)((_, i, _) => _parent.Y + (_open ? Fan * Math.Sin(FanAngles[i] * Math.PI / 180) : 0)),
            Delay = Presto.Stagger(60),
            Ease = _open ? Eases.Spring(bounce: .35, duration: 700) : Eases.InBack(),
            Duration = _open ? null : (TweenValue?)380,
            OnUpdate = _ => Redraw(),
        });
        #endregion
    }

    private void Redraw()
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.SetFillRule(FillRule.NonZero);
            Circle(context, _parent);
            foreach (var child in _children)
            {
                Circle(context, child);
                if (Metaball.Neck(new(_parent.X, _parent.Y), _parent.R, new(child.X, child.Y), child.R, Reach) is not { } neck) continue;
                context.BeginFigure(neck[0], true);
                context.CubicBezierTo(neck[1], neck[2], neck[3]);
                context.LineTo(neck[4]);
                context.CubicBezierTo(neck[5], neck[6], neck[7]);
                context.EndFigure(true);
            }
        }
        Goo.Data = geometry;
    }

    // Counter-clockwise to match the neck contour's winding, so the nonzero fill unions them instead of cutting holes.
    private static void Circle(StreamGeometryContext context, Blob blob)
    {
        context.BeginFigure(new Point(blob.X + blob.R, blob.Y), true);
        context.ArcTo(new Point(blob.X - blob.R, blob.Y), new Size(blob.R, blob.R), 0, false, SweepDirection.CounterClockwise);
        context.ArcTo(new Point(blob.X + blob.R, blob.Y), new Size(blob.R, blob.R), 0, false, SweepDirection.CounterClockwise);
        context.EndFigure(true);
    }

    private sealed class Edges
    {
        public double Left { get; set; }

        public double Right { get; set; }

        public double ScaleY { get; set; }
    }

    private sealed class Blob
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double R { get; set; }
    }
}
