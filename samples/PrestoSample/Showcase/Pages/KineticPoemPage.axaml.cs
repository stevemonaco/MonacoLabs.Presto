using Avalonia.Controls;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Text;

namespace PrestoSample.Showcase.Pages;

public partial class KineticPoemPage : ShowcasePage
{
    private const double Beat = 500;
    private const double Measure = 4 * Beat;
    private const int Beats = 6 * 4;
    private const double Spread = 150;

    public KineticPoemPage() => InitializeComponent();

    protected override void OnAttached() => AfterFirstArrange(Stage, Build);

    private void Build()
    {
        var chars = new SplitTextParams { Words = null, Chars = new SplitPieceParams() };
        var feel = PrestoAvalonia.SplitText(Feel).Words;
        var stretch = PrestoAvalonia.SplitText(Stretch, chars).Chars;
        var ride = PrestoAvalonia.SplitText(Ride, chars).Chars;
        var fall = PrestoAvalonia.SplitText(Fall, chars).Chars;
        var crack = PrestoAvalonia.SplitText(Break, chars).Chars;
        var again = PrestoAvalonia.SplitText(Again).Words;

        #region snippet KineticPoemTimeline
        var timeline = Presto.CreateTimeline()
            .Add(BeatDot, b => b.Scale(from: 1.8, to: 1).Opacity(from: 1, to: .35).Duration(Beat).Ease(Eases.OutQuad).Loop(Beats - 1)
                .WhenReduced(r => r.Opacity(from: 1, to: .35)), 0)
            .Label("feel", 0)
            .Add(feel, Recipes.PopIn(scale: 2.5).Delay(Presto.Stagger(Beat)), "feel")
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
            .Add(crack, Recipes.PopIn(scale: 2.5), "break")
            .Add(crack, Recipes.Scatter(distance: 320, seed: 5), "break+=1000")
            .Label("again", 5 * Measure)
            .Add(again, Recipes.PopIn(scale: 2.5).Delay(Presto.Stagger(Beat)), "again");
        #endregion

        // A tween renders nothing before it starts, so one pass to the end and back hides every piece before the first frame.
        timeline.Pause().Seek(timeline.Duration).Seek(0).Play();
        Bar.Timer = timeline;
    }

    private void SeekLabel(object? sender, RoutedEventArgs e)
    {
        if (Bar.Timer is not Timeline timeline || sender is not Button { Tag: string label }) return;
        timeline.Seek(timeline.Labels[label]);
        Bar.Refresh();
    }
}
