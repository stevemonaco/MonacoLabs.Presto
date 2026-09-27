using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Pages;

public partial class ShapesPage : UserControl
{
    private readonly List<Animation> _animations = [];
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
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var animation in _animations) animation.Revert();
        _animations.Clear();
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
