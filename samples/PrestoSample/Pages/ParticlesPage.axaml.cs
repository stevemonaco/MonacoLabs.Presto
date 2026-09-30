using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Compositor;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

/// <summary>
/// An emitter playground over a <see cref="ParticleScene"/>: every control feeds one <see cref="EmitterParams"/>, printed
/// beneath the stage, and the live count is read from the scene on the render thread a few times a second.
/// </summary>
public partial class ParticlesPage : ShowcasePage
{
    private static readonly Color[][] Palettes =
    [
        [Palette.Coral.Color, Palette.Amber.Color, Palette.Lime.Color, Palette.Aqua.Color, Palette.Accent.Color, Palette.Violet.Color],
        [Palette.Amber.Color, Palette.Coral.Color, Color.FromRgb(0xFF, 0xE8, 0xA3)],
        [Palette.AccentPale.Color, Palette.AccentBright.Color, Palette.Aqua.Color],
        [Palette.Ink.Color],
    ];

    private readonly (ParticleShape Shape, string Code, double Size)[] _shapes;

    // Touched only on the render thread, from Invoke.
    private readonly List<ParticleSystem> _systems = [];

    private Point? _origin;
    private double _angle = -90;
    private bool _aiming;
    private bool _streaming;

    public ParticlesPage()
    {
        InitializeComponent();
        _shapes =
        [
            (ParticleShape.Rect, "ParticleShape.Rect", 8),
            (ParticleShape.RoundedRect(3), "ParticleShape.RoundedRect(3)", 10),
            (ParticleShape.Circle, "ParticleShape.Circle", 8),
            (ParticleShape.Streak, "ParticleShape.Streak", 3),
            (ParticleShape.Glyph('*'), "ParticleShape.Glyph('*')", 22),
            (ParticleShape.Bitmap(new Bitmap(AssetLoader.Open(new Uri("avares://PrestoSample/Assets/Icon.png")))), "ParticleShape.Bitmap(icon)", 24),
            (ParticleShape.FromGeometry((Geometry)Application.Current!.FindResource("HeartIcon")!, new Size(16, 16)), "ParticleShape.FromGeometry(heart, new Size(16, 16))", 16),
        ];
        Code.Text = Format(Params());
    }

    protected override void OnAttached()
    {
        Particles.Scene = new ParticleScene();
        Presto.CreateTimer(new()
        {
            FrameRate = 4,
            OnUpdate = _ => Particles.Invoke<ParticleScene>(scene =>
            {
                var live = scene.Live;
                Dispatcher.UIThread.Post(() => LiveCount.Text = $"{live} live");
            }),
        });
    }

    private EmitterParams Params()
    {
        var (shape, _, size) = _shapes[ShapeBox.SelectedIndex];
        return new()
        {
            Rate = RateSlider.Value,
            Burst = (int)BurstSlider.Value,
            Lifetime = (LifetimeSlider.Value * .6, LifetimeSlider.Value),
            Speed = (SpeedSlider.Value / 2, SpeedSlider.Value),
            Angle = _angle,
            Spread = SpreadSlider.Value,
            Gravity = GravitySlider.Value,
            Drag = DragSlider.Value,
            Spin = (-SpinSlider.Value, SpinSlider.Value),
            Size = new(size, size),
            Colors = [.. Palettes[PaletteBox.SelectedIndex].Select(ColorExtensions.ToPresto)],
            Flutter = FlutterSlider.Value,
            Shape = shape,
        };
    }

    private string Format(EmitterParams p)
    {
        var colors = string.Join(", ", Palettes[PaletteBox.SelectedIndex].Select(c => $"new(0x{c.R:X2}, 0x{c.G:X2}, 0x{c.B:X2})"));
        return $$"""
            new EmitterParams
            {
                Rate = {{N(p.Rate)}},
                Burst = {{p.Burst}},
                Lifetime = ({{N(p.Lifetime.Min)}}, {{N(p.Lifetime.Max)}}),
                Speed = ({{N(p.Speed.Min)}}, {{N(p.Speed.Max)}}),
                Angle = {{N(p.Angle)}},
                Spread = {{N(p.Spread)}},
                Gravity = {{N(p.Gravity)}},
                Drag = {{N(p.Drag)}},
                Spin = ({{N(p.Spin.Min)}}, {{N(p.Spin.Max)}}),
                Size = new({{N(p.Size.From)}}, {{N(p.Size.To)}}),
                Colors = [{{colors}}],
                Flutter = {{N(p.Flutter)}},
                Shape = {{_shapes[ShapeBox.SelectedIndex].Code}},
            }
            """;
    }

    private static string N(double value) => value.ToString("0.#####", CultureInfo.InvariantCulture);

    private void SettingsChanged(object? sender, RoutedEventArgs e)
    {
        if (_shapes is null) return;
        var p = Params();
        Code.Text = Format(p);
        if (_streaming) Emit(p with { Burst = 0 });
    }

    private void Emit(EmitterParams parameters)
    {
        var origin = _origin ?? new Point(Particles.Bounds.Width / 2, Particles.Bounds.Height * .75);
        Particles.Invoke<ParticleScene>(scene =>
        {
            foreach (var system in _systems) system.Stop();
            _systems.RemoveAll(system => system.IsFinished);
            _systems.Add(scene.Add(parameters, origin));
        });
        _streaming = parameters.Rate > 0;
    }

    private void EmitClicked(object? sender, RoutedEventArgs e) => Emit(Params());

    private void StopClicked(object? sender, RoutedEventArgs e)
    {
        Particles.Invoke<ParticleScene>(_ =>
        {
            foreach (var system in _systems) system.Stop();
        });
        _streaming = false;
    }

    private void ClearClicked(object? sender, RoutedEventArgs e)
    {
        Particles.Invoke<ParticleScene>(_ =>
        {
            foreach (var system in _systems)
            {
                system.Stop();
                system.Clear();
            }
            _systems.Clear();
        });
        _streaming = false;
    }

    private void StagePressed(object? sender, PointerPressedEventArgs e)
    {
        _origin = e.GetPosition(Particles);
        _aiming = true;
    }

    private void StageMoved(object? sender, PointerEventArgs e)
    {
        if (!_aiming) return;
        var delta = e.GetPosition(Particles) - _origin!.Value;
        if (Math.Abs(delta.X) + Math.Abs(delta.Y) < 8) return;
        _angle = Math.Round(Utils.RadToDeg(Math.Atan2(delta.Y, delta.X)));
        Code.Text = Format(Params());
    }

    private void StageReleased(object? sender, PointerReleasedEventArgs e)
    {
        _aiming = false;
        Emit(Params());
    }
}
