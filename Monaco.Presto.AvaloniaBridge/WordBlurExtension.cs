namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:WordBlur}</c>: <see cref="Recipes.WordBlur"/>.</summary>
public sealed class WordBlurExtension() : TextRevealExtension(TextPieces.Words)
{
    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) => Recipes.WordBlur(Duration, Delay, ease, LineStep, PieceStep);
}
