using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Utilities;
using MonacoLabs.Presto.Diagnostics;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Bindings;

/// <summary>
/// The displayed side of a value-driven property: each <see cref="Target"/> eases <see cref="Value"/> to the new value
/// with the timing of an <see cref="FollowParams"/>, or snaps to it when it cannot or should not be eased.
/// </summary>
/// <remarks>
/// It runs its own core <see cref="Animatable"/> over a <see cref="FollowerHolder{T}"/> and never registers the control's
/// property with Presto, so an animation of that property layers over the followed value instead of replacing it.
/// </remarks>
internal sealed class Follower : INotifyPropertyChanged, IDisposable
{
    private const string HolderProperty = nameof(FollowerHolder<double>.Value);
    private static readonly PropertyChangedEventArgs ValueChanged = new(nameof(Value));
    private static readonly HashSet<Type> ChannelTypes =
        [typeof(Color), typeof(Thickness), typeof(CornerRadius), typeof(Point), typeof(Size), typeof(Rect), typeof(Vector), typeof(RelativePoint), typeof(BoxShadows)];

    private readonly Type _type;
    private FollowParams _parameters;
    private readonly Action<object, object?> _rendered;
    private readonly object? _initial;
    private object? _target = AvaloniaProperty.UnsetValue;
    private AnimatableProperty _timing;
    private bool _reduced;
    private bool _retimed;
    private Animatable? _animatable;
    private object? _holder;

    /// <param name="type">The followed property's type, which every value is converted to.</param>
    /// <param name="parameters">The timing, validated here.</param>
    /// <param name="initial">The property's value before the binding, which the first value eases from when <see cref="FollowParams.AnimateInitial"/> is set.</param>
    public Follower(Type type, FollowParams parameters, object? initial)
    {
        _type = type;
        _parameters = parameters;
        _timing = FollowTiming.Resolve(parameters, reduced: false);
        _rendered = OnRendered;
        _initial = parameters.AnimateInitial ? Convert(initial) : AvaloniaProperty.UnsetValue;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The displayed value, <see cref="AvaloniaProperty.UnsetValue"/> until the first <see cref="Target"/>.</summary>
    public object? Value { get; private set; } = AvaloniaProperty.UnsetValue;

    /// <summary>Whether <see cref="Target"/> has been called, so the next value is not the first.</summary>
    public bool Started { get; private set; }

    /// <summary>Eases towards <paramref name="value"/>, or shows it at once when <paramref name="snap"/> is set or it cannot be eased.</summary>
    public void Target(object? value, bool snap = false)
    {
        value = Convert(value);
        var from = Started ? _target : _initial;
        Started = true;
        _target = value;
        var reduced = Engine.Current.Defaults.ReducedMotion;
        if (snap || (reduced && _parameters.WhenReduced == null) || !CanEase(from, value, out var channels))
        {
            Land(value);
            return;
        }

        if (_animatable == null || reduced != _reduced || _retimed)
        {
            if (reduced != _reduced) (_timing, _reduced) = (FollowTiming.Resolve(_parameters, reduced), reduced);
            _retimed = false;
            Rebuild(_animatable == null ? from! : Value!);
        }
        if (channels == null) _animatable!.Set(HolderProperty, System.Convert.ToDouble(value, CultureInfo.InvariantCulture));
        else _animatable!.Set(HolderProperty, channels);
    }

    /// <summary>Takes the timing of <paramref name="parameters"/> from the next <see cref="Target"/> on.</summary>
    /// <exception cref="ArgumentException">The timing conflicts.</exception>
    public void Retime(FollowParams parameters)
    {
        _timing = FollowTiming.Resolve(parameters, reduced: false);
        (_parameters, _reduced, _retimed) = (parameters, false, true);
    }

    /// <summary>Lands on the latest value and stops ticking.</summary>
    public void Stop()
    {
        if (_animatable != null) Land(_target);
    }

    public void Dispose() => Halt();

    private object? Convert(object? value) =>
        value is BindingNotification || value == AvaloniaProperty.UnsetValue ? value
        : TypeUtilities.TryConvert(_type, value, CultureInfo.InvariantCulture, out var converted) ? converted
        : AvaloniaProperty.UnsetValue;

    private void Land(object? value)
    {
        Halt();
        Show(value);
    }

    private void Show(object? value)
    {
        Value = value;
        PropertyChanged?.Invoke(this, ValueChanged);
    }

    private void Halt()
    {
        var animatable = _animatable;
        _animatable = null;
        _holder = null;
        animatable?.Revert();
    }

    private void Rebuild(object start)
    {
        Halt();
        _holder = Activator.CreateInstance(typeof(FollowerHolder<>).MakeGenericType(ChannelType(start)!), start, _rendered)!;
        using var origin = PrestoDiagnostics.Origin("Follow");
        _animatable = Presto.CreateAnimatable(_holder, new AvaloniaAnimatableParams { [HolderProperty] = _timing });
    }

    private void OnRendered(object holder, object? value)
    {
        if (holder == _holder) Show(value);
    }

    private static bool CanEase(object? from, object? to, out double[]? channels)
    {
        channels = null;
        var type = ChannelType(to);
        // An unset Width reads as NaN, which would ease as auto width until it lands.
        if (type == null || type != ChannelType(from) || (from is double d && !double.IsFinite(d))) return false;
        if (to is double or float or int) return true;

        var (start, end) = (new DecomposedValue(), new DecomposedValue());
        var codec = Presto.Codecs[type];
        if (!codec.TryDecode(from!, start) || !codec.TryDecode(to!, end)
            || start.Kind != end.Kind || start.Numbers is not { Length: > 0 } || end.Numbers?.Length != start.Numbers.Length
            || !start.Strings.AsSpan().SequenceEqual(end.Strings)) return false;
        channels = end.Numbers;
        return true;
    }

    private static Type? ChannelType(object? value) => value switch
    {
        double or float or int => value.GetType(),
        IBrush => typeof(IBrush),
        not null when ChannelTypes.Contains(value.GetType()) => value.GetType(),
        _ => null,
    };
}
