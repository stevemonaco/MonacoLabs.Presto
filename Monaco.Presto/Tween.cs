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
    // Set only while channels carry their own momentum, each from its own velocity.
    internal Ease[]? _channelEases;
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
    internal Func<double, string>? _textModifier;
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
        Id = Interlocked.Increment(ref s_tweenId);
        Parent = parent;
        Property = property;
        Target = target;
        Accessor = accessor;
    }

    /// <summary>A tween outside any animation, which only carries channels for a <see cref="MotionObjectValue"/> to encode.</summary>
    internal Tween()
    {
        Parent = null!;
        Property = "";
        Target = null!;
        Accessor = null!;
    }

    /// <summary>Process-wide sequence number, assigned in creation order.</summary>
    public int Id { get; }

    /// <summary>The animation this tween belongs to.</summary>
    public Timer Parent { get; }

    /// <summary>The name of the property being animated.</summary>
    public string Property { get; }

    /// <summary>The object whose property is being animated.</summary>
    public object Target { get; }

    /// <summary>The accessor that reads and writes <see cref="Property"/> on <see cref="Target"/>.</summary>
    public IPropertyAccessor Accessor { get; }

    /// <summary>Lerps every channel of a complex value into <c>_numbers</c>, the numeric half of upstream <c>composeComplexValue</c>.</summary>
    internal void ComposeComplexNumbers(double progress, double time, int precision)
    {
        var channelEases = _channelEases;
        for (var i = 0; i < _toNumbers.Length; i++)
        {
            var channelProgress = channelEases == null ? progress : channelEases[i](time);
            _numbers[i] = _modifier(Helpers.Round(Helpers.Lerp(_fromNumbers[i], _toNumbers[i], channelProgress), precision));
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

    /// <summary>Builds the value a target receives for the current <c>_number</c>/<c>_numbers</c>: a plain number only when the tween is numeric and has no text modifier.</summary>
    internal object ComposeValue(double number) => _valueType == ValueKind.Number && _textModifier == null ? number : ComposeString(number);

    /// <summary>The always-string form of <see cref="ComposeValue"/>, for targets whose property is typed as a string.</summary>
    internal string ComposeString(double number) => _textModifier != null ? _textModifier(number) : _valueType switch
    {
        ValueKind.Unit => JsNumber.Format(number) + _unit,
        ValueKind.Color => PrestoColor.FormatRgba(_numbers),
        ValueKind.Complex => ComposeComplexString(),
        _ => JsNumber.Format(number),
    };

    internal Tween Clone() => (Tween)MemberwiseClone();
}
