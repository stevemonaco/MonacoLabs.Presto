using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Showcase.Pages;

/// <summary>
/// The five popup owners with <see cref="Popover"/> as an editor toolbar: a <c>MenuFlyout</c>, a <c>ContextMenu</c>, a
/// <c>ToolTip</c> and a <c>ComboBox</c> drop-down styled with <c>{presto:Popover}</c>, a <c>Flyout</c> that sets its own,
/// and rows that arrive staggered through <see cref="Presence"/>.
/// </summary>
public partial class PopupsPage : ShowcasePage
{
    public PopupsPage() => InitializeComponent();
}
