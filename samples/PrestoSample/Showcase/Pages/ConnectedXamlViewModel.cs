using CommunityToolkit.Mvvm.ComponentModel;

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

    /// <summary>Shows the pages in the cross-fading host instead of the swapping one, starting the list back at the top.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SwappedPage), nameof(FadedPage))]
    public partial bool CrossFade { get; set; }

    /// <summary><see cref="CurrentPage"/> for the swapping host; only one host holds a view, so the two never share keys.</summary>
    public ObservableObject? SwappedPage => CrossFade ? null : CurrentPage;

    /// <summary><see cref="CurrentPage"/> for the cross-fading host.</summary>
    public ObservableObject? FadedPage => CrossFade ? CurrentPage : null;

    private void Open(Destination destination) => CurrentPage = new DestinationViewModel(destination, () => CurrentPage = Gallery);

    // Runs before the hosts rebuild: a new list clamps an offset it has no extent for yet and writes the clamp back.
    partial void OnCrossFadeChanged(bool value) => Gallery.Offset = default;
}
