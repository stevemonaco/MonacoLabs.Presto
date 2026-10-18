using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using MonacoLabs.Presto.Compositor;

namespace MonacoLabs.Presto;

/// <summary>
/// Items removed from a list with <see cref="Animate.ItemGroupProperty"/> and added to a list of the same group under the same
/// top level before the removing list's next layout pass has finished: the arriving container flies from where the leaving
/// one was drawn, and the transfer is confirmed at that container's first layout pass if it is effectively visible.
/// </summary>
internal static class ItemTransfers
{
    private static readonly List<Transfer> s_pending = [];

    /// <summary>Whether <paramref name="item"/> has just arrived in <paramref name="list"/> through a transfer that has not been declined.</summary>
    public static bool IsTransferredIn(ItemsControl list, object item) => Find(item, to: list) is not null;

    /// <summary>Whether the removal of <paramref name="item"/> from <paramref name="list"/> is claimed by a confirmed transfer.</summary>
    public static bool IsClaimed(ItemsControl list, object item) => Find(item, from: list) is { Confirmed: true };

    /// <summary>Records the removals of a grouped list and matches its additions to earlier removals; the panel has not yet heard of the change.</summary>
    public static void OnItemsChanged(ItemsControl list, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move || Animate.GetItemGroup(list) is not { } group || TopLevel.GetTopLevel(list) is not { } top) return;
        if (e.OldItems is { } removed)
        {
            for (var i = 0; i < removed.Count; i++)
            {
                if (removed[i] is not { } item || list.ContainerFromIndex(e.OldStartingIndex + i) is not { } container ||
                    LayoutTransition.MeasureDrawn(container) is not { } drawn) continue;
                if (Find(from: list) is null) ItemChanges.AfterLayout(list, () => Drop(list));
                s_pending.Add(new Transfer(top, group, item, list, drawn));
            }
        }
        if (e.NewItems is not { } added) return;
        for (var i = 0; i < added.Count; i++)
        {
            foreach (var transfer in s_pending)
            {
                if (transfer.To is not null || transfer.Top != top || transfer.Group != group || !ReferenceEquals(transfer.Item, added[i])) continue;
                transfer.To = list;
                break;
            }
        }
    }

    /// <summary>Hands the transfer of <paramref name="item"/> out of <paramref name="from"/> the exit ghost its confirmation dismisses.</summary>
    internal static void Claim(ItemsControl from, object item, Presence.ExitGhost ghost)
    {
        if (Find(item, from: from) is { } transfer) transfer.Ghost = ghost;
    }

    /// <summary>Starts the flight of <paramref name="container"/>, just prepared for <paramref name="item"/>, when the item arrived through a transfer.</summary>
    public static void Arrive(ItemsControl list, Control container, object? item)
    {
        if (s_pending.Count == 0 || item is null || Find(item, to: list) is not { Flight: false } transfer) return;
        transfer.Flight = true;
        _ = new LayoutTransition(
            container, transfer.Drawn, firstOnScreen: true, LayoutMotion.From(Animate.GetItemLayout(list)!), Animate.GetLayoutMode(list), once: true,
            scoped: false)
        {
            Confirm = () => Confirm(transfer, container),
        };
    }

    private static bool Confirm(Transfer transfer, Control container)
    {
        if (!container.IsEffectivelyVisible)
        {
            s_pending.Remove(transfer);
            Presence.EnterDeclined(container);
            return false;
        }
        transfer.Confirmed = true;
        transfer.Ghost?.Cancel();
        return true;
    }

    private static Transfer? Find(object? item = null, ItemsControl? from = null, ItemsControl? to = null)
    {
        foreach (var transfer in s_pending)
        {
            if ((item is null || ReferenceEquals(transfer.Item, item)) && (from is null || transfer.From == from) && (to is null || transfer.To == to)) return transfer;
        }
        return null;
    }

    private static void Drop(ItemsControl list) => s_pending.RemoveAll(transfer => transfer.From == list);

    private sealed class Transfer(TopLevel top, string group, object item, ItemsControl from, Rect drawn)
    {
        public TopLevel Top { get; } = top;

        public string Group { get; } = group;

        public object Item { get; } = item;

        public ItemsControl From { get; } = from;

        public Rect Drawn { get; } = drawn;

        public ItemsControl? To { get; set; }

        public bool Flight { get; set; }

        public bool Confirmed { get; set; }

        public Presence.ExitGhost? Ghost { get; set; }
    }
}
