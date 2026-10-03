using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

/// <summary>The Value-driven page's model: a level that a bar, gauge, swatch and card follow, and a list of rows whose progress keeps changing.</summary>
public sealed partial class ValueDrivenViewModel : ObservableObject
{
    private static readonly (AnimationBuilder Builder, string Code)[] TimingChoices =
    [
        Snippet.Of(Presto.Build().Ease(Springs.Gentle)),
        Snippet.Of(Presto.Build().Ease(Springs.Snappy)),
        Snippet.Of(Presto.Build().Ease(Springs.Bouncy)),
        Snippet.Of(Presto.Build().Ease(Springs.Stiff)),
        Snippet.Of(Presto.Build().Duration(600).Ease(Eases.OutQuint)),
    ];

    private int _built;

    public ValueDrivenViewModel() => Rows = NewRows();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BarWidth), nameof(Angle), nameof(Fill), nameof(CardMargin))]
    public partial double Level { get; set; } = 40;

    public double BarWidth => Level * 3;

    public double Angle => Level * 1.8 - 90;

    public IBrush Fill => new ImmutableSolidColorBrush(HsvColor.ToRgb(220 - Level * 2.2, .65, .95));

    public Thickness CardMargin => new(Level * 2.4, 0, 0, 0);

    public IReadOnlyList<string> Timings { get; } = ["Gentle", "Snappy", "Bouncy", "Stiff", "600 ms outQuint"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Timing), nameof(TimingCode))]
    public partial int TimingIndex { get; set; }

    public AnimationBuilder Timing => TimingChoices[TimingIndex].Builder;

    public string TimingCode => $"Timing = {TimingChoices[TimingIndex].Code};";

    [ObservableProperty]
    public partial IReadOnlyList<ValueDrivenRow> Rows { get; set; }

    /// <summary>Moves about a third of the rows to new progress values.</summary>
    public void Step()
    {
        foreach (var row in Rows)
        {
            if (Random.Shared.NextDouble() < .3) row.Progress = Random.Shared.Next(0, 101);
        }
    }

    [RelayCommand]
    private void Lower() => Level = Math.Max(0, Level - 10);

    [RelayCommand]
    private void Raise() => Level = Math.Min(100, Level + 10);

    [RelayCommand]
    private void Randomize() => Level = Random.Shared.Next(0, 101);

    [RelayCommand]
    private void Shuffle() => Rows = [.. Rows.OrderBy(_ => Random.Shared.Next())];

    [RelayCommand]
    private void Replace() => Rows = NewRows();

    private ValueDrivenRow[] NewRows() =>
        [.. Enumerable.Range(0, 200).Select(_ => new ValueDrivenRow($"Job {++_built}", Random.Shared.Next(0, 101)))];
}
