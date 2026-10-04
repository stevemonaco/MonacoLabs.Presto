using CommunityToolkit.Mvvm.ComponentModel;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Builder;

/// <summary>View state that picks the animation: <see cref="IsAlert"/> swaps <see cref="Emphasis"/> between an idle float and an alert shake.</summary>
public sealed partial class EmphasisViewModel : ObservableObject
{
    #region snippet XamlEmphasis
    public static readonly AnimationBuilder Idle = Recipes.Float();
    public static readonly AnimationBuilder Alert = Recipes.Shake().Loop().LoopDelay(700);

    [ObservableProperty]
    public partial AnimationBuilder? Emphasis { get; set; } = Idle;

    [ObservableProperty]
    public partial bool IsAlert { get; set; }

    partial void OnIsAlertChanged(bool value) => Emphasis = value ? Alert : Idle;
    #endregion
}
