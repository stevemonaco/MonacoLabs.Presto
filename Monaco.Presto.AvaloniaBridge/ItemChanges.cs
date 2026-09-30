using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// The items a list has just added and removed, and whether it has just reset, kept until the layout pass that realizes
/// them has finished. A list is watched from the first lifecycle arrival in it; until that pass ends it cannot yet tell
/// an added item from a realized one, so <see cref="Of"/> answers null and the arrival is classified by the general rule.
/// It also remembers the items whose rows have been shown, for <see cref="Presence.RevealOnceProperty"/>, until they leave
/// the list, and where the list was scrolled when the last layout pass ended.
/// </summary>
internal sealed class ItemChanges
{
    private static readonly ConditionalWeakTable<ItemsControl, ItemChanges> s_lists = new();

    private readonly ItemsControl _owner;
    private bool _watching;
    private bool _clearing;
    private Vector _offset;
    private bool _scrolled;

    private ItemChanges(ItemsControl owner)
    {
        _owner = owner;
        owner.Items.CollectionChanged += OnCollectionChanged;
        owner.LayoutUpdated += OnLayoutUpdated;
        Clear();
    }

    public HashSet<object> Added { get; } = [];

    public HashSet<object> Removed { get; } = [];

    public HashSet<object> Seen { get; } = [];

    public bool Reset { get; private set; }

    /// <summary>Whether items were added, removed or reset and the layout pass that realizes them has not yet finished.</summary>
    public bool Pending => Reset || Added.Count > 0 || Removed.Count > 0;

    public bool Jumped => Scroller is { } scroll &&
        (Math.Abs(scroll.Offset.X - _offset.X) > scroll.Viewport.Width || Math.Abs(scroll.Offset.Y - _offset.Y) > scroll.Viewport.Height);

    /// <summary>Whether the list is scrolled away from where the last layout pass left it, read from any handler of this pass.</summary>
    public bool Scrolled => _scrolled || (Scroller?.Offset ?? default) != _offset;

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
        var offset = Scroller?.Offset ?? default;
        if (offset == _offset) return;
        _offset = offset;
        // A container's LayoutUpdated handler may run after this one in the same pass, so the scroll is kept until the pass ends.
        _scrolled = true;
        Dispatcher.UIThread.Post(() => _scrolled = false, DispatcherPriority.Background);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            Reset = true;
            Seen.Clear();
        }
        if (e.NewItems is { } added) foreach (var item in added) if (item is not null) Added.Add(item);
        if (e.OldItems is { } removed)
            foreach (var item in removed)
                if (item is not null)
                {
                    Removed.Add(item);
                    if (e.Action != NotifyCollectionChangedAction.Move) Seen.Remove(item);
                }
        Clear();
    }

    private void Clear()
    {
        if (_clearing) return;
        _clearing = true;
        AfterLayout(_owner, () =>
        {
            Added.Clear();
            Removed.Clear();
            Reset = false;
            _clearing = false;
            _watching = true;
        });
    }
}
