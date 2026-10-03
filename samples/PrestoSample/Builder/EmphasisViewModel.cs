using CommunityToolkit.Mvvm.ComponentModel;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>View state that picks the animation: <see cref="IsAlert"/> swaps <see cref="Emphasis"/> between an idle float and an alert shake.</summary>
public sealed partial class EmphasisViewModel : ObservableObject
{
    public static readonly (AnimationBuilder Builder, string Code) Idle = Snippet.Of(Recipes.Float());
    public static readonly (AnimationBuilder Builder, string Code) Alert = Snippet.Of(Recipes.Shake().Loop().LoopDelay(700));

    [ObservableProperty]
    public partial AnimationBuilder? Emphasis { get; set; } = Idle.Builder;

    [ObservableProperty]
    public partial bool IsAlert { get; set; }

    partial void OnIsAlertChanged(bool value) => Emphasis = value ? Alert.Builder : Idle.Builder;
}
