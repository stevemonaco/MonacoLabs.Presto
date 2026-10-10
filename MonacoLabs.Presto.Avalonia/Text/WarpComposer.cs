using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MonacoLabs.Presto.Targets;
using MonacoLabs.Presto.Diagnostics;
using Path = Avalonia.Controls.Shapes.Path;

namespace MonacoLabs.Presto.Text;

/// <summary>
/// The warp shorthands of one split <see cref="TextBlock"/>: once per engine frame in which any of them moved, every flattened
/// outline point of every outline piece is mapped in block coordinates through the <see cref="TextWarp.KindProperty"/> function,
/// and the result is written as the piece's <see cref="Path.Data"/> at <see cref="Avalonia.Data.BindingPriority.Animation"/>.
/// </summary>
/// <remarks>
/// Every kind but <see cref="TextWarpKind.Jelly"/> is a pure function of the shorthands, so seeking reproduces a frame. Jelly
/// keeps a spring per point, kicked by the moves of <c>WarpOrigin</c> and stepped on the engine until it settles.
/// </remarks>
internal sealed class WarpComposer
{
    private const double Radius = 100;
    // Jelly per millisecond: 400/s² stiffness and 8/s damping ring at about 3 Hz with a damping ratio of 0.2.
    private const double Stiffness = 0.0004;
    private const double Damping = 0.008;
    private const double Kick = 0.01;
    private const double MaxStep = 4;
    private const double Rest = 0.01;

    private static readonly ConditionalWeakTable<TextBlock, WarpComposer> s_composers = new();

    private readonly TextBlock _block;
    private readonly MotionValue _requests = new(0);
    private readonly Action<MotionValue> _write;
    private readonly List<Control> _pieces = [];
    private readonly List<AnimatedValue<Geometry?>?> _written = [];
    private TextSplitter? _hooked;
    private int _animated;
    private double _warp;
    private double _phase;
    private Point? _origin;
    private double _radius = Radius;
    private double[] _offsets = [];
    private double[] _velocities = [];
    private Timer? _jelly;
    private double _stepped;
    private bool _moving;

    private WarpComposer(TextBlock block)
    {
        _block = block;
        _write = _ => Write();
        _requests.Changed += _write;
    }

    public static WarpShorthand? Parse(string name) => name switch
    {
        "Warp" => WarpShorthand.Warp,
        "WarpPhase" => WarpShorthand.WarpPhase,
        "WarpOrigin" => WarpShorthand.WarpOrigin,
        "WarpRadius" => WarpShorthand.WarpRadius,
        _ => null,
    };

    public static object Identity(Visual block, WarpShorthand shorthand) => shorthand switch
    {
        WarpShorthand.WarpOrigin => new Rect(block.Bounds.Size).Center,
        WarpShorthand.WarpRadius => Radius,
        _ => 0d,
    };

    public static object Get(TextBlock block, WarpShorthand shorthand)
    {
        if (!s_composers.TryGetValue(block, out var composer)) return Identity(block, shorthand);
        return shorthand switch
        {
            WarpShorthand.Warp => composer._warp,
            WarpShorthand.WarpPhase => composer._phase,
            WarpShorthand.WarpOrigin => composer.Origin,
            _ => composer._radius,
        };
    }

    public static void Set(TextBlock block, WarpShorthand shorthand, double value)
    {
        if (Take(block, shorthand) is not { } composer) return;
        switch (shorthand)
        {
            case WarpShorthand.Warp:
                composer._warp = value;
                break;
            case WarpShorthand.WarpPhase:
                composer._phase = value;
                break;
            default:
                composer._radius = value;
                break;
        }
        composer.Request();
    }

    public static void SetOrigin(TextBlock block, Point origin)
    {
        if (Take(block, WarpShorthand.WarpOrigin) is not { } composer) return;
        composer.Excite(origin - composer.Origin);
        composer._origin = origin;
        composer.Request();
    }

    /// <summary>Releases one shorthand back to its identity; the last of them gives the pieces back their outlines.</summary>
    public static void Revert(TextBlock block, WarpShorthand shorthand)
    {
        if (!s_composers.TryGetValue(block, out var composer)) return;
        composer._animated &= ~(1 << (int)shorthand);
        composer.Reset(shorthand);
        if (composer._animated != 0)
        {
            composer.Request();
            return;
        }
        composer._requests.Changed -= composer._write;
        composer.Forget();
        s_composers.Remove(block);
    }

