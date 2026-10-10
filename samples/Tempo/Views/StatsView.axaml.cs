using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Tempo.Views;

public partial class StatsView : UserControl
{
    public const double ChartHeight = 160;

    /// <summary>Converts a <see cref="ViewModels.StatsDay.Height"/> fraction to the bar's height in the chart.</summary>
    public static readonly IValueConverter BarHeight = new FuncValueConverter<double, double>(height => height * ChartHeight);

    public StatsView()
    {
        InitializeComponent();
    }
}
