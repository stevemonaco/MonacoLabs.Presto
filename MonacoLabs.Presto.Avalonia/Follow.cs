using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Bindings;
using MonacoLabs.Presto.Effects;
using MonacoLabs.Presto.Shapes;
using MonacoLabs.Presto.Transforms;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto;

/// <summary>
/// Attached shorthands that ease a visual's transform, blur or a shape's drawn stroke to each new value, usually bound to a view-model property:
/// <c>presto:Follow.Rotate="{Binding Angle}"</c>. A value is a number, or a string with a unit such as <c>"45deg"</c>
/// or <c>"1turn"</c> for <see cref="RotateProperty"/>; <see langword="null"/> or clearing the property releases the shorthand.
/// Any other string throws <see cref="FormatException"/>, and the shorthand keeps following its previous value.
/// </summary>
/// <remarks>
/// The first value snaps unless <see cref="AnimateInitialProperty"/> is set, as does a value caused by a new <c>DataContext</c>,
/// a later one that arrives while the visual is detached (or not yet attached), and every value under
/// <see cref="PrestoDefaults.ReducedMotion"/> unless <see cref="WhenReducedProperty"/> gives a timing; detaching lands on the
/// latest value. The followed value is the base of the shorthand: an animation of the same shorthand overrides it while it holds it, and reverting that animation shows the latest
/// followed value again. <see cref="DrawProperty"/> is the exception: it writes the shape's dashes as a <c>"Draw"</c> tween
/// does, so the later write wins, and releasing it restores the dashes the shape had.
/// <para>
/// A <see cref="MotionValue"/> is written as each value flushes, ignoring the timing properties: smoothing belongs to
/// the graph, as in <c>value.Spring(...)</c>. It is listened to only while the visual is attached.
/// </para>
/// </remarks>
public static class Follow
{
    /// <summary>The horizontal offset, in device independent pixels.</summary>
    public static readonly AttachedProperty<object?> XProperty =
        AvaloniaProperty.RegisterAttached<Visual, object?>("X", typeof(Follow));

    /// <summary>The vertical offset, in device independent pixels.</summary>
    public static readonly AttachedProperty<object?> YProperty =
        AvaloniaProperty.RegisterAttached<Visual, object?>("Y", typeof(Follow));

    /// <summary>The uniform scale, which multiplies <see cref="ScaleXProperty"/> and <see cref="ScaleYProperty"/>.</summary>
    public static readonly AttachedProperty<object?> ScaleProperty =
        AvaloniaProperty.RegisterAttached<Visual, object?>("Scale", typeof(Follow));

    /// <summary>The horizontal scale.</summary>
    public static readonly AttachedProperty<object?> ScaleXProperty =
        AvaloniaProperty.RegisterAttached<Visual, object?>("ScaleX", typeof(Follow));

    /// <summary>The vertical scale.</summary>
    public static readonly AttachedProperty<object?> ScaleYProperty =
        AvaloniaProperty.RegisterAttached<Visual, object?>("ScaleY", typeof(Follow));

    /// <summary>The rotation, in degrees unless the value carries a unit of its own.</summary>
    public static readonly AttachedProperty<object?> RotateProperty =
        AvaloniaProperty.RegisterAttached<Visual, object?>("Rotate", typeof(Follow));

    /// <summary>The blur radius.</summary>
    public static readonly AttachedProperty<object?> BlurProperty =
        AvaloniaProperty.RegisterAttached<Visual, object?>("Blur", typeof(Follow));

    /// <summary>
    /// How much of a <see cref="Shape"/>'s stroke is drawn from its start, 0 to 1, as the <c>"Draw"</c> property's
    /// <c>"0 p"</c>; a slice that starts elsewhere is a <c>"Draw"</c> tween or <see cref="PrestoAvalonia.CreateDrawable"/>.
    /// </summary>
    public static readonly AttachedProperty<object?> DrawProperty =
        AvaloniaProperty.RegisterAttached<Shape, object?>("Draw", typeof(Follow));

    /// <summary>
    /// The builder whose duration and ease every shorthand of the visual moves with, from the next value on; unset, they
    /// follow <see cref="Springs.Gentle"/>.
    /// </summary>
    public static readonly AttachedProperty<AnimationBuilder?> TimingProperty =
        AvaloniaProperty.RegisterAttached<Visual, AnimationBuilder?>("Timing", typeof(Follow));

    /// <summary>
    /// The builder whose duration and ease every shorthand of the visual moves with while <see cref="PrestoDefaults.ReducedMotion"/>
    /// is set, from the next value on; unset, each value snaps.
    /// </summary>
    public static readonly AttachedProperty<AnimationBuilder?> WhenReducedProperty =
        AvaloniaProperty.RegisterAttached<Visual, AnimationBuilder?>("WhenReduced", typeof(Follow));

