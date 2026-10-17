using MonacoLabs.Presto.Targets;

namespace MonacoLabs.Presto.Bindings;

/// <summary>
/// What a follower's animatable writes: a plain object whose <see cref="Value"/> has the followed type, so the core writes
/// a number unboxed and a codec decomposes anything else into channels. Each write is reported as a rendered frame.
/// </summary>
internal sealed class FollowerHolder<T>(T value, Action<object, object?> rendered) : IMemberTarget
{
    private static readonly Dictionary<string, IPropertyAccessor> s_members = new()
    {
        [nameof(Value)] = ObjectTargetAdapter.MemberAccessor.Of<FollowerHolder<T>, T>(holder => holder.Value, (holder, value) => holder.Value = value),
    };

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

    IReadOnlyDictionary<string, IPropertyAccessor> IMemberTarget.Members => s_members;
}
