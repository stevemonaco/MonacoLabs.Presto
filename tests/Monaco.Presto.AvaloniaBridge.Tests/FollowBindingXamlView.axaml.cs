using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Utilities;
using Monaco.Presto.AvaloniaBridge.Bindings;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>The places an <c>{presto:Follow}</c>-shaped extension can sit, each wrapping a different kind of binding (GAPS-PLAN §1.7).</summary>
public sealed partial class FollowBindingXamlView : UserControl
{
    public FollowBindingXamlView() => InitializeComponent();
}

public sealed class LevelViewModel : INotifyPropertyChanged
{
    private double _level = 20;

    public event PropertyChangedEventHandler? PropertyChanged;

    public double Level
    {
        get => _level;
        set
        {
            _level = value;
            PropertyChanged?.Invoke(this, new(nameof(Level)));
        }
    }

    public LevelViewModel? Child { get; init; }

    public MotionValue Motion { get; init; } = Presto.CreateMotionValue(20);
}

public sealed class Doubler : IValueConverter
{
    public static Doubler Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is double d ? d * 2 : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// The plumbing of <c>{presto:Follow}</c> without the animation: the wrapped binding is bound to a companion attached
/// property on the target, and each companion value is passed straight through a follower returned as <c>ToBinding()</c>.
/// </summary>
public sealed class ProbeExtension(BindingBase binding) : MarkupExtension
{
    public static List<(object? TargetObject, object? TargetProperty)> Provided { get; } = [];

    /// <summary>Each target's events in order, as <c>"Name:Event"</c>, with unnamed targets named by their hash.</summary>
    public static List<string> Log { get; } = [];

    public static string NameOf(AvaloniaObject target) =>
        (target as StyledElement)?.Name is { Length: > 0 } name ? name : $"#{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target)}";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var provided = (IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget))!;
        Provided.Add((provided.TargetObject, provided.TargetProperty));
        if (provided is not { TargetObject: AvaloniaObject target, TargetProperty: AvaloniaProperty property }) return binding;

        var companion = CompanionProperties.For(property);
        var follower = new ProbeFollower(NameOf(target));
        if (target is StyledElement element) element.DataContextChanged += (_, _) => Log.Add($"{NameOf(target)}:DataContextChanged");
        target.PropertyChanged += (_, e) =>
        {
            if (e.Property != companion) return;
            Log.Add($"{NameOf(target)}:Companion={e.NewValue}");
            // The companion is untyped, so a FallbackValue arrives as the string XAML wrote and a recycled row's as null.
            follower.Push(TypeUtilities.TryConvert(property.PropertyType, e.NewValue, CultureInfo.InvariantCulture, out var value) ? value : AvaloniaProperty.UnsetValue);
        };
        target.Bind(companion, binding);
        return follower.ToBinding();
    }

    private sealed class ProbeFollower(string name) : IObservable<object?>, IDisposable
    {
        private IObserver<object?>? _observer;
        // Not null: the value store throws if a subscribe hands a value-typed property null.
        private object? _value = AvaloniaProperty.UnsetValue;

        public void Push(object? value)
        {
            _value = value;
            _observer?.OnNext(value);
        }

        public IDisposable Subscribe(IObserver<object?> observer)
        {
            _observer = observer;
            observer.OnNext(_value);
            return this;
        }

        public void Dispose()
        {
            Log.Add($"{name}:Unsubscribed");
            _observer = null;
        }
    }
}
