using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Text;
using static MonacoLabs.Presto.PrestoKeys;
using SkiaSharp;
using Path = Avalonia.Controls.Shapes.Path;

namespace PrestoSample.Showcase.Pages;

public partial class GlyphLabPage : ShowcasePage
{
    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string Counters = "O8B0";
    private const double Step = 1400;
    private const double WriteTime = 1600;

    private static readonly string[] Words = ["SHARDS", "GLYPHS", "VECTOR"];

    private Timeline? _write;
    private Timeline? _shatter;
    private int _letter;
    private int _word;

    public GlyphLabPage()
    {
        InitializeComponent();
        FontPicker.SelectionChanged += PickFont;
        HowCode.Code = string.Join("\n\n", Snippets.GlyphLabWriteIn, Snippets.GlyphLabCycle, Snippets.GlyphLabContours, Snippets.GlyphLabShatter);
    }

    protected override void OnAttached()
    {
        OneFigure.Data = OneFigureOf(Paired.Text!);
        AfterFirstArrange(Handwriting, WriteIn);
        Presto.CreateTimer(new TimerParams
        {
            Duration = Step,
            // Reduced, the letters crossfade through one pass and stop.
            Loop = Presto.Defaults.ReducedMotion ? Letters.Length - 1 : true,
            OnLoop = _ => Advance(),
        });
    }

    private void Replay(object? sender, RoutedEventArgs e) => WriteIn();

    #region snippet GlyphLabWriteIn
    private void WriteIn()
    {
        _write?.Complete();
        var duration = WriteTime / WriteSpeed.Value;
        _write = LetterByLetter.IsChecked == true || Presto.Defaults.ReducedMotion ? Recipes.WriteIn(Handwriting, duration) : TraceAtOnce(duration);
    }

    // WriteIn always staggers its letters, so all at once traces the same outline split with no stagger.
    private Timeline TraceAtOnce(double duration)
    {
        var split = PrestoAvalonia.SplitText(Handwriting, new SplitTextParams { Words = null, Chars = new SplitPieceParams(), Outlines = true });
        var outlines = split.Chars.OfType<Path>().ToList();
        foreach (var piece in outlines)
        {
            piece.Fill = Brushes.Transparent;
            piece.Stroke = Handwriting.Foreground;
            piece.StrokeThickness = Handwriting.FontSize / 32;
        }
        return Presto.CreateTimeline(new() { OnComplete = _ => split.Revert() })
            .Add(outlines, b => b.Draw(from: "0 0", to: "0 1").Duration(duration).Ease(Recipes.Tokens.Ease))
            .Add(outlines, b => b.Property("Fill", TweenValue.Of(Handwriting.Foreground)).Property("StrokeThickness", 0).Duration(Recipes.Tokens.Medium));
    }
    #endregion

    #region snippet GlyphLabCycle
    private void Advance()
    {
        var from = Letters[_letter].ToString();
        _letter = (_letter + 1) % Letters.Length;
        var to = Letters[_letter].ToString();
        Recipes.MorphGlyphs(Glyph, to);
        if (ContoursToggle.IsChecked == true) DrawContours(from, to);

        // MorphGlyphs pairs each contour with its own; one figure through MorphTo drags the counters along its outline.
        var counter = Counters[_letter % Counters.Length].ToString();
        Recipes.MorphGlyphs(Paired, counter);
        if (Presto.Defaults.ReducedMotion) OneFigure.Data = OneFigureOf(counter);
        else OneFigure.Animate(new() { [Data] = PrestoAvalonia.MorphTo(OneFigureOf(counter)), Duration = Recipes.Tokens.Long, Ease = Recipes.Tokens.EmphasizedEase });
    }

    private Geometry OneFigureOf(string text) =>
        new PolylineGeometry(Keyhole(Contours(PrestoAvalonia.TextOutline(text, TypefaceOf(Paired), Paired.FontSize)).Select(c => c.Points)), isFilled: true);

    // Each ring joins by a bridge out and back along one segment between its closest points, so at rest the figure has no seam.
    private static List<Point> Keyhole(IEnumerable<Point[]> rings)
    {
        var figure = new List<Point>();
        foreach (var ring in rings)
        {
            if (figure.Count == 0)
            {
                figure.AddRange(ring);
                continue;
            }
            var (at, from) = (0, 0);
            for (var i = 0; i < figure.Count; i++)
            {
                for (var j = 0; j < ring.Length; j++)
                {
                    if (Point.Distance(figure[i], ring[j]) < Point.Distance(figure[at], ring[from])) (at, from) = (i, j);
                }
            }
            figure.InsertRange(at + 1, [.. ring[from..], .. ring[..from], ring[from], figure[at]]);
        }
        return figure;
    }
    #endregion

