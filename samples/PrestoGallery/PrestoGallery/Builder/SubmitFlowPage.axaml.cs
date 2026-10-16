using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;

namespace PrestoGallery.Builder;

/// <summary>A submit button that collapses to a spinner, draws a tick and throws confetti or draws a cross on the outcome and springs back, one awaited recipe per step.</summary>
public partial class SubmitFlowPage : ShowcasePage
{
    #region snippet SubmitFlowRecipes
    private static readonly AnimationBuilder Collapse = Presto.Build()
        .Width(48)
        .CornerRadius(new CornerRadius(24))
        .Duration(380)
        .Ease(Eases.InOutQuint)
        .WhenReduced(reduced => reduced.Width(48).CornerRadius(new CornerRadius(24)).Duration(0));

    private static readonly AnimationBuilder Hide = Presto.Build()
        .Opacity(0)
        .Scale(.6)
        .Duration(160)
        .WhenReduced(reduced => reduced.Opacity(0));

    private static readonly AnimationBuilder Show = Presto.Build()
        .Opacity(from: 0, to: 1)
        .Scale(from: .6, to: 1)
        .Duration(220)
        .WhenReduced(reduced => reduced.Opacity(from: 0, to: 1));

    private static readonly AnimationBuilder Spin = Presto.Build()
        .Rotate(from: 0, to: 360)
        .Duration(700)
        .Ease(Eases.None)
        .Loop()
        .WhenReduced(reduced => reduced.Loop(false));

    private static readonly AnimationBuilder Succeed = Presto.Build()
        .Background(Palette.Aqua)
        .Duration(420);

    private static readonly AnimationBuilder Pop = Recipes.Pulse(intensity: 2.5);

    private static readonly AnimationBuilder Fail = Presto.Build()
        .Background(Palette.Coral)
        .Duration(460);

    private static readonly AnimationBuilder Shake = Recipes.Shake();

    private static readonly AnimationBuilder DrawIn = Recipes.DrawIn(duration: 420, ease: Eases.OutQuad);

    private static readonly AnimationBuilder Erase = Recipes.DrawOut(duration: 260, ease: Eases.InQuad);

    private static readonly AnimationBuilder Expand = Presto.Build()
        .Width(200)
        .CornerRadius(new CornerRadius(8))
        .Background(Palette.Accent)
        .Ease(Eases.Spring(bounce: 0, duration: 650))
        .WhenReduced(reduced => reduced.Width(200).CornerRadius(new CornerRadius(8)).Background(Palette.Accent).Duration(0).Ease(Eases.None));

    private static readonly Func<Control, ParticleEffect> Celebrate = button => Recipes.Confetti(button, count: 80);
    #endregion

    public SubmitFlowPage() => InitializeComponent();

    /// <summary>Completes when the flow the last click started has sprung back; <see langword="null"/> before the first click.</summary>
    public Task? Flow { get; private set; }

    protected override void OnAttached()
    {
        PrestoAvalonia.CreateDrawable(Tick);
        PrestoAvalonia.CreateDrawable(Cross);
    }

    private void SubmitClicked(object? sender, RoutedEventArgs e)
    {
        if (Flow is { IsCompleted: false }) return;
        Flow = Send(FailBox.IsChecked == true);
    }

    #region snippet SubmitFlowSend
    private async Task Send(bool fail)
    {
        Label.Animate(Hide);
        await Resize(Collapse);
        Spinner.Animate(Show);
        var spin = Scope.Execute(_ => Spinner.Animate(Spin));

        await Task.Delay(1400);

        spin.Pause();
        Spinner.Animate(Hide);
        var mark = fail ? Cross : Tick;
        Submit.Animate(fail ? Fail : Succeed);
        Field.Animate(fail ? Shake : Pop);
        if (!fail) _ = Celebrate(Submit);
        await mark.Animate(DrawIn);
        await Presto.Delay(700);

        await mark.Animate(Erase);
        Label.Text = fail ? "Try again" : "Send another";
        Label.Animate(Show);
        await Resize(Expand);
    }

    // Unrounded while the width moves, so at fractional scaling both edges glide instead of stepping one side at a time.
    private async Task Resize(AnimationBuilder resize)
    {
        Submit.UseLayoutRounding = false;
        await Submit.Animate(resize);
        Submit.UseLayoutRounding = true;
    }
    #endregion
}
