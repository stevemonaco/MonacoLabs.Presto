using Avalonia.Controls;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using static MonacoLabs.Presto.PrestoKeys;
using MonacoLabs.Presto.DevTools;

namespace PrestoSample.Showcase.Pages;

/// <summary>Four kinds of motion left running for the Presto DevTools overlay to inspect: a looping timeline, a stagger, a spring and a paused animation.</summary>
public partial class DevToolsPage : ShowcasePage
{
    public DevToolsPage()
    {
        InitializeComponent();
        HowCode.Code = $"{Snippets.DevToolsAttach}\n\n{Snippets.DevToolsSubjects}";
    }

    public Timeline? Sequence { get; private set; }

    public Animation? Wave { get; private set; }

    public Animatable? Bounce { get; private set; }

    public Animation? Parked { get; private set; }

    protected override void OnAttached() => Scope.Add(_ => Build());

    private void Build()
    {
        #region snippet DevToolsSubjects
        Sequence = Presto.CreateTimeline(new() { Id = "sequence", Loop = true, Alternate = true })
            .Add(Square, a => a.X(100).Duration(700).Ease(Eases.InOutQuad))
            .Label("spin")
            .Add(Square, a => a.Rotate(180).Duration(500))
            .Add(Circle, a => a.Scale(1.6).Duration(600), "spin");

        Wave = Presto.Build().Id("wave")
            .ScaleY(from: .25, to: 1)
            .Delay(Presto.Stagger(90)).Duration(520)
            .Ease(Eases.InOutSine).Loop().Alternate()
            .Animate(Bars.Children);

        var bounce = Bounce = Ball.CreateAnimatable(new() { [X] = new(), Ease = Springs.Bouncy });
        var side = 1;
        Presto.CreateTimer(new() { Id = "retarget", Duration = 1400, Loop = true, OnLoop = _ => bounce.Set(X, 60 * (side = -side)) });

        Parked = Presto.Build().Id("parked")
            .Rotate(360).Scale(.6)
            .Duration(2000).Loop().Autoplay(false)
            .Animate(Gear);
        #endregion
    }

    // The registry only discovers paused and unplayed timers created while it is on, so rebuild them under it.
    private void ToggleInspector(object? sender, RoutedEventArgs e)
    {
        PrestoDevTools.Toggle(TopLevel.GetTopLevel(this)!);
        Scope.Refresh();
    }
}
