namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// How a host's arrivals wait for its exits, set through <see cref="Presence.ModeProperty"/>. <see cref="Sync"/> and
/// <see cref="PopLayout"/> currently behave the same, because a ghost never holds layout.
/// </summary>
public enum PresenceMode
{
    /// <summary>Arrivals enter while the exits play.</summary>
    Sync,

    /// <summary>Arrivals stay hidden, their entrances held back, until every exit in flight under the host has ended.</summary>
    Wait,

    /// <summary>Exits leave layout at once while arrivals enter, which is what a ghost already does.</summary>
    PopLayout,
}
