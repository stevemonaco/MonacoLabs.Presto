using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Targets;

/// <summary>
/// The built-in fallback adapter. <see cref="IDictionary{TKey,TValue}"/> targets behave like JavaScript objects (a missing
/// key reads as null, a write creates it); any other target is reached through its public properties and fields, written in
/// their own type and cached per (type, property) as compiled delegates.
/// </summary>
public sealed class ObjectTargetAdapter : ITargetAdapter
{
    private static readonly ConcurrentDictionary<(Type Type, string Property), MemberAccessor> s_accessors = new();

    /// <summary>
    /// Private, static and write-only members are not reached. A member whose type is not numeric, <see cref="string"/>,
    /// <see cref="object"/> or <see cref="PrestoColor"/> and has no codec in <see cref="Presto.Codecs"/> is discrete (<see cref="DiscreteCodec"/>).
    /// </summary>
    public IPropertyAccessor Resolve(object target, string property)
    {
        if (target is IDictionary<string, object?>) return new DictionaryAccessor(property);
        if (target is IMemberTarget { Members: var members })
        {
            if (!members.TryGetValue(property, out var member))
                throw new ArgumentException($"{target.GetType()} has no animatable member named '{property}'; it has {string.Join(", ", members.Keys)}.", nameof(property));
            return member is MemberAccessor { CodecIsCurrent: false } stale ? stale.WithCurrentCodec() : member;
        }
        var type = target.GetType();
        var accessor = s_accessors.GetOrAdd((type, property), static key => MemberAccessor.Create(key.Type, key.Property));
        return accessor.CodecIsCurrent ? accessor : s_accessors[(type, property)] = accessor.WithCurrentCodec();
    }

    private sealed class DictionaryAccessor(string property) : IPropertyAccessor
    {
        public IValueCodec? Codec => null;

        public object? GetValue(object target) => Entries(target).TryGetValue(property, out var value) ? value : null;

        public void SetValue(object target, double number, Tween tween) => Entries(target)[property] = tween.ComposeValue(number);

        public void Revert(object target, object? originalValue)
        {
            if (originalValue == null) Entries(target).Remove(property);
            else Entries(target)[property] = originalValue;
        }

        private static IDictionary<string, object?> Entries(object target) => (IDictionary<string, object?>)target;
    }

    private enum MemberWrite
    {
        Number,
        String,
        Color,
        Object,
        Codec,
    }

