namespace MonacoLabs.Presto;

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

    /// <summary>Realized by a list for an item it already had, such as one scrolled into view; plays Reveal.</summary>
    Realize,

    /// <summary>Taken out of a list's container for an item the list still has, such as one scrolled out of view; nothing plays.</summary>
    Recycle,

    /// <summary>Hosted again by a list for an item it has just moved; nothing plays.</summary>
    Move,
}
