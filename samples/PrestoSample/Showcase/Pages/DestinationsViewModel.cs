using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PrestoSample.Showcase.Pages;

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
