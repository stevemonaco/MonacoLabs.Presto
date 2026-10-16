using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Bindings;

/// <summary>
/// One value-driven property: the source is bound to the property's companion on the target, each companion change
/// retargets an <see cref="Follower"/>, and this observable carries the follower's displayed value to the
/// property. Subscribing connects the source; disposing the subscription tears both halves down.
/// </summary>
/// <remarks>
/// A <see cref="MotionValue"/> or <see cref="MotionObjectValue"/> arriving on the companion is listened to instead: each
/// flushed value retargets the follower, or passes straight through when the timing has no duration.
/// </remarks>
internal sealed class FollowBinding : IObservable<object?>, IDisposable
{
    private readonly AvaloniaObject _target;
    private readonly AttachedProperty<object?> _companion;
    private readonly BindingBase _source;
    private readonly bool _followsItems;
    private readonly bool _immediate;
    private readonly Follower _follower;
    private readonly Action _itemSettled;
    private readonly Action<MotionValue> _numberChanged;
    private readonly Action<MotionObjectValue> _objectChanged;
    private IObserver<object?>? _observer;
    private IDisposable? _input;
    private object? _listened;
    private bool _newItem;
    private bool _detached;

    /// <summary>Validates the source and timing; set <paramref name="followsItems"/> when the source resolves against the <c>DataContext</c>, so a new item snaps.</summary>
    /// <exception cref="ArgumentException"><paramref name="source"/> writes back, or the timing conflicts.</exception>
    public FollowBinding(AvaloniaObject target, AvaloniaProperty property, BindingBase source, FollowParams options, bool followsItems)
    {
        var mode = source switch
        {
            CompiledBinding b => b.Mode,
            ReflectionBinding b => b.Mode,
            MultiBinding b => b.Mode,
            TemplateBinding b => b.Mode,
            _ => BindingMode.Default,
        };
        if (mode is BindingMode.TwoWay or BindingMode.OneWayToSource)
            throw new ArgumentException($"A value-driven binding shows animated values, so it cannot write them back: its Mode cannot be {mode}.", nameof(source));

        _target = target;
        _companion = CompanionProperties.For(property);
        _source = source;
        _followsItems = followsItems;
        _follower = new Follower(property.PropertyType, options, target.GetValue(property));
        _immediate = FollowTiming.Resolve(options, reduced: false).Duration?.AsDouble == 0;
        _follower.PropertyChanged += (_, _) => _observer?.OnNext(_follower.Value);
        _itemSettled = () => _newItem = false;
        _numberChanged = _ => Show(snap: Immediate);
        _objectChanged = _ => Show(snap: Immediate);
    }

    public IDisposable Subscribe(IObserver<object?> observer)
    {
        _target.PropertyChanged += OnTargetChanged;
        if (_followsItems && _target is StyledElement element) element.DataContextChanged += OnDataContextChanged;
        if (_target is Visual visual)
        {
            visual.AttachedToVisualTree += OnAttached;
            visual.DetachedFromVisualTree += OnDetached;
            _detached = !visual.IsAttachedToVisualTree();
        }
        _input = _target.Bind(_companion, _source);
        _observer = observer;
        // The follower starts at UnsetValue, never null, which a value-typed property would reject.
        observer.OnNext(_follower.Value);
        return this;
    }

    public void Dispose()
    {
        _observer = null;
        _target.PropertyChanged -= OnTargetChanged;
        if (_target is StyledElement element) element.DataContextChanged -= OnDataContextChanged;
        if (_target is Visual visual)
        {
            visual.AttachedToVisualTree -= OnAttached;
            visual.DetachedFromVisualTree -= OnDetached;
        }
        var input = _input;
        _input = null;
        input?.Dispose();
        Listen(null);
        _follower.Dispose();
    }

    private void OnTargetChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != _companion) return;
        // The first value follows AnimateInitial even before the first attach.
        var snap = _newItem || (_detached && _follower.Started);
        _newItem = false;
        Listen(e.NewValue is MotionValue or MotionObjectValue ? e.NewValue : null);
        if (_listened != null) Show(snap: true);
        else _follower.Target(e.NewValue, snap);
    }

    private void Listen(object? source)
    {
        if (!_detached) Watch(_listened, false);
        _listened = source;
        if (!_detached) Watch(source, true);
    }

    private void Watch(object? source, bool watch)
    {
        if (source is MotionValue number)
        {
            if (watch) number.Changed += _numberChanged;
            else number.Changed -= _numberChanged;
        }
        else if (source is MotionObjectValue value)
        {
            if (watch) value.Changed += _objectChanged;
            else value.Changed -= _objectChanged;
        }
    }

    // An immediate binding still eases under reduced motion when it has a WhenReduced timing.
    private bool Immediate => _immediate && !Engine.Current.Defaults.ReducedMotion;

    private void Show(bool snap) => _follower.Target(_listened is MotionValue number ? number.Value : ((MotionObjectValue)_listened!).Value, snap);

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // The value a new item causes follows synchronously; the post clears the flag if the value did not change.
        _newItem = true;
        Dispatcher.UIThread.Post(_itemSettled, DispatcherPriority.Background);
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Watch(_listened, false);
        _detached = true;
        _follower.Stop();
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (!_detached) return;
        _detached = false;
        if (_listened == null) return;
        Watch(_listened, true);
        Show(snap: true);
    }
}
