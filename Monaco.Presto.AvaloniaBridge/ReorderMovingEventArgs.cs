using Avalonia.Interactivity;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>One step of a reorder drag, raised as <see cref="Reorder.MovingEvent"/> and passed to <see cref="Reorder.CommandProperty"/>.</summary>
public sealed class ReorderMovingEventArgs : RoutedEventArgs
{
    internal ReorderMovingEventArgs(object item, int oldIndex, int newIndex) : base(Reorder.MovingEvent)
    {
        Item = item;
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    /// <summary>The item being dragged.</summary>
    public object Item { get; }

    /// <summary>The item's index before the step.</summary>
    public int OldIndex { get; }

    /// <summary>The item's index after the step, one away from <see cref="OldIndex"/>.</summary>
    public int NewIndex { get; }

    /// <summary>Set to keep the item where it is.</summary>
    public bool Cancel { get; set; }
}