    private void PickFont(object? sender, SelectionChangedEventArgs e)
    {
        Glyph.FontFamily = ((ComboBoxItem)FontPicker.SelectedItem!).FontFamily;
        ToggleContours(sender, e);
    }

    private void ToggleContours(object? sender, RoutedEventArgs e)
    {
        if (ContoursToggle.IsChecked == true) DrawContours(Letters[_letter].ToString(), Letters[_letter].ToString());
        else ContourLayer.Children.Clear();
    }

    #region snippet GlyphLabContours
    private void DrawContours(string from, string to)
    {
        ContourLayer.Children.Clear();
        var typeface = TypefaceOf(Glyph);
        var before = Contours(PrestoAvalonia.TextOutline(from, typeface, Glyph.FontSize));
        var after = Contours(PrestoAvalonia.TextOutline(to, typeface, Glyph.FontSize));
        using var layout = new TextLayout(to, typeface, Glyph.FontSize, null);
        var origin = new Vector((ContourLayer.Bounds.Width - layout.WidthIncludingTrailingWhitespace) / 2, (ContourLayer.Bounds.Height - layout.Height) / 2);
        for (var i = 0; i < after.Count; i++)
        {
            ContourLayer.Children.Add(new Polygon
            {
                Points = [.. after[i].Points.Select(p => p + origin)],
                Stroke = Palette.Categorical[i % Palette.Categorical.Count],
                StrokeThickness = 2,
                StrokeDashArray = after[i].IsHole ? [4, 3] : null,
            });
        }
        // An unpaired contour collapses into, or grows from, the centroid of the outer contour it morphs with.
        foreach (var hole in (bool[])[false, true])
        {
            var (leaving, arriving) = (before.Count(c => c.IsHole == hole), after.Count(c => c.IsHole == hole));
            if (leaving != arriving) Mark(Centroid(leaving > arriving ? after : before) + origin);
        }
    }

    private void Mark(Point at)
    {
        var dot = new Ellipse { Width = 18, Height = 18, Stroke = Palette.Ink, StrokeThickness = 2, StrokeDashArray = [1, 2] };
        Canvas.SetLeft(dot, at.X - 9);
        Canvas.SetTop(dot, at.Y - 9);
        ContourLayer.Children.Add(dot);
    }

    // A Geometry hides its figures, so this reads the SKPath Avalonia.Skia fills it with. A hole winds against the largest contour.
    private static List<(Point[] Points, bool IsHole)> Contours(Geometry outline)
    {
        var impl = typeof(Geometry).GetProperty("PlatformImpl", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(outline)!;
        using var measure = new SKPathMeasure((SKPath)impl.GetType().GetProperty("FillPath")!.GetValue(impl)!, forceClosed: true);
        var rings = new List<Point[]>();
        do
        {
            var count = Math.Max(12, (int)(measure.Length / 3));
            if (measure.Length > 0) rings.Add([.. Enumerable.Range(0, count).Select(i => measure.GetPosition(measure.Length * i / count)).Select(p => new Point(p.X, p.Y))]);
        }
        while (measure.NextContour());
        var winding = Math.Sign(rings.Select(Area).MaxBy(Math.Abs));
        return [.. rings.Select(ring => (ring, Math.Sign(Area(ring)) != winding))];
    }

    private static double Area(Point[] ring) =>
        ring.Select((p, i) => (p.X * ring[(i + 1) % ring.Length].Y) - (ring[(i + 1) % ring.Length].X * p.Y)).Sum() / 2;

    private static Point Centroid(List<(Point[] Points, bool IsHole)> contours)
    {
        var outer = contours.MaxBy(c => Math.Abs(Area(c.Points))).Points;
        return new Point(outer.Average(p => p.X), outer.Average(p => p.Y));
    }
    #endregion

    #region snippet GlyphLabShatter
    // MorphText's vector shards take no count, so the word shatters through Shatter and the next one pops in after it.
    private void Shatter(object? sender, TappedEventArgs e)
    {
        if (_shatter is { Completed: false }) return;
        _shatter = Recipes.Shatter(ShatterWord, (int)ShardCount.Value, seed: _word);
        _shatter.Then(shatter =>
        {
            if (shatter.Reversed) return;
            _word = (_word + 1) % Words.Length;
            ShatterWord.Text = Words[_word];
            // Shatter leaves the block held at Opacity 0, and PopIn fades in to the opacity it finds.
            Presto.Set(ShatterWord, new() { ["Opacity"] = 1 });
            Recipes.PopIn().Animate(ShatterWord);
        });
    }

    private void Reverse(object? sender, RoutedEventArgs e)
    {
        if (_shatter is { Completed: false }) _shatter.Reverse();
    }
    #endregion

    private static Typeface TypefaceOf(TextBlock block) => new(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch);
}
