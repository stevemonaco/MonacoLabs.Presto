using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Pages;

public partial class ShapesPage : UserControl
{
    private static readonly Point[] SplinePoints = [new(30, 110), new(110, 30), new(180, 120), new(250, 40), new(330, 120), new(390, 50)];

    private readonly List<Animation> _animations = [];
    private Animation? _spline;
    private Animation? _morph;
    private bool _morphed;

    public ShapesPage() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _animations.Add(PrestoAvalonia.CreateDrawable(SignaturePath).Animate(new()
        {
            ["Draw"] = TweenValue.FromTo("0 0", "0 1"),
            Duration = 2200,
            Ease = Eases.InOutQuad,
            Loop = true,
            Alternate = true,
        }));

        var motion = PrestoAvalonia.CreateMotionPath(TrackPath);
        _animations.Add(Rider.Animate(new()
        {
            ["X"] = motion.X,
            ["Y"] = motion.Y,
            ["Rotate"] = motion.Rotate,
            Duration = 3000,
            Ease = Eases.None,
            Loop = true,
        }));

        StartSpline();
    }

    private void StartSpline()
    {
        _spline?.Revert();
        var curviness = CurvinessSlider.Value;
        TrackSpline.Data = PrestoAvalonia.MotionPathGeometry(SplinePoints, curviness);
        var motion = PrestoAvalonia.CreateMotionPath(SplinePoints, curviness, autoRotate: 90);
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
    }

    private void OnCurvinessChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_spline != null) StartSpline();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var animation in _animations) animation.Revert();
        _animations.Clear();
        _spline?.Revert();
        _spline = null;
        _morph?.Revert();
        _morph = null;
    }

    private void OnMorph(object? sender, RoutedEventArgs e)
    {
        _morphed = !_morphed;
        var destination = (Geometry)this.FindResource(_morphed ? "StarGeometry" : "CircleGeometry")!;
        _morph = MorphPath.Animate(new()
        {
            ["Data"] = PrestoAvalonia.MorphTo(destination),
            Duration = 900,
            Ease = Eases.InOutQuad,
        });
    }
}
