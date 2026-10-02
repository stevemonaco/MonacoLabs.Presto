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

    /// <summary>Every page of every section, by title: pages added to <see cref="MainViewModel.Sections"/> are covered without a change here.</summary>
    public static TheoryData<string> Titles => [.. Pages.Select(page => page.Title)];

    [AvaloniaTheory]
    [MemberData(nameof(Titles))]
    public void RunsForASecondAndRevertsWhenDetached(string title)
    {
        TestFrames.Install();

        var page = Pages.First(page => page.Title == title).Create();
        var reverted = false;
        // A showcase page already owns a scope for its lifetime; a reference page gets one wrapped around it.
        var scope = page is ShowcasePage showcase ? showcase.Scope : PrestoAvalonia.CreateScope(page);
        scope.Add(_ => () => reverted = true);

        var window = new Window { Content = page, Width = 1280, Height = 900 };
        window.Show();
        for (var i = 0; i < 60; i++) TestFrames.Tick();

        window.Content = null;
        TestFrames.Tick();
        window.Close();

        Assert.True(reverted);
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
