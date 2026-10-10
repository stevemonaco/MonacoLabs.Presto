namespace MonacoLabs.Presto;

/// <summary>A copy of a tween's value, taken by <see cref="Tween.GetFrom"/>, <see cref="Tween.GetTo"/> or <see cref="Tween.GetCurrent"/>.</summary>
/// <param name="Number">The value of a number or unit tween; meaningless for a color or complex one.</param>
/// <param name="Channels">A copy of the channels of a color or complex tween; empty otherwise.</param>
/// <param name="Text">The value composed as the string a target would receive, such as <c>"50px"</c> or <c>"rgba(255, 0, 0, 1)"</c>.</param>
public readonly record struct TweenSnapshot(double Number, double[] Channels, string Text);