    public static void KindChanged(TextBlock block)
    {
        if (!s_composers.TryGetValue(block, out var composer)) return;
        composer.Settle();
        composer.Request();
    }

    /// <summary>Where <paramref name="point"/>, in the coordinates of a block of <paramref name="size"/>, lands under one of the pure kinds.</summary>
    internal static Point Warp(TextWarpKind kind, Point point, double amount, double phase, Point origin, double radius, Size size, double fontSize)
    {
        var (x, y) = (point.X, point.Y);
        switch (kind)
        {
            case TextWarpKind.Wave:
                return new Point(x, y + amount * fontSize / 2 * Math.Sin(2 * Math.PI * x / radius - phase));
            case TextWarpKind.Bend:
            {
                var sweep = amount * Math.PI;
                if (sweep == 0 || size.Width <= 0) return point;
                var bend = size.Width / sweep;
                var (cx, cy) = (size.Width / 2, size.Height / 2);
                var reach = bend - (y - cy);
                var (sin, cos) = Math.SinCos((x - cx) / bend);
                return new Point(cx + reach * sin, cy + bend - reach * cos);
            }
            case TextWarpKind.Bulge:
            {
                var distance = Point.Distance(point, origin);
                if (distance >= radius) return point;
                var falloff = 1 - distance / radius;
                return origin + (point - origin) * (1 + amount * falloff * falloff);
            }
            case TextWarpKind.Twist:
            {
                var (sin, cos) = Math.SinCos(amount * Point.Distance(point, origin) / radius);
                var (dx, dy) = (x - origin.X, y - origin.Y);
                return new Point(origin.X + dx * cos - dy * sin, origin.Y + dx * sin + dy * cos);
            }
            case TextWarpKind.Melt:
            {
                var column = x / (fontSize / 2);
                var left = (int)Math.Floor(column);
                var t = column - left;
                var drip = 0.25 + 0.75 * Utils.Lerp(Seeded(left), Seeded(left + 1), t * t * (3 - 2 * t));
                return new Point(x, y + amount * fontSize * drip * Depth(y, size));
            }
            case TextWarpKind.Perspective:
            {
                var cx = size.Width / 2;
                return new Point(cx + (x - cx) * (1 - amount * (1 - Depth(y, size))), y);
            }
            default:
                return point;
        }
    }

    private static double Depth(double y, Size size) => size.Height > 0 ? Math.Clamp(y / size.Height, 0, 1) : 0;

    private static double Seeded(int column)
    {
        var hash = (uint)column * 0x9E3779B9u;
        hash ^= hash >> 16;
        hash *= 0x85EBCA6Bu;
        hash ^= hash >> 13;
        return hash / (double)uint.MaxValue;
    }

    /// <summary>Marks <paramref name="shorthand"/> animated; under reduced motion holds it at its identity and returns null.</summary>
    private static WarpComposer? Take(TextBlock block, WarpShorthand shorthand)
    {
        var composer = s_composers.GetValue(block, static b => new WarpComposer(b));
        composer._animated |= 1 << (int)shorthand;
        if (!Engine.Current.Defaults.ReducedMotion) return composer;
        composer.Reset(shorthand);
        composer.Request();
        return null;
    }

    private void Reset(WarpShorthand shorthand)
    {
        switch (shorthand)
        {
            case WarpShorthand.Warp:
                _warp = 0;
                break;
            case WarpShorthand.WarpPhase:
                _phase = 0;
                break;
            case WarpShorthand.WarpOrigin:
                _origin = null;
                break;
            default:
                _radius = Radius;
                break;
        }
    }

    private Point Origin => _origin ?? new Rect(_block.Bounds.Size).Center;

    private bool IsJelly => TextWarp.GetKind(_block) == TextWarpKind.Jelly;

    private void Request() => _requests.Set(_requests.Value + 1);

