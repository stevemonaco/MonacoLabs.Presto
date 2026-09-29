using Avalonia.Markup.Xaml;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// The base of the recipe markup extensions, such as <c>{presto:Fade}</c>: each provides a new builder from its
/// <see cref="Recipes"/> factory per element. Set on <see cref="Presence.ExitProperty"/>, or with <see cref="Out"/> set,
/// a recipe that has an exit form provides that instead.
/// </summary>
public abstract class RecipeExtension : MarkupExtension
{
    /// <summary>The duration in ms, or the recipe's token default when unset.</summary>
    public double? Duration { get; set; }

    /// <summary>The delay in ms before it plays, or before the first element of a batch plays with <see cref="Stagger"/>.</summary>
    public double? Delay { get; set; }

    /// <summary>An ease in the <see cref="EaseParser"/> grammar, such as <c>"outBack"</c>, or the recipe's token default when unset.</summary>
    public string? Ease { get; set; }

    /// <summary>The ms each element of a batch starts after the one before.</summary>
    public double? Stagger { get; set; }

    /// <summary>
    /// Whether to provide the exit form. Unset, the exit form is provided on <see cref="Presence.ExitProperty"/> only; a
    /// style <c>Setter</c> is not the property, so it needs <c>Out="True"</c>.
    /// </summary>
    public bool? Out { get; set; }

    /// <inheritdoc/>
    public sealed override object ProvideValue(IServiceProvider serviceProvider)
    {
        var target = serviceProvider?.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
        var exit = Out ?? ReferenceEquals(target?.TargetProperty, Presence.ExitProperty);
        var builder = Build(exit, Ease is { } name ? (EaseValue?)EaseParser.Parse(name) : null);
        return Stagger is { } step ? builder.Delay(Presto.Stagger(step, new() { Start = Delay })) : builder;
    }

    /// <summary>Calls the recipe's factory with <see cref="Duration"/>, <see cref="Delay"/> and <paramref name="ease"/>: its exit form when <paramref name="exit"/> is set and it has one.</summary>
    protected abstract AnimationBuilder Build(bool exit, EaseValue? ease);
}
