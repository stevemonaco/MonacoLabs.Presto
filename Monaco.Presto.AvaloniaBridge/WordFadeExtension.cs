namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:WordFade}</c>: <see cref="Recipes.WordFade"/>.</summary>
public sealed class WordFadeExtension() : TextRevealExtension(TextPieces.Words)
{
    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) => Recipes.WordFade(Duration, Delay, ease, LineStep, PieceStep);
}
