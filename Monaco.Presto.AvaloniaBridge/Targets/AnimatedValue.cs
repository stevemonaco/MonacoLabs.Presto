using Avalonia;
using Avalonia.Data;

namespace Monaco.Presto.AvaloniaBridge.Targets;

/// <summary>
/// One property held at <see cref="BindingPriority.Animation"/>, with every frame pushed through the same binding. It serves
/// as its own subscription token, so disposing it tears the binding down and uncovers the value underneath.
/// </summary>
internal sealed class AnimatedValue : IObservable<object?>, IDisposable
{
    private IObserver<object?>? _observer;
    private IDisposable? _binding;

    public AnimatedValue(AvaloniaObject target, AvaloniaProperty property) =>
        _binding = target.Bind(property, this, BindingPriority.Animation);

    public IDisposable Subscribe(IObserver<object?> observer)
    {
        _observer = observer;
        return this;
    }

    public void Push(object? value) => _observer?.OnNext(value);

    public void Dispose()
    {
        _observer = null;
        // Cleared first: disposing the binding disposes this subscription token in turn.
        var binding = _binding;
        _binding = null;
        binding?.Dispose();
    }
}
