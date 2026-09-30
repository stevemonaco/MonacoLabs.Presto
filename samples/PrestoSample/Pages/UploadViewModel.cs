using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PrestoSample.Pages;

/// <summary>An upload whose Phase drives the States page's card through Motion.State and whose IsStarred drives it through a class.</summary>
public sealed partial class UploadViewModel : ObservableObject
{
    [ObservableProperty]
    public partial UploadPhase Phase { get; set; }

    [ObservableProperty]
    public partial bool IsStarred { get; set; }

    [RelayCommand]
    private void NextPhase() => Phase = (UploadPhase)(((int)Phase + 1) % Enum.GetValues<UploadPhase>().Length);
}
