using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Controls;
using PrestoSample.Showcase.Pages;
using Path = Avalonia.Controls.Shapes.Path;

namespace PrestoSample.Tests;

public sealed class TypeLabPageTests
{
    private const double Shift = 8;

    [AvaloniaFact]
    public void TheDisplayFontShipsEveryWeightAndSixWidthsAsDistinctFaces()
    {
        var family = (FontFamily)Application.Current!.FindResource("DisplayFont")!;

        double[] weights = [.. Enumerable.Range(1, 9).Select(step => Width(family, (FontWeight)(step * 100), FontStretch.Normal))];
        double[] widths = [.. Enumerable.Range((int)FontStretch.ExtraCondensed, 6).Select(stretch => Width(family, FontWeight.Bold, (FontStretch)stretch))];

        for (var i = 1; i < weights.Length; i++) Assert.True(weights[i] > weights[i - 1], $"weight {(i + 1) * 100} is not wider than {i * 100}: {string.Join(", ", weights)}");
        for (var i = 1; i < widths.Length; i++) Assert.True(widths[i] > widths[i - 1], $"stretch {i + 2} is not wider than {i + 1}: {string.Join(", ", widths)}");
    }

    [AvaloniaFact]
    public void MorphReplacesThePathTextOntoTheNewCurve()
    {
        var page = new TypeLabPage();
        var window = SceneHarness.Show(page, 1280, 1000, frames: 2);
        var curve = page.GetControl<Path>("Curve");
        var ribbon = page.GetControl<TextBlock>("Ribbon");
        Assert.Equal("TYPE THAT RIDES THE CURVE".Replace(" ", "").Length, Pieces(ribbon).Length);
        AssertOnContour(Pieces(ribbon), curve);
        var before = Centers(ribbon, curve);

        SceneHarness.Click(window, page.GetControl<Button>("MorphButton"));
        TestFrames.Tick(160);

        var after = Centers(ribbon, curve);
        var moved = before.Zip(after, Point.Distance).Max();
        Assert.True(moved > 20, $"no letter moved more than {moved:0.#}");
        Assert.True(before.Max(center => DistanceToContour(curve, center)) > 20, "the old placement already sat on the new curve");
        AssertOnContour(Pieces(ribbon), curve);
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData("WeightLine", "StrokeThickness", 0, 5)]
    [InlineData("SquashLine", "ScaleY", .7, 1.25)]
    [InlineData("ObliqueLine", "SkewX", -18, 0)]
    [InlineData("TrackingLine", "X", -48, 48)]
    public void EachWaveSweepsSmoothlyWithinItsRangeAndItsStaggerOffsetsNeighbors(string line, string property, double min, double max)
    {
        var page = new TypeLabPage();
        var window = SceneHarness.Show(page, 1280, 1000, frames: 2);
        var letters = Letters(page.GetControl<TextBlock>(line));
        var seen = new HashSet<double>();
        var offset = false;

        for (var frame = 0; frame < 150; frame++)
        {
            TestFrames.Tick();
            foreach (var letter in letters) Assert.InRange(Value(letter, property), min - .001, max + .001);
            seen.Add(Math.Round(Value(letters[0], property), 3));
            offset |= Math.Abs(Value(letters[0], property) - Value(letters[1], property)) > .01;
        }

        Assert.True(seen.Count >= 30, $"the first letter only took {seen.Count} values");
        Assert.True(offset, "neighboring letters always moved together");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheFauxWeightStrokesEachLetterOutlineInItsOwnColor()
    {
        var page = new TypeLabPage();
        var window = SceneHarness.Show(page, 1280, 1000, frames: 2);
        var letters = Letters(page.GetControl<TextBlock>("WeightLine")).Cast<Path>().ToArray();

        Assert.Equal("Fauxweight".Length, letters.Length);
        Assert.All(letters, letter => Assert.Same(letter.Fill, letter.Stroke));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionTheWavesHoldAStaticRampAndMorphSwapsAtOnce()
    {
        using var reduced = ReducedMotion.On();
        var page = new TypeLabPage();
        var window = SceneHarness.Show(page, 1280, 1000, frames: 2);
        var letters = Letters(page.GetControl<TextBlock>("WeightLine"));
        var ramp = letters.Select(letter => Value(letter, "StrokeThickness")).ToArray();

        TestFrames.Tick(60);

        Assert.Equal(0, ramp[0]);
        Assert.Equal(5, ramp[^1]);
        Assert.Equal(ramp, letters.Select(letter => Value(letter, "StrokeThickness")));
        Assert.False(page.GetControl<ToggleButton>("FlowToggle").IsEnabled);

        var curve = page.GetControl<Path>("Curve");
        var ribbon = page.GetControl<TextBlock>("Ribbon");
        var before = Centers(ribbon, curve);
        SceneHarness.Click(window, page.GetControl<Button>("MorphButton"));
        Assert.True(before.Max(center => DistanceToContour(curve, center)) > 20, "Morph did not swap the curve at once");
        AssertOnContour(Pieces(ribbon), curve);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ShatterEndsAsThePlainNextWord()
    {
        var page = new TypeLabPage();
        var window = SceneHarness.Show(page, 1280, 1800, frames: 2);
        var word = page.GetControl<TextBlock>("ShatterWord");

        SceneHarness.Click(window, word);
        Assert.NotNull(word.Inlines);
        TestFrames.Tick(90);

        Assert.Equal("THREATS", word.Text);
        Assert.Null(word.Inlines);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void WithVectorShardsOnTheLeftoverLetterShattersIntoPathsNotSnapshots()
    {
        var page = new TypeLabPage();
        var window = SceneHarness.Show(page, 1280, 1800, frames: 2);
        var word = page.GetControl<TextBlock>("ShatterWord");
        Tap(window, word, 0);
        TestFrames.Tick(90);

        // THREATS to HEARTS leaves one T over.
        Tap(window, word, 1);
        var overlay = OverlayLayer.GetOverlayLayer(word)!.Children.SelectMany(c => c.GetVisualDescendants()).ToArray();

        Assert.True(overlay.OfType<Path>().Count() > 1, "the leftover T did not shatter into shards");
        Assert.Empty(overlay.OfType<Image>());
        TestFrames.Tick(90);
        Assert.Equal("HEARTS", word.Text);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AShapeMorphGrowsTheNewLetterOutOfTheOldOneAndEndsAsTheNextWord()
    {
        var page = new TypeLabPage();
        var window = SceneHarness.Show(page, 1280, 1800, frames: 2);
        page.GetControl<ToggleSwitch>("ExplodeToggle").IsChecked = false;
        page.GetControl<ToggleSwitch>("ShapeToggle").IsChecked = true;
        var word = page.GetControl<TextBlock>("ShatterWord");
        for (var i = 0; i < 2; i++)
        {
            Tap(window, word, i);
            TestFrames.Tick(90);
        }

        // HEARTS to HEARTH morphs the S into the last H.
        Tap(window, word, 2);
        Assert.Contains(word.GetVisualDescendants(), piece => piece is Path);
        TestFrames.Tick(90);

        Assert.Equal("HEARTH", word.Text);
        Assert.Null(word.Inlines);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheDeparturesBoardRollsAndSettlesOnItsText()
    {
        var page = new TypeLabPage();
        var window = SceneHarness.Show(page, 1280, 1800, frames: 2);
        var boards = page.GetControl<StackPanel>("Board").Children.OfType<SplitFlap>().ToArray();
        string[] first = [.. boards.Select(board => board.Text!)];

        TestFrames.Tick((5000 / 16) + 2);
        Assert.Equal(first[1..], boards[..^1].Select(board => board.Text));
        TestFrames.Tick(3600 / 16);

        Assert.All(boards, board => Assert.Equal(board.Text, Shown(board)));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ADraggedLetterPullsItsNeighborsLessWithDistanceAndEverythingSettlesHome()
    {
        var page = new TypeLabPage();
        var window = SceneHarness.Show(page, 1280, 1800, frames: 4);
        var letters = Pieces(page.GetControl<TextBlock>("ElasticWord"));
        Assert.Equal("Elastic".Length, letters.Length);
        var drop = Hold(window, letters[3], new Vector(60, 30));

        Assert.Equal(60, TranslateX(letters[3]), 1);
        Assert.Equal(30, TranslateY(letters[3]), 1);
        for (var distance = 1; distance <= 3; distance++)
        {
            foreach (var side in (int[])[-1, 1])
            {
                var (neighbor, nearer) = (letters[3 + (side * distance)], letters[3 + (side * (distance - 1))]);
                Assert.InRange(TranslateX(neighbor), .1, TranslateX(nearer) - .1);
                Assert.InRange(TranslateY(neighbor), .05, TranslateY(nearer) - .05);
            }
        }

        window.MouseUp(drop, MouseButton.Left);
        TestFrames.Tick(180);

        Assert.All(letters, letter =>
        {
            Assert.Equal(0, TranslateX(letter), .5);
            Assert.Equal(0, TranslateY(letter), .5);
        });
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void UnderReducedMotionADraggedLetterMovesAloneAndDropsHomeAtOnce()
    {
        using var reduced = ReducedMotion.On();
        var page = new TypeLabPage();
        var window = SceneHarness.Show(page, 1280, 1800, frames: 4);
        var letters = Pieces(page.GetControl<TextBlock>("ElasticWord"));

        var drop = Hold(window, letters[3], new Vector(60, 30));
        Assert.Equal(60, TranslateX(letters[3]), 1);
        Assert.All(letters.Where((_, i) => i != 3), letter => Assert.Equal(0, TranslateX(letter)));

        window.MouseUp(drop, MouseButton.Left);
        TestFrames.Tick();
        Assert.Equal(0, TranslateX(letters[3]));
        Assert.Equal(0, TranslateY(letters[3]));
        SceneHarness.Close(window);
    }

    // Each tap lands apart from the last, so none counts as the second of a double tap.
    private static void Tap(Window window, Control word, int nth)
    {
        var at = SceneHarness.Center(word, window) + new Vector(nth * 12, 0);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        TestFrames.Tick();
    }

    // Held still for the last ten frames, so the release carries no throw.
    private static Point Hold(Window window, Control letter, Vector by)
    {
        var grab = SceneHarness.Center(letter, window);
        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 20; i++)
        {
            window.MouseMove(grab + (by * Math.Min(i, 10) / 10));
            TestFrames.Tick();
        }
        return grab + by;
    }

    private static double TranslateX(Control control) => Convert.ToDouble(PrestoAvalonia.Get(control, "X"));

    private static double TranslateY(Control control) => Convert.ToDouble(PrestoAvalonia.Get(control, "Y"));

    private static string Shown(SplitFlap board) => string.Concat(board.GetVisualDescendants().OfType<StackPanel>().First().Children
        .Select(cell => ((TextBlock)((Border)((Panel)cell).Children[1]).Child!).Text));

    private static Control[] Letters(TextBlock block) => [.. block.GetVisualDescendants().OfType<Control>()
        .Where(piece => piece is Path || (piece is TextBlock { Text: { } text } && !string.IsNullOrWhiteSpace(text)))];

    private static double Value(Control letter, string property) => Convert.ToDouble(Presto.Get(letter, property, false));

    private static Control[] Pieces(TextBlock block) =>[.. block.GetVisualDescendants().OfType<TextBlock>().Where(piece => !string.IsNullOrWhiteSpace(piece.Text))];

    private static Point[] Centers(TextBlock ribbon, Visual curve) => [.. Pieces(ribbon).Select(letter => CenterIn(letter, curve))];

    private static double Width(FontFamily family, FontWeight weight, FontStretch stretch)
    {
        var text = new TextBlock { Text = "Weight wave", FontSize = 40, FontFamily = family, FontWeight = weight, FontStretch = stretch };
        text.Measure(Size.Infinity);
        return text.DesiredSize.Width;
    }

    private static Point CenterIn(Control letter, Visual curve) => letter.TranslatePoint(new Point(letter.Bounds.Width / 2, letter.Bounds.Height / 2), curve)!.Value;

    private static void AssertOnContour(Control[] letters, Path curve)
    {
        foreach (var letter in letters.Where(letter => letter.Opacity > 0))
        {
            var distance = DistanceToContour(curve, CenterIn(letter, curve));
            Assert.True(distance is > Shift - 2 and < Shift + 0.5, $"'{((TextBlock)letter).Text}' sits {distance:0.##} off the contour, not {Shift}");
        }
    }

    private static double DistanceToContour(Path curve, Point point)
    {
        var geometry = curve.RenderedGeometry!;
        var length = geometry.ContourLength;
        var nearest = double.MaxValue;
        for (var i = 0; i <= 2000; i++)
        {
            geometry.TryGetPointAtDistance(length * i / 2000, out var on);
            nearest = Math.Min(nearest, Point.Distance(on, point));
        }
        return nearest;
    }
}
