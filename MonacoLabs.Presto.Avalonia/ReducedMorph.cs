namespace MonacoLabs.Presto;

/// <summary>What <see cref="Recipes.MorphGlyphs"/> does in place of its morph under reduced motion.</summary>
public enum ReducedMorph
{
    /// <summary>The block fades out, takes the new text and fades back in.</summary>
    Crossfade,

    /// <summary>The block takes the new text at once, with no fade, which suits a ticking counter.</summary>
    Instant,
}