    private void Write()
    {
        if (TextSplitter.Of(_block) is not { } split || !Sync(split))
        {
            Forget();
            return;
        }
        if (_warp == 0 || (IsJelly && !_moving))
        {
            Restore();
            return;
        }

        var kind = TextWarp.GetKind(_block);
        var (origin, size, fontSize) = (Origin, _block.Bounds.Size, _block.FontSize);
        var index = 0;
        for (var i = 0; i < _pieces.Count; i++)
        {
            if (TextSplitter.ContoursOf(_pieces[i]) is not { } contours) continue;
            var slot = split.UncheckedSlotOf(_pieces[i]).Position;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.SetFillRule(FillRule.NonZero);
                foreach (var contour in contours.Contours)
                {
                    var points = contour.Points;
                    for (var p = 0; p < points.Length; p++, index++)
                    {
                        var warped = kind == TextWarpKind.Jelly
                            ? points[p] + new Vector(_offsets[2 * index], _offsets[2 * index + 1])
                            : Warp(kind, points[p] + slot, _warp, _phase, origin, _radius, size, fontSize) - slot;
                        if (p == 0) context.BeginFigure(warped, true);
                        else context.LineTo(warped);
                    }
                    context.EndFigure(true);
                }
            }
            (_written[i] ??= new AnimatedValue<Geometry?>(_pieces[i], Path.DataProperty)).Push(geometry);
        }
    }

    /// <summary>Takes up the split's current pieces, starting afresh after a re-split; false when there are none.</summary>
    private bool Sync(TextSplitter split)
    {
        if (_hooked != split)
        {
            _hooked = split;
            var block = _block;
            split.AddEffect(_ =>
            {
                if (s_composers.TryGetValue(block, out var composer)) composer.Request();
                return (Action?)null;
            });
        }
        var chars = split.Chars;
        var same = chars.Count == _pieces.Count;
        for (var i = 0; same && i < chars.Count; i++) same = chars[i] == _pieces[i];
        if (same) return chars.Count > 0;

        Forget();
        _pieces.AddRange(chars);
        var count = 0;
        foreach (var piece in chars)
        {
            _written.Add(null);
            if (TextSplitter.ContoursOf(piece) is not { } contours) continue;
            foreach (var contour in contours.Contours) count += contour.Points.Length;
        }
        _offsets = new double[2 * count];
        _velocities = new double[2 * count];
        return chars.Count > 0;
    }

    private void Excite(Vector moved)
    {
        if (!IsJelly || _warp == 0 || moved == default || TextSplitter.Of(_block) is not { } split || !Sync(split)) return;
        var origin = Origin + moved;
        var index = 0;
        foreach (var piece in _pieces)
        {
            if (TextSplitter.ContoursOf(piece) is not { } contours) continue;
            var slot = split.UncheckedSlotOf(piece).Position;
            foreach (var contour in contours.Contours)
            {
                foreach (var point in contour.Points)
                {
                    var falloff = Math.Max(0, 1 - Point.Distance(point + slot, origin) / _radius);
                    _velocities[2 * index] += _warp * falloff * moved.X * Kick;
                    _velocities[2 * index + 1] += _warp * falloff * moved.Y * Kick;
                    index++;
                }
            }
        }
        if (_moving) return;
        _moving = true;
        _stepped = Engine.Current.Now;
        if (_jelly == null)
        {
            using var diagnostics = PrestoDiagnostics.Origin("TextWarp");
            _jelly = Presto.CreateTimer(new TimerParams { Loop = true, OnUpdate = _ => Step() });
        }
        else
        {
            _jelly.Resume();
        }
    }

    private void Step()
    {
        var now = Engine.Current.Now;
        var elapsed = now - _stepped;
        _stepped = now;
        var moving = false;
        for (var i = 0; i < _offsets.Length; i++)
        {
            for (var left = elapsed; left > 0; left -= MaxStep)
            {
                var h = Math.Min(left, MaxStep);
                _velocities[i] += (-Stiffness * _offsets[i] - Damping * _velocities[i]) * h;
                _offsets[i] += _velocities[i] * h;
            }
            moving |= Math.Abs(_offsets[i]) >= Rest || Math.Abs(_velocities[i]) * 1000 >= Rest;
        }
        if (!moving) Settle();
        Request();
    }

    private void Settle()
    {
        _moving = false;
        _jelly?.Pause();
        Array.Clear(_offsets);
        Array.Clear(_velocities);
    }

    private void Restore()
    {
        for (var i = 0; i < _written.Count; i++)
        {
            _written[i]?.Dispose();
            _written[i] = null;
        }
    }

    private void Forget()
    {
        Restore();
        _pieces.Clear();
        _written.Clear();
        _jelly?.Revert();
        _jelly = null;
        _moving = false;
        _offsets = _velocities = [];
    }
}