    /// <summary>Eases a shorthand's first value from what it shows, instead of snapping to it.</summary>
    public static readonly AttachedProperty<bool> AnimateInitialProperty =
        AvaloniaProperty.RegisterAttached<Visual, bool>("AnimateInitial", typeof(Follow));

    private static readonly FollowParams Immediate = new() { Duration = 0 };

    private static readonly AttachedProperty<Followed?> FollowedProperty =
        AvaloniaProperty.RegisterAttached<Visual, Followed?>("Followed", typeof(Follow));

    private static readonly AttachedProperty<object?>[] s_properties = [XProperty, YProperty, ScaleProperty, ScaleXProperty, ScaleYProperty, RotateProperty, BlurProperty, DrawProperty];
    private static readonly TransformShorthand[] s_transforms =
        [TransformShorthand.X, TransformShorthand.Y, TransformShorthand.Scale, TransformShorthand.ScaleX, TransformShorthand.ScaleY, TransformShorthand.Rotate];

    static Follow()
    {
        foreach (var property in s_properties) property.Changed.AddClassHandler<Visual>(HandleValueChanged);
        TimingProperty.Changed.AddClassHandler<Visual>(Retime);
        WhenReducedProperty.Changed.AddClassHandler<Visual>(Retime);
        AnimateInitialProperty.Changed.AddClassHandler<Visual>(Retime);
    }

    /// <summary>Gets the value <paramref name="visual"/>'s horizontal offset follows.</summary>
    public static object? GetX(Visual visual) => visual.GetValue(XProperty);

    /// <summary>Sets the value <paramref name="visual"/>'s horizontal offset follows.</summary>
    public static void SetX(Visual visual, object? value) => visual.SetValue(XProperty, value);

    /// <summary>Gets the value <paramref name="visual"/>'s vertical offset follows.</summary>
    public static object? GetY(Visual visual) => visual.GetValue(YProperty);

    /// <summary>Sets the value <paramref name="visual"/>'s vertical offset follows.</summary>
    public static void SetY(Visual visual, object? value) => visual.SetValue(YProperty, value);

    /// <summary>Gets the value <paramref name="visual"/>'s uniform scale follows.</summary>
    public static object? GetScale(Visual visual) => visual.GetValue(ScaleProperty);

    /// <summary>Sets the value <paramref name="visual"/>'s uniform scale follows.</summary>
    public static void SetScale(Visual visual, object? value) => visual.SetValue(ScaleProperty, value);

    /// <summary>Gets the value <paramref name="visual"/>'s horizontal scale follows.</summary>
    public static object? GetScaleX(Visual visual) => visual.GetValue(ScaleXProperty);

    /// <summary>Sets the value <paramref name="visual"/>'s horizontal scale follows.</summary>
    public static void SetScaleX(Visual visual, object? value) => visual.SetValue(ScaleXProperty, value);

    /// <summary>Gets the value <paramref name="visual"/>'s vertical scale follows.</summary>
    public static object? GetScaleY(Visual visual) => visual.GetValue(ScaleYProperty);

    /// <summary>Sets the value <paramref name="visual"/>'s vertical scale follows.</summary>
    public static void SetScaleY(Visual visual, object? value) => visual.SetValue(ScaleYProperty, value);

    /// <summary>Gets the value <paramref name="visual"/>'s rotation follows.</summary>
    public static object? GetRotate(Visual visual) => visual.GetValue(RotateProperty);

    /// <summary>Sets the value <paramref name="visual"/>'s rotation follows.</summary>
    public static void SetRotate(Visual visual, object? value) => visual.SetValue(RotateProperty, value);

    /// <summary>Gets the value <paramref name="visual"/>'s blur radius follows.</summary>
    public static object? GetBlur(Visual visual) => visual.GetValue(BlurProperty);

    /// <summary>Sets the value <paramref name="visual"/>'s blur radius follows.</summary>
    public static void SetBlur(Visual visual, object? value) => visual.SetValue(BlurProperty, value);

    /// <summary>Gets the share of <paramref name="shape"/>'s stroke that follows its value.</summary>
    public static object? GetDraw(Shape shape) => shape.GetValue(DrawProperty);

    /// <summary>Sets the share of <paramref name="shape"/>'s stroke that follows its value.</summary>
    public static void SetDraw(Shape shape, object? value) => shape.SetValue(DrawProperty, value);

    /// <summary>Gets the builder whose timing <paramref name="visual"/>'s shorthands follow with.</summary>
    public static AnimationBuilder? GetTiming(Visual visual) => visual.GetValue(TimingProperty);

