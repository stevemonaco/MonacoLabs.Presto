namespace MonacoLabs.Presto.Text;

/// <summary>Parameters for <see cref="PrestoAvalonia.SplitText"/>: which kinds of piece to produce, and how each is built.</summary>
/// <remarks>A <c>null</c> kind is upstream's <c>false</c> — no pieces of that kind — so words are on and lines and characters are off by default.</remarks>
public sealed record SplitTextParams
{
    /// <summary>One piece per rendered line, so a re-split at a new width moves the text between them.</summary>
    public SplitPieceParams? Lines { get; init; }

    /// <summary>One piece per word.</summary>
    public SplitPieceParams? Words { get; init; } = new();

    /// <summary>One piece per character, nested inside the word pieces when those are on too.</summary>
    public SplitPieceParams? Chars { get; init; }

    /// <summary>Appends the space that follows a word to that word's last piece, instead of leaving it between the pieces.</summary>
    public bool IncludeSpaces { get; init; }

    /// <summary>
    /// Makes each character piece a <see cref="Avalonia.Controls.Shapes.Path"/> drawing the character's outline in the block's
    /// foreground, on the slot its text piece would have had. Requires <see cref="Chars"/>; a character with no outline,
    /// such as an emoji, stays a text piece.
    /// </summary>
    public bool Outlines { get; init; }
}
