using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto;

/// <summary>
/// A per-target value derived from the target's distance to an origin. Created through
/// <see cref="Presto.Stagger"/> and accepted wherever a per-target value is (a delay, a duration, a property value), or as
/// the position of a <see cref="Timeline"/> child, which then adds one child per target.
/// </summary>
public sealed class Stagger
{
    private readonly StaggerParams _parameters;
    private readonly bool _isRange;
    private readonly double _value1;
    private readonly double _value2;
    private readonly string? _unit;
    private readonly bool _hasStart;
    private readonly TweenValue _start;
    private readonly RandomGenerator _random;
    private readonly Ease? _ease;
    private readonly double _jitterStart;
    private readonly double _jitterEnd;
    private double[] _values = [];
    private double[]? _jitterSamples;
    private double _maxValue;
    private double _fromIndex;
    private double? _cachedOffset;

    internal Stagger(TweenValue value, StaggerParams parameters)
    {
        _parameters = parameters;
        var range = value.Raw as TweenValue[];
        _isRange = range != null;
        _value1 = ParseNumber(range != null ? range[0] : value);
        _value2 = range != null ? ParseNumber(range[1]) : 0;
        _unit = (range != null ? range[1] : value).Raw is string text && ValueParser.TryParseUnit(text, out _, out var unit) ? unit : null;
        _hasStart = parameters.Start != null;
        _start = IsTruthy(parameters.Start) ? parameters.Start!.Value : _isRange ? _value1 : 0;
        _random = parameters.Seed is { } seed ? Utils.CreateSeededRandom(seed) : Utils.Random;
        _ease = parameters.Ease is { } ease ? ease.Resolve(this, 0, 1, out _) : null;
        _fromIndex = parameters.From.Index ?? 0;
        if (parameters.Jitter is { Length: > 0 } jitter)
        {
            _jitterStart = jitter[0];
            _jitterEnd = jitter.Length > 1 ? jitter[1] : jitter[0];
        }
    }

    /// <summary>
    /// The value for one target. The distances are computed on the first call and reused, so a stagger belongs to the one
    /// group of targets it is first used with.
    /// </summary>
    internal TweenValue Invoke(object target, int index, IReadOnlyList<object> targets, Timeline? timeline)
    {
        var total = _parameters.Total ?? targets.Count;
        var from = _parameters.From;
        if (from.IsCenter) _fromIndex = (total - 1) / 2d;
        else if (from.IsLast) _fromIndex = total - 1;

        var customIndex =
            _parameters.Use is { } use ? use(target, index, total) :
            _parameters.UseProperty is { } property ? JsNumber.ToNumber(Presto.ResolveAccessor(target, property).GetValue(target), double.NaN) :
            double.NaN;
        var staggerIndex = customIndex >= 0 && customIndex < total ? (int)customIndex : index;

        // Deviation: upstream keeps the first count it sees, which reads past the cache once a shared stagger meets a larger group.
        if (_values.Length != total) BuildValues(targets, total);
        _cachedOffset ??= timeline != null ? StartPosition(timeline).Parse(timeline) : JsNumber.ToNumber(_start.Raw, double.NaN);

        // A Total below the target count leaves the trailing targets without a value, which upstream reads as undefined.
        var value = staggerIndex < _values.Length ? _values[staggerIndex] : double.NaN;
        var spacing = _isRange ? (_value2 - _value1) / _maxValue : _value1;
        var spaced = spacing * Helpers.Round(value, 2);
        var output = _cachedOffset.Value + (double.IsNaN(spaced) ? 0 : spaced);
        if (_jitterSamples != null && staggerIndex < _jitterSamples.Length)
        {
            var progress = _maxValue == 0 ? 0 : value / _maxValue;
            output += _jitterSamples[staggerIndex] * (_jitterStart + ((_jitterEnd - _jitterStart) * progress));
        }
        if (_parameters.Modifier is { } modifier) output = modifier(output);
        return _unit is { } unit ? JsNumber.Format(output) + unit : output;
    }

    private void BuildValues(IReadOnlyList<object> targets, int total)
    {
        var values = new double[total];
        if (_parameters.AutoGrid)
        {
            if (!TryFillFromPositions(values, targets, total)) Fill1D(values);
        }
        else if (_parameters.Grid is { } grid)
        {
            FillGrid(values, grid);
        }
        else
        {
            Fill1D(values);
        }

        _maxValue = values[0];
        for (var i = 1; i < total; i++)
        {
            if (values[i] > _maxValue) _maxValue = values[i];
        }

        if (_ease != null || _parameters.Reversed)
        {
            for (var i = 0; i < total; i++)
            {
                var value = _ease != null ? _ease(values[i] / _maxValue) * _maxValue : values[i];
                values[i] = _parameters.Reversed ? _parameters.Axis != null ? -value : Math.Abs(_maxValue - value) : value;
            }
        }

        if (_parameters.Jitter != null)
        {
            _jitterSamples = new double[total];
            for (var i = 0; i < total; i++) _jitterSamples[i] = _random(-1, 1, 4);
        }

        if (_parameters.From.IsRandom) Utils.Shuffle(values, _random);
        _values = values;
    }

