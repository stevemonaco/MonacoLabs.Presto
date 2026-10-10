using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Tempo.Motion;

namespace Tempo.Views;

public partial class StatsView : UserControl
{
    public const double ChartHeight = 120;

    private static readonly IBrush[] s_heatBrushes =
    [
        new ImmutableSolidColorBrush(Color.Parse("#2D3039")),
        .. new[] { .3, .5, .75, 1 }.Select(alpha => new ImmutableSolidColorBrush(TempoMotion.FocusAccent, alpha)),
    ];

    /// <summary>Converts a <see cref="ViewModels.StatsDay.Height"/> fraction to the bar's height in the chart.</summary>
    public static readonly IValueConverter BarHeight = new FuncValueConverter<double, double>(height => height * ChartHeight);

    /// <summary>Converts a <see cref="ViewModels.HeatmapCell.Level"/> to its shade of the focus accent.</summary>
    public static readonly IValueConverter HeatBrush = new FuncValueConverter<int, IBrush>(level => s_heatBrushes[level]);

    public StatsView()
    {
        InitializeComponent();
    }
}