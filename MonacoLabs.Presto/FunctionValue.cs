namespace MonacoLabs.Presto;

/// <summary>A per-target parameter value (upstream <c>(target, index, targets) => value</c>), as returned by staggers.</summary>
public delegate TweenValue FunctionValue(object target, int index, int total);
