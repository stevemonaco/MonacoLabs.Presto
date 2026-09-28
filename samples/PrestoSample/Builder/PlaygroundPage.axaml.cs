using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Builder;

/// <summary>Every control edits one step of a builder chain; the page replays the chain and prints it as the C# that would build it.</summary>
public partial class PlaygroundPage : ShowcasePage
{
    private static readonly StaggerFrom[] Origins = [StaggerFrom.First, StaggerFrom.Center, StaggerFrom.Last, StaggerFrom.Random];
    private Timer? _debounce;

    public PlaygroundPage() => InitializeComponent();

    public Animation? Animation { get; private set; }

    protected override void OnAttached() => Play();

    private void Play()
    {
        Animation?.Revert();
        var builder = Presto.Build();
        var code = new StringBuilder("Presto.Build()");
        void Step(string line) => code.Append("\n    ").Append(line);

        if (Math.Round(XSlider.Value) is var x and not 0) { builder.X(x); Step($".X({Format(x, "0")})"); }
        if (Math.Round(YSlider.Value) is var y and not 0) { builder.Y(y); Step($".Y({Format(y, "0")})"); }
        if (Math.Round(RotateSlider.Value) is var rotate and not 0) { builder.Rotate(rotate); Step($".Rotate({Format(rotate, "0")})"); }
        if (Math.Round(ScaleSlider.Value, 1) is var scale and not 1) { builder.Scale(scale); Step($".Scale({Format(scale, "0.0")})"); }
        var duration = Math.Round(DurationSlider.Value);
        builder.Duration(duration);
        Step($".Duration({Format(duration, "0")})");
        var ease = (string)((ComboBoxItem)EaseBox.SelectedItem!).Content!;
        builder.Ease(ease);
        Step($".Ease(\"{ease}\")");
        if (Math.Round(StaggerSlider.Value) is var step and > 0)
        {
            var from = FromBox.SelectedIndex;
            builder.Delay(Presto.Stagger(step, new() { From = Origins[from] }));
            Step(from == 0
                ? $".Delay(Presto.Stagger({Format(step, "0")}))"
                : $".Delay(Presto.Stagger({Format(step, "0")}, new() {{ From = StaggerFrom.{((ComboBoxItem)FromBox.SelectedItem!).Content} }}))");
        }
        if (LoopBox.IsChecked == true) { builder.Loop(); Step(".Loop()"); }
        if (AlternateBox.IsChecked == true) { builder.Alternate(); Step(".Alternate()"); }

        Animation = builder.Animate(Squares.Children);
        CodeText.Text = $"{code}\n    .Animate(Squares.Children);";
    }

    private static string Format(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private void Rebuild()
    {
        if (!this.IsAttachedToVisualTree()) return;
        _debounce?.Cancel();
        _debounce = Scope.Execute(_ => Presto.CreateTimer(new() { Duration = 150, OnComplete = _ => Play() }));
    }

    private void SliderChanged(object? sender, RangeBaseValueChangedEventArgs e) => Rebuild();

    private void SelectionChanged(object? sender, SelectionChangedEventArgs e) => Rebuild();

    private void CheckChanged(object? sender, RoutedEventArgs e) => Rebuild();

    private void Replay(object? sender, RoutedEventArgs e) => Play();
}
