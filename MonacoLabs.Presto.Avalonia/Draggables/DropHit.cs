namespace MonacoLabs.Presto.Draggables;

/// <summary>How a draggable decides which drop zone it is over.</summary>
public enum DropHit
{
    /// <summary>The zone the drawn target's center lies in.</summary>
    Center,

    /// <summary>The zone the grabbing pointer lies in, kept from the release on.</summary>
    Pointer,

    /// <summary>The zone the drawn target overlaps the most.</summary>
    Overlap,
}
