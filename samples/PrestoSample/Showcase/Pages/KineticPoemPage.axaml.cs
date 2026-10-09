using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Text;
using Path = Avalonia.Controls.Shapes.Path;

namespace PrestoSample.Showcase.Pages;

public partial class KineticPoemPage : ShowcasePage
{
    private const double Beat = 500;
    private const double Measure = 4 * Beat;
    private const int Beats = 7 * 4;
    private const double Spread = 150;

    public KineticPoemPage() => InitializeComponent();

    protected override void OnAttached() => AfterFirstArrange(Stage, Build);

    private void Build()
    {
        #region snippet KineticPoemTimeline
        var chars = new SplitTextParams { Words = null, Chars = new SplitPieceParams() };
        var outlines = chars with { Outlines = true };
        // Traced and filled as Recipes.WriteIn writes, but as timeline children: WriteIn unsplits as it ends, so it could not be seeked back into.
        var feel = PrestoAvalonia.SplitText(Feel, outlines).Chars;
        if (!Presto.Defaults.ReducedMotion)
        {
            foreach (var piece in feel.OfType<Path>())
            {
                piece.Fill = Brushes.Transparent;
                piece.Stroke = Feel.Foreground;
                piece.StrokeThickness = Feel.FontSize / 32;
            }
        }
        var stretch = PrestoAvalonia.SplitText(Stretch, chars).Chars;
        var ride = PrestoAvalonia.SplitText(Ride, chars).Chars;
        var fall = PrestoAvalonia.SplitText(Fall, chars).Chars;
        var crack = PrestoAvalonia.SplitText(Break, outlines);
        var melt = PrestoAvalonia.SplitText(Melt, outlines).Chars;
        TextWarp.SetKind(Melt, TextWarpKind.Melt);
        var again = PrestoAvalonia.SplitText(Again).Words;
        Stage.UpdateLayout();
        var shards = ShardsOf(crack);

        var timeline = Presto.CreateTimeline()
            .Add(BeatDot, b => b.Scale(from: 1.8, to: 1).Opacity(from: 1, to: .35).Duration(Beat).Ease(Eases.OutQuad).Loop(Beats - 1)
                .WhenReduced(r => r.Opacity(from: 1, to: .35)), 0)
            .Label("feel", 0)
            .Add(feel, b => b.Property("Draw", TweenValue.FromTo("0 0", "0 1")).Duration(1.5 * Beat).Ease(Eases.InOutSine)
                .Delay(Presto.Stagger(Beat / 10)).WhenReduced(r => r.Opacity(from: 0, to: 1)), "feel")
            .Add(feel, b => b.Property("Fill", TweenValue.Of(Feel.Foreground)).Property("StrokeThickness", 0).Duration(Beat / 2)
                .WhenReduced(_ => { }), "feel+=1250")
            .Add(feel, Recipes.FadeOut(), "feel+=1750")
            .Label("stretch", Measure)
            .Add(stretch, Recipes.PopIn(scale: 2.5), "stretch")
            .Add(stretch, b => b.X(Presto.Stagger(new[] { -Spread, Spread })).Duration(2 * Beat).Ease(Eases.OutElastic())
                .WhenReduced(_ => { }), "stretch+=500")
            .Add(stretch, Recipes.FadeOut(), "stretch+=1750")
            .Label("ride", 2 * Measure)
            .Add(ride, Recipes.Fade(stagger: true), "ride")
            .Add(ride, Recipes.Wave(distance: 36, step: 30, duration: Beat / 2).Loop(3), "ride+=250")
            .Add(ride, Recipes.FadeOut(), "ride+=1750")
            .Label("fall", 3 * Measure)
            .Add(fall, Recipes.Fade(duration: Beat), "fall")
            .Add(fall, Recipes.Fall(seed: 4).Delay(Presto.Stagger(30)), "fall+=800")
            .Label("break", 4 * Measure)
            .Add(crack.Chars, Recipes.PopIn(scale: 2.5), "break")
            .Add(crack.Chars, b => b.Opacity(0).Duration(1), "break+=1500")
            .Add(shards, b => b.Opacity(1).Duration(1), "break+=1500")
            .Add(shards, Recipes.Scatter(distance: 320, seed: 5), "break+=1501")
            .Label("melt", 5 * Measure)
            .Add(melt, Recipes.Fade(duration: Beat), "melt")
            .Add(Melt, b => b.Property("Warp", TweenValue.FromTo(0, 6)).Duration(Measure - 750).Ease(Eases.InQuad), "melt+=750")
            .Add(melt, Recipes.FadeOut(), "melt+=1750")
            .Label("again", 6 * Measure)
            .Add(again, Recipes.PopIn(scale: 2.5).Delay(Presto.Stagger(Beat)), "again");

        // A tween renders nothing before it starts, so one pass to the end and back hides every piece before the first frame.
        timeline.Pause().Seek(timeline.Duration).Seek(0).Play();
        Bar.Timer = timeline;
    }

    // Recipes.Shatter's shards leave the overlay when it ends, so these live on the stage where a seek back finds them again.
    private List<Control> ShardsOf(TextSplitter split)
    {
        Shards.Children.Clear();
        var origin = Break.TranslatePoint(default, Shards) ?? default;
        for (var i = 0; i < split.Chars.Count; i++)
        {
            if (split.Chars[i] is not Path piece) continue;
            var at = origin + split.SlotOf(piece).Position;
            foreach (var shard in GlyphShards.Of(piece.Data!, 8, i))
            {
                var path = new Path { Data = shard, Fill = Break.Foreground, Opacity = 0, RenderTransformOrigin = new RelativePoint(shard.Bounds.Center, RelativeUnit.Absolute) };
                Canvas.SetLeft(path, at.X);
                Canvas.SetTop(path, at.Y);
                Shards.Children.Add(path);
            }
        }
        return [.. Shards.Children];
    }
    #endregion

    private void SeekLabel(object? sender, RoutedEventArgs e)
    {
        if (Bar.Timer is not Timeline timeline || sender is not Button { Tag: string label }) return;
        timeline.Seek(timeline.Labels[label]);
        Bar.Refresh();
    }
}
