using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Events;

/// <summary>
/// Writes an element's opacity, or the ends of a fading control's opacity mask, from how far a scroller's content goes past
/// its edges.
/// </summary>
internal sealed class ScrollCue
{
    private readonly Visual _element;
    private readonly Control _target;
    private readonly LinearGradientBrush? _mask;
    private ScrollContainer? _container;
    private double? _localOpacity;

    public ScrollCue(Visual element, Control target, bool fade = false)
    {
        _element = element;
        _target = target;
        if (fade)
        {
            _mask = new LinearGradientBrush
            {
                GradientStops = { new GradientStop(Colors.Black, 0), new GradientStop(Colors.Black, 0), new GradientStop(Colors.Black, 1), new GradientStop(Colors.Black, 1) },
            };
        }
        element.AttachedToVisualTree += HandleAttached;
        element.DetachedFromVisualTree += HandleDetached;
        if (element.IsAttachedToVisualTree()) HandleAttached(null, null);
    }

    /// <summary>The scroller <paramref name="control"/> is, or the one its own template holds; scrollers in its content never count.</summary>
    public static ScrollViewer? ScrollerOf(Control control)
    {
        if (control is ScrollViewer scroller) return scroller;
        foreach (var visual in control.GetVisualDescendants())
        {
            if (visual is ScrollViewer templated && templated.TemplatedParent == control) return templated;
        }
        return null;
    }

    public void Release()
    {
        _element.AttachedToVisualTree -= HandleAttached;
        _element.DetachedFromVisualTree -= HandleDetached;
        HandleDetached(null, null);
    }

    public void Update()
    {
        if (_container is not { Scroller: var scroller }) return;
        if (_mask == null)
        {
            _element.SetCurrentValue(Visual.OpacityProperty, Strength(scroller, Scroll.GetCueEdge(_element)));
            return;
        }
        var horizontal = scroller.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled
            && scroller.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
        var length = horizontal ? _target.Bounds.Width : _target.Bounds.Height;
        var ramp = length > 0 ? Math.Min(Scroll.GetFadeEdges(_target), length / 2) / length : 0;
        var stops = _mask.GradientStops;
        _mask.EndPoint = new RelativePoint(horizontal ? 1 : 0, horizontal ? 0 : 1, RelativeUnit.Relative);
        stops[0].Color = Faded(Strength(scroller, horizontal ? ScrollEdge.Left : ScrollEdge.Top));
        stops[1].Offset = ramp;
        stops[2].Offset = 1 - ramp;
        stops[3].Color = Faded(Strength(scroller, horizontal ? ScrollEdge.Right : ScrollEdge.Bottom));
        // A mask mutated in place repaints only where something under it is invalidated, and a scroller draws nothing itself.
        scroller.Presenter?.InvalidateVisual();
    }

    private double Strength(ScrollViewer scroller, ScrollEdge edge)
    {
        var offset = scroller.Offset;
        var extent = scroller.Extent;
        var viewport = scroller.Viewport;
        var past = edge switch
        {
            ScrollEdge.Top => offset.Y,
            ScrollEdge.Bottom => extent.Height - viewport.Height - offset.Y,
            ScrollEdge.Left => offset.X,
            _ => extent.Width - viewport.Width - offset.X,
        };
        return Math.Clamp(past / Scroll.GetCueRamp(_target), 0, 1);
    }

    private static Color Faded(double strength) => Color.FromArgb((byte)Math.Round(255 * (1 - strength)), 0, 0, 0);

    private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs? e)
    {
        if (_target is TemplatedControl templated) templated.TemplateApplied += HandleTemplateApplied;
        Register();
    }

    private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs? e)
    {
        if (_target is TemplatedControl templated) templated.TemplateApplied -= HandleTemplateApplied;
        Unregister();
    }

    private void HandleTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (ScrollerOf(_target) == _container?.Scroller) return;
        Unregister();
        Register();
    }

    private void Register()
    {
        if (ScrollerOf(_target) is not { } scroller) return;
        if (_mask != null)
        {
            _target.OpacityMask = _mask;
            _target.PropertyChanged += HandleTargetPropertyChanged;
        }
        else
        {
            // SetCurrentValue overwrites a local value in place, and ClearValue would drop it, so it is kept to restore.
            _localOpacity = _element.GetDiagnostic(Visual.OpacityProperty).Priority == BindingPriority.LocalValue ? _element.Opacity : null;
        }
        _container = ScrollContainer.Register(scroller, this);
        Update();
    }

    private void Unregister()
    {
        if (_container == null) return;
        if (_mask != null)
        {
            _target.PropertyChanged -= HandleTargetPropertyChanged;
            _target.ClearValue(Visual.OpacityMaskProperty);
        }
        else if (_localOpacity is { } local) _element.SetValue(Visual.OpacityProperty, local);
        else _element.ClearValue(Visual.OpacityProperty);
        _container.Remove(this);
        _container = null;
    }

    private void HandleTargetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.BoundsProperty) Update();
    }
}
