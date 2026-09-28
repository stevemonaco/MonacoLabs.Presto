using CommunityToolkit.Mvvm.ComponentModel;
using Monaco.Presto;

namespace PrestoSample.Builder;

/// <summary>View state that picks the animation: <see cref="IsAlert"/> swaps <see cref="Emphasis"/> between an idle float and an alert shake.</summary>
public sealed partial class EmphasisViewModel : ObservableObject
{
    [ObservableProperty]
    public partial AnimationBuilder? Emphasis { get; set; } = Motions.Float;

    [ObservableProperty]
    public partial bool IsAlert { get; set; }

    partial void OnIsAlertChanged(bool value) => Emphasis = value ? Motions.Shake : Motions.Float;
}
