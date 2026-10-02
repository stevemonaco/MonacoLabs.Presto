using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// The items a list has just added, moved and removed, and whether it has just reset, kept until the layout pass that
/// realizes them has finished. A list is watched from the first lifecycle arrival in it; until that pass ends it cannot yet tell
/// an added item from a realized one, so <see cref="Of"/> answers null and the arrival is classified by the general rule.
/// It also remembers the items whose rows have been shown, for <see cref="Presence.RevealOnceProperty"/>, until they leave
/// the list, the rows realized for a reveal until a layout pass shows them, and where the list was scrolled when the last
/// layout pass ended, and it scrolls the list for <see cref="Presence.ScrollAnchorProperty"/>.
/// </summary>
internal sealed class ItemChanges
{
    private static readonly ConditionalWeakTable<ItemsControl, ItemChanges> s_lists = new();

    private readonly ItemsControl _owner;
    private bool _watching;
    private bool _clearing;
    private Vector _offset;
    private bool _scrolled;
    private object? _anchor;
    private Point _anchorAt;
    private Vector _anchorOffset;
    private int _corrections;
    private bool _correcting;

    private ItemChanges(ItemsControl owner)
    {
        _owner = owner;
        owner.Items.CollectionChanged += OnCollectionChanged;
        owner.LayoutUpdated += OnLayoutUpdated;
        Clear();
    }

    public HashSet<object> Added { get; } = [];

    public HashSet<object> Removed { get; } = [];

    public HashSet<object> Moved { get; } = [];

    public HashSet<object> Seen { get; } = [];

    /// <summary>Rows realized for a reveal and not yet in view, in realization order; the layout pass that shows one reveals it.</summary>
    public List<Control> Held { get; } = [];

    public bool Reset { get; private set; }

    /// <summary>Whether items were added, removed, moved or reset and the layout pass that realizes them has not yet finished.</summary>
    public bool Pending => Reset || Added.Count > 0 || Removed.Count > 0 || Moved.Count > 0;

    public bool Jumped => Scroller is { } scroll &&
        (Math.Abs(scroll.Offset.X - _offset.X) > scroll.Viewport.Width || Math.Abs(scroll.Offset.Y - _offset.Y) > scroll.Viewport.Height);

    /// <summary>Whether the list is scrolled away from where the last layout pass left it, read from any handler of this pass.</summary>
    public bool Scrolled => _scrolled || (Scroller?.Offset ?? default) != _offset;

    /// <summary>Whether a change above the first visible row is being scrolled back out of view, from the change until the layout pass that realizes it has finished or the reader scrolls.</summary>
    public bool Anchoring => _anchor is not null;

    private ScrollViewer? Scroller => _owner.ItemsPanelRoot?.FindAncestorOfType<ScrollViewer>();

    public static ItemChanges For(ItemsControl owner) => s_lists.GetValue(owner, static list => new ItemChanges(list));

    public static ItemChanges? Of(ItemsControl owner) => For(owner) is { _watching: true } changes ? changes : null;

