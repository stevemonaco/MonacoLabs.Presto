using Avalonia;
using Monaco.Presto.AvaloniaBridge.Targets;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// <see cref="AnimationParams"/> with an <see cref="AvaloniaProperty"/> indexer, so typed properties
/// (<c>[Visual.OpacityProperty] = 0.5</c>) and named ones (<c>["Opacity"] = 0.5</c>) mix in one initializer.
/// </summary>
public sealed record AvaloniaAnimationParams : AnimationParams
{
    /// <summary>Installs the bridge, so <c>Presto.Animate(targets, new AvaloniaAnimationParams { … })</c> works without a prior <c>UsePresto()</c>.</summary>
    public AvaloniaAnimationParams() => PrestoAvalonia.EnsureInitialized();

    /// <summary>The values one property is animated through, named by its <see cref="AvaloniaProperty"/> instead of by string.</summary>
    public TweenValue this[AvaloniaProperty property]
    {
        get => Properties[AvaloniaObjectTargetAdapter.Encode(property)];
        init => Properties[AvaloniaObjectTargetAdapter.Encode(property)] = value;
    }
}
