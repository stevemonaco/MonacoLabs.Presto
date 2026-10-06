using Avalonia;
using Avalonia.Controls;

namespace Monaco.Presto.AvaloniaBridge.Draggables;

/// <summary>Parameters for <see cref="PrestoAvalonia.CreateDraggable"/>.</summary>
public sealed record DraggableParams
{
    /// <summary>The control the pointer grabs, when it is not the dragged control itself.</summary>
    public Control? Trigger { get; init; }

    /// <summary>The horizontal axis: the property it drags, and how that value is snapped, modified and composed.</summary>
    public DraggableAxisParams? X { get; init; }

    /// <summary>The vertical axis, with the same settings.</summary>
    public DraggableAxisParams? Y { get; init; }

    /// <summary>Keeps the target inside this control, which has to be one of its visual ancestors.</summary>
    public Control? Container { get; init; }

    /// <summary>
    /// The range <see cref="Draggable.X"/> and <see cref="Draggable.Y"/> are kept inside, read again on every
    /// <see cref="Draggable.Refresh"/>; a fixed range is <c>_ => bounds</c>. Takes precedence over <see cref="Container"/>.
    /// </summary>
    public Func<Draggable, Rect>? ContainerBounds { get; init; }

    /// <summary>Inset of the container's bounds.</summary>
    public Thickness? ContainerPadding { get; init; }

    /// <summary>How much of a drag past the container's bounds is swallowed, .8 by default; 1 pins the target to the edge and 0 lets it drag straight through.</summary>
    public double? ContainerFriction { get; init; }

    /// <summary>The same for the throw a release starts, defaulting to <see cref="ContainerFriction"/>.</summary>
    public double? ReleaseContainerFriction { get; init; }

    /// <summary>
    /// Lets Escape end a drag in progress and spring the target back to where it was grabbed, false by default.
    /// <see cref="OnRelease"/> and <see cref="OnSettle"/> then run as they do for a release.
    /// </summary>
    public bool CancelOnEscape { get; init; }

    /// <summary>How far the pointer has to travel before the drag starts, 3 by default.</summary>
    public double? DragThreshold { get; init; }

    /// <summary>The ease the release animation settles on, <c>Eases.OutQuint</c> by default; a <see cref="Spring"/> here replaces the release springs below.</summary>
    public EaseValue? ReleaseEase { get; init; }

    /// <summary>Mass, stiffness and damping of the spring the release is thrown with: 1, 80 and 20 by default. Ignored when <see cref="ReleaseEase"/> is a spring.</summary>
    public double? ReleaseMass { get; init; }

    /// <inheritdoc cref="ReleaseMass"/>
    public double? ReleaseStiffness { get; init; }

    /// <inheritdoc cref="ReleaseMass"/>
    public double? ReleaseDamping { get; init; }

    /// <summary>
    /// Replaces the thrown release with this inertia on each axis, launched at the pointer's velocity, bounced off the
    /// container bounds and snapped by <see cref="Snap"/> unless it sets its own <see cref="Inertia.ModifyTarget"/>.
    /// Its own velocity and bounds are ignored, and so are <see cref="ReleaseEase"/>, the release springs and <see cref="ReleaseContainerFriction"/>.
    /// </summary>
    public Inertia? ReleaseInertia { get; init; }

    /// <summary>Floor and ceiling of the pointer speed the release throw is scaled by, 0 and 50 by default.</summary>
    public double? MinVelocity { get; init; }

    /// <inheritdoc cref="MinVelocity"/>
    public double? MaxVelocity { get; init; }

    /// <summary>Scales the measured pointer speed, 1 by default.</summary>
    public double? VelocityMultiplier { get; init; }

    /// <summary>How fast the nearest <see cref="ScrollViewer"/> scrolls while the pointer sits in the edge band, 1.5 by default; 0 turns auto-scroll off.</summary>
    public double? ScrollSpeed { get; init; }

    /// <summary>Width of that edge band, 20 by default.</summary>
    public double? ScrollThreshold { get; init; }

    /// <summary>The trigger's hover and drag cursors; <c>new() { Enabled = false }</c> leaves the cursor alone.</summary>
    public DraggableCursorParams? Cursor { get; init; }

    /// <summary>Snaps both axes on release, and reports the steps crossed while dragging through <see cref="OnSnap"/>: <c>Utils.Snap(50)</c>, or <c>Utils.Snap([0, 100, 250])</c> for fixed stops.</summary>
    public Func<double, double>? Snap { get; init; }

    /// <summary>Applies to both axes, unless the axis brings a <see cref="DraggableAxisParams.Modifier"/> of its own.</summary>
    public Func<double, double>? Modifier { get; init; }

    /// <summary>Runs when the pointer takes hold of the trigger, before it has moved.</summary>
    public Action<Draggable>? OnGrab { get; init; }

    /// <summary>Runs on the first movement past <see cref="DragThreshold"/>, where the drag itself begins.</summary>
    public Action<Draggable>? OnDrag { get; init; }

    /// <summary>Runs on every frame the target actually moves on.</summary>
    public Action<Draggable>? OnUpdate { get; init; }

    /// <summary>Runs when the pointer lets go, once the release animations exist so they can still be canceled.</summary>
    public Action<Draggable>? OnRelease { get; init; }

    /// <summary>Runs once the axis animations have nothing left to play.</summary>
    public Action<Draggable>? OnSettle { get; init; }

    /// <summary>Runs whenever the snapped step changes, while dragging and again on release.</summary>
    public Action<Draggable>? OnSnap { get; init; }

    /// <summary>Runs before the bounds are measured again, after the target or the container was resized.</summary>
    public Action<Draggable>? OnResize { get; init; }

    /// <summary>Runs once the bounds have been measured again, where <see cref="OnResize"/> runs before.</summary>
    public Action<Draggable>? OnAfterResize { get; init; }
}
