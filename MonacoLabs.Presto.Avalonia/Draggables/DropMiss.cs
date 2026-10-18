namespace MonacoLabs.Presto.Draggables;

/// <summary>What a draggable does when its release settles over no drop zone.</summary>
public enum DropMiss
{
    /// <summary>The target stays where it came to rest.</summary>
    Stay,

    /// <summary>The target springs back to where it was grabbed, with the release spring.</summary>
    ReturnHome,
}
