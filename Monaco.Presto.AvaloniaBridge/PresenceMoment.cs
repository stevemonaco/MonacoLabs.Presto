namespace Monaco.Presto.AvaloniaBridge;

/// <summary>What an arrival or departure was, as <see cref="Presence"/> classifies it.</summary>
internal enum PresenceMoment
{
    /// <summary>Appeared as part of a view's initial load.</summary>
    Initial,

    /// <summary>Arrived on its own into a view that had loaded.</summary>
    Enter,

    /// <summary>Left on its own while its parent stayed.</summary>
    Exit,

    /// <summary>Left with its view; nothing plays.</summary>
    Unload,
}