    /// <summary>Sets the builder whose timing <paramref name="visual"/>'s shorthands follow with.</summary>
    public static void SetTiming(Visual visual, AnimationBuilder? value) => visual.SetValue(TimingProperty, value);

    /// <summary>Gets the builder whose timing <paramref name="visual"/>'s shorthands follow with under reduced motion.</summary>
    public static AnimationBuilder? GetWhenReduced(Visual visual) => visual.GetValue(WhenReducedProperty);

    /// <summary>Sets the builder whose timing <paramref name="visual"/>'s shorthands follow with under reduced motion.</summary>
    public static void SetWhenReduced(Visual visual, AnimationBuilder? value) => visual.SetValue(WhenReducedProperty, value);

    /// <summary>Gets whether <paramref name="visual"/>'s shorthands ease their first value.</summary>
    public static bool GetAnimateInitial(Visual visual) => visual.GetValue(AnimateInitialProperty);

    /// <summary>Sets whether <paramref name="visual"/>'s shorthands ease their first value.</summary>
    public static void SetAnimateInitial(Visual visual, bool value) => visual.SetValue(AnimateInitialProperty, value);

    /// <summary>
    /// Binds <paramref name="property"/> at <see cref="BindingPriority.LocalValue"/> so it eases to each value of
    /// <paramref name="source"/>, which resolves against <paramref name="target"/> as it would on the property itself.
    /// </summary>
    /// <returns>The binding; disposing it stops the animation and removes both the binding and its source.</returns>
    /// <exception cref="ArgumentException"><paramref name="source"/> is <c>TwoWay</c> or <c>OneWayToSource</c>, or the timing conflicts.</exception>
    public static IDisposable Bind(AvaloniaObject target, AvaloniaProperty property, BindingBase source, FollowParams? options = null) =>
        target.Bind(property, new FollowBinding(target, property, source, options ?? new(), followsItems: true), BindingPriority.LocalValue);

    /// <summary>Binds <paramref name="property"/> at <see cref="BindingPriority.LocalValue"/> so it eases to each value of <paramref name="source"/>.</summary>
    /// <returns>The binding; disposing it stops the animation and unsubscribes from <paramref name="source"/>.</returns>
    public static IDisposable Bind<T>(AvaloniaObject target, AvaloniaProperty<T> property, IObservable<T> source, FollowParams? options = null) =>
        target.Bind(property, new FollowBinding(target, property, source.ToBinding(), options ?? new(), followsItems: false), BindingPriority.LocalValue);

    /// <summary>
    /// Binds <paramref name="property"/> at <see cref="BindingPriority.LocalValue"/> to each flushed value of <paramref name="source"/>,
    /// converted to the property's type: written as it is, or eased to through the timing of <paramref name="options"/> when given.
    /// </summary>
    /// <returns>The binding; disposing it unsubscribes from <paramref name="source"/> and removes the binding.</returns>
    /// <exception cref="ArgumentException">The timing conflicts.</exception>
    public static IDisposable Bind(AvaloniaObject target, AvaloniaProperty property, MotionValue source, FollowParams? options = null) =>
        options == null && property is StyledProperty<double> number
            ? target.Bind(number, new MotionValueBinding(target, source), BindingPriority.LocalValue)
            : BindCell(target, property, source, options);

    /// <inheritdoc cref="Bind(AvaloniaObject, AvaloniaProperty, MotionValue, FollowParams?)"/>
    public static IDisposable Bind(AvaloniaObject target, AvaloniaProperty property, MotionObjectValue source, FollowParams? options = null) =>
        BindCell(target, property, source, options);

    private static IDisposable BindCell(AvaloniaObject target, AvaloniaProperty property, object source, FollowParams? options) =>
        target.Bind(property, new FollowBinding(target, property, new Binding { Source = source }, options ?? Immediate, followsItems: false), BindingPriority.LocalValue);

    private static void Retime(Visual visual, AvaloniaPropertyChangedEventArgs e) => visual.GetValue(FollowedProperty)?.Retime();

    private static void HandleValueChanged(Visual visual, AvaloniaPropertyChangedEventArgs e)
    {
        var index = Array.IndexOf(s_properties, e.Property);
        var followed = visual.GetValue(FollowedProperty);
        if (e.NewValue is null)
        {
            followed?.Release(index);
            return;
        }
        var value = e.NewValue is string text ? ToNumber(index, text) : e.NewValue;
        if (followed is null) visual.SetValue(FollowedProperty, followed = new Followed(visual));
        followed.Target(index, value);
    }

