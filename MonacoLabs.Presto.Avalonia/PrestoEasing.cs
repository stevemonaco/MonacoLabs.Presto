using Avalonia.Animation.Easings;

namespace MonacoLabs.Presto;

/// <summary>An Avalonia <see cref="Easing"/> backed by a Presto <see cref="MonacoLabs.Presto.Ease"/>.</summary>
/// <remarks>The other direction needs no wrapper: an easing's own <c>Ease</c> method is already a <see cref="MonacoLabs.Presto.Ease"/>.</remarks>
public sealed class PrestoEasing(Ease ease) : Easing
{
    /// <inheritdoc/>
    public override double Ease(double progress) => ease(progress);
}