    private void Fill1D(double[] values)
    {
        for (var i = 0; i < values.Length; i++) values[i] = Math.Abs(_fromIndex - i);
    }

    private void FillGrid(double[] values, int[] grid)
    {
        var is3D = grid.Length == 3;
        var layer = grid[0] * grid[1];
        var from = _parameters.From;
        double fromX, fromY, fromZ;
        if (from.Position is { } position)
        {
            fromX = position[0] * (grid[0] - 1);
            fromY = position[1] * (grid[1] - 1);
            fromZ = is3D ? (position.Length >= 3 ? position[2] : .5) * (grid[2] - 1) : 0;
        }
        else if (from.IsCenter)
        {
            fromX = (grid[0] - 1) / 2d;
            fromY = (grid[1] - 1) / 2d;
            fromZ = is3D ? (grid[2] - 1) / 2d : 0;
        }
        else
        {
            fromX = _fromIndex % grid[0];
            fromY = Math.Floor(_fromIndex / grid[0]) % grid[1];
            fromZ = is3D ? Math.Floor(_fromIndex / layer) : 0;
        }
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = Distance(
                fromX - (i % grid[0]),
                fromY - (Math.Floor((double)i / grid[0]) % grid[1]),
                is3D ? fromZ - Math.Floor((double)i / layer) : 0,
                is3D);
        }
    }

    private bool TryFillFromPositions(double[] values, IReadOnlyList<object> targets, int total)
    {
        var xs = new double[total];
        var ys = new double[total];
        var zs = new double[total];
        var has3D = false;
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;
        for (var i = 0; i < total; i++)
        {
            if (i >= targets.Count) return false;
            if (Coordinates(targets[i]) is not { } coordinates) return false;
            var x = coordinates[0];
            var y = coordinates[1];
            var z = 0d;
            if (coordinates.Length >= 3)
            {
                z = coordinates[2];
                has3D = true;
            }
            xs[i] = x;
            ys[i] = y;
            zs[i] = z;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            minZ = Math.Min(minZ, z);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            maxZ = Math.Max(maxZ, z);
        }

        var from = _parameters.From;
        double fromX = xs[0], fromY = ys[0], fromZ = zs[0];
        if (from.Position is { } position)
        {
            fromX = minX + (position[0] * (maxX - minX));
            fromY = minY + (position[1] * (maxY - minY));
            fromZ = has3D ? minZ + ((position.Length >= 3 ? position[2] : .5) * (maxZ - minZ)) : 0;
        }
        else if (from.IsCenter)
        {
            fromX = (minX + maxX) / 2;
            fromY = (minY + maxY) / 2;
            fromZ = (minZ + maxZ) / 2;
        }
        else if (from.IsLast)
        {
            fromX = xs[total - 1];
            fromY = ys[total - 1];
            fromZ = zs[total - 1];
        }
        else if (from.Index is { } index)
        {
            fromX = xs[index];
            fromY = ys[index];
            fromZ = zs[index];
        }

        var minDistance = double.PositiveInfinity;
        for (var i = 0; i < total; i++)
        {
            values[i] = Distance(fromX - xs[i], fromY - ys[i], fromZ - zs[i], has3D);
            var distance = Math.Abs(values[i]);
            if (distance > 0 && distance < minDistance) minDistance = distance;
        }
        // Normalized so the closest pair of cells sits one unit apart, matching an explicit grid's spacing.
        if (!double.IsPositiveInfinity(minDistance))
        {
            for (var i = 0; i < total; i++) values[i] /= minDistance;
        }
        return true;
    }

    private double Distance(double x, double y, double z, bool is3D) => _parameters.Axis switch
    {
        StaggerAxis.X => -x,
        StaggerAxis.Y => -y,
        StaggerAxis.Z => -z,
        _ => Math.Sqrt((x * x) + (y * y) + (is3D ? z * z : 0)),
    };

    private TimelinePosition StartPosition(Timeline timeline)
    {
        if (!_hasStart) return timeline.IterationDuration;
        return _start.Raw is string label ? label : JsNumber.ToNumber(_start.Raw, double.NaN);
    }

    private double[]? Coordinates(object target)
    {
        if (_parameters.Coordinates?.Invoke(target) is { Length: >= 2 } measured) return measured;
        var x = Coordinate(target, "X");
        var y = Coordinate(target, "Y");
        if (double.IsNaN(x) || double.IsNaN(y)) return null;
        var z = Coordinate(target, "Z");
        return double.IsNaN(z) ? [x, y] : [x, y, z];
    }

    private static double Coordinate(object target, string property)
    {
        try
        {
            return JsNumber.ToNumber(Presto.ResolveAccessor(target, property).GetValue(target), double.NaN);
        }
        catch (ArgumentException)
        {
            // The built-in adapter reports an unknown member by throwing, where upstream simply reads undefined.
            return double.NaN;
        }
    }

    private static bool IsTruthy(TweenValue? value) => value?.Raw switch
    {
        double number => number != 0,
        string text => text.Length > 0,
        _ => false,
    };

    // Like JS parseFloat, so a value carrying a unit keeps its number.
    private static double ParseNumber(TweenValue value) =>
        value.Raw is string text && ValueParser.TryParseUnit(text, out var number, out _) ? number : JsNumber.ToNumber(value.Raw, double.NaN);
}
