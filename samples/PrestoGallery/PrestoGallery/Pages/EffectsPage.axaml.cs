using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Rendering;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;

namespace PrestoGallery.Pages;

/// <summary>
/// The effect shorthands on real controls: blur, element blur against backdrop blur, a live drop shadow, <c>BoxShadow</c> beside <c>DropShadowEffect</c>, a
/// <see cref="Flip"/> card, a pointer tilt, clip and mask reveals and a blur-out exit. Each stage prints the code it ran.
/// </summary>
public partial class EffectsPage : ShowcasePage
{
    #region snippet EffectsBlurOut
    public static readonly AnimationBuilder BlurOut = Presto.Build().Blur(12).Opacity(0).Scale(.96).Duration(420).Ease(Eases.OutQuad);
    #endregion

    public static readonly AnimationBuilder FlipMotion = Presto.Build().Duration(900).Ease(Eases.InOutQuad);

    private static readonly (double Left, double Top, double Size, IBrush Fill)[] Blobs =
    [
        (12, 18, 120, Palette.Coral), (150, 96, 140, Palette.Aqua), (262, 6, 112, Palette.Amber),
        (58, 124, 96, Palette.Violet), (300, 120, 124, Palette.Rose),
    ];

    private static readonly string[] Chores = ["Water the plants", "Reply to Grace", "Book the dentist", "Fix the bike light"];

    private Animatable _tilt = null!;
    private string _tiltCode = "";
    private Animation? _reveal;
    private bool _raised;
    private bool _frosted;

    public EffectsPage()
    {
        InitializeComponent();
        Rows.ItemsSource = Items;
        Refill();
        ExitCode.Xaml = $"{Snippets.EffectsList}\n    {Snippets.EffectsRow}";
        ExitCode.CSharp = Snippets.EffectsBlurOut;
        Populate(ElementScene);
        Populate(BackdropScene);
    }

    public ObservableCollection<string> Items { get; } = [];

    protected override void OnAttached()
    {
        (_tilt, var tilt) = Snippet.Of(TiltCard.CreateAnimatable(new()
        {
            ["RotateX"] = new(),
            ["RotateY"] = new(),
            Ease = new Spring(stiffness: 120, damping: 10),
        }));
        var (_, depth) = Snippet.Of(Presto.Set(TiltCard, b => b.Perspective(500)));
        TiltCode.Code = _tiltCode = $"var tilt = {tilt};\n{depth};";
        ApplyShadow();
        Drift(ElementScene);
        Drift(BackdropScene);
        var top = TopLevel.GetTopLevel(this);
        Scope.Add(_ => () =>
        {
            if (top != null) top.RendererDiagnostics.DebugOverlays = RendererDebugOverlays.None;
            TimingToggle.IsChecked = false;
        });
    }

    private static void Populate(Canvas scene)
    {
        foreach (var (left, top, size, fill) in Blobs)
        {
            var blob = new Ellipse { Width = size, Height = size, Fill = fill };
            Canvas.SetLeft(blob, left);
            Canvas.SetTop(blob, top);
            scene.Children.Add(blob);
        }
        var words = new TextBlock { Text = "presto presto presto", FontSize = 46, FontWeight = FontWeight.Black, Foreground = Brushes.White, Opacity = .8 };
        Canvas.SetLeft(words, -30);
        Canvas.SetTop(words, 150);
        scene.Children.Add(words);
    }

    // Both scenes drift identically, so the two blurs are compared over the same motion; reduced, they hold still.
    private static void Drift(Canvas scene)
    {
        if (Presto.Defaults.ReducedMotion) return;
        Presto.Build()
            .X((_, i, _) => (i % 2 == 0 ? 1 : -1) * (36 + (i * 14)))
            .Y((_, i, _) => (i % 3 == 0 ? -1 : 1) * (20 + (i * 6)))
            .Duration((FunctionValue)((_, i, _) => 2600 + (i * 450)))
            .Ease(Eases.InOutSine)
            .Loop()
            .Alternate()
            .Animate(scene.Children);
    }

    private void FrostClicked(object? sender, RoutedEventArgs e) => Frost(FrostSlider.Value, 600);

    private void ClearClicked(object? sender, RoutedEventArgs e) => Frost(0, 600);

