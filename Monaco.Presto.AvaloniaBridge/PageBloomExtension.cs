using Avalonia.Markup.Xaml;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:PageBloom}</c>: <see cref="Recipes.PageBloom"/>.</summary>
public sealed class PageBloomExtension : MarkupExtension
{
    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider) => Recipes.PageBloom();
}
