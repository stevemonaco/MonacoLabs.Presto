namespace MonacoLabs.Presto;

/// <summary>Parameters for <see cref="Presto.ScrambleText"/>: which characters the text is scrambled with, and how the reveal runs.</summary>
public sealed record ScrambleTextParams
{
    /// <summary>
    /// The characters to scramble with: literal characters, <c>-</c> ranges such as <c>"a-z"</c>, or the name of a set —
    /// <c>lowercase</c>, <c>uppercase</c>, <c>numbers</c>, <c>symbols</c>, <c>braille</c>, <c>blocks</c> or <c>shades</c>.
    /// </summary>
    /// <remarks>A <c>-</c> at either end of the string is a literal one.</remarks>
    public string Chars { get; init; } = "a-zA-Z0-9!%#_";

    /// <summary>The text the scramble settles on, when it is not the text the target started with.</summary>
    public string? Text { get; init; }

    /// <summary>Which character is revealed first, the rest following outwards from it.</summary>
    public ScrambleFrom From { get; init; }

    /// <summary>Reveals outwards from this character index instead of from <see cref="From"/>.</summary>
    public int? FromIndex { get; init; }

    /// <summary>Reveals the characters in the opposite order.</summary>
    public bool Reversed { get; init; }

    /// <summary>Offsets each character's reveal randomly by up to this fraction of <see cref="SettleDuration"/>, so the wave front breaks up.</summary>
    public double Perturbation { get; init; }

    /// <summary>The characters running ahead of the reveal, the last one leading; upstream's <c>cursor: true</c> is <c>"_"</c>.</summary>
    public string? Cursor { get; init; }

    /// <summary>Makes the scramble reproducible; omitting it draws from the shared generator.</summary>
    public int? Seed { get; init; }

    /// <summary>Scrambles the characters the reveal has not reached yet, instead of leaving the text the target started with in place.</summary>
    public bool Override { get; init; } = true;

    /// <summary>
    /// The characters that scramble the text the target started with, when they are not <see cref="Chars"/>; an empty
    /// string starts from no text at all. Ignored when <see cref="Override"/> is false.
    /// </summary>
    public string? OverrideChars { get; init; }

    /// <summary>Characters revealed per second, which sets the duration unless <see cref="Duration"/> does.</summary>
    public double RevealRate { get; init; } = 60;

    /// <summary>How often per second a scrambling character is drawn again.</summary>
    public double SettleRate { get; init; } = 30;

    /// <summary>How long one character scrambles before it settles; 300ms by default.</summary>
    public double? SettleDuration { get; init; }

    /// <summary>The duration of the whole scramble, in place of the one <see cref="RevealRate"/> derives.</summary>
    public double? Duration { get; init; }

    /// <summary>Scrambles for this long before the first character is revealed.</summary>
    public double RevealDelay { get; init; }

    /// <summary>Leaves the text the target started with in place for this long before the scramble starts.</summary>
    public double Delay { get; init; }

    /// <summary>Spreads the reveal along an ease instead of evenly.</summary>
    public EaseValue? Ease { get; init; }
}
