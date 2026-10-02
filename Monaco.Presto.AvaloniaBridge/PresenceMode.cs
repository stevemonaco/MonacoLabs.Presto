namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// How a host's arrivals wait for its exits, set through <see cref="Presence.ModeProperty"/>. There is no Motion-style
/// <c>popLayout</c>: a ghost never holds layout, so <see cref="Sync"/> already behaves that way.
/// </summary>
public enum PresenceMode
{
    /// <summary>Arrivals enter while the exits play.</summary>
    Sync,

    /// <summary>Arrivals stay hidden, their entrances held back, until every exit in flight under the host has ended.</summary>
    Wait,
}
