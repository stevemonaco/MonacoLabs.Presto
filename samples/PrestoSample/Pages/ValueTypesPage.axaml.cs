using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Pages;

public partial class ValueTypesPage : UserControl
{
    private readonly List<Animation> _animations = [];

    public ValueTypesPage() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _animations.Add(NumberButton.Animate(Looping(new() { ["X"] = 220 })));
        _animations.Add(TurnButton.Animate(Looping(new() { ["Rotate"] = "1turn" })));
        _animations.Add(RelativeButton.Animate(Looping(new() { ["X"] = "+=140", ["Scale"] = "*=1.5" })));
        _animations.Add(FromToButton.Animate(Looping(new()
        {
            ["X"] = new double[] { 0, 220 },
            ["Opacity"] = new double[] { .2, 1 },
        })));
        _animations.Add(PercentBar.Animate(Looping(new() { ["Value"] = "75%" })));

        _animations.Add(HexButton.Animate(Looping(new() { ["Background"] = "#f5c542" })));
        _animations.Add(RgbButton.Animate(Looping(new() { ["Background"] = "rgb(74, 222, 128)" })));
        _animations.Add(HslButton.Animate(Looping(new() { ["Background"] = "hsl(280, 70%, 60%)" })));
        _animations.Add(GradientCard.Animate(Looping(new()
        {
            ["Background"] = TweenValue.Of(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#f5c542"), 0),
                    new GradientStop(Color.Parse("#f43f5e"), .5),
                    new GradientStop(Color.Parse("#8b5cf6"), 1),
                },
            }),
        })));
        _animations.Add(ElevationCard.Animate(Looping(new() { ["BoxShadow"] = "0 14 28 2 #804c8dff", ["Y"] = -6 })));
        _animations.Add(GlowCard.Animate(Looping(new() { ["ShadowY"] = 12, ["ShadowBlur"] = 24, ["ShadowColor"] = "rgba(76, 141, 255, .8)" })));
        _animations.Add(((SolidColorBrush)Avatar.Background!).Animate(
            Looping(new() { [SolidColorBrush.ColorProperty] = TweenValue.Of(Colors.OrangeRed) })));

        _animations.Add(ProfileCard.Animate(Looping(new()
        {
            ["BorderThickness"] = TweenValue.Of(new Thickness(10)),
            ["CornerRadius"] = TweenValue.Of(new CornerRadius(30)),
        })));
        _animations.Add(PointLine.Animate(Looping(new() { ["EndPoint"] = TweenValue.Of(new Point(240, 44)) })));

        var arc = (ArcSegment)((PathGeometry)ArcPath.Data!).Figures![0].Segments![0];
        _animations.Add(arc.Animate(Looping(new() { ["Size"] = TweenValue.Of(new Size(100, 40)) })));

        var rectangle = (RectangleGeometry)RectPath.Data!;
        _animations.Add(rectangle.Animate(Looping(new() { ["Rect"] = TweenValue.Of(new Rect(184, 4, 56, 44)) })));

        _animations.Add(OffsetScroller.Animate(Looping(new() { ["Offset"] = TweenValue.Of(new Vector(200, 0)) })));

        _animations.Add(Presto.Animate(DotsPanel.Children, Looping(new()
        {
            ["Y"] = new FunctionValue((t, i, n) => -10 - i * 6),
            Delay = new FunctionValue((t, i, n) => i * 80),
        })));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var animation in _animations) animation.Revert();
        _animations.Clear();
    }

    private static AvaloniaAnimationParams Looping(AvaloniaAnimationParams values) =>
        values with { Duration = 1400, Ease = Eases.InOutQuad, Loop = true, Alternate = true };
}
