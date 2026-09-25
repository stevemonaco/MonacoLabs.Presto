using Avalonia;
using Avalonia.Data;

namespace Monaco.Presto.AvaloniaBridge.Targets;

/// <summary>
/// One property held at <see cref="BindingPriority.Animation"/>, with every frame pushed through the same binding. It serves
/// as its own subscription token, so disposing it tears the binding down and uncovers the value underneath.
/// </summary>
/// <remarks>
/// <typeparamref name="T"/> is either <see cref="object"/>, bound untyped, or the property's own type, bound through the
/// typed overloads so a value type is pushed without boxing.
/// </remarks>
internal sealed class AnimatedValue<T> : IObservable<T>, IDisposable
{
    private IObserver<T>? _observer;
    private IDisposable? _binding;

    public AnimatedValue(AvaloniaObject target, AvaloniaProperty property) =>
        _binding = this switch
        {
            IObservable<object?> untyped => target.Bind(property, untyped, BindingPriority.Animation),
            _ when property is DirectPropertyBase<T> direct => target.Bind(direct, this),
            _ => target.Bind((StyledProperty<T>)property, this, BindingPriority.Animation),
        };

    public IDisposable Subscribe(IObserver<T> observer)
    {
        _observer = observer;
        return this;
    }

    public void Push(T value) => _observer?.OnNext(value);

    public void Dispose()
    {
        _observer = null;
        // Cleared first: disposing the binding disposes this subscription token in turn.
        var binding = _binding;
        _binding = null;
        binding?.Dispose();
    }
}
