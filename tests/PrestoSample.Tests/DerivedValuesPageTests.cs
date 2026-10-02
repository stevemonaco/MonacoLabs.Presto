using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Monaco.Presto;
using PrestoSample.Pages;

namespace PrestoSample.Tests;

public sealed class DerivedValuesPageTests
{
    [AvaloniaFact]
    public void MovingThePointerAcrossTheStageMovesTiltsAndFadesTheCard()
    {
        var page = new DerivedValuesPage();
        var window = SceneHarness.Show(page, 1100, 700);
        var stage = page.GetControl<Border>("Stage");
        var card = page.GetControl<Border>("Card");
        var header = page.GetControl<TextBlock>("Header");
        var left = stage.TranslatePoint(new Point(0, stage.Bounds.Height / 2), window)!.Value;
        Assert.Equal(-160, Get(card, "X"), 3);
        Assert.Equal(.35, card.Opacity, 3);

        for (var i = 1; i <= 10; i++)
        {
            window.MouseMove(left.WithX(left.X + ((stage.Bounds.Width - 1) * i / 10)));
            TestFrames.Tick();
        }
        TestFrames.Tick(120);

        Assert.InRange(Get(card, "X"), 159, 160);
        Assert.InRange(Get(card, "Rotate"), 29.5, 30);
        Assert.InRange(card.Opacity, .35, .36);
        Assert.InRange(header.Opacity, .25, .27);
        SceneHarness.Close(window);
    }

    [Fact]
    public void DerivedValuesSnippetsMatchTheMarkup() =>
        BuilderPagesTests.AssertSnippetsMatchMarkup(typeof(DerivedValuesPage), "samples/PrestoSample/Pages/DerivedValuesPage.axaml", 1);

    private static double Get(Border card, string property) => Convert.ToDouble(Presto.Get(card, property, withUnit: false));
}
