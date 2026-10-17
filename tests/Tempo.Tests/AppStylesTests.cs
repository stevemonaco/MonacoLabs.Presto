using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Tempo.Tests;

public sealed class AppStylesTests
{
    [AvaloniaFact]
    public void NoSemiTransitionIsLeftOnTheAuditedControls()
    {
        TestFrames.Install();
        Control[] controls =
        [
            new Button(), new ListBox { ItemsSource = new[] { "a" } }, new ToggleButton(), new CheckBox(),
            new Expander { Header = "h", Content = "c", [MonacoLabs.Presto.Accordion.EnabledProperty] = true }, new FlyoutPresenter(), new TextBox(),
        ];
        var panel = new StackPanel();
        panel.Children.AddRange(controls);
        var window = new Window { Content = panel };
        window.Show();
        TestFrames.Tick();

        Assert.DoesNotContain(controls.SelectMany(c => c.GetSelfAndVisualDescendants()).OfType<Animatable>(), a => a.Transitions is { Count: > 0 });
        window.Close();
    }

    [Fact]
    public void LaunchOptionsAcceptSkipIntroAndRejectUnknownArguments()
    {
        Assert.True(LaunchOptions.TryParse(["--skip-intro"], out var options, out _));
        Assert.True(options.SkipIntro);
        Assert.False(LaunchOptions.TryParse(["--bogus"], out _, out var error));
        Assert.Contains("--bogus", error);
    }
}
