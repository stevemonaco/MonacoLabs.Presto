using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;

namespace PrestoGallery.Pages;

public partial class ShapesPage : ShowcasePage
{
    private Animation? _spline;
    private Animation? _morph;
    private bool _morphed;

    public ShapesPage() => InitializeComponent();

    protected override void OnAttached()
    {
        _morphed = false;
        DrawCode.Code = Snippet.Of(PrestoAvalonia.CreateDrawable(SignaturePath).Animate(new()
        {
            ["Draw"] = TweenValue.FromTo("0 0", "0 1"),
            Duration = 2200,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        })).Code;
        DrawBuilderCode.Code = Snippet.Of(Presto.Build().Draw(from: "0 0", to: "0 1").Duration(2200).Ease(Eases.InOutQuad).Loop().Alternate()).Code;

        #region snippet ShapesRider
        var motion = PrestoAvalonia.CreateMotionPath(TrackPath);
        Rider.Animate(new()
        {
            ["X"] = motion.X,
            ["Y"] = motion.Y,
            ["Rotate"] = motion.Rotate,
            Duration = 3000,
            Ease = Eases.None,
            Loop = true,
        });
        #endregion

        StartSpline();
    }

    private void StartSpline()
    {
        _spline?.Revert();
        #region snippet ShapesSpline
        Point[] points = [new(30, 110), new(110, 30), new(180, 120), new(250, 40), new(330, 120), new(390, 50)];
        var curviness = CurvinessSlider.Value;
        TrackSpline.Data = PrestoAvalonia.MotionPathGeometry(points, curviness);
        var motion = PrestoAvalonia.CreateMotionPath(points, curviness, autoRotate: 90);
        _spline = SplineRider.Animate(new()
        {
            ["X"] = motion.X,
            ["Y"] = motion.Y,
            ["Rotate"] = motion.Rotate,
            Duration = 4000,
            Ease = Eases.InOutSine,
            Loop = true,
            Alternate = true,
        });
        #endregion
    }

    private void OnCurvinessChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_spline != null) Scope.Execute(_ => StartSpline());
    }

    private void OnMorph(object? sender, RoutedEventArgs e) => Scope.Execute(_ => Morph());

    private void Morph()
    {
        // Cancel, not Revert: the next morph starts from whatever shape is on screen.
        _morph?.Cancel();
        _morphed = !_morphed;
        var key = _morphed ? "StarGeometry" : "CircleGeometry";
        var destination = (Geometry)this.FindResource(key)!;
        (_morph, var code) = Snippet.Of(MorphPath.Animate(new()
        {
            ["Data"] = PrestoAvalonia.MorphTo(destination),
            Duration = 900,
            Ease = Eases.InOutQuad,
        }));
        MorphCode.Code = $"var destination = (Geometry)this.FindResource(\"{key}\")!;\n{code}";
        MorphBuilderCode.Code = Snippet.Of(Presto.Build().Data(PrestoAvalonia.MorphTo(destination)).Duration(900).Ease(Eases.InOutQuad)).Code;
        MorphCode.IsVisible = MorphBuilderCode.IsVisible = true;
    }
}
