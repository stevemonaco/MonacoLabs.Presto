namespace Monaco.Presto.AvaloniaBridge.Text;

/// <summary>Where a scramble starts revealing, upstream's <c>from</c>.</summary>
public enum ScrambleFrom
{
    /// <summary>From the right when the text is shrinking, from the left otherwise.</summary>
    Auto,

    /// <summary>From the first character to the last.</summary>
    Left,

    /// <summary>From the last character back to the first.</summary>
    Right,

    /// <summary>Outwards from the middle character.</summary>
    Center,

    /// <summary>In a random order.</summary>
    Random,
}
