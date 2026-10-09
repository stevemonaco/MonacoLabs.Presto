using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Monaco.Presto.AvaloniaBridge.Compositor;
using PrestoSample.Showcase;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class ParticleTypePageTests
{
    [AvaloniaFact]
    public async Task TheFormationSettlesOnItsWordAndAClickFliesItToTheNextWithItsOwnCount()
    {
        var (page, window) = Show();
        var field = page.GetControl<CompositorCanvas>("Field");
        Assert.True(await SceneHarness.Pump(() => Fits(field), rounds: 120), "the first word never filled its fit");
        Assert.True(await SceneHarness.Pump(() => Count(page) > 500), "the formation holds too few particles");
        var presto = Count(page);

        SceneHarness.Click(window, field);
        // TYPE fills the stage's height where PRESTO fills its width, so its letters are larger and hold more particles.
        Assert.True(await SceneHarness.Pump(() => Count(page) > presto * 1.1, rounds: 120), "the count never followed the next word");
        Assert.True(await SceneHarness.Pump(() => Fits(field), rounds: 120), "the particles never settled on the next word");

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task EveryTransitionFliesToTheNextWordAtThePickedDuration()
    {
        foreach (var transition in new[] { FormationTransition.Sweep, FormationTransition.Burst, FormationTransition.Swarm })
        {
            var (page, window) = Show();
            var field = page.GetControl<CompositorCanvas>("Field");
            Assert.True(await SceneHarness.Pump(() => Fits(field), rounds: 120), "the first word never filled its fit");
            Assert.True(await SceneHarness.Pump(() => Count(page) > 500), "the formation holds too few particles");
            var presto = Count(page);
            page.GetControl<Slider>("DurationSlider").Value = 200;
            page.GetControl<ComboBox>("TransitionPicker").SelectedIndex = (int)transition;

            SceneHarness.Click(window, field);

            Assert.True(await SceneHarness.Pump(() => Count(page) > presto * 1.1, rounds: 120), $"{transition}: the count never followed the next word");
            Assert.True(await SceneHarness.Pump(() => Fits(field), rounds: 120), $"{transition}: the particles never settled on the next word");
            SceneHarness.Close(window);
        }
    }

    [AvaloniaFact]
    public async Task DisintegrateHidesTheCaptionAndAssembleBringsItBack()
    {
        var (page, window) = Show();
        var caption = page.GetControl<TextBlock>("Caption");

        SceneHarness.Click(window, page.GetControl<Button>("DisintegrateButton"));
        Assert.Equal(0, caption.Opacity);
        Assert.NotEmpty(SceneHarness.ParticleOverlays(caption));

        SceneHarness.Click(window, page.GetControl<Button>("AssembleButton"));
        Assert.Equal(0, caption.Opacity);
        Assert.True(await SceneHarness.Pump(() => caption.Opacity == 1, rounds: 300), "the caption never came back");

        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task UnderReducedMotionTheNextWordFadesInOnItsPointsAndTheCaptionOnlyFades()
    {
        using var reduced = ReducedMotion.On();
        var (page, window) = Show();
        var field = page.GetControl<CompositorCanvas>("Field");
        var caption = page.GetControl<TextBlock>("Caption");
        Assert.True(await SceneHarness.Pump(() => Fits(field), rounds: 120), "the first word never faded in");

        SceneHarness.Click(window, field);
        var strays = 0;
        for (var frame = 0; frame < 40; frame++)
        {
            TestFrames.Tick();
            if (Extent(field) is { } box && !Fit(field).Inflate(4).Contains(box)) strays++;
        }
        Assert.Equal(0, strays);
        Assert.True(Fits(field), "the next word never faded in");

        SceneHarness.Click(window, page.GetControl<Button>("DisintegrateButton"));
        Assert.True(await SceneHarness.Pump(() => caption.Opacity == 0), "the caption never faded out");
        Assert.Empty(SceneHarness.ParticleOverlays(caption));

        SceneHarness.Close(window);
    }

    private static (ParticleTypePage Page, Window Window) Show()
    {
        var page = new ParticleTypePage();
        return (page, SceneHarness.Show(page, 980, 760, frames: 2));
    }

    private static int Count(ParticleTypePage page) => int.Parse(page.GetControl<TextBlock>("CountText").Text!.Split(' ')[0]);

    private static Rect? Extent(CompositorCanvas field) => SceneHarness.Extent(field, Palette.AccentBright.Color);

    // The page fits each word to 85% of the stage's width or 60% of its height, centered.
    private static Rect Fit(CompositorCanvas field)
    {
        var slot = SceneHarness.Slot(field, TopLevel.GetTopLevel(field)!);
        return new Rect(slot.Center.X - slot.Width * .425, slot.Center.Y - slot.Height * .3, slot.Width * .85, slot.Height * .6);
    }

    private static bool Fits(CompositorCanvas field)
    {
        if (Extent(field) is not { } box) return false;
        var fit = Fit(field);
        var centered = Math.Abs(box.Center.X - fit.Center.X) < 6 && Math.Abs(box.Center.Y - fit.Center.Y) < 6;
        var filled = Math.Abs(box.Width - fit.Width) < 8 || Math.Abs(box.Height - fit.Height) < 8;
        return centered && filled && fit.Inflate(4).Contains(box);
    }
}
