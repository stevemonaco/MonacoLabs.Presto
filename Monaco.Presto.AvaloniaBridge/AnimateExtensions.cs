using Avalonia;
using Monaco.Presto.AvaloniaBridge.Targets;

namespace Monaco.Presto.AvaloniaBridge;

public static class AnimateExtensions
{
    /// <summary>Animates this object's properties, installing the bridge first when nothing else has.</summary>
    public static Animation Animate(this AvaloniaObject target, AvaloniaAnimationParams parameters)
    {
        PrestoAvalonia.EnsureInitialized();
        return Presto.Animate(target, parameters);
    }

    /// <summary>Creates an animatable over this object's properties, installing the bridge first when nothing else has.</summary>
    public static Animatable CreateAnimatable(this AvaloniaObject target, AvaloniaAnimatableParams parameters)
    {
        PrestoAvalonia.EnsureInitialized();
        return Presto.CreateAnimatable(target, parameters);
    }

    /// <inheritdoc cref="Animatable.Set(string, double, double?, EaseValue?)"/>
    /// <remarks>The typed form, for a property registered through the <see cref="AvaloniaProperty"/> indexer.</remarks>
    public static Animatable Set(this Animatable animatable, AvaloniaProperty property, double to, double? duration = null, EaseValue? ease = null) =>
        animatable.Set(AvaloniaObjectTargetAdapter.Encode(property), to, duration, ease);

    /// <inheritdoc cref="Animatable.Set(string, IReadOnlyList{double}, double?, EaseValue?)"/>
    /// <remarks>The typed form, for a property registered through the <see cref="AvaloniaProperty"/> indexer.</remarks>
    public static Animatable Set(this Animatable animatable, AvaloniaProperty property, IReadOnlyList<double> to, double? duration = null, EaseValue? ease = null) =>
        animatable.Set(AvaloniaObjectTargetAdapter.Encode(property), to, duration, ease);

    /// <inheritdoc cref="Animatable.Get"/>
    /// <remarks>The typed form, for a property registered through the <see cref="AvaloniaProperty"/> indexer.</remarks>
    public static double Get(this Animatable animatable, AvaloniaProperty property) =>
        animatable.Get(AvaloniaObjectTargetAdapter.Encode(property));

    /// <inheritdoc cref="Animatable.GetChannels"/>
    /// <remarks>The typed form, for a property registered through the <see cref="AvaloniaProperty"/> indexer.</remarks>
    public static double[] GetChannels(this Animatable animatable, AvaloniaProperty property) =>
        animatable.GetChannels(AvaloniaObjectTargetAdapter.Encode(property));
}
