using Avalonia.Animation.Easings;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>An Avalonia <see cref="Easing"/> backed by a Presto <see cref="Monaco.Presto.Ease"/>.</summary>
public sealed class PrestoEasing(Ease ease) : Easing
{
    public override double Ease(double progress) => ease(progress);
}
