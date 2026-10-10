namespace MonacoLabs.Presto;

/// <summary>What an animation does once every visual it animates has left the visual tree.</summary>
public enum WhenDetached
{
    /// <summary>Keeps playing, as if the visuals were still shown.</summary>
    Continue,

    /// <summary>Pauses, leaving the engine so the visuals can be collected, and resumes from the same progress when one is attached again.</summary>
    Pause,

    /// <summary>Reverts, writing back the values the visuals held before the animation ran.</summary>
    Revert,
}
