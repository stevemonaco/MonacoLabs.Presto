namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:CharCascade}</c>: <see cref="Recipes.CharCascade"/>, or <see cref="Recipes.CharCascadeOut"/> as an exit.</summary>
public sealed class CharCascadeExtension() : TextRevealExtension(TextPieces.Chars)
{
    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) =>
        exit ? Recipes.CharCascadeOut(Duration, Delay, ease, LineStep, PieceStep) : Recipes.CharCascade(Duration, Delay, ease, LineStep, PieceStep);
}
