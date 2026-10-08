namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:LineWipe}</c>: <see cref="Recipes.LineWipe"/>, or <see cref="Recipes.LineWipeOut"/> as an exit.</summary>
public sealed class LineWipeExtension() : TextRevealExtension(TextPieces.Lines)
{
    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) =>
        exit ? Recipes.LineWipeOut(Duration, Delay, ease, LineStep, PieceStep) : Recipes.LineWipe(Duration, Delay, ease, LineStep, PieceStep);
}
