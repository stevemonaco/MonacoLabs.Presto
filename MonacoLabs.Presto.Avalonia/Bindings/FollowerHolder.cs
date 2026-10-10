namespace MonacoLabs.Presto.Bindings;

/// <summary>
/// What a follower's animatable writes: a plain object whose <see cref="Value"/> has the followed type, so the core writes
/// a number unboxed and a codec decomposes anything else into channels. Each write is reported as a rendered frame.
/// </summary>
internal sealed class FollowerHolder<T>(T value, Action<object, object?> rendered)
{
    private T _value = value;

    public T Value
    {
        get => _value;
        set
        {
            _value = value;
            rendered(this, value);
        }
    }
}
