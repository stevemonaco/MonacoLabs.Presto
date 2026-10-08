using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary><see cref="Recipes.PopoverIn"/> and <see cref="Recipes.PopoverOut"/> (GAPS-2-PLAN §7.2): the presenter slides from and back into the edge facing its anchor.</summary>
public sealed class PopoverRecipeTests : CompositorTestBase
{
    [AvaloniaTheory]
    [InlineData(PlacementMode.Bottom, VerticalAlignment.Center, "Y", -1)]
    [InlineData(PlacementMode.Top, VerticalAlignment.Center, "Y", 1)]
    [InlineData(PlacementMode.Left, VerticalAlignment.Center, "X", 1)]
    [InlineData(PlacementMode.Right, VerticalAlignment.Center, "X", -1)]
    [InlineData(PlacementMode.Bottom, VerticalAlignment.Bottom, "Y", 1)]
    public void ThePresenterSlidesFromAndBackIntoTheEdgeFacingItsAnchor(PlacementMode placement, VerticalAlignment row, string axis, int sign)
    {
        var anchor = new Button { Width = 60, Height = 30, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = row };
        new Window { Content = new Panel { Children = { anchor } }, Width = 400, Height = 400 }.Show();
        Tick();
        var flyout = new Flyout { Content = Lime(), Placement = placement };
        Popover.SetOpen(flyout, Recipes.PopoverIn(distance: 10, duration: 200, ease: "linear"));
        Popover.SetClose(flyout, Recipes.PopoverOut(distance: 10, duration: 200, ease: "linear"));
        var across = axis == "X" ? "Y" : "X";
        // The first open cannot tell an overlay host from a native one, and takes it to be native.
        flyout.ShowAt(anchor);
        Tick();
        flyout.Hide();
        Frames.Advance(300, 16);
        Tick();

        flyout.ShowAt(anchor);
        var presenter = flyout.Popup.Child!;

        Assert.Equal(sign * 10, Get(presenter, axis));
        Assert.Equal(0, Get(presenter, across));
        Assert.Equal(Recipes.Tokens.Scale, Get(presenter, "Scale"));

        Frames.Advance(300, 16);
        Tick();
        flyout.Hide();
        Frames.Advance(100, 16);

        Assert.True(flyout.IsOpen);
        Assert.InRange(sign * Get(presenter, axis), 3, 7);
        Assert.Equal(0, Get(presenter, across));
    }

    [AvaloniaFact]
    public void AContextMenuAtThePointerSlidesDownFromItsTopEdge()
    {
        var (_, anchor) = PopupStage();
        var menu = new ContextMenu { Items = { new MenuItem { Header = "a" } } };
        Popover.SetOpen(menu, Recipes.PopoverIn(distance: 10, duration: 200, ease: "linear"));
        anchor.ContextMenu = menu;

        menu.Open(anchor);

        Assert.Equal(-10, Get(menu, "Y"));
        Assert.Equal(0, Get(menu, "X"));
    }

    [AvaloniaFact]
    public void AComboBoxDropDownStyledThroughItsTemplateSlidesDownFromItsTopEdge()
    {
        var (window, _) = PopupStage();
        window.Styles.Add(new Style(x => x.OfType<ComboBox>().Template().OfType<Popup>().Name("PART_Popup"))
        {
            Setters = { new Setter(Popover.OpenProperty, Recipes.PopoverIn(distance: 10, duration: 200, ease: "linear")) },
        });
        var combo = new ComboBox { ItemsSource = new[] { "a", "b" }, Width = 100, VerticalAlignment = VerticalAlignment.Center };
        ((Panel)window.Content!).Children.Add(combo);
        Tick();

        combo.IsDropDownOpen = true;
        var child = combo.GetVisualDescendants().OfType<Popup>().Single().Child!;

        Assert.Equal(-10, Get(child, "Y"));
        Assert.Equal(0, Get(child, "X"));
    }

    private static double Get(Visual visual, string property) => Convert.ToDouble(PrestoAvalonia.Get(visual, property));
}
