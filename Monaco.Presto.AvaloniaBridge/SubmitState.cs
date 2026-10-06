namespace Monaco.Presto.AvaloniaBridge;

/// <summary>What a <see cref="SubmitButton"/> shows.</summary>
public enum SubmitState
{
    /// <summary>The button at rest, showing its content.</summary>
    Idle,

    /// <summary>Collapsed to a circle around a spinner while the work runs.</summary>
    Busy,

    /// <summary>A check in the circle.</summary>
    Succeeded,

    /// <summary>A cross in the circle.</summary>
    Failed,
}
