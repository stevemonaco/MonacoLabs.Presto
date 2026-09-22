using System.Text;
using Monaco.Presto.Core;
using Monaco.Presto.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto;

/// <summary>
/// One (target, property) interpolation: the from/to numbers and channels, the ease, the timings relative to its animation
/// and the composition links to the sibling tweens animating the same property.
/// </summary>
public sealed class Tween
{
    private static int s_tweenId;

    internal object? _value;
    internal Func<TweenValue>? _toFunc;
    internal Func<TweenValue>? _fromFunc;
    internal Ease _ease = Eases.None;
    // Unused arrays share Array.Empty; color and complex tweens get fresh ones because rendering mutates _numbers in place.
    internal double[] _fromNumbers = [];
    internal double[] _toNumbers = [];
    internal string[] _strings = [];
    internal double _fromNumber;
    internal double _toNumber;
    internal double[] _numbers = [];
    internal double _number;
    internal string? _unit;
    internal Func<double, double> _modifier = static v => v;
    internal double _currentTime;
    internal double _startTime;
    internal double _delay;
    internal double _updateDuration;
    internal double _changeDuration;
    internal double _absoluteStartTime;
    internal double _absoluteUpdateStartTime;
    internal double _absoluteEndTime;
    internal bool _hasFromValue;
    internal ValueKind _valueType;
    internal Composition _composition;
    internal bool _isOverlapped;
    internal bool _isOverridden;
    internal object? _inlineValue;
    internal Tween? _prevRep;
    internal Tween? _nextRep;
    internal Tween? _prevAdd;
    internal Tween? _nextAdd;
    internal Tween? _prev;
    internal Tween? _next;

    internal Tween(Timer parent, string property, object target, IPropertyAccessor accessor)
    {
        Id = ++s_tweenId;
        Parent = parent;
        Property = property;
        Target = target;
        Accessor = accessor;
    }

    public int Id { get; }

    public Timer Parent { get; }

    public string Property { get; }

    public object Target { get; }

    public IPropertyAccessor Accessor { get; }

    /// <summary>Lerps every channel of a complex value into <c>_numbers</c>, the numeric half of upstream <c>composeComplexValue</c>.</summary>
    internal void ComposeComplexNumbers(double progress, int precision)
    {
        for (var i = 0; i < _toNumbers.Length; i++)
        {
            _numbers[i] = _modifier(Helpers.Round(Helpers.Lerp(_fromNumbers[i], _toNumbers[i], progress), precision));
        }
    }

    /// <summary>Joins <c>_strings</c> around the current <c>_numbers</c>, as upstream <c>composeComplexValue</c> does once it has lerped them.</summary>
    internal string ComposeComplexString()
    {
        var composed = new StringBuilder(_strings[0]);
        for (var i = 0; i < _numbers.Length; i++)
        {
            composed.Append(JsNumber.Format(_numbers[i]));
            composed.Append(_strings[i + 1]);
        }
        return composed.ToString();
    }

    /// <summary>Builds the value a target receives for the current <c>_number</c>/<c>_numbers</c>: a string for every kind but <see cref="ValueKind.Number"/>.</summary>
    internal object ComposeValue(double number) => _valueType == ValueKind.Number ? number : ComposeString(number);

    /// <summary>The always-string form of <see cref="ComposeValue"/>, for targets whose property is typed as a string.</summary>
    internal string ComposeString(double number) => _valueType switch
    {
        ValueKind.Unit => JsNumber.Format(number) + _unit,
        ValueKind.Color => PrestoColor.FormatRgba(_numbers),
        ValueKind.Complex => ComposeComplexString(),
        _ => JsNumber.Format(number),
    };

    internal Tween Clone() => (Tween)MemberwiseClone();
}
