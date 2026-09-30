namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:LineWipe}</c>: <see cref="Recipes.LineWipe"/>.</summary>
public sealed class LineWipeExtension() : TextRevealExtension(TextPieces.Lines)
{
    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) => Recipes.LineWipe(Duration, Delay, ease, LineStep, PieceStep);
}
