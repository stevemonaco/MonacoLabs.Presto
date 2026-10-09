using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using static Monaco.Presto.AvaloniaBridge.PrestoKeys;

namespace PrestoSample.Showcase.Pages;

public partial class MorphLabPage : ShowcasePage
{
    private const int FollowerCount = 5;
    private const double RideDuration = 5200;

    private static readonly Geometry[] Shapes =
    [
        Geometry.Parse("M 120,20 A 100,100 0 0 1 120,220 A 100,100 0 0 1 120,20 Z"),
        Geometry.Parse("M 20,120 C 20,51 51,20 120,20 C 189,20 220,51 220,120 C 220,189 189,220 120,220 C 51,220 20,189 20,120 Z"),
        Geometry.Parse("M 120,15 L 145.86,84.4 L 219.86,87.55 L 161.85,133.6 L 181.72,204.95 L 120,164 L 58.28,204.95 L 78.15,133.6 L 20.14,87.55 L 94.14,84.4 Z"),
        Geometry.Parse("M 120,210 C 36,150 24,96 48,66 C 72,36 108,42 120,72 C 132,42 168,36 192,66 C 216,96 204,150 120,210 Z"),
        Geometry.Parse("M 142,24 L 62,132 L 110,132 L 94,216 L 180,106 L 127,106 Z"),
        Geometry.Parse("M 24,138 C 55,66 86,66 120,138 C 154,210 185,210 216,138 L 216,102 C 185,174 154,174 120,102 C 86,30 55,30 24,102 Z"),
    ];

    private static readonly string[] EaseNames =
        ["linear", "inOutQuad", "inOutCubic", "inOutExpo", "inOutCirc", "outBack(2.4)", "outElastic(1.2, .5)"];

    private Timeline? _loop;

    public MorphLabPage()
    {
        InitializeComponent();
        HowCode.Code = $"{Snippets.MorphLabLoop}\n\n{Snippets.MorphLabSignature}\n\n{Snippets.MorphLabTrail}";
        EaseBox.ItemsSource = EaseNames;
        EaseBox.SelectedIndex = 1;
    }

    protected override void OnAttached()
    {
        RebuildLoop();

        #region snippet MorphLabSignature
        PrestoAvalonia.CreateDrawable(Signature).Animate(new()
        {
            [Draw] = TweenValue.Keyframes("0 0", "0 1", "1 1"),
            Duration = 4000,
            Ease = Eases.InOutQuad,
            LoopDelay = 500,
            Loop = true,
            WhenReduced = new() { [Draw] = "0 1", Duration = 0, Loop = false },
        });
        #endregion

        BuildTrail();
    }

    private void RebuildLoop()
    {
        _loop?.Revert();
        MorphPath.Data = Shapes[0];

        var ease = (string)EaseBox.SelectedItem!;
        var precision = PrecisionSlider.Value;
        var points = 0;
        for (var i = 1; i <= Shapes.Length; i++) points = Math.Max(points, PrestoAvalonia.MorphPointCount(Shapes[i - 1], Shapes[i % Shapes.Length], precision));
        PrecisionText.Text = string.Create(CultureInfo.InvariantCulture, $"Precision {precision:0.000} per unit of contour · up to {points} sample points");
        _loop = Scope.Execute(_ =>
        {
            #region snippet MorphLabLoop
            var timeline = Presto.CreateTimeline(new TimelineParams { Loop = !Presto.Defaults.ReducedMotion });
            // Each child morphs from wherever the previous one left the path, and the last one closes the ring.
            for (var i = 1; i <= Shapes.Length; i++)
            {
                timeline.Add(MorphPath, new()
                {
                    [Data] = PrestoAvalonia.MorphTo(Shapes[i % Shapes.Length], precision),
                    Duration = 900,
                    Ease = ease,
                });
            }
            #endregion
            return timeline;
        });
    }

    private void BuildTrail()
    {
        var lead = PrestoAvalonia.CreateMotionPath(Track);
        Marker.Animate(new()
        {
            [X] = lead.X,
            [Y] = lead.Y,
            [Rotate] = lead.Rotate,
            Duration = RideDuration,
            Ease = Eases.None,
            Loop = true,
            WhenReduced = new() { [X] = lead.X, [Y] = lead.Y, [Rotate] = lead.Rotate, Duration = 0, Loop = false },
        });

        var followers = new Border[FollowerCount];
        for (var i = 0; i < FollowerCount; i++)
        {
            followers[i] = new Border
            {
                Width = 20,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = Palette.Accent,
            };
            Canvas.SetLeft(followers[i], -10);
            Canvas.SetTop(followers[i], -4);
            TrackStage.Children.Add(followers[i]);
        }

        Presto.Set(followers, new() { ["Opacity"] = Presto.Stagger(new double[] { 0.85, 0.25 }) });
        #region snippet MorphLabTrail
        var trails = followers.Select((_, i) => PrestoAvalonia.CreateMotionPath(Track, -0.045 * (i + 1))).ToArray();
        Presto.Animate(followers, new()
        {
            [X] = (FunctionValue)((_, i, _) => trails[i].X),
            [Y] = (FunctionValue)((_, i, _) => trails[i].Y),
            [Rotate] = (FunctionValue)((_, i, _) => trails[i].Rotate),
            Duration = RideDuration,
            Ease = Eases.None,
            Loop = true,
            WhenReduced = new() { ["Opacity"] = 0, Duration = 0, Loop = false },
        });
        #endregion
    }

    private void Rebuild(object? sender, RoutedEventArgs e)
    {
        if (IsLoaded) RebuildLoop();
    }
}
