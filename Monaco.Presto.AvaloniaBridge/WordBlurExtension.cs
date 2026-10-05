namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:WordBlur}</c>: <see cref="Recipes.WordBlur"/>, or <see cref="Recipes.WordBlurOut"/> as an exit.</summary>
public sealed class WordBlurExtension() : TextRevealExtension(TextPieces.Words)
{
    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) =>
        exit ? Recipes.WordBlurOut(Duration, Delay, ease, LineStep, PieceStep) : Recipes.WordBlur(Duration, Delay, ease, LineStep, PieceStep);
}
