namespace Monaco.Presto.AvaloniaBridge;

/// <summary>Where an element's exit plays when its own <c>IsVisible</c> turns false, set through <see cref="Presence.HideModeProperty"/>.</summary>
public enum PresenceHideMode
{
    /// <summary>The exit plays on the live element, which stays visible and keeps its slot in layout until the exit ends.</summary>
    Hold,

    /// <summary>Layout closes the slot at once and the exit plays on a ghost, as for an element that leaves the visual tree.</summary>
    Ghost,
}
