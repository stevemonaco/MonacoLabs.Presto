namespace MonacoLabs.Presto;

/// <summary><c>{presto:WordFade}</c>: <see cref="Recipes.WordFade"/>, or <see cref="Recipes.WordFadeOut"/> as an exit.</summary>
public sealed class WordFadeExtension() : TextRevealExtension(TextPieces.Words)
{
    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) =>
        exit ? Recipes.WordFadeOut(Duration, Delay, ease, LineStep, PieceStep) : Recipes.WordFade(Duration, Delay, ease, LineStep, PieceStep);
}
