using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Transforms;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// Exit animations for elements the tree removes: Framer Motion's <c>AnimatePresence</c> for Avalonia. A removed control
/// cannot be kept, since an items container may be recycled and a panel child re-added the moment it leaves, so the exit
/// plays on a ghost instead: a snapshot of the element shown in the window's <see cref="OverlayLayer"/>, where the element
/// was drawn and clipped as it was.
/// </summary>
/// <remarks>
/// The ghost is an <see cref="Image"/> of the element as it last rendered, so an exit animates what an image has, the
/// transform shorthands and <c>Opacity</c>, and content that was animating inside the element holds still. The transform
/// the shorthands hold moves onto the ghost, and an exit's shorthands compose on top of it; any other render transform,
/// such as a style's pressed scale, is drawn into the snapshot, which is cropped to the element's untransformed box.
/// Entrances need nothing of their own: <see cref="Animate.OnAttachedProperty"/> plays on every attach.
/// </remarks>
public static class Presence
{
    /// <summary>
    /// Plays the builder on a ghost of the element each time it leaves the visual tree on its own: its parent stays attached,
    /// and every visual between it and the root of what was removed wraps only it. That is an item removed from a list, a
    /// panel child removed or a content swap, and not a whole page navigated away, which takes more than the element with it.
    /// </summary>
    /// <remarks>Set it on an item's template root, or on its container through an item container theme or a style.</remarks>
    public static readonly AttachedProperty<AnimationBuilder?> ExitProperty =
        AvaloniaProperty.RegisterAttached<Control, AnimationBuilder?>("Exit", typeof(Presence));

    static Presence() => ExitProperty.Changed.AddClassHandler<Control>(HandleExitChanged);

    /// <summary>Gets the builder played on a ghost of <paramref name="control"/> when it leaves.</summary>
    public static AnimationBuilder? GetExit(Control control) => control.GetValue(ExitProperty);

    /// <summary>Sets the builder played on a ghost of <paramref name="control"/> when it leaves.</summary>
    public static void SetExit(Control control, AnimationBuilder? value) => control.SetValue(ExitProperty, value);

    private static void HandleExitChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        control.DetachedFromVisualTree -= HandleDetached;
        if (e.NewValue is not null) control.DetachedFromVisualTree += HandleDetached;
    }

    private static void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var control = (Control)sender!;
        if (GetExit(control) is not { } exit || e.AttachmentPoint is not { } parent || !parent.IsAttachedToVisualTree()) return;

        // The removed subtree has not been laid out again, so its bounds still hold where it sat in the parent it was cut from.
        var position = default(Point);
        for (Visual visual = control; ; )
        {
            position += visual.Bounds.Position;
            if (visual.GetVisualParent() is not { } above) break;
            if (above.GetVisualChildren().Count() != 1) return;
            visual = above;
        }
        if (TopLevel.GetTopLevel(parent) is { } top && parent.TranslatePoint(position, top) is { } onScreen) Ghost(control, parent, onScreen, exit);
    }

    /// <summary>
    /// Snapshots <paramref name="element"/> into the overlay of <paramref name="anchor"/>'s window at <paramref name="position"/>,
    /// clipped to the boxes of <paramref name="anchor"/> and its ancestors that clip, and plays <paramref name="exit"/> on it.
    /// </summary>
    /// <returns>The exit animation, whose completion or cancellation removes the ghost; null when there is nothing to draw or nowhere to draw it.</returns>
    internal static Animation? Ghost(Control element, Visual anchor, Point position, AnimationBuilder exit)
    {
        var size = element.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0 || TopLevel.GetTopLevel(anchor) is not { } top || OverlayLayer.GetOverlayLayer(anchor) is not { } overlay) return null;
        if (overlay.TranslatePoint(default, top) is not { } overlayOrigin) return null;

        var scaling = top.RenderScaling;
        var snapshot = new RenderTargetBitmap(
            new PixelSize((int)Math.Ceiling(size.Width * scaling), (int)Math.Ceiling(size.Height * scaling)),
            new Vector(96 * scaling, 96 * scaling));
        Matrix? transform = null;
        Styled(element, anchor, () =>
        {
            transform = RenderTransformComposer.Untransformed(element, () => snapshot.Render(element));
            if (transform is null) snapshot.Render(element);
        });

        var clip = Clip(anchor, top);
        var ghost = new Image
        {
            Source = snapshot,
            Width = size.Width,
            Height = size.Height,
            RenderTransformOrigin = element.RenderTransformOrigin,
            RenderTransform = transform is { IsIdentity: false } matrix ? new MatrixTransform(matrix) : null,
        };
        Canvas.SetLeft(ghost, position.X - clip.X);
        Canvas.SetTop(ghost, position.Y - clip.Y);
        var box = new Canvas { Width = clip.Width, Height = clip.Height, ClipToBounds = true, Children = { ghost } };
        Canvas.SetLeft(box, clip.X - overlayOrigin.X);
        Canvas.SetTop(box, clip.Y - overlayOrigin.Y);
        // A zero-size canvas draws its children unclipped, so this host lays out the same whether or not the overlay positions it.
        var host = new Canvas { IsHitTestVisible = false, Children = { box } };
        overlay.Children.Add(host);

        var animation = ghost.Animate(exit);
        var completed = animation.OnComplete;
        var paused = animation.OnPause;
        animation.OnComplete = timer =>
        {
            completed?.Invoke(timer);
            Dismiss(overlay, host, snapshot);
        };
        animation.OnPause = timer =>
        {
            paused?.Invoke(timer);
            if (timer.Cancelled) Dismiss(overlay, host, snapshot);
        };
        return animation;
    }

    /// <summary>
    /// Runs <paramref name="render"/> with <paramref name="element"/> styled as it was drawn. A removed branch has already left
    /// the logical tree, and with it its styles and dynamic resources, so its logical root is lent to the nearest ancestor
    /// still in the tree for the call, its data context pinned so no binding sees another.
    /// </summary>
    private static void Styled(Control element, Visual anchor, Action render)
    {
        StyledElement root = element;
        while (root.Parent is { } parent) root = parent;
        Visual? host = anchor;
        while (host is ILogical { IsAttachedToLogicalTree: false }) host = host.GetVisualParent();
        if (((ILogical)root).IsAttachedToLogicalTree || host is null)
        {
            render();
            return;
        }

        var pinned = !root.IsSet(StyledElement.DataContextProperty);
        if (pinned) root.DataContext = root.DataContext;
        ((ISetLogicalParent)root).SetParent(host);
        try
        {
            render();
        }
        finally
        {
            ((ISetLogicalParent)root).SetParent(null);
            if (pinned) root.ClearValue(StyledElement.DataContextProperty);
        }
    }

    /// <summary>The intersection of the boxes that clip <paramref name="anchor"/>'s content, itself included, in <paramref name="top"/>'s coordinates.</summary>
    private static Rect Clip(Visual anchor, TopLevel top)
    {
        var clip = new Rect(top.Bounds.Size);
        for (Visual? visual = anchor; visual is not null and not TopLevel; visual = visual.GetVisualParent())
        {
            if (visual.ClipToBounds && visual.TranslatePoint(default, top) is { } origin) clip = clip.Intersect(new Rect(origin, visual.Bounds.Size));
        }
        return clip;
    }

    private static void Dismiss(OverlayLayer overlay, Control host, RenderTargetBitmap snapshot)
    {
        if (!overlay.Children.Remove(host)) return;
        snapshot.Dispose();
    }
}
