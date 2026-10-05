namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:LineMask}</c>: <see cref="Recipes.LineMask"/>, or <see cref="Recipes.LineMaskOut"/> as an exit.</summary>
public sealed class LineMaskExtension() : TextRevealExtension(TextPieces.Lines, mask: true)
{
    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) =>
        exit ? Recipes.LineMaskOut(Duration, Delay, ease, LineStep, PieceStep) : Recipes.LineMask(Duration, Delay, ease, LineStep, PieceStep);
}
