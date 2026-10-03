using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
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
