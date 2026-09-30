using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>
/// Every library recipe on its own tile with the call that plays it, under a token preset, the reduced-motion flag and
/// duration and intensity sliders; the XAML tab builds a stage from the recipe markup extensions. The page restores the
/// tokens and the flag it found when it leaves.
/// </summary>
public partial class RecipeGalleryPage : ShowcasePage
{
    public const string ShowcaseXaml = """
        <WrapPanel ItemSpacing="24" LineSpacing="16">
            <Border Classes="swatch" presto:Presence.Arrive="{presto:Fade Direction=Up}" />
            <Border Classes="swatch" presto:Presence.Arrive="{presto:Pop}" />
            <Border Classes="swatch" presto:Animate.OnAttached="{presto:Shake Intensity=1.5}" />
            <Button Classes="specimen" Content="Hover me" presto:Motion.Hover="{presto:Lift}" />
            <Border Classes="specimen" presto:Tilt.Enabled="True">
                <TextBlock Text="Move over me" />
            </Border>
        </WrapPanel>
        """;

    public const string RowXaml = """
        <Border Classes="row" presto:Presence.Exit="{presto:Fade}">
            <DockPanel>
                <Button Content="Remove" DockPanel.Dock="Right" Click="RemoveRow" />
                <TextBlock Text="Quarterly report is ready" />
            </DockPanel>
        </Border>
        """;

