namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:LineRise}</c>: <see cref="Recipes.LineRise"/>.</summary>
public sealed class LineRiseExtension() : TextRevealExtension(TextPieces.Lines)
{
    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) => Recipes.LineRise(Duration, Delay, ease, LineStep, PieceStep);
}
