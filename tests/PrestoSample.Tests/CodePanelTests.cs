using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PrestoSample.Showcase;

namespace PrestoSample.Tests;

public sealed class CodePanelTests
{
    [AvaloniaFact]
    public void SettingOrBindingCodeRebuildsTheHighlightedText()
    {
        var panel = new CodePanel { Code = "var x = 1;" };
        var window = Show(panel);

        Assert.Equal("var x = 1;", Text(panel));
        Assert.All(Runs(panel), run => Assert.NotNull(run.Foreground));

        var source = new TextBox { Text = "new Foo(\"a\")" };
        panel.Bind(CodePanel.CodeProperty, source.GetObservable(TextBox.TextProperty));
        Assert.Equal("new Foo(\"a\")", Text(panel));

        source.Text = "// changed";
        Assert.Equal("// changed", Text(panel));
        window.Close();
    }

    [AvaloniaFact]
    public void SwitchingLanguageRetokenizes()
    {
        var panel = new CodePanel { Code = "<Border Classes=\"new\" />" };
        var window = Show(panel);
        var runs = Runs(panel).Count;

        panel.Language = CodeLanguage.Xaml;

        Assert.NotEqual(runs, Runs(panel).Count);
        Assert.Equal(panel.Code, Text(panel));
        Assert.Contains(Runs(panel), run => run.Text == "Classes" && ReferenceEquals(run.Foreground, Palette.Aqua));
        window.Close();
    }