    internal sealed class MemberAccessor(
        Func<object, object?> get,
        Action<object, object?> set,
        Action<object, double>? setNumber,
        Action<object, PrestoColor>? setColor,
        Type memberType) : IPropertyAccessor
    {
        private readonly MemberWrite _write = WriteOf(memberType);
        private readonly IValueCodec? _codec = WriteOf(memberType) == MemberWrite.Codec ? CodecFor(memberType) : null;

        [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Plain user types keep their members through Presto.RegisterTarget<T>(); library types use IMemberTarget tables, which the Debug check enforces.")]
        public static MemberAccessor Create(Type type, string name)
        {
            AssertNotLibraryType(type);
            MemberInfo? member = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) is { CanRead: true, CanWrite: true } property
                ? property
                : type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (member is null) throw new ArgumentException($"{type} has no public readable and writable property or field named '{name}'. In a trimmed or NativeAOT app, call Presto.RegisterTarget<T>() for this type or register an ITargetAdapter.", nameof(name));

            var memberType = member is PropertyInfo info ? info.PropertyType : ((FieldInfo)member).FieldType;
            var memberWrite = WriteOf(memberType);

            var target = Expression.Parameter(typeof(object), "target");
            var access = Expression.MakeMemberAccess(Expression.Convert(target, type), member);
            var value = Expression.Parameter(typeof(object), "value");
            var number = Expression.Parameter(typeof(double), "number");

            Action<object, double>? setNumber = null;
            if (memberWrite == MemberWrite.Number)
            {
                Expression source = IsIntegral(memberType)
                    ? Expression.Call(typeof(Helpers).GetMethod(nameof(Helpers.Round))!, number, Expression.Constant(0))
                    : number;
                setNumber = Expression.Lambda<Action<object, double>>(Expression.Assign(access, Expression.Convert(source, memberType)), target, number).Compile();
            }

            Action<object, PrestoColor>? setColor = null;
            if (memberWrite == MemberWrite.Color)
            {
                var color = Expression.Parameter(typeof(PrestoColor), "color");
                setColor = Expression.Lambda<Action<object, PrestoColor>>(Expression.Assign(access, color), target, color).Compile();
            }

            return new MemberAccessor(
                Expression.Lambda<Func<object, object?>>(Expression.Convert(access, typeof(object)), target).Compile(),
                Expression.Lambda<Action<object, object?>>(Expression.Assign(access, Expression.Convert(value, memberType)), target, value).Compile(),
                setNumber,
                setColor,
                memberType);
        }

        public static MemberAccessor Of<TTarget, TValue>(Func<TTarget, TValue> get, Action<TTarget, TValue> set) => new(
            target => get((TTarget)target),
            (target, value) => set((TTarget)target, (TValue)value!),
            (object)set switch
            {
                Action<TTarget, double> write => (target, number) => write((TTarget)target, number),
                Action<TTarget, float> write => (target, number) => write((TTarget)target, (float)number),
                Action<TTarget, int> write => (target, number) => write((TTarget)target, (int)Helpers.Round(number, 0)),
                _ => null,
            },
            (object)set is Action<TTarget, PrestoColor> writeColor ? (target, color) => writeColor((TTarget)target, color) : null,
            typeof(TValue));

        public IValueCodec? Codec => _codec;

        public bool CodecIsCurrent =>
            _write != MemberWrite.Codec ||
            (Presto.Codecs.TryGetValue(memberType, out var registered) ? ReferenceEquals(registered, _codec) : _codec is DiscreteCodec);

        public MemberAccessor WithCurrentCodec() => new(get, set, setNumber, setColor, memberType);

        public object? GetValue(object target) => get(target);

        public void SetValue(object target, double number, Tween tween)
        {
            if (setNumber != null) setNumber(target, number);
            else if (setColor != null) setColor(target, PrestoColor.FromChannels(tween._numbers));
            else set(target, Compose(number, tween));
        }

        public void Revert(object target, object? originalValue) => set(target, originalValue);

        [Conditional("DEBUG")]
        private static void AssertNotLibraryType(Type type)
        {
            if (type.Assembly.GetName().Name is "MonacoLabs.Presto" or "MonacoLabs.Presto.Avalonia" or "MonacoLabs.Presto.Avalonia.Lottie" or "MonacoLabs.Presto.Avalonia.DevTools")
                throw new InvalidOperationException($"{type} is a library type reached by reflection; it needs an IMemberTarget table.");
        }

        private static MemberWrite WriteOf(Type type) =>
            type == typeof(string) ? MemberWrite.String :
            type == typeof(PrestoColor) ? MemberWrite.Color :
            type == typeof(object) ? MemberWrite.Object :
            IsNumeric(type) ? MemberWrite.Number :
            MemberWrite.Codec;

        private static IValueCodec CodecFor(Type type) => Presto.Codecs.TryGetValue(type, out var codec) ? codec : new DiscreteCodec(type);

        private static bool IsNumeric(Type type) =>
            type == typeof(double) || type == typeof(float) || type == typeof(decimal) || IsIntegral(type);

        private static bool IsIntegral(Type type) =>
            type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte);

        private object? Compose(double number, Tween tween) => _write switch
        {
            MemberWrite.String => tween.ComposeString(number),
            MemberWrite.Codec => _codec!.Encode(number, tween),
            _ => tween.ComposeValue(number),
        };
    }
}
