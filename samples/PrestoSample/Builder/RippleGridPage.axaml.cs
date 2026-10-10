using Avalonia;
using Avalonia.Controls;
using MonacoLabs.Presto;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>A grid staggered from the clicked cell: the stagger sets each cell's delay and, as a range, how deep its scale dips.</summary>
public partial class RippleGridPage : ShowcasePage
{
    private const int Columns = 16;
    private const int Rows = 9;
    private readonly List<Border> _cells = [];

    public RippleGridPage()
    {
        InitializeComponent();
        CodeText.Code = Snippets.RippleWave;
        Cells.Columns = Columns;
        for (var i = 0; i < Columns * Rows; i++)
        {
            var index = i;
            var cell = new Border
            {
                Width = 26,
                Height = 26,
                Margin = new Thickness(3),
                CornerRadius = new CornerRadius(6),
                Background = Palette.AccentDeep,
            };
            cell.PointerPressed += (_, _) => Ripple(index);
            _cells.Add(cell);
            Cells.Children.Add(cell);
        }
    }

    public Animation? Animation { get; private set; }

    protected override void OnAttached() => Ripple(StaggerFrom.Center);

    private void Ripple(StaggerFrom from)
    {
        #region snippet RippleWave
        var wave = new StaggerParams { Grid = [Columns, Rows], From = from };
        Animation = Presto.Build()
            .Scale(k => k.To(Presto.Stagger(new[] { .2, .9 }, wave)).To(1))
            .Rotate(k => k.To(Presto.Stagger(new[] { 90, 0 }, wave)).To(0))
            .Background(k => k.To(TweenValue.Of(Palette.AccentPale)).To(TweenValue.Of(Palette.AccentDeep)))
            .Delay(Presto.Stagger(45, wave))
            .Duration(1100)
            .Ease(Eases.InOutQuad)
            .WhenReduced(reduced => reduced.Background(k => k.To(TweenValue.Of(Palette.AccentPale)).To(TweenValue.Of(Palette.AccentDeep))))
            .Animate(_cells);
        #endregion
    }
}
