using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Pages;

public partial class EasesPage : UserControl
{
    private const int Samples = 241;
    private const double PlotPadding = 16;
    private const double DotTravel = -140;
    private const double DotDuration = 1600;

    private static readonly EaseEntry[] Catalogue =
    [
        new("linear", Eases.None),
        new("inQuad", Eases.InQuad), new("outQuad", Eases.OutQuad), new("inOutQuad", Eases.InOutQuad), new("outInQuad", Eases.OutInQuad),
        new("inCubic", Eases.InCubic), new("outCubic", Eases.OutCubic), new("inOutCubic", Eases.InOutCubic), new("outInCubic", Eases.OutInCubic),
        new("inQuart", Eases.InQuart), new("outQuart", Eases.OutQuart), new("inOutQuart", Eases.InOutQuart), new("outInQuart", Eases.OutInQuart),
        new("inQuint", Eases.InQuint), new("outQuint", Eases.OutQuint), new("inOutQuint", Eases.InOutQuint), new("outInQuint", Eases.OutInQuint),
        new("inSine", Eases.InSine), new("outSine", Eases.OutSine), new("inOutSine", Eases.InOutSine), new("outInSine", Eases.OutInSine),
        new("inCirc", Eases.InCirc), new("outCirc", Eases.OutCirc), new("inOutCirc", Eases.InOutCirc), new("outInCirc", Eases.OutInCirc),
        new("inExpo", Eases.InExpo), new("outExpo", Eases.OutExpo), new("inOutExpo", Eases.InOutExpo), new("outInExpo", Eases.OutInExpo),
        new("inBounce", Eases.InBounce), new("outBounce", Eases.OutBounce), new("inOutBounce", Eases.InOutBounce), new("outInBounce", Eases.OutInBounce),
        new("Eases.In() — power 1.68", Eases.In()), new("Eases.Out(3)", Eases.Out(3)),
        new("Eases.InBack(1.7)", Eases.InBack()), new("Eases.OutBack(2.6)", Eases.OutBack(2.6)),
        new("Eases.InOutBack(1.7)", Eases.InOutBack()), new("Eases.OutInBack(1.7)", Eases.OutInBack()),
        new("Eases.InElastic(1, .3)", Eases.InElastic()), new("Eases.OutElastic(1.4, .5)", Eases.OutElastic(1.4, .5)),
        new("Eases.InOutElastic(1, .3)", Eases.InOutElastic()), new("Eases.OutInElastic(1, .3)", Eases.OutInElastic()),
        new("Eases.CubicBezier(.6, -.3, .3, 1.3)", Eases.CubicBezier(.6, -.3, .3, 1.3)),
        new("Eases.Steps(6)", Eases.Steps(6)),
        new("Eases.Steps(6, fromStart: true)", Eases.Steps(6, fromStart: true)),
        new("Eases.Irregular(8, .8)", Eases.Irregular(8, .8, new Random(7))),
        new("Eases.Linear(0, \"0.25 75%\", 1)", Eases.Linear(0, "0.25 75%", 1)),
        new("Eases.Spring(1, 80, 8, 0)", Eases.Spring(1, 80, 8, 0)),
        new("Eases.Spring(1, 220, 14, 2)", Eases.Spring(1, 220, 14, 2)),
        new("Eases.Spring(4, 140, 18, 0)", Eases.Spring(4, 140, 18, 0)),
    ];

    private Animation? _animation;

    public EasesPage()
    {
        InitializeComponent();
        EaseBox.ItemsSource = Catalogue;
        EaseBox.SelectedIndex = 0;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animation?.Revert();
        _animation = null;
    }

    private void EaseSelected(object? sender, SelectionChangedEventArgs e) => Rebuild();

    private void EaseTyped(object? sender, TextChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        if (!IsLoaded) return;

        var entry = (EaseEntry)EaseBox.SelectedItem!;
        var typed = EaseNameBox.Text;
        Ease curve;
        EaseValue ease;
        if (string.IsNullOrWhiteSpace(typed))
        {
            curve = entry.Curve;
            ease = entry.Spring is { } spring ? spring : entry.Curve;
            StatusText.Text = entry.Spring is { } settling
                ? string.Create(CultureInfo.InvariantCulture, $"{entry.Name} — the spring computes the duration: {settling.SettlingDuration:0} ms")
                : string.Create(CultureInfo.InvariantCulture, $"{entry.Name} — duration {DotDuration:0} ms");
        }
        else
        {
            try
            {
                curve = EaseParser.Parse(typed);
            }
            catch (FormatException error)
            {
                StatusText.Text = error.Message;
                return;
            }
            ease = curve;
            StatusText.Text = $"EaseParser.Parse(\"{typed.Trim()}\") — a name the parser does not know resolves to linear.";
        }

        Plot(curve);

        _animation?.Revert();
        _animation = Dot.Animate(new()
        {
            ["Y"] = DotTravel,
            Ease = ease,
            Duration = DotDuration,
            Loop = true,
            Alternate = true,
        });
    }

    private void Plot(Ease ease)
    {
        var samples = new double[Samples];
        double lowest = 0, highest = 1;
        for (var i = 0; i < Samples; i++)
        {
            samples[i] = ease((double)i / (Samples - 1));
            lowest = Math.Min(lowest, samples[i]);
            highest = Math.Max(highest, samples[i]);
        }

        var width = PlotCanvas.Width;
        var span = PlotCanvas.Height - (2 * PlotPadding);
        double PlotY(double value) => PlotPadding + ((highest - value) / (highest - lowest) * span);

        var points = new List<Point>(Samples);
        for (var i = 0; i < Samples; i++) points.Add(new Point((double)i / (Samples - 1) * width, PlotY(samples[i])));
        Curve.Points = points;

        ZeroLine.StartPoint = new Point(0, PlotY(0));
        ZeroLine.EndPoint = new Point(width, PlotY(0));
        OneLine.StartPoint = new Point(0, PlotY(1));
        OneLine.EndPoint = new Point(width, PlotY(1));
    }

    private sealed record EaseEntry(string Name, Ease Curve, Spring? Spring = null)
    {
        private EaseEntry(string name, Spring spring) : this(name, spring.Ease, spring) { }

        public override string ToString() => Name;
    }
}
