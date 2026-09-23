namespace Monaco.Presto.AvaloniaBridge.Text;

/// <summary>How one kind of piece — a line, a word or a character — is built by <see cref="TextSplitter"/>.</summary>
public sealed record SplitPieceParams
{
    /// <summary>A style class added to every piece of this kind, so a XAML <c>Style</c> can reach them.</summary>
    public string? Class { get; init; }

    /// <summary>Puts each piece in a clipping container of its own, so a piece animated out of its box is masked instead of overflowing.</summary>
    public bool Wrap { get; init; }
}
