using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PrestoSample.Showcase.Pages;

/// <summary>Navigation between the destinations list and one destination: opening and going back only change <see cref="CurrentPage"/>.</summary>
public sealed partial class ConnectedXamlViewModel : ObservableObject
{
    public ConnectedXamlViewModel()
    {
        Gallery = new DestinationsViewModel(Open);
        CurrentPage = Gallery;
    }

    public DestinationsViewModel Gallery { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SwappedPage), nameof(FadedPage))]
    public partial ObservableObject CurrentPage { get; set; }

    /// <summary>Shows the pages in the cross-fading host instead of the swapping one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SwappedPage), nameof(FadedPage))]
    public partial bool CrossFade { get; set; }

    /// <summary><see cref="CurrentPage"/> for the swapping host; only one host holds a view, so the two never share keys.</summary>
    public ObservableObject? SwappedPage => CrossFade ? null : CurrentPage;

    /// <summary><see cref="CurrentPage"/> for the cross-fading host.</summary>
    public ObservableObject? FadedPage => CrossFade ? CurrentPage : null;

    private void Open(Destination destination) => CurrentPage = new DestinationViewModel(destination, () => CurrentPage = Gallery);
}

/// <summary>A destination as both views bind it; <see cref="Id"/> builds their connected keys.</summary>
public sealed record Destination(int Id, string City, string Country, double Hue, string Blurb);

/// <summary>The scrolling list of destinations, which keeps its scroll position while a destination is open.</summary>
public sealed partial class DestinationsViewModel(Action<Destination> open) : ObservableObject
{
    public IReadOnlyList<Destination> Destinations { get; } =
        [.. Datasets.Places.Select((place, i) => new Destination(i, place.City, place.Country, place.Hue, place.Blurb))];

    [ObservableProperty]
    public partial Vector Offset { get; set; }

    [RelayCommand]
    private void Open(Destination destination) => open(destination);
}

/// <summary>One destination in detail.</summary>
public sealed partial class DestinationViewModel(Destination destination, Action back) : ObservableObject
{
    public Destination Destination => destination;

    [RelayCommand]
    private void Back() => back();
}
