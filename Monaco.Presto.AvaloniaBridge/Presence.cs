using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Transforms;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// Lifecycle animations: what plays when an element appears with its view (<see cref="InitialProperty"/>), arrives on its
/// own into a view that has loaded (<see cref="EnterProperty"/>), or leaves on its own (<see cref="ExitProperty"/>). An
/// element that leaves with its whole view, such as a page navigated away, plays nothing. Every arrival and departure is
/// classified once, by one rule, so the three never disagree about what happened.
/// </summary>
/// <remarks>
/// <para>
/// A removed control cannot be kept, since an items container may be recycled and a panel child re-added the moment it
/// leaves, so an exit plays on a ghost: a snapshot of the element shown in the window's <see cref="OverlayLayer"/>, where
/// the element was drawn and clipped as it was. The ghost is an <see cref="Image"/>, so an exit animates the transform
/// shorthands and <c>Opacity</c>, and content that was animating inside the element holds still. The transform the
/// shorthands hold moves onto the ghost; any other render transform, such as a style's pressed scale, is drawn into the
/// snapshot, which is cropped to the element's untransformed box.
/// </para>
/// <para>
/// Elements that arrive in one pass under the same panel form a batch, and each one's position in it is the index its
/// animation resolves function values and <see cref="Presto.Stagger"/> with, so <c>Delay(Presto.Stagger(40))</c> ripples
/// a list in. The total is not known while a batch is still arriving, so staggers run in arrival order.
/// </para>
/// <para>
/// <see cref="Animate.OnAttachedProperty"/> is the raw trigger beside these: it plays on every attach, unclassified.
/// </para>
/// </remarks>
public static class Presence
{
    /// <summary>
    /// Plays the builder on the element when it appears as part of a view's initial load: the window's first show, a page
    /// navigated in, the items a list is realized with during its first layout, or a view shown again.
    /// </summary>
    /// <remarks>
    /// An arrival is initial unless the element's first host that is already loaded (<see cref="Control.IsLoaded"/>) is
    /// reached through ancestors that are new and wrap only it; see <see cref="EnterProperty"/>. The animation reverts when
    /// the element detaches.
    /// </remarks>
    public static readonly AttachedProperty<AnimationBuilder?> InitialProperty =
        AvaloniaProperty.RegisterAttached<Control, AnimationBuilder?>("Initial", typeof(Presence));

    /// <summary>
    /// Plays the builder on the element when it arrives on its own into a view that has already loaded: an item added to
    /// its collection, content swapped into a <see cref="ContentControl"/>, a child added to a panel.
    /// </summary>
    /// <remarks>
    /// The element's ancestors are walked up past the ones that are not loaded yet and wrap only it, such as a fresh item
    /// container; a panel always stops the walk. The arrival enters when the ancestor that stops the walk is loaded. The
    /// animation reverts when the element detaches, after any exit has snapshotted it.
    /// </remarks>
    public static readonly AttachedProperty<AnimationBuilder?> EnterProperty =
        AvaloniaProperty.RegisterAttached<Control, AnimationBuilder?>("Enter", typeof(Presence));

    /// <summary>
    /// Plays the builder on a ghost of the element each time it leaves the visual tree on its own: its parent stays attached
    /// and has loaded, and every visual between it and the root of what was removed wraps only it. That is an item removed
    /// from a list, a panel child removed or a content swap, and not a whole page navigated away, which takes more than the
    /// element with it, nor the containers a list re-creates while a view it belongs to is loading again.
    /// </summary>
    /// <remarks>
    /// Set it on an item's template root, or on its container through an item container theme or a style. An element
    /// that attaches again while its ghost is still exiting dismisses the ghost.
    /// </remarks>
    public static readonly AttachedProperty<AnimationBuilder?> ExitProperty =
        AvaloniaProperty.RegisterAttached<Control, AnimationBuilder?>("Exit", typeof(Presence));

    private static readonly AttachedProperty<Tracker?> TrackerProperty =
        AvaloniaProperty.RegisterAttached<Control, Tracker?>("Tracker", typeof(Presence));

    // Arrivals under one panel in one pass; cleared once the pass has rendered.
    private static readonly Dictionary<Visual, List<object>> s_batches = [];

    static Presence()
    {
        InitialProperty.Changed.AddClassHandler<Control>(HandleChanged);
        EnterProperty.Changed.AddClassHandler<Control>(HandleChanged);
        ExitProperty.Changed.AddClassHandler<Control>(HandleChanged);
    }

    /// <summary>Gets the builder played on <paramref name="control"/> when it appears with its view.</summary>
    public static AnimationBuilder? GetInitial(Control control) => control.GetValue(InitialProperty);

    /// <summary>Sets the builder played on <paramref name="control"/> when it appears with its view.</summary>
    public static void SetInitial(Control control, AnimationBuilder? value) => control.SetValue(InitialProperty, value);

    /// <summary>Gets the builder played on <paramref name="control"/> when it arrives on its own.</summary>
    public static AnimationBuilder? GetEnter(Control control) => control.GetValue(EnterProperty);

