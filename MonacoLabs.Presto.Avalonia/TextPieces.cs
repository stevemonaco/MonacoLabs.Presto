namespace MonacoLabs.Presto;

/// <summary>Which split pieces of a <see cref="Avalonia.Controls.TextBlock"/> a trigger animates instead of the block, set through <see cref="TextReveal.PiecesProperty"/>.</summary>
public enum TextPieces
{
    /// <summary>The block itself is animated.</summary>
    None,

    /// <summary>One piece per wrapped line.</summary>
    Lines,

    /// <summary>One piece per word.</summary>
    Words,

    /// <summary>One piece per character.</summary>
    Chars,
}
