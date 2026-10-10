namespace MonacoLabs.Presto;

/// <summary><c>{presto:LineRise}</c>: <see cref="Recipes.LineRise"/>, or <see cref="Recipes.LineRiseOut"/> as an exit.</summary>
public sealed class LineRiseExtension() : TextRevealExtension(TextPieces.Lines)
{
    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) =>
        exit ? Recipes.LineRiseOut(Duration, Delay, ease, LineStep, PieceStep) : Recipes.LineRise(Duration, Delay, ease, LineStep, PieceStep);
}
