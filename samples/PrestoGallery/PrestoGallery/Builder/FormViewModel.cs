using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;

namespace PrestoGallery.Builder;

public enum FormState
{
    Idle,
    Validating,
    Error,
    Success,
}

/// <summary>A sign-up form as a state machine; the card and badge animate from its State through Motion.State.</summary>
public sealed partial class FormViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status), nameof(Glyph), nameof(Tint))]
    public partial FormState State { get; set; }

    [ObservableProperty]
    public partial string Email { get; set; } = "";

    public string Status => State switch
    {
        FormState.Validating => "Checking…",
        FormState.Error => "That doesn't look like an email address",
        FormState.Success => "You're on the list",
        _ => "Enter your email to join the beta",
    };

    public string Glyph => State switch
    {
        FormState.Validating => "…",
        FormState.Error => "✕",
        FormState.Success => "✓",
        _ => "@",
    };

    public IBrush Tint => State switch
    {
        FormState.Validating => Palette.Amber,
        FormState.Error => Palette.Coral,
        FormState.Success => Palette.Aqua,
        _ => Palette.AccentDim,
    };

    [RelayCommand]
    private async Task Submit()
    {
        State = FormState.Validating;
        await Task.Delay(1200);
        State = Email.Contains('@') ? FormState.Success : FormState.Error;
    }

    partial void OnEmailChanged(string value)
    {
        if (State is FormState.Error or FormState.Success) State = FormState.Idle;
    }
}
