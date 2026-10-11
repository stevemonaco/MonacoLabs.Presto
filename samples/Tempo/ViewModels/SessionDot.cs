using CommunityToolkit.Mvvm.ComponentModel;

namespace Tempo.ViewModels;

/// <summary>One of a task's session dots (<see cref="TaskItemViewModel.Dots"/>).</summary>
public sealed partial class SessionDot : ObservableObject
{
    [ObservableProperty]
    public partial bool IsFilled { get; set; }
}
