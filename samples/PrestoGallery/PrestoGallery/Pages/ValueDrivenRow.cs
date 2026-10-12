using CommunityToolkit.Mvvm.ComponentModel;

namespace PrestoGallery.Pages;

/// <summary>A row of the Value-driven page's list, whose progress the page's timer keeps changing.</summary>
public sealed partial class ValueDrivenRow(string name, double progress) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    public partial double Progress { get; set; } = progress;
}
