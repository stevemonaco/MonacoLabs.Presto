using Avalonia;

namespace MonacoLabs.Presto;

/// <summary>Stops a bridge <see cref="MotionValue"/> source listening when its visual leaves the tree or its scope reverts.</summary>
internal sealed class MotionValueSource : IRevertible
{
    private readonly Visual? _visual;
    private Action? _stop;

    private MotionValueSource(AvaloniaObject owner, Action stop)
    {
        Engine.Current.CurrentScope?.Register(this);
        _stop = stop;
        _visual = owner as Visual;
        if (_visual != null) _visual.DetachedFromVisualTree += HandleDetached;
    }

    internal static MotionValue Listen(AvaloniaObject owner, MotionValue value, Action stop)
    {
        _ = new MotionValueSource(owner, stop);
        return value;
    }

    public void Revert()
    {
        if (_stop is not { } stop) return;
        _stop = null;
        if (_visual != null) _visual.DetachedFromVisualTree -= HandleDetached;
        stop();
    }

    private void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e) => Revert();
}