    /// <summary>
    /// Runs <paramref name="action"/> once the next layout pass has finished, which is where the arrivals and departures a
    /// change causes have happened.
    /// </summary>
    /// <remarks>
    /// Not a dispatcher priority: a desktop lays out on its next render tick, so a job posted after a click at any priority
    /// below input runs before the layout pass it was meant to follow.
    /// </remarks>
    public static void AfterLayout(Layoutable target, Action action)
    {
        void Handle(object? sender, EventArgs e)
        {
            target.LayoutUpdated -= Handle;
            Dispatcher.UIThread.Post(action, DispatcherPriority.Background);
        }
        target.LayoutUpdated += Handle;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var corrected = false;
        if (_anchor is { } anchor && !_correcting)
        {
            // Any other scroll is the reader's, and correcting it would take it back.
            if (Scroller?.Offset != _anchorOffset) _anchor = null;
            // The panel can re-estimate its start again after the first correction, so a second one is allowed.
            else if (_corrections < 2) corrected = Correct(anchor);
        }
        // A correction's own pass lays the rows out where they are drawn.
        if (Held.Count > 0 && !_correcting && !corrected) RevealShown();

        var offset = Scroller?.Offset ?? default;
        if (offset == _offset) return;
        _offset = offset;
        // A container's LayoutUpdated handler may run after this one in the same pass, so the scroll is kept until the pass ends.
        _scrolled = true;
        Dispatcher.UIThread.Post(() => _scrolled = false, DispatcherPriority.Background);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Presence.GetScrollAnchor(_owner) && _anchor is null && e.Action != NotifyCollectionChangedAction.Reset) RecordAnchor(e);
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            Reset = true;
            Seen.Clear();
        }
        var moved = e.Action == NotifyCollectionChangedAction.Move;
        if (e.NewItems is { } added) foreach (var item in added) if (item is not null) (moved ? Moved : Added).Add(item);
        if (e.OldItems is { } removed && !moved)
            foreach (var item in removed)
                if (item is not null)
                {
                    Removed.Add(item);
                    Seen.Remove(item);
                }
        Clear();
    }

    private void RecordAnchor(NotifyCollectionChangedEventArgs e)
    {
        if (Scroller is not { } scroll || scroll.Offset == default || LayoutTransition.MeasureOnScreen(scroll) is not { } viewport) return;
        // The panel hears of the change after this, so containers keep their old indices and slots while Items is already new.
        var first = int.MaxValue;
        Control? anchor = null;
        var at = default(Point);
        foreach (var container in _owner.GetRealizedContainers())
        {
            var index = _owner.IndexFromContainer(container);
            if (index < 0 || index >= first || !container.IsVisible || LayoutTransition.MeasureOnScreen(container) is not { } slot || !viewport.Contains(slot.Position)) continue;
            (first, anchor, at) = (index, container, slot.Position);
        }

        var start = e.Action switch
        {
            NotifyCollectionChangedAction.Add => e.NewStartingIndex,
            NotifyCollectionChangedAction.Move => Math.Min(e.OldStartingIndex, e.NewStartingIndex),
            _ => e.OldStartingIndex,
        };
        if (anchor?.DataContext is null || start > first || (start == first && e.Action != NotifyCollectionChangedAction.Add)) return;
        _anchor = anchor.DataContext;
        _anchorAt = at;
        _anchorOffset = scroll.Offset;
        _corrections = 0;
    }

    private bool Correct(object anchor)
    {
        if (Scroller is not { } scroll) return false;
        _correcting = true;
        try
        {
            // Re-estimating after an insert before the realized range can leave the anchor unrealized; this nests layout passes.
            if (_owner.ContainerFromItem(anchor) is null) _owner.ScrollIntoView(anchor);
            if (_owner.ContainerFromItem(anchor) is not { } container || LayoutTransition.MeasureOnScreen(container) is not { } slot || slot.Position == _anchorAt) return false;
            _corrections++;
            scroll.Offset += slot.Position - _anchorAt;
            return true;
        }
        finally
        {
            _anchorOffset = scroll.Offset;
            _correcting = false;
        }
    }

    private void RevealShown()
    {
        if (Scroller is not { } scroll || LayoutTransition.MeasureOnScreen(scroll) is not { } viewport) return;
        var jumped = Jumped;
        for (var i = 0; i < Held.Count; i++)
        {
            var row = Held[i];
            var container = Presence.ItemContainerOf(row.GetVisualParent()) ?? row;
            if (!jumped && (!container.IsEffectivelyVisible || LayoutTransition.MeasureOnScreen(container) is not { } slot || !slot.Intersects(viewport))) continue;
            Held.RemoveAt(i--);
            Seen.Add(row.DataContext!);
            if (!jumped) Presence.RevealHeld(row);
        }
    }

    private void Clear()
    {
        if (_clearing) return;
        _clearing = true;
        AfterLayout(_owner, () =>
        {
            Added.Clear();
            Removed.Clear();
            Moved.Clear();
            Reset = false;
            _anchor = null;
            _clearing = false;
            _watching = true;
        });
    }
}
