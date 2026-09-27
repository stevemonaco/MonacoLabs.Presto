using Avalonia;
using Monaco.Presto.AvaloniaBridge.Targets;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// <see cref="AnimatableParams"/> with an <see cref="AvaloniaProperty"/> indexer, so typed properties
/// (<c>[Visual.OpacityProperty] = 40</c>) and named ones (<c>["X"] = 40</c>) mix in one initializer.
/// </summary>
public sealed record AvaloniaAnimatableParams : AnimatableParams
{
    /// <summary>Installs the bridge, so <c>Presto.CreateAnimatable(targets, new AvaloniaAnimatableParams { … })</c> works without a prior <c>UsePresto()</c>.</summary>
    public AvaloniaAnimatableParams() => PrestoAvalonia.EnsureInitialized();

    /// <summary>The settings for one property, named by its <see cref="AvaloniaProperty"/> instead of by string.</summary>
    public AnimatableProperty this[AvaloniaProperty property]
    {
        get => Properties[AvaloniaObjectTargetAdapter.Encode(property)];
        init => Properties[AvaloniaObjectTargetAdapter.Encode(property)] = value;
    }
}
