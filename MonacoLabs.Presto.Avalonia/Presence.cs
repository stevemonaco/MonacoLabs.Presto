using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Events;
using MonacoLabs.Presto.Targets;
using MonacoLabs.Presto.Text;
using MonacoLabs.Presto.Transforms;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto;

/// <summary>
/// Lifecycle animations: what plays when an element appears with its view (<see cref="InitialProperty"/>), arrives on its
/// own into a view that has loaded (<see cref="EnterProperty"/>), either of those (<see cref="ArriveProperty"/>), is
/// scrolled into view (<see cref="RevealProperty"/>), or leaves on its own
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
/// A host whose <see cref="ModeProperty"/> is <see cref="PresenceMode.Wait"/> holds what arrives under it at opacity 0,
/// its entrance not yet played, while a ghost of something that left it is still exiting.
/// </para>
/// <para>
/// An element's own <see cref="Visual.IsVisible"/> flip is a moment too: shown, it plays <see cref="EnterProperty"/>, or
/// <see cref="InitialProperty"/> while its view is still loading; hidden while drawn on screen, it plays
/// <see cref="ExitProperty"/> as <see cref="HideModeProperty"/> says. Only flips of the value set below
/// <see cref="BindingPriority.Animation"/> count, so Presto's own visibility writes play nothing.
/// A host with <see cref="EnterOnShowProperty"/> shown also replays the entrances of the descendants it reveals.
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
    /// Plays the builder on the element when it is scrolled into view: a row a virtualizing list realizes for an item it
    /// already had, or any other element inside a <see cref="ScrollViewer"/> crossing <see cref="RevealThresholdProperty"/>.
    /// It never plays on an initial load, an enter or a recycle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In a virtualizing list it plays in the layout pass that brings the row into view, so a row realized out of view, as
    /// a list does when it re-estimates after a change above, reveals when scrolled to. Rows shown in one pass stagger in
    /// realization order.
    /// </para>
    /// <para>
    /// Anywhere else under a <see cref="ScrollViewer"/>, a list that does not virtualize included, the element is held at
    /// opacity 0 from its attach until it crosses the threshold, one already in view crossing it on its first frame.
    /// Elements that cross in the same frame play as one batch in reading order, a frame later, so
    /// <c>Delay(Presto.Stagger(40))</c> ripples them in. It does nothing outside a <see cref="ScrollViewer"/>.
    /// </para>
    /// <para>The animation reverts when the row is recycled or the element detaches.</para>
    /// </remarks>
    public static readonly AttachedProperty<AnimationBuilder?> RevealProperty =
        AvaloniaProperty.RegisterAttached<Control, AnimationBuilder?>("Reveal", typeof(Presence));

    /// <summary>
    /// Whether <see cref="RevealProperty"/> plays only the first time the element's item is shown, so rows scrolled back
    /// over stay still. Defaults to true. Outside a virtualizing list, an element that does reveal again is held hidden
    /// once it leaves the view.
    /// </summary>
    public static readonly AttachedProperty<bool> RevealOnceProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("RevealOnce", typeof(Presence), defaultValue: true);

    /// <summary>
    /// Where an element outside a virtualizing list counts as scrolled into view, as a <see cref="Events.ScrollThreshold"/>
    /// such as <c>"bottom-=120 top"</c>; unset, its top reaching the viewport's bottom.
    /// </summary>
    public static readonly AttachedProperty<string?> RevealThresholdProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("RevealThreshold", typeof(Presence));

    /// <summary>
    /// Plays the builder on a ghost of the element each time it leaves the visual tree on its own: its parent stays attached
    /// and has loaded, and every visual between it and the root of what was removed wraps only it. That is an item removed
    /// from a list, a panel child removed or a content swap, and not a whole page navigated away, which takes more than the
    /// element with it, nor the containers a list re-creates while a view it belongs to is loading again.
    /// </summary>
    /// <remarks>
    /// Set it on an item's template root, or on its container through an item container theme or a style. An element
    /// that attaches again while its ghost is still exiting dismisses the ghost. It also plays when the element's own
    /// <see cref="Visual.IsVisible"/> turns false; see <see cref="HideModeProperty"/>.
    /// </remarks>
    public static readonly AttachedProperty<AnimationBuilder?> ExitProperty =
        AvaloniaProperty.RegisterAttached<Control, AnimationBuilder?>("Exit", typeof(Presence));

    /// <summary>
    /// Whether the list scrolls to keep the first row in view where it is when items are added or removed above it, so a
    /// live list can take inserts while it is read. At the top it does not, and new items stay in view. A scroll that
    /// lands before the list has laid the change out ends the anchoring, so the scroll is kept. Nor does it while
    /// <see cref="Scroll.StickToEndProperty"/> holds the list at its end.
    /// </summary>
    public static readonly AttachedProperty<bool> ScrollAnchorProperty =
        AvaloniaProperty.RegisterAttached<ItemsControl, bool>("ScrollAnchor", typeof(Presence));

    /// <summary>
    /// Whether arrivals under this content host or list wait for its exits to end. The nearest ancestor that sets it is
    /// the one that applies.
    /// </summary>
    public static readonly AttachedProperty<PresenceMode> ModeProperty =
        AvaloniaProperty.RegisterAttached<Control, PresenceMode>("Mode", typeof(Presence));

    /// <summary>
    /// Where <see cref="ExitProperty"/> plays when the element's own <see cref="Visual.IsVisible"/> turns false. Defaults to
    /// <see cref="PresenceHideMode.Hold"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="PresenceHideMode.Hold"/> keeps the element visible at <see cref="BindingPriority.Animation"/> while the
    /// exit plays on it, then lets the hidden value show; <see cref="Animate.ItemLayoutProperty"/> or
    /// <see cref="Animate.ContentSizeProperty"/> can close the slot afterwards. Showing it again mid-exit plays the exit back
    /// from where it stands. A held hide leaves no ghost, so it starts no <see cref="PresenceMode.Wait"/>.
    /// <see cref="PresenceHideMode.Ghost"/> plays the exit on a ghost, as a departure does, and showing the element again
    /// dismisses the ghost and plays its entrance.
    /// </remarks>
    public static readonly AttachedProperty<PresenceHideMode> HideModeProperty =
        AvaloniaProperty.RegisterAttached<Control, PresenceHideMode>("HideMode", typeof(Presence));

    /// <summary>
    /// Whether the element's own <see cref="Visual.IsVisible"/> turning true replays the entrances of the descendants it
    /// reveals, which get no notification of their own. Defaults to false.
    /// </summary>
    /// <remarks>
    /// Its visible descendants with <see cref="EnterProperty"/> or <see cref="ArriveProperty"/> play that builder as one
    /// batch in visual-tree order once the pass that shows them has laid them out, so <c>Delay(Presto.Stagger(40))</c>
    /// ripples them in. A descendant hidden itself is skipped, and one under a nested host shown in the same pass plays
    /// once. The host's own entrance is not part of the batch and starts in the same frame. Hiding the host plays only its
    /// own <see cref="ExitProperty"/>. In the Fluent templates set it, through a <c>/template/</c> style, on the part that
    /// collapses: an <see cref="Expander"/>'s <c>Border#ExpanderContent</c> or a <see cref="TreeViewItem"/>'s
    /// <c>PART_ItemsPresenter</c>.
    /// </remarks>
    public static readonly AttachedProperty<bool> EnterOnShowProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("EnterOnShow", typeof(Presence));

    private static readonly AttachedProperty<Tracker?> TrackerProperty =
        AvaloniaProperty.RegisterAttached<Control, Tracker?>("Tracker", typeof(Presence));

    private static readonly AttachedProperty<Waiting?> WaitingProperty =
        AvaloniaProperty.RegisterAttached<Control, Waiting?>("Waiting", typeof(Presence));

    // Arrivals under one panel in one pass; cleared once the pass has rendered.
    private static readonly Dictionary<Visual, List<object>> s_batches = [];

    // EnterOnShow hosts shown and waiting for the layout pass that reveals their descendants.
    private static readonly HashSet<Control> s_revealing = [];

    // Elements outside virtualizing lists that crossed their reveal threshold this frame.
    private static readonly List<Tracker> s_scrolledIn = [];

    static Presence()
    {
        InitialProperty.Changed.AddClassHandler<Control>(HandleChanged);
        EnterProperty.Changed.AddClassHandler<Control>(HandleChanged);
        ArriveProperty.Changed.AddClassHandler<Control>(HandleChanged);
        RevealProperty.Changed.AddClassHandler<Control>(HandleChanged);
        ExitProperty.Changed.AddClassHandler<Control>(HandleChanged);
        EnterOnShowProperty.Changed.AddClassHandler<Control>(HandleChanged);
        ScrollAnchorProperty.Changed.AddClassHandler<ItemsControl>((list, _) => ItemChanges.For(list));
        ModeProperty.Changed.AddClassHandler<Control>((host, _) =>
        {
            if (GetMode(host) != PresenceMode.Wait) host.GetValue(WaitingProperty)?.Release();
        });
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

    /// <summary>Gets where <paramref name="control"/> counts as scrolled into view.</summary>
    public static string? GetRevealThreshold(Control control) => control.GetValue(RevealThresholdProperty);

    /// <summary>Sets where <paramref name="control"/> counts as scrolled into view.</summary>
    public static void SetRevealThreshold(Control control, string? value) => control.SetValue(RevealThresholdProperty, value);

    /// <summary>Gets the builder played on a ghost of <paramref name="control"/> when it leaves.</summary>
    public static AnimationBuilder? GetExit(Control control) => control.GetValue(ExitProperty);

    /// <summary>Sets the builder played on a ghost of <paramref name="control"/> when it leaves.</summary>
    public static void SetExit(Control control, AnimationBuilder? value) => control.SetValue(ExitProperty, value);

    /// <summary>Gets whether <paramref name="list"/> keeps its first visible row in place when items change above it.</summary>
    public static bool GetScrollAnchor(ItemsControl list) => list.GetValue(ScrollAnchorProperty);

    /// <summary>Sets whether <paramref name="list"/> keeps its first visible row in place when items change above it.</summary>
    public static void SetScrollAnchor(ItemsControl list, bool value) => list.SetValue(ScrollAnchorProperty, value);

    /// <summary>Gets whether arrivals under <paramref name="host"/> wait for its exits to end.</summary>
    public static PresenceMode GetMode(Control host) => host.GetValue(ModeProperty);

    /// <summary>Sets whether arrivals under <paramref name="host"/> wait for its exits to end.</summary>
    public static void SetMode(Control host, PresenceMode value) => host.SetValue(ModeProperty, value);

    /// <summary>Gets where the exit of <paramref name="control"/> plays when it is hidden.</summary>
    public static PresenceHideMode GetHideMode(Control control) => control.GetValue(HideModeProperty);

    /// <summary>Sets where the exit of <paramref name="control"/> plays when it is hidden.</summary>
    public static void SetHideMode(Control control, PresenceHideMode value) => control.SetValue(HideModeProperty, value);

    /// <summary>Gets whether showing <paramref name="host"/> replays the entrances of the descendants it reveals.</summary>
    public static bool GetEnterOnShow(Control host) => host.GetValue(EnterOnShowProperty);

    /// <summary>Sets whether showing <paramref name="host"/> replays the entrances of the descendants it reveals.</summary>
    public static void SetEnterOnShow(Control host, bool value) => host.SetValue(EnterOnShowProperty, value);

    /// <summary>
    /// What <paramref name="control"/>, which has just attached, arrived as. The content of an item container enters only
    /// when its item was just added: a virtualizing list realizes and recycles containers as it scrolls, which is neither.
    /// </summary>
    internal static PresenceMoment ClassifyArrival(Control control)
    {
        var moment = LoadMoment(control);
        if (OwnerOfItem(control.GetVisualParent()) is not { } owner || ItemChanges.Of(owner) is not { } changes) return moment;
        if (moment == PresenceMoment.Initial || changes.Added.Contains(control.DataContext!)) return moment;
        if (changes.Moved.Contains(control.DataContext!)) return PresenceMoment.Move;
        return changes.Reset ? PresenceMoment.Initial : PresenceMoment.Realize;
    }

    private static PresenceMoment LoadMoment(Control control)
    {
        for (var visual = control.GetVisualParent(); visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is not Control ancestor) continue;
            if (IsPrestoPage(ancestor)) return PresenceMoment.Initial;
            if (ancestor.IsLoaded) return PresenceMoment.Enter;
            // A panel realizing its first item has one child for a moment, so only a child count outside panels means a wrapper.
            if (ancestor is Panel || ancestor.GetVisualChildren().Count() != 1) break;
        }
        return PresenceMoment.Initial;
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
            if (visual.Opacity == 0) return (PresenceMoment.Unload, default);
            position += visual.Bounds.Position;
            if (visual.GetVisualParent() is not { } above) break;
            if (above.GetVisualChildren().Count() != 1) return (PresenceMoment.Unload, default);
            visual = above;
        }
        return parent.TranslatePoint(position, top) is { } onScreen ? (PresenceMoment.Exit, onScreen) : (PresenceMoment.Unload, default);
    }

    /// <summary>
    /// The list whose item <paramref name="parent"/> hosts as content: <paramref name="parent"/> is a list's items panel, an
    /// item container, or part of one's template, such as a <see cref="ListBoxItem"/>'s presenter. Null for anything else.
    /// </summary>
    internal static ItemsControl? OwnerOfItem(Visual? parent) =>
        // Found through the panel: a container being recycled has already lost the logical parent ItemsControlFromItemContainer reads.
        (parent is Panel { IsItemsHost: true } ? parent : ItemContainerOf(parent)?.GetVisualParent())?.FindAncestorOfType<ItemsControl>();

    /// <summary>The item container <paramref name="parent"/> is, or is part of the template of; null for anything else.</summary>
    internal static Control? ItemContainerOf(Visual? parent)
    {
        if (parent is not Control host) return null;
        var container = IsItemContainer(host) ? host : host.TemplatedParent as Control;
        return container is not null && IsItemContainer(container) ? container : null;
    }

    private static Control? ModeHost(Visual? from)
    {
        for (var visual = from; visual is not null; visual = OwnerOfItem(visual) ?? visual.GetVisualParent())
        {
            if (visual is Control host && host.IsSet(ModeProperty)) return host;
        }
        return null;
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

    /// <summary>Reveals <paramref name="row"/>, which its list held from its realization until a layout pass showed it.</summary>
    internal static void RevealHeld(Control row) => row.GetValue(TrackerProperty)?.Reveal();

    private static void HandleChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (Design.IsDesignMode) return;
        var tracker = control.GetValue(TrackerProperty);
        // Leaving the logical tree withdraws style setters before the visual detach that plays the exit.
        if (tracker is not null && control.IsAttachedToVisualTree() && !((ILogical)control).IsAttachedToLogicalTree)
        {
            if (e.Property == ExitProperty && e.OldValue is AnimationBuilder exit) tracker.Withdrawn = exit;
            return;
        }
        if (IsUnused(control))
        {
            tracker?.Release();
            control.ClearValue(TrackerProperty);
        }
        else if (tracker is null)
        {
            control.SetValue(TrackerProperty, new Tracker(control));
        }
    }

    private static bool IsUnused(Control control) =>
        GetInitial(control) is null && GetEnter(control) is null && GetArrive(control) is null && GetReveal(control) is null && GetExit(control) is null &&
        !GetEnterOnShow(control);

    /// <summary>
    /// Animates <paramref name="control"/> as the next arrival of its panel's batch, which gives the animation its index and
    /// the arrivals so far as the targets function values and staggers resolve against.
    /// </summary>
    /// <remarks>
    /// The panel, not the ancestor that classified the arrival, keys the batch: an item container that has loaded classifies
    /// its own content, and an <see cref="EnterOnShowProperty"/> host keys what it reveals. A text reveal
    /// (<see cref="TextReveal"/>) plays on its pieces instead, which are its own batch.
    /// </remarks>
    private static IRevertible Arrive(Control control, AnimationBuilder builder, bool laidOut = false, Visual? batchKey = null)
    {
        PrestoAvalonia.EnsureInitialized();
        var parameters = builder.Resolve() with { HostLifetime = WhenDetached.Continue };
        using var origin = PrestoDiagnostics.Origin("Presence");
        if (PieceTargets.Play(control, parameters) is { } reveal) return reveal;
        var key = batchKey ?? control.FindAncestorOfType<Panel>() ?? (Visual?)TopLevel.GetTopLevel(control) ?? control;
        if (!s_batches.TryGetValue(key, out var batch))
        {
            s_batches[key] = batch = [];
            // Called from LayoutUpdated, AfterLayout would keep the batch open until the next pass.
            if (laidOut) Dispatcher.UIThread.Post(() => s_batches.Remove(key), DispatcherPriority.Background);
            else ItemChanges.AfterLayout((Layoutable)key, () => s_batches.Remove(key));
        }
        batch.Add(control);
        return new Animation(control, parameters, null, 0, index: batch.Count - 1, allTargets: batch.ToArray()).Init();
    }

    /// <summary>Owns an element's lifecycle animations, so a departure snapshots its exit before reverting its entrance.</summary>
    private sealed class Tracker
    {
        private readonly Control _control;
        private IRevertible? _arrival;
        private ItemChanges? _list;
        private Animation? _ghost;
        private Point? _slot;
        private bool _recording;
        private bool _baseVisible;
        // The held exit: an Animation of the element, or one of its split pieces.
        private IRevertible? _hide;
        private ScrollObserver? _watch;
        // Holds an element outside a virtualizing list hidden until it is scrolled into view.
        private IDisposable? _unseen;

        public Tracker(Control control)
        {
            _control = control;
            _baseVisible = BaseVisible(control);
            control.AttachedToVisualTree += HandleAttached;
            control.DetachedFromVisualTree += HandleDetached;
            control.PropertyChanged += HandleProperty;
        }

        /// <summary>The exit a style setter held until the element left the logical tree, on its way to the visual detach.</summary>
        public AnimationBuilder? Withdrawn { get; set; }

        public void Release()
        {
            _control.AttachedToVisualTree -= HandleAttached;
            _control.DetachedFromVisualTree -= HandleDetached;
            _control.PropertyChanged -= HandleProperty;
            CancelRevealed();
            Unwatch();
            EndHide();
            Leave();
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
            // A template binding activating on attach can set the base value the control already shows, which raises no change.
            _baseVisible = BaseVisible(_control);
            // The ghost stood in for this control, which is back.
            _ghost?.Cancel();
            _ghost = null;
            _arrival?.Revert();
            _arrival = null;

            var changes = _control.DataContext is not null && OwnerOfItem(_control.GetVisualParent()) is { } owner ? ItemChanges.For(owner) : null;
            var moment = ClassifyArrival(_control);
            // A container being recycled first hosts its template built for no item, which is not a row to reveal.
            if (moment == PresenceMoment.Realize && changes is { Jumped: false } && GetReveal(_control) is not null &&
                (!GetRevealOnce(_control) || !changes.Seen.Contains(_control.DataContext!)))
            {
                // A panel re-estimating after a change above can realize rows out of view, so the pass that shows the row reveals it.
                _list = changes;
                changes.Held.Add(_control);
            }
            else
            {
                changes?.Seen.Add(_control.DataContext!);
                var builder = moment switch
                {
                    PresenceMoment.Initial => GetInitial(_control) ?? GetArrive(_control),
                    PresenceMoment.Enter => GetEnter(_control) ?? GetArrive(_control),
                    _ => null,
                };
                if (moment is PresenceMoment.Initial or PresenceMoment.Enter && ModeHost(_control.GetVisualParent())?.GetValue(WaitingProperty) is { } waiting)
                {
                    waiting.Hold(_control, builder is null ? null : () => _arrival = Arrive(_control, builder, laidOut: true));
                }
                else if (builder is not null)
                {
                    _arrival = Arrive(_control, builder);
                }
            }
            if (GetReveal(_control) is not null && PlainScroller(_control) is { } scroller) Watch(scroller);
            if (GetExit(_control) is not null) StartRecording();
        }

        public void Reveal()
        {
            if (GetReveal(_control) is { } builder) _arrival = Arrive(_control, builder, laidOut: true);
        }

        private void Watch(ScrollViewer scroller)
        {
            _unseen = Unseen(_control);
            using var origin = PrestoDiagnostics.Origin("Presence");
            _watch = PrestoAvalonia.OnScroll(new ScrollObserverParams
            {
                Target = _control,
                Container = scroller,
                Enter = GetRevealThreshold(_control) is { } threshold ? threshold : (ScrollThreshold?)null,
                OnEnter = _ => ScrolledIn(),
                OnLeave = _ => ScrolledOut(),
            });
        }

        private void ScrolledIn()
        {
            if (s_scrolledIn.Count == 0) Utils.Sync(_ => RevealScrolledIn());
            s_scrolledIn.Add(this);
        }

        private void ScrolledOut()
        {
            s_scrolledIn.Remove(this);
            if (_unseen is not null) return;
            _arrival?.Revert();
            _arrival = null;
            _unseen = Unseen(_control);
        }

        private static void RevealScrolledIn()
        {
            var crossed = s_scrolledIn.ToArray();
            s_scrolledIn.Clear();
            var at = Array.ConvertAll(crossed, tracker => tracker._control.TranslatePoint(default, tracker.Window) ?? default);
            Array.Sort(at, crossed, Comparer<Point>.Create((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X)));
            foreach (var tracker in crossed) tracker.RevealScrolled();
            // Each crossing is its own batch, so the next one does not stagger on from this one's last index.
            foreach (var tracker in crossed) s_batches.Remove(tracker.Window);
        }

        private Visual Window => TopLevel.GetTopLevel(_control) ?? _control;

        private void RevealScrolled()
        {
            if (GetRevealOnce(_control)) Unwatch();
            _unseen?.Dispose();
            _unseen = null;
            _arrival?.Revert();
            _arrival = GetReveal(_control) is { } builder ? Arrive(_control, builder, laidOut: true, Window) : null;
        }

        private void Unwatch()
        {
            s_scrolledIn.Remove(this);
            _watch?.Revert();
            _watch = null;
            _unseen?.Dispose();
            _unseen = null;
        }

        private void Leave()
        {
            if (_list is not { } list) return;
            list.Held.Remove(_control);
            // A panel re-estimating for the scroll anchor recycles rows the reader has not seen revealed.
            if (list.Anchoring && _arrival is Animation { Completed: false } && _control.DataContext is { } item) list.Seen.Remove(item);
            _list = null;
        }

        private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            CancelRevealed();
            Unwatch();
            EndHide();
            Leave();
            var withdrawn = Withdrawn;
            Withdrawn = null;
            if (_baseVisible && (GetExit(_control) ?? withdrawn) is { } exit && ClassifyDeparture(_control, e.AttachmentPoint) is (PresenceMoment.Exit, var position))
            {
                _ghost = PlayGhost(e.AttachmentPoint!, _slot ?? position, exit);
            }
            StopRecording();
            _arrival?.Revert();
            _arrival = null;
            if (!IsUnused(_control)) return;
            Release();
            _control.ClearValue(TrackerProperty);
        }

        private Animation? PlayGhost(Visual anchor, Point position, AnimationBuilder exit)
        {
            var waiting = ModeHost(anchor) is { } host && GetMode(host) == PresenceMode.Wait ? Waiting.Start(host, anchor) : null;
            var ghost = Ghost(_control, anchor, position, exit, waiting is null ? null : waiting.Ended);
            if (ghost is null) waiting?.Ended();
            return ghost;
        }

        private void HandleProperty(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Visual.IsVisibleProperty && e.Priority != BindingPriority.Animation) Sync();
        }

        // A hold hides base flips from notifications, so the held exit's OnUpdate calls this each frame.
        private void Sync()
        {
            var visible = BaseVisible(_control);
            if (visible == _baseVisible) return;
            _baseVisible = visible;
            if (!_control.IsAttachedToVisualTree()) return;
            if (visible) Show();
            // A hide plays only when the element was drawn.
            else if (GetExit(_control) is { } exit && _control.IsLoaded && _control.Opacity != 0 && _control.GetVisualParent() is { } parent && IsOnScreen(parent)) Hide(exit);
        }

        private void Show()
        {
            if (_hide is not null)
            {
                VisibilityHold.Release(_control, this);
                HideAnimation?.Reverse();
                return;
            }
            _ghost?.Cancel();
            _ghost = null;
            _arrival?.Revert();
            _arrival = null;
            var builder = LoadMoment(_control) == PresenceMoment.Initial ? GetInitial(_control) ?? GetArrive(_control) : GetEnter(_control) ?? GetArrive(_control);
            if (builder is not null) _arrival = Arrive(_control, builder);
            if (GetEnterOnShow(_control) && s_revealing.Add(_control)) _control.LayoutUpdated += EnterRevealed;
        }

        private void EnterRevealed(object? sender, EventArgs e)
        {
            _control.LayoutUpdated -= EnterRevealed;
            if (!s_revealing.Remove(_control) || !_control.IsEffectivelyVisible) return;
            // A host shown in the same pass as one above it is part of that one's batch.
            for (var visual = _control.GetVisualParent(); visual is not null; visual = visual.GetVisualParent())
            {
                if (visual is Control host && s_revealing.Contains(host)) return;
            }
            EnterUnder(_control);
        }

        private void EnterUnder(Visual parent)
        {
            foreach (var child in parent.GetVisualChildren())
            {
                if (!child.IsVisible || child is not Control control) continue;
                var tracker = control.GetValue(TrackerProperty);
                tracker?.CancelRevealed();
                tracker?.Reenter(_control);
                EnterUnder(control);
            }
        }

        private void Reenter(Visual batchKey)
        {
            if (_hide is not null || (GetEnter(_control) ?? GetArrive(_control)) is not { } builder) return;
            _arrival?.Revert();
            _arrival = Arrive(_control, builder, laidOut: true, batchKey);
        }

        private void CancelRevealed()
        {
            if (s_revealing.Remove(_control)) _control.LayoutUpdated -= EnterRevealed;
        }

        private void Hide(AnimationBuilder exit)
        {
            VisibilityHold.Hold(_control, this, true);
            if (GetHideMode(_control) == PresenceHideMode.Ghost)
            {
                // Held for the snapshot only, since a hidden element draws nothing.
                var parent = _control.GetVisualParent()!;
                if (parent.TranslatePoint(_control.Bounds.Position, TopLevel.GetTopLevel(_control)!) is { } position) _ghost = PlayGhost(parent, position, exit);
                VisibilityHold.Release(_control, this);
                _arrival?.Revert();
                _arrival = null;
                return;
            }
            if (_hide is not null)
            {
                HideAnimation?.Play();
                return;
            }

            using var origin = PrestoDiagnostics.Origin("Presence");
            PrestoAvalonia.EnsureInitialized();
            var parameters = exit.Resolve();
            var updated = parameters.OnUpdate;
            var completed = parameters.OnComplete;
            var paused = parameters.OnPause;
            parameters = parameters with
            {
                HostLifetime = WhenDetached.Continue,
                OnUpdate = timer =>
                {
                    updated?.Invoke(timer);
                    Sync();
                },
                OnComplete = timer =>
                {
                    completed?.Invoke(timer);
                    if (timer == HideAnimation) EndHide();
                },
                OnPause = timer =>
                {
                    paused?.Invoke(timer);
                    if (timer.Canceled && timer == HideAnimation) EndHide();
                },
            };
            // A block still split by its reveal would split its emptied text again.
            if (_arrival is PieceTargets)
            {
                _arrival.Revert();
                _arrival = null;
            }
            _hide = (IRevertible?)PieceTargets.Play(_control, parameters, exit: true) ?? Presto.Animate(_control, parameters);
            // An instant exit, as PrestoDefaults.ReduceAll makes every one, completes inside Animate.
            if (HideAnimation is not { Completed: false }) EndHide();
        }

        private Animation? HideAnimation => _hide as Animation ?? (_hide as PieceTargets)?.Animation;

        private void EndHide()
        {
            if (_hide is not { } hide) return;
            _hide = null;
            VisibilityHold.Release(_control, this);
            hide.Revert();
            if (_baseVisible) return;
            _arrival?.Revert();
            _arrival = null;
        }
    }

    private static IDisposable Unseen(Control control) => control.SetValue(Visual.OpacityProperty, 0d, BindingPriority.Animation)!;

    // A virtualizing panel realizes its rows as they scroll in, and those reveal through ItemChanges instead.
    private static ScrollViewer? PlainScroller(Control control)
    {
        for (var visual = control.GetVisualParent(); visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is VirtualizingPanel) return null;
            if (visual is ScrollViewer scroller) return scroller;
        }
        return null;
    }

    private static bool BaseVisible(Visual visual) => visual.GetBaseValue(Visual.IsVisibleProperty).GetValueOrDefault(true);

    /// <summary>
    /// The exits in flight under a host in <see cref="PresenceMode.Wait"/>, and what arrived under it meanwhile, held at
    /// opacity 0 until the last exit ends. A content host's new content is held whether or not it has a lifecycle builder.
    /// </summary>
    private sealed class Waiting(Control host)
    {
        private readonly List<(Control Element, IDisposable Opacity, Action? Play)> _held = [];
        private AvaloniaObject? _content;
        private int _exits;

        public static Waiting Start(Control host, Visual from)
        {
            var waiting = host.GetValue(WaitingProperty);
            if (waiting is null)
            {
                waiting = new Waiting(host);
                host.SetValue(WaitingProperty, waiting);
            }
            waiting._exits++;
            if (waiting._content is null && from is ContentPresenter or Decorator)
            {
                waiting._content = from;
                from.PropertyChanged += waiting.HandleContent;
            }
            return waiting;
        }

        public void Ended()
        {
            if (--_exits == 0) Release();
        }

        public void Hold(Control element, Action? play)
        {
            _held.Add((element, element.SetValue(Visual.OpacityProperty, 0d, BindingPriority.Animation)!, play));
            element.DetachedFromVisualTree += Drop;
        }

        public void Release()
        {
            if (host.GetValue(WaitingProperty) == this) host.ClearValue(WaitingProperty);
            if (_content is not null) _content.PropertyChanged -= HandleContent;
            _content = null;
            var held = _held.ToArray();
            _held.Clear();
            foreach (var (element, opacity, _) in held)
            {
                element.DetachedFromVisualTree -= Drop;
                opacity.Dispose();
            }
            // Every hold is lifted first, so no entrance starts from a held opacity.
            foreach (var (_, _, play) in held) play?.Invoke();
        }

        private void HandleContent(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if ((e.Property == ContentPresenter.ChildProperty || e.Property == Decorator.ChildProperty) && e.NewValue is Control content) Hold(content, null);
        }

        private void Drop(object? sender, VisualTreeAttachmentEventArgs e)
        {
            for (var i = _held.Count - 1; i >= 0; i--)
            {
                if (_held[i].Element != sender) continue;
                _held[i].Element.DetachedFromVisualTree -= Drop;
                // Descendants detach after their root and must still see it hidden, so they are not ghosted.
                Dispatcher.UIThread.Post(_held[i].Opacity.Dispose);
                _held.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Snapshots <paramref name="element"/> into the overlay of <paramref name="anchor"/>'s window at <paramref name="position"/>,
    /// clipped to the boxes of <paramref name="anchor"/> and its ancestors that clip, and plays <paramref name="exit"/> on it.
    /// A <see cref="TextBlock"/> with <see cref="TextReveal.PiecesProperty"/> leaves as one ghost per piece, the exit's
    /// targets in reading order, unless <paramref name="exit"/> is a reduced form or there are more than <see cref="PieceGhosts.Max"/>.
    /// </summary>
    /// <returns>The exit animation, whose completion or cancellation removes the ghost; null when there is nothing to draw or nowhere to draw it.</returns>
    internal static Animation? Ghost(Control element, Visual anchor, Point position, AnimationBuilder exit, Action? dismissed = null)
    {
        var size = element.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0 || TopLevel.GetTopLevel(anchor) is not { } top || OverlayLayer.GetOverlayLayer(anchor) is not { } overlay) return null;
        if (overlay.TranslatePoint(default, top) is not { } overlayOrigin) return null;

        var parameters = exit.Resolve() with { HostLifetime = WhenDetached.Continue };
        Control ghost = null!;
        Image[] ghosts = [];
        Styled(element, anchor, () =>
        {
            if (element is TextBlock block && TextReveal.GetPieces(block) is var pieces and not TextPieces.None && !parameters.IsReducedMotion &&
                PieceGhosts.Snapshot(block, pieces, top.RenderScaling) is { } split)
            {
                (ghost, ghosts) = split;
                return;
            }
            var whole = Snapshot(element, top.RenderScaling);
            (ghost, ghosts) = (whole, [whole]);
        });

        // An item container leaves with its item, or is recycled for another, so only what is outside it clips the ghost.
        var clipping = ItemContainerOf(anchor)?.GetVisualParent() ?? anchor;
        var from = clipping.TranslatePoint(default, top) ?? default;
        var clips = Clips(clipping);
        var box = new Canvas { ClipToBounds = true, Children = { ghost } };
        // A zero-size canvas draws its children unclipped, so this host lays out the same whether or not the overlay positions it.
        var host = new Canvas { IsHitTestVisible = false, Children = { box } };
        Place(from);
        // A scroll moves the anchor and its clip by a layout pass, so the ghost follows; the handler leaves with the host.
        host.LayoutUpdated += (_, _) =>
        {
            if (clipping.TranslatePoint(default, top) is { } at) Place(at);
        };
        overlay.Children.Add(host);

        void Place(Point at)
        {
            var clip = Clip(clips, top);
            Canvas.SetLeft(ghost, position.X + at.X - from.X - clip.X);
            Canvas.SetTop(ghost, position.Y + at.Y - from.Y - clip.Y);
            box.Width = clip.Width;
            box.Height = clip.Height;
            Canvas.SetLeft(box, clip.X - overlayOrigin.X);
            Canvas.SetTop(box, clip.Y - overlayOrigin.Y);
        }

        using var origin = PrestoDiagnostics.Origin("Presence");
        PrestoAvalonia.EnsureInitialized();
        var animation = Presto.Animate(ghosts, parameters);
        var completed = animation.OnComplete;
        var paused = animation.OnPause;
        animation.OnComplete = timer =>
        {
            completed?.Invoke(timer);
            Dismiss(overlay, host, ghosts, dismissed);
        };
        animation.OnPause = timer =>
        {
            paused?.Invoke(timer);
            if (timer.Canceled) Dismiss(overlay, host, ghosts, dismissed);
        };
        // An instant exit, as PrestoDefaults.ReduceAll makes every one, completes inside Animate.
        if (animation.Completed) Dismiss(overlay, host, ghosts, dismissed);
        return animation;
    }

    /// <summary>
    /// An image of <paramref name="visual"/> cropped to its untransformed box. The transform the shorthands hold moves
    /// onto the image, so an exit animates it on from where it stands.
    /// </summary>
    internal static Image Snapshot(Visual visual, double scaling)
    {
        WriteableBitmap snapshot = null!;
        var transform = RenderTransformComposer.Untransformed(visual, () => snapshot = VisualSnapshot.Render(visual, scaling));
        if (transform is null) snapshot = VisualSnapshot.Render(visual, scaling);
        var size = VisualSnapshot.SizeOf(snapshot, scaling);
        return new Image
        {
            Source = snapshot,
            Width = size.Width,
            Height = size.Height,
            RenderTransformOrigin = visual.RenderTransformOrigin,
            RenderTransform = transform is { IsIdentity: false } matrix ? new MatrixTransform(matrix) : null,
        };
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

    private static (Visual Visual, Size Size)[] Clips(Visual anchor)
    {
        var clips = new List<(Visual, Size)>();
        for (Visual? visual = anchor; visual is not null and not TopLevel; visual = visual.GetVisualParent())
        {
            if (visual.ClipToBounds || ContentSizeTransition.Clips(visual)) clips.Add((visual, visual.Bounds.Size));
        }
        return clips.ToArray();
    }

    /// <summary>
    /// The intersection of <paramref name="clips"/> where they stand now in <paramref name="top"/>'s coordinates. Each keeps the
    /// size it had when the exit started, so a list shrinking as the element leaves does not cut off its ghost.
    /// </summary>
    private static Rect Clip((Visual Visual, Size Size)[] clips, TopLevel top)
    {
        var clip = new Rect(top.Bounds.Size);
        foreach (var (visual, size) in clips)
        {
            if (visual.TranslatePoint(default, top) is { } origin) clip = clip.Intersect(new Rect(origin, size));
        }
        return clip;
    }

    private static void Dismiss(OverlayLayer overlay, Control host, Image[] ghosts, Action? dismissed)
    {
        if (!overlay.Children.Remove(host)) return;
        foreach (var ghost in ghosts) ((Bitmap)ghost.Source!).Dispose();
        dismissed?.Invoke();
    }
}
