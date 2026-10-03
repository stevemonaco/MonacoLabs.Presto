using Avalonia.Controls;
using Avalonia.Layout;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

/// <summary>
/// Derived values: one pointer <see cref="MotionValue"/> mapped, sprung and differentiated into five properties of a card,
/// and a header whose opacity <see cref="DerivedValuesViewModel"/> derives.
/// </summary>
public partial class DerivedValuesPage : ShowcasePage
{
    public DerivedValuesPage() => InitializeComponent();

    protected override void OnAttached()
    {
        var model = (DerivedValuesViewModel)DataContext!;
        var pointer = PrestoAvalonia.PointerValue(Stage, Orientation.Horizontal, relativeToBounds: true);
        var (graph, code) = Snippet.Of((
            X: pointer.Map([0, 1], [-160, 160]),
            Rotate: pointer.Map([0, 1], [-30, 30]).Spring(Springs.Bouncy),
            Opacity: pointer.Map([0, .5, 1], [.35, 1, .35]),
            Fill: pointer.Map([0, .5, 1], [Palette.Coral, Palette.Violet, Palette.Aqua]),
            Blur: pointer.VelocityOf().Map([-.004, 0, .004], [10, 0, 10])));
        Follow.SetX(Card, graph.X);
        Follow.SetRotate(Card, graph.Rotate);
        Follow.Bind(Card, OpacityProperty, graph.Opacity);
        Follow.Bind(Card, Border.BackgroundProperty, graph.Fill);
        Follow.SetBlur(Card, graph.Blur);
        pointer.Changed += value => model.Sweep.Set(value.Value);

        GraphCode.Code = $"""
            var pointer = PrestoAvalonia.PointerValue(Stage, Orientation.Horizontal, relativeToBounds: true);
            var graph = {code};
            """;
    }
}