    private void FrostChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_frosted) Frost(FrostSlider.Value, 160);
    }

    private void Frost(double radius, double duration) => Scope.Execute(_ =>
    {
        _frosted = radius > 0;
        var (_, element) = Snippet.Of(ElementScene.Animate(Presto.Build().Blur(radius).Duration(duration).Ease(Eases.OutQuad)));
        var (_, backdrop) = Snippet.Of(Glass.Animate(Presto.Build().BackdropBlur(radius).Duration(duration).Ease(Eases.OutQuad)));
        FrostCode.Code = $"{element};\n{backdrop};";
    });

    private void TimingChanged(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } top) return;
        top.RendererDiagnostics.DebugOverlays = TimingToggle.IsChecked == true
            ? RendererDebugOverlays.Fps | RendererDebugOverlays.RenderTimeGraph
            : RendererDebugOverlays.None;
    }

    private void BlurOutClicked(object? sender, RoutedEventArgs e) =>
        Show(BlurCode, Scope.Execute(_ => Snippet.Of(Presto.Build().Blur(12).Opacity(.4).Duration(600).Ease(Eases.OutQuad).Animate(BlurRow.Children))));

    private void BlurInClicked(object? sender, RoutedEventArgs e) =>
        Show(BlurCode, Scope.Execute(_ => Snippet.Of(Presto.Build().Blur(0).Opacity(1).Duration(600).Ease(Eases.OutQuad).Animate(BlurRow.Children))));

    private void ShadowChanged(object? sender, RangeBaseValueChangedEventArgs e) => ApplyShadow();

    private void ApplyShadow()
    {
        var (x, y, blur, hue, opacity) = (ShadowXSlider.Value, ShadowYSlider.Value, ShadowBlurSlider.Value, ShadowHueSlider.Value, ShadowOpacitySlider.Value);
        Show(ShadowCode, Scope.Execute(_ => Snippet.Of(ShadowCard.Animate(Presto.Build()
            .ShadowX(x)
            .ShadowY(y)
            .ShadowBlur(blur)
            .ShadowColor(HsvColor.ToRgb(hue, .8, 1))
            .ShadowOpacity(opacity)
            .Duration(160)))));
    }

    private void RaiseClicked(object? sender, RoutedEventArgs e) => Scope.Execute(_ =>
    {
        _raised = !_raised;
        var (offsetY, blur, color, y) = _raised ? (18, 36, "#cc4c8dff", -8) : (2, 6, "#664c8dff", 0);
        var (_, box) = Snippet.Of(BoxCard.Animate(Presto.Build().BoxShadow(BoxShadows.Parse($"0 {offsetY} {blur} 0 {color}")).Y(y).Duration(400).Ease(Eases.OutQuart)));
        var (_, effect) = Snippet.Of(EffectCard.Animate(Presto.Build().Shadow(0, offsetY, blur, Color.Parse(color)).Y(y).Duration(400).Ease(Eases.OutQuart)));
        CompareCode.Code = $"{box};\n{effect};";
    });

    private void TiltMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(TiltStage);
        var (_, code) = Snippet.Of(_tilt
            .Set("RotateY", Utils.MapRange(point.X, 0, TiltStage.Bounds.Width, -20, 20))
            .Set("RotateX", Utils.MapRange(point.Y, 0, TiltStage.Bounds.Height, 20, -20)));
        TiltCode.Code = $"{_tiltCode}\n\n// PointerMoved\n{code};";
    }

    private void TiltExited(object? sender, PointerEventArgs e)
    {
        var (_, code) = Snippet.Of(_tilt.Set("RotateX", 0).Set("RotateY", 0));
        TiltCode.Code = $"{_tiltCode}\n\n// PointerExited\n{code};";
    }

    private void InsetClicked(object? sender, RoutedEventArgs e) => Reveal(() =>
        Snippet.Of(Presto.Build().ClipInset(from: "50%", to: "0%").Duration(900).Ease(Eases.OutQuart).Animate(RevealPicture)));

    private void CircleClicked(object? sender, RoutedEventArgs e) => Reveal(() =>
        Snippet.Of(Presto.Build().ClipCircle(from: 0, to: 1).Duration(900).Ease(Eases.OutQuart).Animate(RevealPicture)));

    private void SweepClicked(object? sender, RoutedEventArgs e) => Reveal(() =>
        Snippet.Of(Presto.Build().MaskSweep(from: 0, to: 1).Duration(1100).Ease(Eases.InOutQuad).Animate(RevealPicture)));

    // An inset and a circle cannot share the one clip, so the previous reveal is reverted before the next claims it.
    private void Reveal(Func<(Animation Value, string Code)> run)
    {
        _reveal?.Revert();
        _reveal = Show(RevealCode, Scope.Execute(_ => run()));
    }

    private void RemoveRow(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: string row }) Items.Remove(row);
    }

    private void RefillClicked(object? sender, RoutedEventArgs e) => Refill();

    private void Refill()
    {
        foreach (var chore in Chores) if (!Items.Contains(chore)) Items.Add(chore);
    }

    private static Animation Show(CodePanel output, (Animation Value, string Code) run)
    {
        output.Code = $"{run.Code};";
        return run.Value;
    }
}
