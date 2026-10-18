using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MonacoLabs.Presto;

/// <summary>
/// <c>{presto:ScrollToItem}</c>: a <see cref="CueAction"/> that calls <see cref="PrestoAvalonia.ScrollToIndex(ItemsControl, int, ScrollAlignment, AnimationBuilder?)"/>
/// on its <see cref="ItemsControl"/> with the value's index in <see cref="ItemsControl.Items"/>, for each value except a first watched one.
/// </summary>
public sealed class ScrollToItemExtension : MarkupExtension
{
    /// <summary>Where the item lands in the viewport.</summary>
    public ScrollAlignment Alignment { get; set; } = ScrollAlignment.Nearest;

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider) => new ScrollToItemAction(Alignment);

    private sealed class ScrollToItemAction(ScrollAlignment alignment) : CueAction
    {
        protected internal override void Invoke(Visual target, object? value, bool first)
        {
            var list = Require<ItemsControl>(target, "{presto:ScrollToItem}");
            if (first || list.ItemsPanelRoot is null) return;
            var index = list.Items.IndexOf(value);
            if (index >= 0) PrestoAvalonia.ScrollToIndex(list, index, alignment);
        }
    }
}