    [AvaloniaFact]
    public void CollapsingHidesTheCodeBehindTheHeaderToggle()
    {
        var panel = new CodePanel { Code = "var x = 1;", Header = "Snippet" };
        var window = Show(panel);
        var toggle = Part<ToggleButton>(panel, "PART_Toggle");
        var code = Part<SelectableTextBlock>(panel, "PART_Text");
        Assert.True(toggle.IsVisible);
        Assert.True(code.IsEffectivelyVisible);

        toggle.IsChecked = false;

        Assert.False(panel.IsExpanded);
        Assert.False(code.IsEffectivelyVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void TheToggleOnlyExistsWithAHeader()
    {
        var panel = new CodePanel { Code = "var x = 1;" };
        var window = Show(panel);

        Assert.False(Part<ToggleButton>(panel, "PART_Toggle").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task CopyPutsTheCodeOnTheClipboard()
    {
        var panel = new CodePanel { Code = "Presto.Build()\n    .X(320)" };
        var window = Show(panel);

        Part<Button>(panel, "PART_Copy").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(panel.Code, await window.Clipboard!.TryGetTextAsync());
        window.Close();
    }

    [AvaloniaFact]
    public void TheCopyButtonsHoverFillFadesInGrayWithoutATealTint()
    {
        var panel = new CodePanel { Code = "var x = 1;" };
        var window = SceneHarness.Show(panel, 600, 300);
        var copy = Part<Button>(panel, "PART_Copy");

        window.MouseMove(SceneHarness.Centre(copy, window));
        TestFrames.Tick(6);

        AssertGrayMidFade(copy);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task TheHoverFillReturnsGrayWithoutATealTintAfterACopy()
    {
        var panel = new CodePanel { Code = "var x = 1;" };
        var window = SceneHarness.Show(panel, 600, 300);
        var copy = Part<Button>(panel, "PART_Copy");
        window.MouseMove(SceneHarness.Centre(copy, window));
        await SceneHarness.Settle();

        copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(await SceneHarness.Pump(() => Fill(copy).A == 0, rounds: 200));
        Assert.True(await SceneHarness.Pump(() => Fill(copy).A > 0));
        TestFrames.Tick(4);

        AssertGrayMidFade(copy);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheBackSheetSpreadsMonotonicallyToAWholePixelWithNoRestFrameOnAQuickReHover()
    {
        var panel = new CodePanel { Code = "var x = 1;" };
        var window = SceneHarness.Show(panel, 600, 300);
        var copy = Part<Button>(panel, "PART_Copy");
        var back = Part<Avalonia.Controls.Shapes.Path>(panel, "PART_CopyBack");
        var (over, away) = (SceneHarness.Centre(copy, window), new Point(5, 250));
        var last = 0.0;

        foreach (var (at, frames) in new[] { (over, 25), (away, 20), (over, 4), (away, 4), (over, 25) })
        {
            window.MouseMove(at);
            var target = at == over ? CodePanel.Spread : 0;
            for (var i = 0; i < frames; i++)
            {
                TestFrames.Tick();
                var offset = back.RenderTransform!.Value;
                Assert.Equal(offset.M31, -offset.M32);
                Assert.InRange(offset.M31, Math.Min(last, target), Math.Max(last, target));
                last = offset.M31;
            }
            if (frames >= 20) Assert.Equal(target, last);
        }
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void CompactCodeIsHitTestableBesideItsGlyphsAndSelectsOnADrag()
    {
        var panel = new CodePanel { Classes = { "compact" }, Code = "x.Y(1);\nlonger.Line(2, 3);", Width = 300, VerticalAlignment = VerticalAlignment.Top };
        var window = SceneHarness.Show(panel, 600, 300);
        var text = Part<SelectableTextBlock>(panel, "PART_Text");
        var start = text.TranslatePoint(new Point(text.Bounds.Width - 2, text.Bounds.Height - 2), window)!.Value;

        Assert.Same(text, window.InputHitTest(start));

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(new Point(0, start.Y), RawInputModifiers.LeftMouseButton);
        window.MouseUp(new Point(0, start.Y), MouseButton.Left);

        Assert.Equal("longer.Line(2, 3);", text.SelectedText);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void APanelCappedShorterThanItsCodeScrollsVertically()
    {
        var panel = new CodePanel { Code = string.Join('\n', Enumerable.Range(1, 30).Select(i => $"var x{i} = {i};")), MaxHeight = 120 };
        var window = Show(panel);
        var scroll = Part<ScrollViewer>(panel, "PART_ScrollViewer");

        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
        window.Close();
    }

    [AvaloniaFact]
    public void CompactCodeTrimmedToOneLineHoldsItAllInItsTooltip()
    {
        var panel = new CodePanel { Classes = { "compact" }, Code = "target.Animate(Recipes.FadeUp(duration: duration))", Width = 120 };
        var window = Show(panel);

        Assert.Equal(panel.Code, ToolTip.GetTip(panel));
        window.Close();
    }

    [AvaloniaFact]
    public void CodeTabsSwitchesBetweenItsSidesAndHidesTheStripWithOnlyOne()
    {
        var tabs = new CodeTabs { CSharp = "var x = 1;", Xaml = "<Border />" };
        var window = Show(tabs);
        var panel = Part<CodePanel>(tabs, "PART_Panel");
        var strip = Part<Panel>(tabs, "PART_Tabs");

        Assert.True(strip.IsVisible);
        Assert.Equal((CodeLanguage.CSharp, "var x = 1;"), (panel.Language, Text(panel)));

        Part<ToggleButton>(tabs, "PART_XamlTab").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(CodeLanguage.Xaml, tabs.SelectedLanguage);
        Assert.Equal((CodeLanguage.Xaml, "<Border />"), (panel.Language, Text(panel)));

        tabs.CSharp = null;
        tabs.SelectedLanguage = CodeLanguage.CSharp;
        Assert.False(strip.IsVisible);
        Assert.Equal((CodeLanguage.Xaml, "<Border />"), (panel.Language, Text(panel)));
        window.Close();
    }

    private static Color Fill(Button copy) => ((ISolidColorBrush)copy.Background!).Color;

    private static void AssertGrayMidFade(Button copy)
    {
        var (fill, gray) = (Fill(copy), Palette.Border.Color);
        Assert.InRange(fill.A, 1, 254);
        Assert.InRange(fill.R, gray.R - 1, gray.R + 1);
        Assert.InRange(fill.G, gray.G - 1, gray.G + 1);
        Assert.InRange(fill.B, gray.B - 1, gray.B + 1);
    }
    private static Window Show(Control content)
    {
        var window = new Window { Content = content, Width = 600, Height = 300 };
        window.Show();
        return window;
    }

    private static T Part<T>(TemplatedControl control, string name) where T : Control =>
        control.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);

    private static List<Run> Runs(CodePanel panel) => [.. Part<SelectableTextBlock>(panel, "PART_Text").Inlines!.OfType<Run>()];

    private static string Text(CodePanel panel) => string.Concat(Runs(panel).Select(run => run.Text));
}
