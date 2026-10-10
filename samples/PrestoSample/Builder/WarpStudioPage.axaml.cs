using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Text;
using PrestoSample.Showcase;
using Timer = MonacoLabs.Presto.Timer;

namespace PrestoSample.Builder;

/// <summary>A headline's outline pieces warped through the <see cref="PrestoKeys.Warp"/> shorthands; every control edits the call, which replays and prints itself.</summary>
public partial class WarpStudioPage : ShowcasePage
{
    private const double PhaseLoop = 2400;

    internal sealed record Preset(string Name, TextWarpKind Kind, double Warp, double Radius, double Stagger, double Duration, string Ease,
        bool Animate = false, bool Follow = false);

    internal static readonly Preset[] Presets =
    [
        new("Flag", TextWarpKind.Wave, 0.5, 160, 120, 800, "outQuad", Animate: true),
        new("Rainbow arc", TextWarpKind.Bend, 0.6, 100, 90, 1000, "outBack"),
        new("Magnifier", TextWarpKind.Bulge, 0.9, 70, 0, 500, "outQuad", Follow: true),
        new("Whirlpool", TextWarpKind.Twist, 0.6, 240, 100, 1400, "inOutQuint"),
        new("Drip", TextWarpKind.Melt, 1, 100, 150, 2600, "inOutSine"),
        new("Star Wars crawl", TextWarpKind.Perspective, 0.6, 100, 0, 1200, "outQuad"),
        new("Jelly", TextWarpKind.Jelly, 1, 150, 0, 300, "outQuad", Follow: true),
    ];

    private Scope? _run;
    private Timer? _debounce;
    private double _duration = Presets[0].Duration;
    private string _ease = Presets[0].Ease;

    public WarpStudioPage()
    {
        InitializeComponent();
        KindBox.ItemsSource = Enum.GetValues<TextWarpKind>();
        KindBox.SelectedIndex = 0;
        foreach (var preset in Presets)
        {
            var button = new Button { Content = preset.Name };
            button.Click += (_, _) => Apply(preset);
            PresetRow.Children.Add(button);
        }
    }

    public IReadOnlyList<TextSplitter> Splits { get; private set; } = [];

    public Animation? Warp { get; private set; }

    public Animation? Phase { get; private set; }

    protected override void OnAttached()
    {
        Scope.Add(_ => () => _run?.Revert());
        AfterFirstArrange(Lines, () =>
        {
            Splits = [.. Lines.Children.OfType<TextBlock>().Select(line =>
                PrestoAvalonia.SplitText(line, new SplitTextParams { Words = null, Chars = new SplitPieceParams(), Outlines = true }))];
            Play();
        });
    }

    private void Apply(Preset preset)
    {
        (_duration, _ease) = (preset.Duration, preset.Ease);
        KindBox.SelectedItem = preset.Kind;
        WarpSlider.Value = preset.Warp;
        PhaseSlider.Value = 0;
        RadiusSlider.Value = preset.Radius;
        StaggerSlider.Value = preset.Stagger;
        AnimateBox.IsChecked = preset.Animate;
        FollowBox.IsChecked = preset.Follow;
        _debounce?.Cancel();
        Play();
    }

    private void Play()
    {
        if (Splits.Count == 0) return;
        _run?.Revert();
        _run = new Scope();
        var lines = Lines.Children.OfType<TextBlock>().ToList();
        var kind = (TextWarpKind)KindBox.SelectedItem!;
        var warp = Math.Round(WarpSlider.Value, 2);
        var phase = Math.Round(PhaseSlider.Value, 2);
        var radius = Math.Round(RadiusSlider.Value);
        var stagger = Math.Round(StaggerSlider.Value);
        var animate = AnimateBox.IsChecked == true;
        var follow = FollowBox.IsChecked == true;

        _run.Execute(_ =>
        {
            foreach (var line in lines) TextWarp.SetKind(line, kind);
            // The warp is block-level, so the sweep staggers line by line rather than letter by letter.
            Warp = Presto.Animate(lines, new AnimationParams
            {
                [PrestoKeys.Warp] = warp,
                [PrestoKeys.WarpPhase] = phase,
                [PrestoKeys.WarpRadius] = radius,
                Duration = _duration,
                Ease = _ease,
                Delay = Presto.Stagger(stagger),
            });
            Phase = animate
                ? Presto.Animate(lines, new AnimationParams
                {
                    [PrestoKeys.WarpPhase] = TweenValue.FromTo(phase, phase + (2 * Math.PI)),
                    Duration = PhaseLoop,
                    Ease = "linear",
                    Loop = !Presto.Defaults.ReducedMotion,
                    Delay = Presto.Stagger(stagger),
                })
                : null;
            if (follow) FollowPointer(lines);
        });

        CodeText.Code = $$"""
            var lines = Lines.Children.OfType<TextBlock>().ToList();
            foreach (var line in lines)
            {
                PrestoAvalonia.SplitText(line, new SplitTextParams { Words = null, Chars = new SplitPieceParams(), Outlines = true });
                TextWarp.SetKind(line, TextWarpKind.{{kind}});
            }
            Presto.Animate(lines, new AnimationParams
            {
                [PrestoKeys.Warp] = {{Format(warp)}},
                [PrestoKeys.WarpPhase] = {{Format(phase)}},
                [PrestoKeys.WarpRadius] = {{Format(radius)}},
                Duration = {{Format(_duration)}},
                Ease = "{{_ease}}",
                Delay = Presto.Stagger({{Format(stagger)}}),
            });
            """
            + (animate ? "\n\n" + $$"""
            Presto.Animate(lines, new AnimationParams
            {
                [PrestoKeys.WarpPhase] = TweenValue.FromTo({{Format(phase)}}, {{Format(phase + (2 * Math.PI))}}),
                Duration = {{Format(PhaseLoop)}},
                Ease = "linear",
                Loop = !Presto.Defaults.ReducedMotion,
                Delay = Presto.Stagger({{Format(stagger)}}),
            });
            """ : "")
            + (follow ? "\n\n" + Snippets.WarpStudioFollow : "");
    }

    #region snippet WarpStudioFollow
    private void FollowPointer(List<TextBlock> lines)
    {
        var x = PrestoAvalonia.PointerValue(Stage, Orientation.Horizontal);
        var y = PrestoAvalonia.PointerValue(Stage, Orientation.Vertical);
        var origins = lines.Select(line => (line, Origin: line.CreateAnimatable(new() { [PrestoKeys.WarpOrigin] = 120 }))).ToList();
        void Aim(MotionValue _)
        {
            foreach (var (line, origin) in origins)
            {
                var at = Stage.TranslatePoint(new Point(x.Value, y.Value), line)!.Value;
                origin.Set(PrestoKeys.WarpOrigin, [at.X, at.Y]);
            }
        }
        x.Changed += Aim;
        y.Changed += Aim;
    }
    #endregion

    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private void Rebuild()
    {
        if (!this.IsAttachedToVisualTree()) return;
        _debounce?.Cancel();
        _debounce = Scope.Execute(_ => Presto.CreateTimer(new() { Duration = 150, OnComplete = _ => Play() }));
    }

    private void SliderChanged(object? sender, RangeBaseValueChangedEventArgs e) => Rebuild();

    private void SelectionChanged(object? sender, SelectionChangedEventArgs e) => Rebuild();

    private void CheckChanged(object? sender, RoutedEventArgs e) => Rebuild();
}
