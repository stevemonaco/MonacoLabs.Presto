using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;
using PrestoSample.ViewModels;
using PrestoSample.Views;

namespace PrestoSample.Tests;

/// <summary>Shows every page the gallery lists, runs a second of frames over it, then takes it away again.</summary>
public sealed class GalleryPageTests
{
    private static readonly IReadOnlyList<GalleryPage> Pages = [.. new MainViewModel().Sections.SelectMany(section => section.Pages)];

    // Keep in step with the motion tooltip in GalleryNav.axaml.
    private static readonly string[] PlaysInFullWhenReduced = ["Loaders", "Particle field", "Stress bench", "DevTools", "Compositor", "Properties and keyframes"];

    /// <summary>Every page of every section in both motion modes: pages added to <see cref="MainViewModel.Sections"/> are covered without a change here.</summary>
    public static TheoryData<string, bool> TitlesInBothModes => [.. Pages.SelectMany(page => new[] { (page.Title, false), (page.Title, true) })];

    [AvaloniaTheory]
    [MemberData(nameof(TitlesInBothModes))]
    public void RunsForASecondAndRevertsWhenDetached(string title, bool reduced)
    {
        TestFrames.Install();
        using var motion = reduced ? ReducedMotion.On() : null;

        var galleryPage = Pages.First(page => page.Title == title);
        var page = galleryPage.Create();
        var reverted = false;
        // A showcase page already owns a scope for its lifetime; a reference page gets one wrapped around it.
        var scope = page is ShowcasePage showcase ? showcase.Scope : PrestoAvalonia.CreateScope(page);
        scope.Add(_ => () => reverted = true);

        var window = new Window { Content = page, Width = 1280, Height = 900 };
        window.Show();
        for (var i = 0; i < 60; i++) TestFrames.Tick();

        string[] endless = reduced && galleryPage.Twin == null && !PlaysInFullWhenReduced.Contains(title)
            ? [.. page.GetSelfAndLogicalDescendants()
                .SelectMany(control => Presto.GetAnimations(control).Select(animation => (control, animation)))
                .Where(found => LoopsForever(found.animation))
                .Select(found => $"{found.control.GetType().Name} {(found.control as Control)?.Name}".TrimEnd())
                .Distinct()]
            : [];

        window.Content = null;
        TestFrames.Tick();
        window.Close();

        Assert.Empty(endless);
        Assert.True(reverted);
    }

    private static bool LoopsForever(Monaco.Presto.Timer? timer)
    {
        for (; timer != null; timer = timer.Parent)
        {
            if (timer.Loop.IsInfinite) return true;
        }
        return false;
    }

    [AvaloniaFact]
    public void TheMotionPickerSetsReducedMotionAndItSurvivesNavigation()
    {
        var gallery = new MainViewModel();
        var window = SceneHarness.Show(new MainView { DataContext = gallery }, 1400, 1000);
        var picker = window.GetLogicalDescendants().OfType<ComboBox>().Single(box => box.Name == "MotionPicker");
        try
        {
            picker.SelectedIndex = 1;
            Assert.Equal(MotionPreference.Reduced, gallery.Motion);
            Assert.True(Presto.Defaults.ReducedMotion);

            gallery.SelectedPage = Pages.First(page => page.Title == "Storefront");
            gallery.SelectedPage = Pages.First(page => page.Title == "Motion tokens");
            Assert.True(Presto.Defaults.ReducedMotion);

            picker.SelectedIndex = 0;
            Assert.False(Presto.Defaults.ReducedMotion);
        }
        finally
        {
            Presto.Defaults.ReducedMotion = false;
            SceneHarness.Close(window);
        }
    }

    [AvaloniaFact]
    public void OnlyTheAnimeJsMirrorPagesNameABuilderTwin()
    {
        var sections = new MainViewModel().Sections;
        var reference = sections.Single(section => section.Name == "Reference").Pages;
        var mirrors = reference.SkipWhile(page => page.Title != "Timer").TakeWhile(page => page.Title != "Compositor").Where(page => page.Title != "Springs").ToList();
        var builders = sections.Single(section => section.Name == "Builder").Pages.Select(page => page.Title).ToList();

        Assert.Equal(15, mirrors.Count);
        Assert.All(mirrors, page => Assert.Contains(page.Twin, builders));
        Assert.All(sections.SelectMany(section => section.Pages).Except(mirrors), page => Assert.Null(page.Twin));
    }

    [AvaloniaFact]
    public void AMirrorPageShowsTheExemptionLineAndItsLinkOpensTheTwin()
    {
        var gallery = new MainViewModel();
        var window = SceneHarness.Show(new MainView { DataContext = gallery }, 1400, 1000);
        var line = window.GetLogicalDescendants().OfType<StackPanel>().Single(panel => panel.Name == "TwinLine");
        try
        {
            gallery.SelectedPage = Pages.First(page => page.Title == "Storefront");
            Assert.False(line.IsVisible);

            gallery.SelectedPage = Pages.First(page => page.Title == "Eases");
            Assert.True(line.IsVisible);
            var link = line.GetLogicalDescendants().OfType<HyperlinkButton>().Single();
            Assert.Equal("Playground", link.Content);
            window.UpdateLayout();

            SceneHarness.Click(window, link);
            Assert.Equal("Playground", gallery.SelectedPage?.Title);
            Assert.False(line.IsVisible);
        }
        finally
        {
            SceneHarness.Close(window);
        }
    }

    [AvaloniaFact]
    public void TheOsPreferenceIsShownWhicheverChoiceIsPicked()
    {
        var gallery = new MainViewModel();
        var window = SceneHarness.Show(new MainView { DataContext = gallery }, 1400, 1000);
        try
        {
            Assert.StartsWith("OS preference: ", gallery.SystemMotion);
            Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), block => block.Text == gallery.SystemMotion);

            gallery.Motion = MotionPreference.System;
            gallery.Motion = MotionPreference.Full;

            Assert.False(Presto.Defaults.ReducedMotion);
            Assert.StartsWith("OS preference: ", gallery.SystemMotion);
        }
        finally
        {
            gallery.Motion = MotionPreference.Full;
            Presto.Defaults.ReducedMotion = false;
            SceneHarness.Close(window);
        }
    }
}
