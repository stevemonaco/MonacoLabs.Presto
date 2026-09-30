using Avalonia.Markup.Xaml;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:PageSharedAxis Axis=X}</c>: <see cref="Recipes.PageSharedAxis"/>.</summary>
public sealed class PageSharedAxisExtension : MarkupExtension
{
    /// <summary>The axis the pages move along.</summary>
    public PageAxis Axis { get; set; }

    /// <summary>How far the pages travel in DIPs, or <see cref="MotionTokens.Distance"/> when unset.</summary>
    public double? Distance { get; set; }

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider) => Recipes.PageSharedAxis(Axis, Distance);
}