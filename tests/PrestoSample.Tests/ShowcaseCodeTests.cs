using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using PrestoSample.Showcase;
using PrestoSample.Showcase.Pages;
using PrestoSample.ViewModels;

namespace PrestoSample.Tests;

/// <summary>Each Showcase page prints its key calls in one collapsed foot panel instead of a closing caption.</summary>
public sealed class ShowcaseCodeTests
{
    private static readonly string[] CodeIsTheDemo = ["Connected XAML", "Swipe deck", "Loaders"];

    [AvaloniaFact]
    public void EveryShowcasePageHasOneCollapsedHowItsBuiltPanelAndNoCaption()
    {
        var failures = new List<string>();
        foreach (var entry in new MainViewModel().Sections.Single(section => section.Name == "Showcase").Pages)
        {
            var page = (UserControl)entry.Create();
            var window = SceneHarness.Show(page, 1280, 900);
            var panels = page.GetLogicalDescendants()
                .Select(control => control switch
                {
                    CodePanel panel => (panel.Header, panel.IsExpanded, Code: panel.Code),
                    CodeTabs tabs => (tabs.Header, tabs.IsExpanded, Code: tabs.Xaml + tabs.CSharp),
                    _ => default,
                })
                .Where(panel => panel.Header == "How it's built").ToList();
            if (!CodeIsTheDemo.Contains(entry.Title) && !(panels is [{ IsExpanded: false } foot] && !string.IsNullOrWhiteSpace(foot.Code)))
                failures.Add($"{entry.Title}: has {panels.Count} \"How it's built\" panels, not one collapsed one with code");
            if (LastRootChild(page) is TextBlock { TextWrapping: TextWrapping.Wrap } caption && caption.Classes.Contains("caption"))
                failures.Add($"{entry.Title}: ends in a wrapped caption");

            SceneHarness.Close(window);
        }
        Assert.Empty(failures);
    }

    [AvaloniaFact]
    public void MicroInteractionsPrintsEveryPatternsXamlAndCSharp()
    {
        var code = new MicroInteractionsPage().GetControl<CodeTabs>("Code");

        string[] xaml = [Snippets.MicroInteractionsRipple, Snippets.MicroInteractionsLike, Snippets.MicroInteractionsTabs, Snippets.MicroInteractionsAccordion, Snippets.MicroInteractionsToasts];
        string[] cSharp = [Snippets.MicroInteractionsBuilders, Snippets.MicroInteractionsSubmit, Snippets.MicroInteractionsLikeHandler, Snippets.MicroInteractionsTabIndicator,
            Snippets.MicroInteractionsAccordionHandler, Snippets.MicroInteractionsToastSwipe, Snippets.MicroInteractionsValidation];
        Assert.All(xaml, snippet => Assert.Contains(snippet, code.Xaml));
        Assert.All(cSharp, snippet => Assert.Contains(snippet, code.CSharp));
    }

    private static Control? LastRootChild(UserControl page)
    {
        var root = page.Content is ScrollViewer scroller ? scroller.Content : page.Content;
        return root is Panel panel ? panel.Children.LastOrDefault() : null;
    }
}
