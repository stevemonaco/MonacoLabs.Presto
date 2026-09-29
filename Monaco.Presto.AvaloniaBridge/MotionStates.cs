using Avalonia.Collections;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>An element's <see cref="MotionState"/> definitions in declaration order, later ones winning shared properties. Holds no runtime state, so a style can share one list across controls.</summary>
public sealed class MotionStates : AvaloniaList<MotionState>;