    private static readonly (string Name, Replay Replay, Tile[] Tiles)[] Catalogue =
    [
        ("Entrances", Replay.Once,
        [
            Play("Fade", (target, duration, _) => target.Animate(Recipes.Fade(duration))),
            Play("FadeUp", (target, duration, _) => target.Animate(Recipes.FadeUp(duration: duration))),
            Play("FadeDown", (target, duration, _) => target.Animate(Recipes.FadeDown(duration: duration))),
            Play("FadeLeft", (target, duration, _) => target.Animate(Recipes.FadeLeft(duration: duration))),
            Play("FadeRight", (target, duration, _) => target.Animate(Recipes.FadeRight(duration: duration))),
            Play("ScaleIn", (target, duration, _) => target.Animate(Recipes.ScaleIn(duration: duration))),
            Play("PopIn", (target, duration, _) => target.Animate(Recipes.PopIn(duration: duration))),
            Play("BlurIn", (target, duration, _) => target.Animate(Recipes.BlurIn(duration: duration))),
            Play("SlideIn", (target, duration, _) => target.Animate(Recipes.SlideIn(Edge.Left, duration: duration))),
            Play("FlipIn", (target, duration, _) => target.Animate(Recipes.FlipIn(duration: duration))),
        ]),
        ("Exits: each restores its tile when it ends", Replay.Restore,
        [
            Play("FadeOut", (target, duration, _) => target.Animate(Recipes.FadeOut(duration))),
            Play("FadeOutUp", (target, duration, _) => target.Animate(Recipes.FadeOutUp(duration: duration))),
            Play("FadeOutDown", (target, duration, _) => target.Animate(Recipes.FadeOutDown(duration: duration))),
            Play("FadeOutLeft", (target, duration, _) => target.Animate(Recipes.FadeOutLeft(duration: duration))),
            Play("FadeOutRight", (target, duration, _) => target.Animate(Recipes.FadeOutRight(duration: duration))),
            Play("ScaleOut", (target, duration, _) => target.Animate(Recipes.ScaleOut(duration: duration))),
            Play("PopOut", (target, duration, _) => target.Animate(Recipes.PopOut(duration: duration))),
            Play("BlurOut", (target, duration, _) => target.Animate(Recipes.BlurOut(duration: duration))),
            Play("SlideOut", (target, duration, _) => target.Animate(Recipes.SlideOut(Edge.Right, duration: duration))),
            Play("FlipOut", (target, duration, _) => target.Animate(Recipes.FlipOut(duration: duration))),
        ]),
        ("Attention", Replay.Once,
        [
            Play("Shake", (target, duration, intensity) => target.Animate(Recipes.Shake(intensity, duration))),
            Play("Pulse", (target, duration, intensity) => target.Animate(Recipes.Pulse(intensity, duration))),
            Play("Wobble", (target, duration, intensity) => target.Animate(Recipes.Wobble(intensity, duration))),
            Play("Jelly", (target, duration, intensity) => target.Animate(Recipes.Jelly(intensity, duration))),
            Play("Heartbeat", (target, duration, intensity) => target.Animate(Recipes.Heartbeat(intensity, duration))),
            Play("Tada", (target, duration, intensity) => target.Animate(Recipes.Tada(intensity, duration))),
            Play("Bounce", (target, duration, intensity) => target.Animate(Recipes.Bounce(intensity, duration))),
        ]),
        ("Loops: play toggles", Replay.Toggle,
        [
            Play("Float", (target, duration, _) => target.Animate(Recipes.Float(duration: duration))),
            Play("Spin", (target, duration, _) => target.Animate(Recipes.Spin(duration))),
            Play("Breathe", (target, duration, intensity) => target.Animate(Recipes.Breathe(intensity, duration))),
            Play("Blink", (target, duration, intensity) => target.Animate(Recipes.Blink(intensity, duration))),
        ]),
        ("Interactions: on real controls", Replay.Once,
        [
            Apply("Lift", "Hover me", (button, duration, intensity) => Motion.SetHover(button, Recipes.Lift(intensity: intensity, duration: duration))),
            Apply("Grow", "Tab to me", (button, duration, intensity) => Motion.SetFocus(button, Recipes.Grow(intensity, duration))),
            Apply("PressIn", "Press me", (button, duration, intensity) => Motion.SetPress(button, Recipes.PressIn(intensity, duration))),
            Apply("Glow", "Hover me", (button, duration, intensity) => Motion.SetHover(button, Recipes.Glow(intensity: intensity, duration: duration))),
            Apply("Dim", "Press me", (button, duration, intensity) => Motion.SetPress(button, Recipes.Dim(intensity, duration))),
        ]),
        ("Loading: play toggles", Replay.Toggle,
        [
            Play("Shimmer", (target, duration, _) => target.Animate(Recipes.Shimmer(duration)), () => new Border { Classes = { "skeleton" } }),
            Play("Spinner", (target, duration, _) => target.Animate(Recipes.Spinner(duration)), () => new Border { Classes = { "ring" } }),
            Play("Dots", (dots, duration, _) => Recipes.Dots(duration: duration).Animate(((Panel)dots).Children), Dots),
            Play("Indeterminate", (target, duration, _) => target.Animate(Recipes.Indeterminate(90, duration)), () => new Border { Classes = { "fill" } }),
        ]),
        ("Tilt", Replay.Once,
        [
            Apply("Tilt", "Move over me", (button, _, intensity) => { Tilt.SetEnabled(button, true); Tilt.SetStrength(button, 10 * intensity); }),
        ]),
        ("Text", Replay.Once,
        [
            Play("Typewriter", (text, duration, _) => Recipes.Typewriter((TextBlock)text, duration: duration),
                () => new TextBlock { Text = "Typed out, one letter at a time", TextWrapping = TextWrapping.Wrap, Width = 170, Foreground = Palette.Ink }),
            Play("CountUp", (text, duration, _) => Recipes.CountUp((TextBlock)text, 12480, duration: duration),
                () => new TextBlock { Text = "0", Classes = { "readout" } }),
        ]),
    ];

    private readonly List<(Tile Tile, Control Specimen)> _applied = [];
    private readonly List<Control> _rows;

    public RecipeGalleryPage()
    {
        InitializeComponent();
        foreach (var (name, replay, tiles) in Catalogue)
        {
            var wrap = new WrapPanel { ItemSpacing = 12, LineSpacing = 12 };
            foreach (var tile in tiles) wrap.Children.Add(BuildTile(tile, replay));
            FamilyPanel.Children.Add(new TextBlock { Classes = { "caption" }, Text = name, Margin = new(0, 8, 0, 0) });
            FamilyPanel.Children.Add(wrap);
        }
        _rows = [.. XamlRows.Children];
        XamlCode.Text = $"{ShowcaseXaml}\n\n{RowXaml}";
        TokensBox.SelectionChanged += (_, _) => SettingsChanged();
        ReducedSwitch.IsCheckedChanged += (_, _) => SettingsChanged();
        DurationSlider.ValueChanged += (_, _) => SettingsChanged();
        IntensitySlider.ValueChanged += (_, _) => SettingsChanged();
    }

