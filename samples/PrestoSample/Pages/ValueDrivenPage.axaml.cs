using Monaco.Presto;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

/// <summary>
/// Value-driven animation: a bar, swatch, card and list rows bound through <c>{presto:Follow}</c> and a gauge through
/// <c>presto:Follow.Rotate</c>, all following <see cref="ValueDrivenViewModel"/> with no animation code. The printed XAML
/// constants are pinned to the markup by a test.
/// </summary>
public partial class ValueDrivenPage : ShowcasePage
{
    public const string BarXaml = """
        <Border x:Name="Bar" HorizontalAlignment="Left" CornerRadius="6" Background="{x:Static showcase:Palette.Accent}"
                Width="{presto:Follow {Binding BarWidth}, Spring=Snappy}" />
        """;

    public const string GaugeXaml = """
        <Border x:Name="Needle" Width="4" Height="96" Margin="0,0,0,10" CornerRadius="2" HorizontalAlignment="Center" VerticalAlignment="Bottom"
                Background="{x:Static showcase:Palette.Amber}" RenderTransformOrigin="50%,100%"
                presto:Follow.Rotate="{Binding Angle}" presto:Follow.Timing="{Binding Timing}" />
        """;

    public const string SwatchXaml = """
        <Border x:Name="Swatch" Width="160" Height="80" CornerRadius="12" HorizontalAlignment="Left" VerticalAlignment="Center"
                Background="{presto:Follow {Binding Fill}}" />
        """;

    public const string CardXaml = """
        <Border x:Name="Card" Classes="specimen" Margin="{presto:Follow {Binding CardMargin}, Spring=Bouncy}">
        """;

    public const string RowXaml = """
        <ProgressBar Grid.Column="1" Maximum="100" Value="{presto:Follow {Binding Progress}}" />
        """;

    public ValueDrivenPage() => InitializeComponent();

    protected override void OnAttached()
    {
        Presto.CreateTimer(new() { Duration = 1200, Loop = true, OnLoop = _ => ((ValueDrivenViewModel)DataContext!).Step() });
    }
}