    /// <summary>Sets the builder played on <paramref name="control"/> when it arrives on its own.</summary>
    public static void SetEnter(Control control, AnimationBuilder? value) => control.SetValue(EnterProperty, value);

    /// <summary>Gets the builder played on a ghost of <paramref name="control"/> when it leaves.</summary>
    public static AnimationBuilder? GetExit(Control control) => control.GetValue(ExitProperty);

    /// <summary>Sets the builder played on a ghost of <paramref name="control"/> when it leaves.</summary>
    public static void SetExit(Control control, AnimationBuilder? value) => control.SetValue(ExitProperty, value);

    /// <summary>What <paramref name="control"/>, which has just attached, arrived as.</summary>
    internal static PresenceMoment ClassifyArrival(Control control)
    {
        for (var visual = control.GetVisualParent(); visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is not Control ancestor) continue;
            if (ancestor.IsLoaded) return PresenceMoment.Enter;
            // A panel realizing its first item has one child for a moment, so only a child count outside panels means a wrapper.
            if (ancestor is Panel || ancestor.GetVisualChildren().Count() != 1) return PresenceMoment.Initial;
        }
        return PresenceMoment.Initial;
    }

    /// <summary>
    /// What <paramref name="control"/>, which has just detached from <paramref name="parent"/>, left as, and for an exit
    /// where it was, in its window's coordinates.
    /// </summary>
    internal static (PresenceMoment Moment, Point Position) ClassifyDeparture(Control control, Visual? parent)
    {
        if (parent is null || !parent.IsAttachedToVisualTree() || TopLevel.GetTopLevel(parent) is not { } top) return (PresenceMoment.Unload, default);
        // A view shown again re-creates its item containers before it has loaded, which is part of its load and not an exit.
        if (parent.FindAncestorOfType<Control>(includeSelf: true) is { IsLoaded: false }) return (PresenceMoment.Unload, default);

        // The removed subtree has not been laid out again, so its bounds still hold where it sat in the parent it was cut from.
        var position = default(Point);
        for (Visual visual = control; ; )
        {
            position += visual.Bounds.Position;
            if (visual.GetVisualParent() is not { } above) break;
            if (above.GetVisualChildren().Count() != 1) return (PresenceMoment.Unload, default);
            visual = above;
        }
        return parent.TranslatePoint(position, top) is { } onScreen ? (PresenceMoment.Exit, onScreen) : (PresenceMoment.Unload, default);
    }

    private static void HandleChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        var tracker = control.GetValue(TrackerProperty);
        if (GetInitial(control) is null && GetEnter(control) is null && GetExit(control) is null)
        {
            tracker?.Release();
            control.ClearValue(TrackerProperty);
        }
        else if (tracker is null)
        {
            control.SetValue(TrackerProperty, new Tracker(control));
        }
    }

    /// <summary>
    /// Animates <paramref name="control"/> as the next arrival of its panel's batch, which gives the animation its index and
    /// the arrivals so far as the targets function values and staggers resolve against.
    /// </summary>
    /// <remarks>The panel, not the ancestor that classified the arrival, keys the batch: an item container that has loaded classifies its own content.</remarks>
    private static Animation Arrive(Control control, AnimationBuilder builder)
    {
        PrestoAvalonia.EnsureInitialized();
        Visual key = control.FindAncestorOfType<Panel>() ?? (Visual?)TopLevel.GetTopLevel(control) ?? control;
        if (!s_batches.TryGetValue(key, out var batch))
        {
            s_batches[key] = batch = [];
            // Loaded runs after the pass has laid out and rendered, which is where a batch of arrivals ends.
            Dispatcher.UIThread.Post(() => s_batches.Remove(key), DispatcherPriority.Loaded);
        }
        batch.Add(control);
        return (Animation)new Animation(control, builder.ToParams(), null, 0, index: batch.Count - 1, allTargets: batch.ToArray()).Init();
    }

    /// <summary>Owns an element's lifecycle animations, so a departure snapshots its exit before reverting its entrance.</summary>
    private sealed class Tracker
    {
        private readonly Control _control;
        private Animation? _arrival;
        private Animation? _ghost;

        public Tracker(Control control)
        {
            _control = control;
            control.AttachedToVisualTree += HandleAttached;
            control.DetachedFromVisualTree += HandleDetached;
        }

        public void Release()
        {
            _control.AttachedToVisualTree -= HandleAttached;
            _control.DetachedFromVisualTree -= HandleDetached;
            _arrival?.Revert();
            _arrival = null;
        }

        private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // The ghost stood in for this control, which is back.
            _ghost?.Cancel();
            _ghost = null;
            _arrival?.Revert();
            _arrival = null;

            var builder = ClassifyArrival(_control) == PresenceMoment.Enter ? GetEnter(_control) : GetInitial(_control);
            if (builder is not null) _arrival = Arrive(_control, builder);
        }

        private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (GetExit(_control) is { } exit && ClassifyDeparture(_control, e.AttachmentPoint) is (PresenceMoment.Exit, var position))
            {
                _ghost = Ghost(_control, e.AttachmentPoint!, position, exit);
            }
            _arrival?.Revert();
            _arrival = null;
        }
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
