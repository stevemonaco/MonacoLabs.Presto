using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Controls;
using Monaco.Presto.AvaloniaBridge.Text;
using static Monaco.Presto.AvaloniaBridge.PrestoKeys;

namespace PrestoSample.Showcase.Pages;

public partial class LiquidTypePage : ShowcasePage
{
    public LiquidTypePage()
    {
        InitializeComponent();
        HowCode.Code = string.Join("\n\n", Snippets.LiquidTypeFlow, Snippets.LiquidTypeGoo, Snippets.LiquidTypeGlitch, Snippets.LiquidTypeWave);
    }

    protected override void OnAttached()
    {
        GlitchIn();
        Strength.IsEnabled = !Presto.Defaults.ReducedMotion;
        if (Presto.Defaults.ReducedMotion) return;
        Flow();
        Fuse();
        AfterFirstArrange(VectorWord, Wave);
    }

    #region snippet LiquidTypeFlow
    private void Flow()
    {
        Scope.Add(_ => FlowFx.Bind(Distort.TurbulenceProperty, Strength.GetObservable(RangeBase.ValueProperty)).Dispose);
        Raster.Turbulence = 6;
        // Fractal noise does not repeat, so the phase flows back and forth instead of jumping on each loop.
        Presto.Animate(new[] { FlowFx, Raster }, new AnimationParams
        {
            ["TurbulencePhase"] = TweenValue.FromTo(0, 24),
            Duration = 40000,
            Ease = "inOutSine",
            Alternate = true,
            Loop = true,
        });
    }
    #endregion

    #region snippet LiquidTypeGoo
    private void Fuse()
    {
        FuseFx.Goo = 14;
        Presto.Animate(new[] { LeftWord, RightWord }, new AnimationParams
        {
            [X] = (FunctionValue)((_, i, _) => i == 0 ? 72 : -72),
            Duration = 1400,
            Ease = "inOutSine",
            Alternate = true,
            Loop = true,
            LoopDelay = 500,
        });
    }
    #endregion

    #region snippet LiquidTypeGlitch
    private void GlitchHeading(object? sender, PointerEventArgs e)
    {
        if (Presto.Defaults.ReducedMotion) return;
        Scope.Execute(_ => Presto.Animate(Heading, new AnimationParams
        {
            ["Glitch"] = new TweenParams[]
            {
                new() { To = 10, Duration = 60 },
                new() { To = -6, Duration = 80 },
                new() { To = 14, Duration = 60 },
                new() { To = 0, Duration = 100 },
            },
            Ease = "linear",
        }));
    }

    private void RaiseError(object? sender, RoutedEventArgs e) => Scope.Execute(_ => GlitchIn());

    private void GlitchIn() => Presto.Animate(Banner, new AnimationParams
    {
        ["Opacity"] = new TweenParams { From = 0, To = 1, Duration = 120 },
        ["Glitch"] = TweenValue.FromTo(24, 0),
        Duration = 700,
        Ease = "outQuad",
        WhenReduced = new() { ["Opacity"] = TweenValue.FromTo(0, 1), Duration = 300 },
    });
    #endregion

    #region snippet LiquidTypeWave
    private void Wave()
    {
        PrestoAvalonia.SplitText(VectorWord, new SplitTextParams { Words = null, Chars = new SplitPieceParams(), Outlines = true });
        TextWarp.SetKind(VectorWord, TextWarpKind.Wave);
        Presto.Animate(VectorWord, new AnimationParams { [Warp] = 0.3, [WarpRadius] = 160, Duration = 800 });
        Presto.Animate(VectorWord, new AnimationParams
        {
            [WarpPhase] = TweenValue.FromTo(0, 2 * Math.PI),
            Duration = 2400,
            Ease = "linear",
            Loop = true,
        });
    }
    #endregion
}
