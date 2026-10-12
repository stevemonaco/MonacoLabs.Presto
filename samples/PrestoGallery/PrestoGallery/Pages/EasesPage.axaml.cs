using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;

namespace PrestoGallery.Pages;

public partial class EasesPage : ShowcasePage
{
    private const int Samples = 241;
    private const double PlotPadding = 16;
    private const double DotTravel = -140;
    private const double DotBase = 16;

    private static readonly EaseEntry[] Catalog =
    [
        new("linear", Snippet.Of(Eases.None)),
        new("inQuad", Snippet.Of(Eases.InQuad)), new("outQuad", Snippet.Of(Eases.OutQuad)), new("inOutQuad", Snippet.Of(Eases.InOutQuad)), new("outInQuad", Snippet.Of(Eases.OutInQuad)),
        new("inCubic", Snippet.Of(Eases.InCubic)), new("outCubic", Snippet.Of(Eases.OutCubic)), new("inOutCubic", Snippet.Of(Eases.InOutCubic)), new("outInCubic", Snippet.Of(Eases.OutInCubic)),
        new("inQuart", Snippet.Of(Eases.InQuart)), new("outQuart", Snippet.Of(Eases.OutQuart)), new("inOutQuart", Snippet.Of(Eases.InOutQuart)), new("outInQuart", Snippet.Of(Eases.OutInQuart)),
        new("inQuint", Snippet.Of(Eases.InQuint)), new("outQuint", Snippet.Of(Eases.OutQuint)), new("inOutQuint", Snippet.Of(Eases.InOutQuint)), new("outInQuint", Snippet.Of(Eases.OutInQuint)),
        new("inSine", Snippet.Of(Eases.InSine)), new("outSine", Snippet.Of(Eases.OutSine)), new("inOutSine", Snippet.Of(Eases.InOutSine)), new("outInSine", Snippet.Of(Eases.OutInSine)),
        new("inCirc", Snippet.Of(Eases.InCirc)), new("outCirc", Snippet.Of(Eases.OutCirc)), new("inOutCirc", Snippet.Of(Eases.InOutCirc)), new("outInCirc", Snippet.Of(Eases.OutInCirc)),
        new("inExpo", Snippet.Of(Eases.InExpo)), new("outExpo", Snippet.Of(Eases.OutExpo)), new("inOutExpo", Snippet.Of(Eases.InOutExpo)), new("outInExpo", Snippet.Of(Eases.OutInExpo)),
        new("inBounce", Snippet.Of(Eases.InBounce)), new("outBounce", Snippet.Of(Eases.OutBounce)), new("inOutBounce", Snippet.Of(Eases.InOutBounce)), new("outInBounce", Snippet.Of(Eases.OutInBounce)),
        new(Snippet.Of(Eases.In())), new(Snippet.Of(Eases.Out(3))),
        new(Snippet.Of(Eases.InBack())), new(Snippet.Of(Eases.OutBack(2.6))),
        new(Snippet.Of(Eases.InOutBack())), new(Snippet.Of(Eases.OutInBack())),
        new(Snippet.Of(Eases.InElastic())), new(Snippet.Of(Eases.OutElastic(1.4, .5))),
        new(Snippet.Of(Eases.InOutElastic())), new(Snippet.Of(Eases.OutInElastic())),
        new(Snippet.Of(Eases.CubicBezier(.6, -.3, .3, 1.3))),
        new(Snippet.Of(Eases.Steps(6))),
        new(Snippet.Of(Eases.Steps(6, fromStart: true))),
        new(Snippet.Of(Eases.Irregular(8, .8, new Random(7)))),
        new(Snippet.Of(Eases.Linear(0, "0.25 75%", 1))),
        new(Snippet.Of(Eases.Spring(1, 80, 8, 0))),
        new(Snippet.Of(Eases.Spring(1, 220, 14, 2))),
        new(Snippet.Of(Eases.Spring(4, 140, 18, 0))),
        new(Snippet.Of(Eases.FromPath("M0,0 C.1,.6 .3,1.3 .5,1 .7,.8 .85,1.05 1,1"))),
        new(Snippet.Of(Eases.Wiggle(6))),
        new(Snippet.Of(Eases.Wiggle(6, WiggleType.Anticipate))),
        new(Snippet.Of(Eases.Rough(20, 1, random: new Random(3)))),
        new(Snippet.Of(Eases.SlowMo(.5, .8))),
        new(Snippet.Of(Eases.ExpoScale(.5, 4))),
    ];

    private Animation? _animation;

    public EasesPage()
    {
        InitializeComponent();
        EaseBox.ItemsSource = Catalog;
        EaseBox.SelectedIndex = 0;
    }

    protected override void OnAttached() => Rebuild();

    private void EaseSelected(object? sender, SelectionChangedEventArgs e) => Scope.Execute(_ => Rebuild());

    private void EaseTyped(object? sender, TextChangedEventArgs e) => Scope.Execute(_ => Rebuild());

    private void Rebuild()
    {
        if (VisualRoot == null) return;

        var entry = (EaseEntry)EaseBox.SelectedItem!;
        var typed = EaseNameBox.Text;
        Ease curve;
        EaseValue ease;
        string easeCode;
        SpringsLine.IsVisible = string.IsNullOrWhiteSpace(typed) && entry.Spring != null;
        if (string.IsNullOrWhiteSpace(typed))
        {
            curve = entry.Curve;
            ease = entry.Spring is { } spring ? spring : entry.Curve;
            easeCode = entry.Code;
            StatusText.Text = entry.Spring is { } settling
                ? string.Create(CultureInfo.InvariantCulture, $"{entry.Name} — the spring computes the duration: {settling.SettlingDuration:0} ms")
                : $"{entry.Name} — duration 1600 ms";
        }
        else
        {
            try
            {
                curve = EaseParser.Parse(typed);
            }
            catch (Exception error) when (error is FormatException or ArgumentException)
            {
                StatusText.Text = error.Message;
                return;
            }
            ease = curve;
            easeCode = $"EaseParser.Parse(\"{typed.Trim()}\")";
            StatusText.Text = $"{easeCode} — a name the parser does not know resolves to linear.";
        }

        var (lowest, highest) = Plot(curve);
        var travel = DotTravel / (highest - lowest);
        Dot.Margin = new Thickness(0, 0, 0, DotBase + (lowest * travel));

        _animation?.Revert();
        (_animation, var call) = Snippet.Of(Dot.Animate(new()
        {
            ["Y"] = travel,
            Ease = ease,
            Duration = 1600,
            Loop = true,
            Alternate = true,
        }));
        Code.Code = string.Create(CultureInfo.InvariantCulture, $"var travel = {travel:0.#};\nvar ease = {easeCode};\n\n{call};");
    }

    private (double Lowest, double Highest) Plot(Ease ease)
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
        return (lowest, highest);
    }

    private sealed record EaseEntry(string Name, string Code, Ease Curve, Spring? Spring = null)
    {
        public EaseEntry(string name, (Ease Curve, string Code) ease) : this(name, ease.Code, ease.Curve) { }

        public EaseEntry((Ease Curve, string Code) ease) : this(ease.Code, ease) { }

        public EaseEntry((Spring Spring, string Code) spring) : this(spring.Code, spring.Code, spring.Spring.Ease, spring.Spring) { }

        public override string ToString() => Name;
    }
}
