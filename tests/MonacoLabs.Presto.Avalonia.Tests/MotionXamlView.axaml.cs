using Avalonia.Controls;

namespace MonacoLabs.Presto.Tests;

/// <summary>Motion states from styles: one <see cref="MotionStates"/> list shared by three tiles, and a descendant selector setting <see cref="Motion.StateProperty"/>.</summary>
public sealed partial class MotionXamlView : UserControl
{
    public static readonly AnimationBuilder Dim = Presto.Build().Opacity(0.4).Duration(100).Ease(Eases.None);

    public static readonly AnimationBuilder Nudge = Presto.Build().X(10).Duration(100).Ease(Eases.None);

    public MotionXamlView() => InitializeComponent();
}
