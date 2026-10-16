using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PrestoGallery.Showcase.Pages;

/// <summary>One destination in detail.</summary>
public sealed partial class DestinationViewModel(Destination destination, Action back) : ObservableObject
{
    public Destination Destination => destination;

    [RelayCommand]
    private void Back() => back();
}
