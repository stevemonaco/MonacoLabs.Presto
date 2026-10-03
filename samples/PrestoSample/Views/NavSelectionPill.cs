using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Views;

/// <summary>The nav's selection pill, which glides to each newly selected item.</summary>

internal sealed class NavSelectionPill
{
    private const double GlideDuration = 450;
    private static readonly Spring Glide = Eases.Spring(bounce: .2, duration: GlideDuration);

    private readonly TreeView _nav;
    private Border? _pill;
    private Animatable? _motion;
    private Border? _target;
    private Rect _placed;

    public NavSelectionPill(TreeView nav)
    {
        _nav = nav;
        nav.TemplateApplied += (_, e) =>
        {
            _pill = e.NameScope.Find<Border>("SelectionPill");
            _motion = _pill?.CreateAnimatable(new() { [Canvas.TopProperty] = 0, [Layoutable.HeightProperty] = 0 });
            _target = null;
        };
        nav.SelectionChanged += (_, _) =>
        {
            _target = null;
            Place(_pill is { Opacity: > 0 });
        };
        nav.LayoutUpdated += (_, _) => Place(false);
    }

    private void Place(bool glide)
    {
        glide &= !Presto.Defaults.ReducedMotion;
        if (_pill is null || _motion is null) return;
        _target ??= _nav.SelectedItem is { } item && _nav.TreeContainerFromItem(item) is { } container
            ? container.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Name == "PART_LayoutRoot")
            : null;
        if (_target is not { IsEffectivelyVisible: true } || Folded(_target) || _target.TranslatePoint(default, _pill.GetVisualParent()!) is not { } at)
        {
            _pill.Opacity = 0;
            return;
        }

        var slot = new Rect(at, _target.Bounds.Size);
        if (slot == _placed && _pill.Opacity > 0) return;
        _placed = slot;
        Canvas.SetLeft(_pill, slot.X);
        _pill.Width = slot.Width;
        if (glide)
        {
            _motion.Set("Canvas.Top", slot.Y, GlideDuration, Glide).Set("Height", slot.Height, GlideDuration, Glide);
        }
        else
        {
            // Written now so the pill never lags a frame behind; the zero-length Set keeps the animatable's start in step.
            Canvas.SetTop(_pill, slot.Y);
            _pill.Height = slot.Height;
            _motion.Set("Canvas.Top", slot.Y, 0).Set("Height", slot.Height, 0);
        }
        _pill.Opacity = 1;
    }

    // A collapsing section keeps its pages laid out under a clipped host whose height eases, so the pill hides once that clip reaches the item.
    private static bool Folded(Border target) =>
        target.GetVisualAncestors().OfType<Border>().FirstOrDefault(border => border.Name == "ItemsHost") is { } host
        && target.TranslatePoint(new Point(0, target.Bounds.Height), host) is { Y: var bottom } && bottom > host.Bounds.Height + .5;
}
