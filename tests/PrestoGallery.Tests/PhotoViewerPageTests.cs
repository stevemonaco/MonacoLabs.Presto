using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Controls;
using PrestoGallery.Showcase.Pages;

namespace PrestoGallery.Tests;

public sealed class PhotoViewerPageTests
{
    [AvaloniaFact]
    public void ThePageShowsItsPhotos()
    {
        var page = new PhotoViewerPage();
        var window = SceneHarness.Show(page, 712, 440);

        var photo = Photo(page);
        Assert.True(photo.Bounds.Width > 0 && photo.Bounds.Height > 0);
        Assert.Equal(3, page.GetControl<ListBox>("Filmstrip").ItemCount);
        Assert.All(page.GetControl<Carousel>("Photos").Items.Cast<Panel>(),
            panel => Assert.NotNull(((Image)panel.Children[0]).Source));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ControlWheelZoomsThePhotoAndResetEasesItBack()
    {
        var page = new PhotoViewerPage();
        var window = SceneHarness.Show(page, 712, 440);
        var scale = page.GetControl<TextBlock>("ScaleText");

        window.MouseWheel(SceneHarness.Center(Photo(page), window), new Vector(0, 2), RawInputModifiers.Control);
        TestFrames.Tick();
        Assert.True(SceneHarness.Number(scale) > 1, $"the readout shows {scale.Text}");

        SceneHarness.Click(window, page.GetControl<Button>("ResetButton"));
        TestFrames.Tick(60);
        Assert.Equal(1, SceneHarness.Number(scale));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task PickingAPhotoDissolvesTheBackdropToIt()
    {
        var page = new PhotoViewerPage();
        var window = SceneHarness.Show(page, 712, 440);
        var backdrop = page.GetControl<FilteredImage>("Backdrop");
        var filmstrip = page.GetControl<ListBox>("Filmstrip");
        Assert.Same(((Image)filmstrip.Items[0]!).Source, backdrop.Source);

        filmstrip.SelectedIndex = 2;
        TestFrames.Tick();

        Assert.Same(((Image)filmstrip.Items[2]!).Source, backdrop.Source);
        Assert.NotEmpty(Presto.GetAnimations(backdrop));
        await SceneHarness.Settle();
        Assert.Empty(Presto.GetAnimations(backdrop));
        SceneHarness.Close(window);
    }

    private static Image Photo(PhotoViewerPage page) =>
        page.GetControl<Carousel>("Photos").GetVisualDescendants().OfType<Image>().First(image => image.IsEffectivelyVisible);
}
