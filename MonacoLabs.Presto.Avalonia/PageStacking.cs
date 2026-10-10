namespace MonacoLabs.Presto;

/// <summary>How a <see cref="PrestoPageTransition"/> stacks the two hosts while it runs.</summary>
public enum PageStacking
{
    /// <summary>The incoming host above going forward and the outgoing host above going back, as a push and a pop stack.</summary>
    IncomingAboveForward,

    /// <summary>The incoming host above in both directions.</summary>
    IncomingAbove,

    /// <summary>The outgoing host above in both directions.</summary>
    OutgoingAbove,

    /// <summary>Leaves the order to the host.</summary>
    Host,
}
