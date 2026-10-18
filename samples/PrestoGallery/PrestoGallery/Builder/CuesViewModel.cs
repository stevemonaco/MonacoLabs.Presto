using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonacoLabs.Presto;

namespace PrestoGallery.Builder;

/// <summary>A save form that only raises cues; the view decides how each one looks.</summary>
public sealed partial class CuesViewModel : ObservableObject
{
    #region snippet XamlCuesViewModel
    public Cue ValidationFailed { get; } = new();
    public Cue<string> Saved { get; } = new();
    public Cue Celebrate { get; } = new();

    [ObservableProperty]
    public partial string Email { get; set; } = "";

    [RelayCommand]
    private void Submit()
    {
        if (Email.Contains('@')) Saved.Raise($"Saved {Email}");
        else ValidationFailed.Raise();
    }

    [RelayCommand]
    private void Cheer() => Celebrate.Raise();
    #endregion
}
