namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:LineMask}</c>: <see cref="Recipes.LineMask"/>.</summary>
public sealed class LineMaskExtension() : TextRevealExtension(TextPieces.Lines, mask: true)
{
    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) => Recipes.LineMask(Duration, Delay, ease, LineStep, PieceStep);
}
