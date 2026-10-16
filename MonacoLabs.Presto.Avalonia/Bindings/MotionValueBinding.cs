using Avalonia;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Bindings;

/// <summary>
/// Carries each flushed value of a <see cref="MotionValue"/> to a <c>double</c> property unboxed, which saves the box a
/// <see cref="FollowBinding"/> pays per frame. Like it, it stops listening while the target is detached and shows the latest value on reattach.
/// </summary>
internal sealed class MotionValueBinding(AvaloniaObject target, MotionValue source) : IObservable<double>, IDisposable
{
    private IObserver<double>? _observer;
    private bool _detached;

    public IDisposable Subscribe(IObserver<double> observer)
    {
        _observer = observer;
        if (target is Visual visual)
        {
            visual.AttachedToVisualTree += OnAttached;
            visual.DetachedFromVisualTree += OnDetached;
            _detached = !visual.IsAttachedToVisualTree();
        }
        if (!_detached) source.Changed += OnChanged;
        observer.OnNext(source.Value);
        return this;
    }

    public void Dispose()
    {
        _observer = null;
        if (target is Visual visual)
        {
            visual.AttachedToVisualTree -= OnAttached;
            visual.DetachedFromVisualTree -= OnDetached;
        }
        source.Changed -= OnChanged;
    }

    private void OnChanged(MotionValue value) => _observer?.OnNext(value.Value);

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _detached = true;
        source.Changed -= OnChanged;
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (!_detached) return;
        _detached = false;
        source.Changed += OnChanged;
        _observer?.OnNext(source.Value);
    }
}
