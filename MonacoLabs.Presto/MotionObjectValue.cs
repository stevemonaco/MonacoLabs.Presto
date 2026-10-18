using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Targets;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto;

/// <summary>
/// A <see cref="MotionValue"/> mapped onto colors, brushes, structs or strings by
/// <see cref="MotionValue.Map(double[], object[], EaseValue?, bool)"/>, interpolating each output's channels separately.
/// </summary>
/// <remarks>
/// Each new <see cref="Value"/> is encoded through the outputs' codec, which boxes a struct per changed frame; a codec
/// such as the brush codec updates and returns the same instance instead. The <c>double</c> graph it follows stays
/// allocation-free.
/// </remarks>
public sealed class MotionObjectValue
{
    private readonly MotionValue _source;
    private readonly double[] _input;
    private readonly Ease _ease;
    private readonly bool _clamp;
    private readonly IValueCodec? _codec;
    private readonly Tween[] _segments;
    private readonly Action<MotionValue> _sourceChanged;
    private Action<MotionObjectValue>? _changed;
    private object? _value;
    private int _segment = -1;
    private double _progress;
    private int _flushedSegment;
    private double _flushedProgress;

    internal MotionObjectValue(MotionValue source, double[] input, object[] outputs, Ease ease, bool clamp)
    {
        _source = source;
        _input = input;
        _ease = ease;
        _clamp = clamp;
        _codec = SharedCodec(outputs);
        _sourceChanged = OnSourceChanged;
        _segments = new Tween[outputs.Length - 1];
        for (var i = 0; i < _segments.Length; i++)
        {
            var (from, to) = (new DecomposedValue(), new DecomposedValue());
            ValueParser.Decompose(outputs[i], from, _codec);
            ValueParser.Decompose(outputs[i + 1], to, _codec);
            if (_codec is IReconcilingValueCodec reconciling) reconciling.Reconcile(from, to);
            if (from.Kind != to.Kind || from.Unit != to.Unit || from.Numbers?.Length != to.Numbers?.Length
                || !from.Strings.AsSpan().SequenceEqual(to.Strings))
            {
                throw new ArgumentException("Every output must be the same kind of value with the same channels.", nameof(outputs));
            }
            _segments[i] = new Tween
            {
                _valueType = to.Kind,
                _unit = to.Unit,
                _fromNumber = from.Number,
                _toNumber = to.Number,
                _fromNumbers = Helpers.CloneNumbers(from.Numbers),
                _toNumbers = Helpers.CloneNumbers(to.Numbers),
                _numbers = Helpers.CloneNumbers(from.Numbers),
                _strings = to.Strings ?? [],
                _changeDuration = 1,
            };
        }
    }

    /// <summary>The output for the source's current value, encoded again only when its segment or eased progress moved.</summary>
    public object Value
    {
        get
        {
            var segment = Locate(out var progress);
            if (segment != _segment || progress != _progress) Encode(segment, progress);
            return _value!;
        }
    }

    /// <summary>Raised from the engine frame as <see cref="MotionValue.Changed"/> is, at most once per frame and only when the output moved.</summary>
    public event Action<MotionObjectValue>? Changed
    {
        add
        {
            if (value == null) return;
            if (_changed == null)
            {
                _source.Changed += _sourceChanged;
                _flushedSegment = Locate(out _flushedProgress);
            }
            _changed += value;
        }
        remove
        {
            if (_changed == null) return;
            _changed -= value;
            if (_changed == null) _source.Changed -= _sourceChanged;
        }
    }

    private int Locate(out double progress) => MotionValue.Locate(_source.Value, _input, _ease, _clamp, out progress) - 1;

    private void Encode(int segment, double progress)
    {
        (_segment, _progress) = (segment, progress);
        var tween = _segments[segment];
        for (var i = 0; i < tween._numbers.Length; i++) tween._numbers[i] = Helpers.Lerp(tween._fromNumbers[i], tween._toNumbers[i], progress);
        tween._number = Helpers.Lerp(tween._fromNumber, tween._toNumber, progress);
        tween._currentTime = progress;
        _value = _codec != null ? _codec.Encode(tween._number, tween) : tween.ComposeValue(tween._number);
    }

    private void OnSourceChanged(MotionValue source)
    {
        var segment = Locate(out var progress);
        if (segment == _flushedSegment && progress == _flushedProgress) return;
        (_flushedSegment, _flushedProgress) = (segment, progress);
        _changed!(this);
    }

    private static IValueCodec? SharedCodec(object[] outputs)
    {
        IValueCodec? shared = null;
        foreach (var output in outputs)
        {
            if (output is null or string || FindCodec(output.GetType()) is not { } codec) continue;
            if (shared != null && codec != shared) throw new ArgumentException("Every output must be the same kind of value.", nameof(outputs));
            shared = codec;
        }
        return shared;
    }

    private static IValueCodec? FindCodec(Type type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            if (Presto.Codecs.TryGetValue(t, out var codec)) return codec;
        }
        foreach (var (key, codec) in Presto.Codecs)
        {
            if (key.IsInterface && key.IsAssignableFrom(type)) return codec;
        }
        return null;
    }
}