    public Animation? Played { get; private set; }

    private double? Duration => DurationSlider.Value > 0 ? DurationSlider.Value : null;

    private double Intensity => IntensitySlider.Value;

    protected override void OnAttached()
    {
        RecipeSettings.Preserve(Scope);
        ApplySettings();
    }

    private static Tile Play(string name, Func<Control, double?, double, Animation> play, Func<Control>? specimen = null,
        [CallerArgumentExpression(nameof(play))] string code = "") =>
        new(name, specimen ?? (() => new Border { Classes = { "swatch" } }), Body(Snippet.Of(play, code).Code), Play: play);

    private static Tile Apply(string name, string hint, Action<Control, double?, double> apply,
        [CallerArgumentExpression(nameof(apply))] string code = "") =>
        new(name, () => new Button { Classes = { "specimen" }, Content = hint }, Body(Snippet.Of(apply, code).Code), Apply: apply);

    private static string Body(string lambda) => lambda[(lambda.IndexOf("=> ", StringComparison.Ordinal) + 3)..];

    private static Control Dots() => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        Children = { new Border { Classes = { "dot" } }, new Border { Classes = { "dot" } }, new Border { Classes = { "dot" } } },
    };

    private Border BuildTile(Tile tile, Replay replay)
    {
        var specimen = tile.Specimen();
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Classes = { "name" }, Text = tile.Name });
        panel.Children.Add(new Border { Classes = { "well" }, Child = specimen });
        if (tile.Play is { } play)
        {
            Animation? played = null;
            var button = new Button { Content = "Play", Tag = tile.Name };
            button.Click += (_, _) =>
            {
                if (played is { Completed: false } && replay != Replay.Toggle) played.Complete();
                played?.Revert();
                if (replay == Replay.Toggle && played is not null)
                {
                    played = null;
                    button.Content = "Play";
                    return;
                }
                played = Played = Scope.Execute(_ => play(specimen, Duration, Intensity));
                if (replay == Replay.Restore) played.OnComplete = exit => exit.Revert();
                if (replay == Replay.Toggle) button.Content = "Stop";
            };
            panel.Children.Add(button);
        }
        else
        {
            _applied.Add((tile, specimen));
        }
        panel.Children.Add(new TextBlock { Classes = { "code" }, Text = tile.Code });
        return new Border { Classes = { "tile" }, Child = panel };
    }

    private void SettingsChanged()
    {
        if (this.IsAttachedToVisualTree()) ApplySettings();
    }

    private void ApplySettings()
    {
        var settings = RecipeSettings.Apply(TokensBox, ReducedSwitch);
        foreach (var (tile, specimen) in _applied) tile.Apply!(specimen, Duration, Intensity);
        var duration = Duration?.ToString(CultureInfo.InvariantCulture) ?? "null";
        SettingsCode.Text = $"""
            {settings}
            double? duration = {duration}; // null: each recipe takes its duration from the tokens
            double intensity = {Intensity.ToString(CultureInfo.InvariantCulture)};
            """;
    }

    private void RemoveRow(object? sender, RoutedEventArgs e) => XamlRows.Children.Remove(((Control)sender!).FindAncestorOfType<Border>()!);

    private void ReplayXaml(object? sender, RoutedEventArgs e)
    {
        var stage = XamlHost.Child;
        XamlHost.Child = null;
        XamlRows.Children.Clear();
        XamlRows.Children.AddRange(_rows);
        XamlHost.Child = stage;
    }

    private enum Replay
    {
        Once,
        Restore,
        Toggle,
    }

    private sealed record Tile(string Name, Func<Control> Specimen, string Code,
        Func<Control, double?, double, Animation>? Play = null, Action<Control, double?, double>? Apply = null);
}
