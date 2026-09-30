using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PrestoSample.Showcase.Pages;

/// <summary>The storefront's catalogue, the products the picked filter chips let through, the quick-view product and the cart count.</summary>
public sealed partial class StorefrontViewModel : ObservableObject
{
    private static readonly IReadOnlyList<Product> Catalogue =
    [
        new("Trail lantern", "Outdoor", "$48", 30, 0),
        new("Canvas daypack", "Travel", "$86", 200, 1),
        new("Linen throw", "Home", "$64", 340, 2),
        new("Camp kettle", "Outdoor", "$39", 12, 3),
        new("Passport wallet", "Travel", "$29", 160, 4),
        new("Stoneware mug", "Home", "$18", 45, 5),
        new("Folding stool", "Outdoor", "$55", 110, 6),
        new("Packing cubes", "Travel", "$34", 250, 7),
        new("Oak side table", "Home", "$210", 70, 8),
        new("Star map", "Outdoor", "$22", 230, 9),
        new("Weekender bag", "Travel", "$140", 290, 10),
        new("Candle trio", "Home", "$27", 320, 11),
    ];

    private readonly HashSet<string> _picked = [];

    public static IReadOnlyList<string> Categories { get; } = ["Outdoor", "Travel", "Home"];

    /// <summary>The products on show, in catalogue order; filtering removes and inserts items rather than resetting, so the cards that stay keep their containers.</summary>
    public ObservableCollection<Product> Shown { get; } = [.. Catalogue];

    [ObservableProperty]
    public partial int CartCount { get; set; }

    [ObservableProperty]
    public partial Product? QuickView { get; set; }

    /// <summary>Picks or unpicks a category chip; with none picked every product shows.</summary>
    public void Pick(string category, bool picked)
    {
        if (picked) _picked.Add(category);
        else _picked.Remove(category);

        List<Product> wanted = [.. Catalogue.Where(product => _picked.Count == 0 || _picked.Contains(product.Category))];
        for (var i = Shown.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(Shown[i])) Shown.RemoveAt(i);
        }
        for (var i = 0; i < wanted.Count; i++)
        {
            if (i == Shown.Count || Shown[i] != wanted[i]) Shown.Insert(i, wanted[i]);
        }
    }
}
