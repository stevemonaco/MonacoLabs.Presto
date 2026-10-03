using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>
/// Stat tiles counted by Text tweens with their own formats and bumped when their watched value changes, an odometer whose
/// strips take their digit from a function value, and a readout rolled by <see cref="Recipes.RollDigits"/>.
/// </summary>
public partial class CountersPage : ShowcasePage
{
    private const int Digits = 7;
    private const double DigitHeight = 48;

    private static readonly (string Title, Func<double, string> Format, double Min, double Max)[] Metrics =
    [
        ("Revenue", v => v.ToString("$#,0", CultureInfo.InvariantCulture), 200_000, 2_400_000),
        ("Active users", v => v.ToString("#,0", CultureInfo.InvariantCulture), 5_000, 90_000),
        ("Conversion", v => v.ToString("0.00'%'", CultureInfo.InvariantCulture), 1, 9),
        ("Uptime", v => v.ToString("0.000'%'", CultureInfo.InvariantCulture), 99, 100),
    ];

    private readonly TextBlock[] _readouts = new TextBlock[Metrics.Length];
    private readonly Border[] _tiles = new Border[Metrics.Length];
    private readonly double[] _values = new double[Metrics.Length];
    private readonly List<StackPanel> _strips = [];
    private readonly Random _random = new(7);
    private readonly string _bumpCode;

    public CountersPage()
    {
        InitializeComponent();
        (var bump, _bumpCode) = Snippet.Of(Presto.Build()
            .Scale(k => k.To(1.06).To(1))
            .Duration(420)
            .Ease(Eases.OutQuad));
        for (var i = 0; i < Metrics.Length; i++)
        {
            _readouts[i] = new TextBlock { Classes = { "readout" }, Text = Metrics[i].Format(0) };
            Tiles.Children.Add(_tiles[i] = new Border
            {
                Classes = { "tile" },
                Child = new StackPanel { Spacing = 4, Children = { new TextBlock { Classes = { "caption" }, Text = Metrics[i].Title }, _readouts[i] } },
            });
            Animate.SetOnChanged(_tiles[i], bump);
        }
        for (var i = 0; i < Digits; i++)
        {
            var strip = new StackPanel();
            for (var digit = 0; digit < 10; digit++) strip.Children.Add(new TextBlock { Text = digit.ToString(CultureInfo.InvariantCulture), Width = 34, Height = DigitHeight });
            _strips.Add(strip);
            Odometer.Children.Add(new Border { Height = DigitHeight, Child = strip });
        }
    }

    public IReadOnlyList<Animation> Counters { get; private set; } = [];

    public Animation? Roll { get; private set; }

    public int OdometerValue { get; private set; }

    public Animation? Rolled { get; private set; }

    protected override void OnAttached() => Refresh();

    private void Refresh()
    {
        var counters = new Animation[Metrics.Length];
        for (var i = 0; i < Metrics.Length; i++)
        {
            var next = Metrics[i].Min + (_random.NextDouble() * (Metrics[i].Max - Metrics[i].Min));
            (counters[i], CountersCode.Code) = Snippet.Of(_readouts[i].Animate(Presto.Build()
                .Property("Text", from: _values[i], to: next, textModifier: Metrics[i].Format)
                .Delay(i * 120)
                .Duration(1600)
                .Ease(Eases.OutExpo)));
            _values[i] = next;
            Animate.SetWatch(_tiles[i], next);
        }
        Counters = counters;
        CountersCode.Code += $";\n\nAnimate.SetOnChanged(_tiles[i], {_bumpCode});\nAnimate.SetWatch(_tiles[i], next);";

        var value = OdometerValue = _random.Next((int)Math.Pow(10, Digits));
        var (odometer, code) = Snippet.Of(Presto.Build()
            .Y((_, i, _) => -DigitAt(value, i) * DigitHeight)
            .Delay(Presto.Stagger(90, new() { From = StaggerFrom.Last }))
            .Duration(1400)
            .Ease(Eases.OutQuart));
        if (MechanicalBox.IsChecked == true)
        {
            odometer.Modifier(SnapToDigit);
            code += "\n    .Modifier(SnapToDigit)";
        }
        Roll = odometer.Animate(_strips);
        OdometerCode.Code = $"// value = {value.ToString("#,0", CultureInfo.InvariantCulture)}\n{code}\n    .Animate(_strips);";
    }

    private static int DigitAt(int value, int column) => value / (int)Math.Pow(10, Digits - 1 - column) % 10;

    private static double SnapToDigit(double y) => Math.Round(y / DigitHeight) * DigitHeight;

    private void Refresh(object? sender, RoutedEventArgs e) => Refresh();

    private void RollDigits(object? sender, RoutedEventArgs e) => Rolled = Recipes.RollDigits(RollReadout, _random.Next(1_000_000));
}
