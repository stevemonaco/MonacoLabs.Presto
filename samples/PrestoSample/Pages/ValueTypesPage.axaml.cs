using Avalonia;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

public partial class ValueTypesPage : ShowcasePage
{
    public ValueTypesPage() => InitializeComponent();

    protected override void OnAttached()
    {
        NumberCode.Code = Snippet.Of(NumberButton.Animate(new()
        {
            ["X"] = 220,
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        TurnCode.Code = Snippet.Of(TurnButton.Animate(new()
        {
            ["Rotate"] = "1turn",
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        RelativeCode.Code = Snippet.Of(RelativeButton.Animate(new()
        {
            ["X"] = "+=140",
            ["Scale"] = "*=1.5",
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        FromToCode.Code = Snippet.Of(FromToButton.Animate(new()
        {
            ["X"] = new double[] { 0, 220 },
            ["Opacity"] = new double[] { .2, 1 },
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        PercentCode.Code = Snippet.Of(PercentBar.Animate(new()
        {
            ["Value"] = "75%",
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;

        HexCode.Code = Snippet.Of(HexButton.Animate(new()
        {
            ["Background"] = "#f5c542",
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        RgbCode.Code = Snippet.Of(RgbButton.Animate(new()
        {
            ["Background"] = "rgb(74, 222, 128)",
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        HslCode.Code = Snippet.Of(HslButton.Animate(new()
        {
            ["Background"] = "hsl(280, 70%, 60%)",
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        GradientCode.Code = Snippet.Of(GradientCard.Animate(new()
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
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        ElevationCode.Code = Snippet.Of(ElevationCard.Animate(new()
        {
            ["BoxShadow"] = "0 14 28 2 #804c8dff",
            ["Y"] = -6,
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        GlowCode.Code = Snippet.Of(GlowCard.Animate(new()
        {
            ["ShadowY"] = 12,
            ["ShadowBlur"] = 24,
            ["ShadowColor"] = "rgba(76, 141, 255, .8)",
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        AvatarCode.Code = Snippet.Of(((SolidColorBrush)Avatar.Background!).Animate(new()
        {
            [SolidColorBrush.ColorProperty] = TweenValue.Of(Colors.OrangeRed),
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;

        ProfileCode.Code = Snippet.Of(ProfileCard.Animate(new()
        {
            ["BorderThickness"] = TweenValue.Of(new Thickness(10)),
            ["CornerRadius"] = TweenValue.Of(new CornerRadius(30)),
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        PointCode.Code = Snippet.Of(PointLine.Animate(new()
        {
            ["EndPoint"] = TweenValue.Of(new Point(240, 44)),
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;

        #region snippet ValueTypesArc
        var arc = (ArcSegment)((PathGeometry)ArcPath.Data!).Figures![0].Segments![0];
        arc.Animate(new()
        {
            ["Size"] = TweenValue.Of(new Size(100, 40)),
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        });
        #endregion

        #region snippet ValueTypesRect
        var rectangle = (RectangleGeometry)RectPath.Data!;
        rectangle.Animate(new()
        {
            ["Rect"] = TweenValue.Of(new Rect(184, 4, 56, 44)),
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        });
        #endregion

        OffsetCode.Code = Snippet.Of(OffsetScroller.Animate(new()
        {
            ["Offset"] = TweenValue.Of(new Vector(200, 0)),
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;

        DotsCode.Code = Snippet.Of(Presto.Animate(DotsPanel.Children, new()
        {
            ["Y"] = new FunctionValue((t, i, n) => -10 - i * 6),
            Delay = new FunctionValue((t, i, n) => i * 80),
            Duration = 1400,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
    }
}
