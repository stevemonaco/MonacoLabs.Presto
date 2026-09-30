namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:CharCascade}</c>: <see cref="Recipes.CharCascade"/>.</summary>
public sealed class CharCascadeExtension() : TextRevealExtension(TextPieces.Chars)
{
    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) => Recipes.CharCascade(Duration, Delay, ease, LineStep, PieceStep);
}
