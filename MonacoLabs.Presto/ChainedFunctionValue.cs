namespace MonacoLabs.Presto;

/// <summary>
/// A <see cref="FunctionValue"/> that also receives the tween it chains off (upstream's <c>prevTween</c> argument): the
/// previous keyframe's tween, or the previous sibling's when the animation is a timeline child.
/// </summary>
public delegate TweenValue ChainedFunctionValue(object target, int index, int total, Tween? previous);
