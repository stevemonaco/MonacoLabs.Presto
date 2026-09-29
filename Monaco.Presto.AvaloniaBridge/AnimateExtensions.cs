using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.AvaloniaBridge.Targets;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// The <see cref="Presto"/> factories as extension methods on an <see cref="AvaloniaObject"/>, and the typed
/// <see cref="AvaloniaProperty"/> forms of the <see cref="Animatable"/> accessors.
/// </summary>
public static class AnimateExtensions
{
    /// <summary>Animates this object's properties, installing the bridge first when nothing else has.</summary>
    [OverloadResolutionPriority(1)]
    public static Animation Animate(this AvaloniaObject target, AvaloniaAnimationParams parameters)
    {
        PrestoAvalonia.EnsureInitialized();
        return Presto.Animate(target, parameters);
    }

    /// <summary>Animates this object with a snapshot of <paramref name="builder"/>, installing the bridge first when nothing else has.</summary>
    public static Animation Animate(this AvaloniaObject target, AnimationBuilder builder)
    {
        PrestoAvalonia.EnsureInitialized();
        return builder.Animate(target);
    }

    /// <inheritdoc cref="Animate(AvaloniaObject, AnimationBuilder)"/>
    public static Animation Animate(this AvaloniaObject target, Action<AnimationBuilder> build) => target.Animate(Presto.Build(build));

    /// <inheritdoc cref="PrestoAvalonia.AnimateOnCompositor(object?, CompositorAnimationParams)"/>
    [OverloadResolutionPriority(1)]
    public static CompositorAnimation AnimateOnCompositor(this Visual target, CompositorAnimationParams parameters) =>
        PrestoAvalonia.AnimateOnCompositor(target, parameters);

    /// <inheritdoc cref="PrestoAvalonia.AnimateOnCompositor(object?, AnimationBuilder)"/>
    public static CompositorAnimation AnimateOnCompositor(this Visual target, AnimationBuilder builder) =>
        PrestoAvalonia.AnimateOnCompositor(target, builder);

    /// <inheritdoc cref="PrestoAvalonia.AnimateOnCompositor(object?, AnimationBuilder)"/>
    public static CompositorAnimation AnimateOnCompositor(this Visual target, Action<AnimationBuilder> build) =>
        PrestoAvalonia.AnimateOnCompositor(target, build);

    /// <inheritdoc cref="PrestoAvalonia.AnimateLayout(Control, double, EaseValue?, LayoutAnimateMode)"/>
    public static LayoutTransition AnimateLayout(
        this Control target,
        double duration,
        EaseValue? ease = null,
        LayoutAnimateMode mode = LayoutAnimateMode.Position) =>
        PrestoAvalonia.AnimateLayout(target, duration, ease, mode);

    /// <inheritdoc cref="PrestoAvalonia.AnimateLayout(Control, AnimationBuilder, LayoutAnimateMode)"/>
    public static LayoutTransition AnimateLayout(this Control target, AnimationBuilder builder, LayoutAnimateMode mode = LayoutAnimateMode.Position) =>
        PrestoAvalonia.AnimateLayout(target, builder, mode);

    /// <inheritdoc cref="PrestoAvalonia.AnimateLayout(Control, AnimationBuilder, LayoutAnimateMode)"/>
    public static LayoutTransition AnimateLayout(this Control target, Action<AnimationBuilder> build, LayoutAnimateMode mode = LayoutAnimateMode.Position) =>
        PrestoAvalonia.AnimateLayout(target, build, mode);

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
