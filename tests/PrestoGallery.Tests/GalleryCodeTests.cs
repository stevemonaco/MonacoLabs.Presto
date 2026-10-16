using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using PrestoGallery.Showcase;
using PrestoGallery.ViewModels;

namespace PrestoGallery.Tests;

/// <summary>Every gallery page prints its code in a highlighted, copyable <see cref="CodePanel"/> and never in a TextBlock.</summary>
public sealed partial class GalleryCodeTests
{
    [AvaloniaFact]
    public void EveryPagePrintsItsCodeInAHighlightedCopyableCodePanel()
    {
        var failures = new List<string>();
        foreach (var entry in new MainViewModel().Sections.SelectMany(section => section.Pages))
        {
            var page = entry.Create();
            var window = SceneHarness.Show(page, 1280, 900, frames: 5);
            failures.AddRange(CodeInTextBlocks(entry.Title, page));
            failures.AddRange(UnhighlightedPanels(entry.Title, page));
            SceneHarness.Close(window);
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [AvaloniaTheory]
    [InlineData("mono", "Box.Animate(new() { [\"X\"] = 100 });", "Box.Animate(new() { [\"X\"] = 100 });")]
    [InlineData("code", "x", "the class \"code\"")]
    public void TheDetectorReportsCodeInATextBlock(string @class, string text, string reported)
    {
        var entry = new MainViewModel().Sections.First().Pages.First();
        var page = entry.Create();
        page.GetLogicalDescendants().OfType<Panel>().First().Children.Add(new TextBlock { Classes = { @class }, Text = text });

        Assert.Contains(CodeInTextBlocks(entry.Title, page), failure => failure.Contains(reported));
    }

    private static IEnumerable<string> CodeInTextBlocks(string title, Control page)
    {
        foreach (var text in page.GetLogicalDescendants().OfType<TextBlock>())
        {
            if (text.Classes.Contains("code"))
                yield return $"{title}: TextBlock {text.Name} has the class \"code\"";
            else if (text.Text is { } value && CodeShaped(value))
                yield return $"{title}: TextBlock {text.Name} holds code: {value}";
        }
    }

    private static bool CodeShaped(string text) => CodePattern().IsMatch(text) || (text.Contains('\n') && text.Contains(';'));

    private static IEnumerable<string> UnhighlightedPanels(string title, Control page)
    {
        var panels = page.GetLogicalDescendants().OfType<CodePanel>()
            .Concat(page.GetLogicalDescendants().OfType<CodeTabs>().SelectMany(tabs => tabs.GetVisualDescendants().OfType<CodePanel>()));
        // A hidden panel is revealed with the code of the press that shows it.
        foreach (var panel in panels.Where(panel => panel.IsVisible))
        {
            var name = $"{title}: CodePanel {panel.Name ?? panel.Header ?? panel.Code}";
            if (string.IsNullOrWhiteSpace(panel.Code))
            {
                yield return $"{name} has no code";
                continue;
            }
            // A panel in an unselected tab has no template yet.
            if (!panel.IsExpanded || TopLevel.GetTopLevel(panel) is null) continue;
            if (panel.GetVisualDescendants().OfType<SelectableTextBlock>().SingleOrDefault(part => part.Name == "PART_Text") is not { Inlines: { } inlines })
            {
                yield return $"{name} has no PART_Text";
                continue;
            }
            var runs = inlines.OfType<Run>().ToList();
            if (string.Concat(runs.Select(run => run.Text)) != panel.Code)
                yield return $"{name} shows different text from its Code";
            var colorable = CodeHighlighter.Tokenize(panel.Code, panel.Language).Any(token => token.Kind != CodeTokenKind.Plain);
            if (colorable && !runs.Any(run => run.IsSet(TextElement.ForegroundProperty)))
                yield return $"{name} is not highlighted";
            if (panel.GetVisualDescendants().OfType<Button>().SingleOrDefault(part => part.Name == "PART_Copy") is not { IsEnabled: true })
                yield return $"{name} has no enabled Copy button";
        }
    }

    [GeneratedRegex(@"\w+\.Animate\w*\(new|new \w+Params\s*\{|=>\s*\w+\.|;\s*$", RegexOptions.Multiline)]
    private static partial Regex CodePattern();
}
