using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

/// <summary>
/// <see cref="Ripple"/> on buttons in three forms, on focusable cards in three colors with the state layer, and on the
/// rows of a scrolling <c>ListBox</c>.
/// </summary>
public partial class RipplePage : ShowcasePage
{
    public RipplePage()
    {
        InitializeComponent();
        SurfacesCode.Code = $"{Snippets.RippleButtons}\n\n{Snippets.RippleCards}";
        ListCode.Code = Snippets.RippleList;
        Rows.ItemsSource = Enumerable.Range(1, 20).Select(i => $"Order #{1040 + i} · {i * 3 % 7 + 1} items · shipped").ToArray();
    }
}
