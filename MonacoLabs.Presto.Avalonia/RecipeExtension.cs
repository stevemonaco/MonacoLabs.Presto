using Avalonia.Data.Core;
using Avalonia.Markup.Xaml;

namespace MonacoLabs.Presto;

/// <summary>
/// The base of the recipe markup extensions, such as <c>{presto:Fade}</c>: each provides a new builder from its
/// <see cref="Recipes"/> factory per element. Set on <see cref="Presence.ExitProperty"/>, <see cref="Popover.CloseProperty"/>,
/// <see cref="PrestoPageTransition.Exit"/> or <see cref="PrestoPageTransition.BackExit"/>, or with <see cref="Out"/> set,
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
    /// Whether to provide the exit form. Unset, the exit form is provided on <see cref="Presence.ExitProperty"/>, <see cref="Popover.CloseProperty"/> and a page
    /// transition's exits only; a style <c>Setter</c> is not the property, so it needs <c>Out="True"</c>.
    /// </summary>
    public bool? Out { get; set; }

    /// <inheritdoc/>
    public sealed override object ProvideValue(IServiceProvider serviceProvider)
    {
        var target = serviceProvider?.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
        // Compiled XAML names a plain object's CLR property with an IPropertyInfo that does not know its declaring type.
        var exit = Out ?? (ReferenceEquals(target?.TargetProperty, Presence.ExitProperty) || ReferenceEquals(target?.TargetProperty, Popover.CloseProperty)
            || target is { TargetObject: PrestoPageTransition, TargetProperty: IPropertyInfo { Name: nameof(PrestoPageTransition.Exit) or nameof(PrestoPageTransition.BackExit) } });
        Configure(target);
        var builder = Build(exit, Ease is { } name ? (EaseValue?)EaseParser.Parse(name) : null);
        return Stagger is { } step ? builder.Delay(Presto.Stagger(step, new() { Start = Delay })) : builder;
    }

    /// <summary>Prepares the element the recipe is set on, when XAML names one; <paramref name="target"/> is null from code.</summary>
    protected virtual void Configure(IProvideValueTarget? target)
    {
    }

    /// <summary>Calls the recipe's factory with <see cref="Duration"/>, <see cref="Delay"/> and <paramref name="ease"/>: its exit form when <paramref name="exit"/> is set and it has one.</summary>
    protected abstract AnimationBuilder Build(bool exit, EaseValue? ease);
}
