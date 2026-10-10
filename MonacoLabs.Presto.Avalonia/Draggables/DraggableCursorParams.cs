using Avalonia.Input;

namespace MonacoLabs.Presto.Draggables;

/// <summary>
/// Upstream's <c>cursor: { onHover, onGrab }</c>: the cursors the trigger takes while the pointer is over it and while it
/// is being dragged, restored to whatever it had when the draggable is disabled or reverted.
/// </summary>
public sealed record DraggableCursorParams
{
    /// <summary>Upstream's <c>cursor: false</c>: the trigger keeps its own cursor throughout.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Hand by default, the closest Avalonia has to the CSS <c>grab</c> cursor.</summary>
    public Cursor? OnHover { get; init; }

    /// <summary>SizeAll by default, standing in for the CSS <c>grabbing</c> cursor.</summary>
    public Cursor? OnGrab { get; init; }
}
