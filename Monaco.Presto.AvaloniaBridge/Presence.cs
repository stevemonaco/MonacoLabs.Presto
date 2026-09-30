using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Text;
using Monaco.Presto.AvaloniaBridge.Transforms;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// Lifecycle animations: what plays when an element appears with its view (<see cref="InitialProperty"/>), arrives on its
/// own into a view that has loaded (<see cref="EnterProperty"/>), either of those (<see cref="ArriveProperty"/>), is
/// scrolled into view in a virtualizing list (<see cref="RevealProperty"/>), or leaves on its own
/// (<see cref="ExitProperty"/>). An element that leaves with its whole view, such as a page navigated away, plays nothing. Every arrival and departure is classified once, by one rule, so they never disagree about what happened.
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
/// a list in, and rows a scroll reveals together ripple the same way. The total is not known while a batch is still
/// arriving, so staggers run in arrival order.
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
    /// Plays the builder on the element on every classified arrival, initial load and enter alike. A set
    /// <see cref="InitialProperty"/> or <see cref="EnterProperty"/> wins for its own moment.
    /// </summary>
    public static readonly AttachedProperty<AnimationBuilder?> ArriveProperty =
        AvaloniaProperty.RegisterAttached<Control, AnimationBuilder?>("Arrive", typeof(Presence));

    /// <summary>
    /// Plays the builder on the element when a virtualizing list realizes it for an item the list already had, such as a
    /// row scrolled into view. It never plays on an initial load, an enter or a recycle.
    /// </summary>
    /// <remarks>
    /// It does nothing outside an item container of a list. A list that does not virtualize never realizes rows on scroll,
    /// so scroll reveals there belong to <see cref="Events.ScrollObserver"/>. The animation reverts when the row is
    /// recycled or detaches.
    /// </remarks>
    public static readonly AttachedProperty<AnimationBuilder?> RevealProperty =
        AvaloniaProperty.RegisterAttached<Control, AnimationBuilder?>("Reveal", typeof(Presence));

    /// <summary>
    /// Whether <see cref="RevealProperty"/> plays only the first time the element's item is shown, so rows scrolled back
    /// over stay still. Defaults to true.
    /// </summary>
    public static readonly AttachedProperty<bool> RevealOnceProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("RevealOnce", typeof(Presence), defaultValue: true);

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

    /// <summary>
    /// Whether the list scrolls to keep the first row in view where it is when items are added or removed above it, so a
    /// live list can take inserts while it is read. At the top it does not, and new items stay in view.
    /// </summary>
    public static readonly AttachedProperty<bool> ScrollAnchorProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, bool>("ScrollAnchor", typeof(Presence));

    private static readonly AttachedProperty<Tracker?> TrackerProperty =
        AvaloniaProperty.RegisterAttached<Control, Tracker?>("Tracker", typeof(Presence));

    // Arrivals under one panel in one pass; cleared once the pass has rendered.
    private static readonly Dictionary<Visual, List<object>> s_batches = [];

    static Presence()
    {
        InitialProperty.Changed.AddClassHandler<Control>(HandleChanged);
        EnterProperty.Changed.AddClassHandler<Control>(HandleChanged);
        ArriveProperty.Changed.AddClassHandler<Control>(HandleChanged);
        RevealProperty.Changed.AddClassHandler<Control>(HandleChanged);
        ExitProperty.Changed.AddClassHandler<Control>(HandleChanged);
        ScrollAnchorProperty.Changed.AddClassHandler<ItemsControl>((list, _) => ItemChanges.For(list));
    }

    /// <summary>Gets the builder played on <paramref name="control"/> when it appears with its view.</summary>
    public static AnimationBuilder? GetInitial(Control control) => control.GetValue(InitialProperty);

    /// <summary>Sets the builder played on <paramref name="control"/> when it appears with its view.</summary>
    public static void SetInitial(Control control, AnimationBuilder? value) => control.SetValue(InitialProperty, value);

    /// <summary>Gets the builder played on <paramref name="control"/> when it arrives on its own.</summary>
    public static AnimationBuilder? GetEnter(Control control) => control.GetValue(EnterProperty);

    /// <summary>Sets the builder played on <paramref name="control"/> when it arrives on its own.</summary>
    public static void SetEnter(Control control, AnimationBuilder? value) => control.SetValue(EnterProperty, value);

    /// <summary>Gets the builder played on <paramref name="control"/> when it appears with its view or arrives on its own.</summary>
    public static AnimationBuilder? GetArrive(Control control) => control.GetValue(ArriveProperty);

    /// <summary>Sets the builder played on <paramref name="control"/> when it appears with its view or arrives on its own.</summary>
    public static void SetArrive(Control control, AnimationBuilder? value) => control.SetValue(ArriveProperty, value);

    /// <summary>Gets the builder played on <paramref name="control"/> when a list realizes it scrolled into view.</summary>
    public static AnimationBuilder? GetReveal(Control control) => control.GetValue(RevealProperty);

    /// <summary>Sets the builder played on <paramref name="control"/> when a list realizes it scrolled into view.</summary>
    public static void SetReveal(Control control, AnimationBuilder? value) => control.SetValue(RevealProperty, value);

    /// <summary>Gets whether <paramref name="control"/> reveals only the first time its item is shown.</summary>
    public static bool GetRevealOnce(Control control) => control.GetValue(RevealOnceProperty);

    /// <summary>Sets whether <paramref name="control"/> reveals only the first time its item is shown.</summary>
    public static void SetRevealOnce(Control control, bool value) => control.SetValue(RevealOnceProperty, value);

    /// <summary>Gets the builder played on a ghost of <paramref name="control"/> when it leaves.</summary>
    public static AnimationBuilder? GetExit(Control control) => control.GetValue(ExitProperty);

    /// <summary>Sets the builder played on a ghost of <paramref name="control"/> when it leaves.</summary>
    public static void SetExit(Control control, AnimationBuilder? value) => control.SetValue(ExitProperty, value);

    /// <summary>Gets whether <paramref name="list"/> keeps its first visible row in place when items change above it.</summary>
    public static bool GetScrollAnchor(ItemsControl list) => list.GetValue(ScrollAnchorProperty);

    /// <summary>Sets whether <paramref name="list"/> keeps its first visible row in place when items change above it.</summary>
    public static void SetScrollAnchor(ItemsControl list, bool value) => list.SetValue(ScrollAnchorProperty, value);

    /// <summary>
    /// What <paramref name="control"/>, which has just attached, arrived as. The content of an item container enters only
    /// when its item was just added: a virtualizing list realizes and recycles containers as it scrolls, which is neither.
    /// </summary>
    internal static PresenceMoment ClassifyArrival(Control control)
    {
        var moment = PresenceMoment.Initial;
        for (var visual = control.GetVisualParent(); visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is not Control ancestor) continue;
            if (IsPrestoPage(ancestor)) return PresenceMoment.Initial;
            if (ancestor.IsLoaded)
            {
                moment = PresenceMoment.Enter;
                break;
            }
            // A panel realizing its first item has one child for a moment, so only a child count outside panels means a wrapper.
            if (ancestor is Panel || ancestor.GetVisualChildren().Count() != 1) break;
        }

        if (OwnerOfItem(control.GetVisualParent()) is not { } owner || ItemChanges.Of(owner) is not { } changes) return moment;
        if (moment == PresenceMoment.Initial || changes.Added.Contains(control.DataContext!)) return moment;
        return changes.Reset ? PresenceMoment.Initial : PresenceMoment.Realize;
    }

    /// <summary>
    /// What <paramref name="control"/>, which has just detached from <paramref name="parent"/>, left as, and for an exit
    /// where it was, in its window's coordinates. The content of an item container exits only when its item was just
    /// removed or its list reset.
    /// </summary>
    internal static (PresenceMoment Moment, Point Position) ClassifyDeparture(Control control, Visual? parent)
    {
        if (parent is null || !parent.IsAttachedToVisualTree() || TopLevel.GetTopLevel(parent) is not { } top) return (PresenceMoment.Unload, default);
        // A view shown again re-creates its item containers before it has loaded, which is part of its load and not an exit.
        if (parent.FindAncestorOfType<Control>(includeSelf: true) is { IsLoaded: false }) return (PresenceMoment.Unload, default);
        if (IsPrestoPage(parent) || !IsOnScreen(parent)) return (PresenceMoment.Unload, default);
        if (OwnerOfItem(parent) is { } owner && ItemChanges.Of(owner) is { } changes && !changes.Reset &&
            (control.DataContext is not { } item || !changes.Removed.Contains(item)))
        {
            return (PresenceMoment.Recycle, default);
        }

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

    /// <summary>
    /// The list whose item <paramref name="parent"/> hosts as content: <paramref name="parent"/> is an item container, or
    /// part of one's template, such as a <see cref="ListBoxItem"/>'s presenter. Null for anything else.
    /// </summary>
    internal static ItemsControl? OwnerOfItem(Visual? parent) =>
        // Found through the panel: a container being recycled has already lost the logical parent ItemsControlFromItemContainer reads.
        ItemContainerOf(parent) is { } container ? container.GetVisualParent()!.FindAncestorOfType<ItemsControl>() : null;

    /// <summary>The item container <paramref name="parent"/> is, or is part of the template of; null for anything else.</summary>
    private static Control? ItemContainerOf(Visual? parent)
    {
        if (parent is not Control host) return null;
        var container = IsItemContainer(host) ? host : host.TemplatedParent as Control;
        return container is not null && IsItemContainer(container) ? container : null;
    }

    private static bool IsItemContainer(Control control) => control.GetVisualParent() is Panel { IsItemsHost: true };

    // A page navigated in or away under a Presto transition is part of that transition, not an arrival or exit of its own.
    private static bool IsPrestoPage(Visual host) => ((host as Control)?.TemplatedParent switch
    {
        TransitioningContentControl owner => owner.PageTransition,
        NavigationPage owner => owner.PageTransition,
        TabControl owner when host is ContentPresenter => owner.PageTransition,
        _ => OwnerOfItem(host) is Carousel carousel ? carousel.PageTransition : null,
    }) is PrestoPageTransition;

    private static bool IsOnScreen(Visual parent)
    {
        if (!parent.IsEffectivelyVisible) return false;
        for (Visual? visual = parent; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual.Opacity == 0) return false;
        }
        return true;
    }

    /// <summary>The items of <paramref name="list"/> whose rows have been shown, which <see cref="RevealOnceProperty"/> does not reveal again.</summary>
    internal static IReadOnlySet<object> Seen(ItemsControl list) => ItemChanges.For(list).Seen;

    private static void HandleChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        var tracker = control.GetValue(TrackerProperty);
        if (GetInitial(control) is null && GetEnter(control) is null && GetArrive(control) is null && GetReveal(control) is null && GetExit(control) is null)
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
    /// <remarks>
    /// The panel, not the ancestor that classified the arrival, keys the batch: an item container that has loaded classifies
    /// its own content. A text reveal (<see cref="TextReveal"/>) plays on its pieces instead, which are its own batch.
    /// </remarks>
    private static IRevertible Arrive(Control control, AnimationBuilder builder)
    {
        PrestoAvalonia.EnsureInitialized();
        var parameters = builder.ToParams();
        if (PieceTargets.Play(control, parameters) is { } reveal) return reveal;
        Visual key = control.FindAncestorOfType<Panel>() ?? (Visual?)TopLevel.GetTopLevel(control) ?? control;
        if (!s_batches.TryGetValue(key, out var batch))
        {
            s_batches[key] = batch = [];
            ItemChanges.AfterLayout((Layoutable)key, () => s_batches.Remove(key));
        }
        batch.Add(control);
        return new Animation(control, parameters, null, 0, index: batch.Count - 1, allTargets: batch.ToArray()).Init();
    }

    /// <summary>Owns an element's lifecycle animations, so a departure snapshots its exit before reverting its entrance.</summary>
    private sealed class Tracker
    {
        private readonly Control _control;
        private IRevertible? _arrival;
        private Animation? _ghost;
        private Point? _slot;
        private bool _recording;

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
            StopRecording();
            _arrival?.Revert();
            _arrival = null;
        }

        /// <summary>
        /// A virtualizing panel moves the container of a removed item before its content detaches, so a row there records
        /// where it is drawn after every layout pass instead of being measured on the way out.
        /// </summary>
        private void StartRecording()
        {
            if (_recording || ItemContainerOf(_control.GetVisualParent()) is not { } container || container.GetVisualParent() is not VirtualizingPanel) return;
            _recording = true;
            _control.LayoutUpdated += RecordSlot;
            RecordSlot(null, EventArgs.Empty);
        }

        private void StopRecording()
        {
            if (!_recording) return;
            _recording = false;
            _control.LayoutUpdated -= RecordSlot;
            _slot = null;
        }

        private void RecordSlot(object? sender, EventArgs e)
        {
            if (TopLevel.GetTopLevel(_control) is { } top && _control.GetVisualParent() is { } parent) _slot = parent.TranslatePoint(_control.Bounds.Position, top);
        }

        private void HandleAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // The ghost stood in for this control, which is back.
            _ghost?.Cancel();
            _ghost = null;
            _arrival?.Revert();
            _arrival = null;

            var changes = _control.DataContext is not null && OwnerOfItem(_control.GetVisualParent()) is { } owner ? ItemChanges.For(owner) : null;
            var firstShown = changes?.Seen.Add(_control.DataContext!) == true;
            var builder = ClassifyArrival(_control) switch
            {
                PresenceMoment.Initial => GetInitial(_control) ?? GetArrive(_control),
                PresenceMoment.Enter => GetEnter(_control) ?? GetArrive(_control),
                // A container being recycled first hosts its template built for no item, which is not a row to reveal.
                PresenceMoment.Realize when changes is { Jumped: false } && (firstShown || !GetRevealOnce(_control)) => GetReveal(_control),
                _ => null,
            };
            if (builder is not null) _arrival = Arrive(_control, builder);
            if (GetExit(_control) is not null) StartRecording();
        }

        private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (GetExit(_control) is { } exit && ClassifyDeparture(_control, e.AttachmentPoint) is (PresenceMoment.Exit, var position))
            {
                _ghost = Ghost(_control, e.AttachmentPoint!, _slot ?? position, exit);
            }
            StopRecording();
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

        // An item container leaves with its item, or is recycled for another, so only what is outside it clips the ghost.
        var clip = Clip(ItemContainerOf(anchor)?.GetVisualParent() ?? anchor, top);
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
        // Inherited values such as the font come from where the branch was drawn, not from the ancestor lending it styles.
        if (root is Visual branch && branch != anchor && !branch.IsVisualAncestorOf(anchor)) ((ISetInheritanceParent)root).SetParent(anchor);
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