    private static double ToNumber(int index, string text)
    {
        var parsed = new DecomposedValue();
        ValueParser.Decompose(text, parsed);
        if (parsed.Kind is ValueKind.Number or ValueKind.Unit)
        {
            if (s_properties[index] == RotateProperty) return TransformShorthands.ToDegrees(parsed.Number, parsed.Unit);
            if (parsed.Unit is null or "px") return parsed.Number;
        }
        var units = s_properties[index] == RotateProperty ? "an angle unit" : "px";
        throw new FormatException($"Follow.{s_properties[index].Name} takes a number or a number in {units}, not \"{text}\".");
    }

    private sealed class Followed
    {
        private const int Blur = 6;
        private const int Draw = 7;

        private readonly Visual _visual;
        private readonly Follower?[] _followers = new Follower?[s_properties.Length];
        private readonly MotionValue?[] _sources = new MotionValue?[s_properties.Length];
        private readonly Action<MotionValue>?[] _sourceChanged = new Action<MotionValue>?[s_properties.Length];
        private readonly Action _itemSettled;
        private int _newItems;
        private bool _detached;

        public Followed(Visual visual)
        {
            _visual = visual;
            _detached = !visual.IsAttachedToVisualTree();
            _itemSettled = () => _newItems = 0;
            visual.DataContextChanged += OnDataContextChanged;
            visual.AttachedToVisualTree += OnAttached;
            visual.DetachedFromVisualTree += OnDetached;
        }

        public void Target(int index, object value)
        {
            Unlisten(index);
            if (value is MotionValue source)
            {
                _followers[index]?.Dispose();
                _followers[index] = null;
                _sources[index] = source;
                var changed = _sourceChanged[index] ??= moved => Write(index, moved.Value);
                if (_detached) return;
                source.Changed += changed;
                Write(index, source.Value);
                return;
            }
            if (_followers[index] is not { } follower)
            {
                follower = _followers[index] = new Follower(typeof(double), Parameters(), Current(index));
                follower.PropertyChanged += (_, _) => Show(index, follower.Value);
            }
            follower.Target(value, snap: (_detached && follower.Started) || (_newItems & (1 << index)) != 0);
            _newItems &= ~(1 << index);
        }

        public void Release(int index)
        {
            if (_followers[index] is null && _sources[index] is null) return;
            Unlisten(index);
            _followers[index]?.Dispose();
            _followers[index] = null;
            Unfollow(index);
            if (Array.Exists(_followers, f => f != null) || Array.Exists(_sources, s => s != null)) return;

            _visual.DataContextChanged -= OnDataContextChanged;
            _visual.AttachedToVisualTree -= OnAttached;
            _visual.DetachedFromVisualTree -= OnDetached;
            _visual.ClearValue(FollowedProperty);
        }

        public void Retime()
        {
            var parameters = Parameters();
            foreach (var follower in _followers) follower?.Retime(parameters);
        }

        private FollowParams Parameters() => new()
        {
            Builder = _visual.GetValue(TimingProperty),
            WhenReduced = _visual.GetValue(WhenReducedProperty),
            AnimateInitial = _visual.GetValue(AnimateInitialProperty),
        };

        private double Current(int index) =>
            index == Blur ? (double)EffectComposer.Get(_visual, EffectShorthand.Blur)
            : index == Draw ? DrawAccessor.End((Shape)_visual)
            : RenderTransformComposer.Get(_visual, s_transforms[index]);

        private void Show(int index, object? value)
        {
            if (value is double number) Write(index, number);
            else Unfollow(index);
        }

        private void Write(int index, double value)
        {
            if (index == Blur) EffectComposer.Follow(_visual, value);
            else if (index == Draw) DrawAccessor.Apply((Shape)_visual, 0, Math.Clamp(value, 0, 1));
            else RenderTransformComposer.Follow(_visual, s_transforms[index], value);
        }

        private void Unfollow(int index)
        {
            if (index == Blur) EffectComposer.Unfollow(_visual);
            else if (index == Draw) DrawAccessor.Restore((Shape)_visual);
            else RenderTransformComposer.Unfollow(_visual, s_transforms[index]);
        }

        private void Unlisten(int index)
        {
            if (_sources[index] is not { } source) return;
            _sources[index] = null;
            source.Changed -= _sourceChanged[index];
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            // The values a new item causes follow synchronously; the post clears the flags of those that did not change.
            _newItems = -1;
            Dispatcher.UIThread.Post(_itemSettled, DispatcherPriority.Background);
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _detached = true;
            foreach (var follower in _followers) follower?.Stop();
            for (var i = 0; i < _sources.Length; i++)
            {
                if (_sources[i] is { } source) source.Changed -= _sourceChanged[i];
            }
        }

        private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (!_detached) return;
            _detached = false;
            for (var i = 0; i < _sources.Length; i++)
            {
                if (_sources[i] is not { } source) continue;
                source.Changed += _sourceChanged[i];
                Write(i, source.Value);
            }
        }
    }
}
