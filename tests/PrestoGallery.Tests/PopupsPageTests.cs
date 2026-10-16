using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;
using PrestoGallery.Showcase.Pages;

namespace PrestoGallery.Tests;

public sealed class PopupsPageTests
{
    private const int Frames = 40;

    [AvaloniaTheory]
    [InlineData("MenuButton")]
    [InlineData("ShareButton")]
    public void AFlyoutOpensFromItsStartValuesAndStaysUntilItsCloseHasPlayed(string button)
    {
        var (window, page) = Loaded();
        var anchor = page.GetControl<Button>(button);
        var flyout = (PopupFlyoutBase)anchor.Flyout!;

        flyout.ShowAt(anchor);
        var presenter = flyout.Popup.Child!;

        Assert.Equal(0, presenter.Opacity);
        TestFrames.Tick(Frames);
        Assert.Equal(1, presenter.Opacity);

        flyout.Hide();
        TestFrames.Tick();
        Assert.True(flyout.IsOpen);
        TestFrames.Tick(Frames);
        Assert.False(flyout.IsOpen);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheFileMenusRowsArriveOneAfterAnotherOnEveryOpen()
    {
        var (window, page) = Loaded();
        var anchor = page.GetControl<Button>("MenuButton");
        var flyout = (PopupFlyoutBase)anchor.Flyout!;

        for (var open = 0; open < 2; open++)
        {
            flyout.ShowAt(anchor);
            TestFrames.Tick(4);
            var rows = flyout.Popup.Child!.GetVisualDescendants().OfType<MenuItem>().ToList();

            Assert.Equal(4, rows.Count);
            Assert.True(rows[0].Opacity > rows[^1].Opacity, $"open {open}: the first row is not ahead of the last");
            TestFrames.Tick(Frames);
            Assert.All(rows, row => Assert.Equal(1, row.Opacity));

            flyout.Hide();
            TestFrames.Tick(Frames);
        }
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheShareFlyoutsAvatarsPopInOneAfterAnother()
    {
        var (window, page) = Loaded();
        var anchor = page.GetControl<Button>("ShareButton");
        var flyout = (PopupFlyoutBase)anchor.Flyout!;

        flyout.ShowAt(anchor);
        TestFrames.Tick(8);
        var avatars = flyout.Popup.Child!.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("avatar")).ToList();

        Assert.Equal(4, avatars.Count);
        Assert.True(Scale(avatars[0]) > Scale(avatars[^1]));
        TestFrames.Tick(Frames);
        Assert.All(avatars, avatar => Assert.Equal(1, Scale(avatar), 3));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task TheContextMenuAndTheDropDownOpenFromTheirStartValuesAndTheDropDownPlaysItsClose()
    {
        var (window, page) = Loaded();
        var target = page.GetControl<Border>("ContextTarget");
        var picker = page.GetControl<ComboBox>("Picker");

        target.ContextMenu!.Open(target);
        Assert.Equal(0, target.ContextMenu.Opacity);
        TestFrames.Tick(Frames);
        Assert.Equal(1, target.ContextMenu.Opacity);
        target.ContextMenu.Close();
        TestFrames.Tick(Frames);

        picker.IsDropDownOpen = true;
        var dropDown = picker.GetVisualDescendants().OfType<Popup>().Single().Child!;
        Assert.Equal(0, dropDown.Opacity);
        TestFrames.Tick(Frames);
        Assert.Equal(1, dropDown.Opacity);

        picker.IsDropDownOpen = false;
        // The held drop-down's IsOpen is read by a real-time dispatcher timer, which lets the engine idle while it is open.
        for (var i = 0; i < 50 && dropDown.Opacity == 1; i++)
        {
            await Task.Delay(10);
            TestFrames.Tick();
        }
        Assert.True(dropDown.IsAttachedToVisualTree());
        Assert.True(dropDown.Opacity < 1);
        TestFrames.Tick(Frames);
        Assert.False(dropDown.IsAttachedToVisualTree());
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheRichToolTipOpensFromItsStartValuesAndItsLinesRiseInTurn()
    {
        var (window, page) = Loaded();
        var anchor = page.GetControl<Button>("TipButton");

        ToolTip.SetIsOpen(anchor, true);
        TestFrames.Tick();
        var tip = window.GetVisualDescendants().OfType<ToolTip>().Single();
        var lines = tip.GetVisualDescendants().OfType<TextBlock>().ToList();

        Assert.True(tip.Opacity < 1);
        Assert.Equal(3, lines.Count);
        TestFrames.Tick(4);
        Assert.True(lines[0].Opacity > lines[^1].Opacity);
        TestFrames.Tick(Frames);
        Assert.Equal(1, tip.Opacity);
        Assert.All(lines, line => Assert.Equal(1, line.Opacity));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void CopyMorphsTheIconIntoACheckBesideACopiedLabelAndBack()
    {
        var (window, page) = Loaded();
        var panel = page.GetVisualDescendants().OfType<CodePanel>().First(code => code.Header is not null);
        var copy = panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_Copy");
        var label = copy.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_CopiedClip");
        var front = copy.GetVisualDescendants().OfType<Path>().Single(path => path.Name == "PART_CopyFront");

        SceneHarness.Click(window, copy);
        TestFrames.Tick(Frames);

        Assert.True(label.Width > 20);
        Assert.Equal(Palette.Aqua.Color, ((Avalonia.Media.ISolidColorBrush)front.Stroke!).Color);

        window.MouseMove(new Avalonia.Point(1, 1));
        TestFrames.Tick(120);

        Assert.Equal(0, label.Width);
        Assert.Equal(Palette.InkDim.Color, ((Avalonia.Media.ISolidColorBrush)front.Stroke!).Color);
        SceneHarness.Close(window);
    }

    private static double Scale(Avalonia.Visual visual) => Convert.ToDouble(PrestoAvalonia.Get(visual, "Scale"));

    private static (Window Window, PopupsPage Page) Loaded()
    {
        var page = new PopupsPage();
        return (SceneHarness.Show(page, 1280, 900), page);
    }
}
