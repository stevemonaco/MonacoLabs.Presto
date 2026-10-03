using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>A submit button that collapses to a spinner, draws a tick and throws confetti or draws a cross on the outcome and springs back, one awaited recipe per step.</summary>
public partial class SubmitFlowPage : ShowcasePage
{
    private static readonly AnimationBuilder Collapse;
    private static readonly AnimationBuilder Expand;
    private static readonly AnimationBuilder Hide;
    private static readonly AnimationBuilder Show;
    private static readonly AnimationBuilder Spin;
    private static readonly AnimationBuilder DrawIn;
    private static readonly AnimationBuilder Erase;
    private static readonly AnimationBuilder Succeed;
    private static readonly AnimationBuilder Fail;
    private static readonly Func<Control, ParticleEffect> Celebrate;
    private static readonly string Code;
    private bool _busy;

    static SubmitFlowPage()
    {
        var declarations = new List<string>();
        AnimationBuilder Recipe(string name, (AnimationBuilder Builder, string Code) recipe)
        {
            declarations.Add(Snippet.Declare($"static readonly AnimationBuilder {name}", recipe.Code));
            return recipe.Builder;
        }

        Collapse = Recipe(nameof(Collapse), Snippet.Of(Presto.Build()
            .Width(48)
            .CornerRadius(new CornerRadius(24))
            .Duration(380)
            .Ease(Eases.InOutQuint)));
        Hide = Recipe(nameof(Hide), Snippet.Of(Presto.Build()
            .Opacity(0)
            .Scale(.6)
            .Duration(160)));
        Show = Recipe(nameof(Show), Snippet.Of(Presto.Build()
            .Opacity(from: 0, to: 1)
            .Scale(from: .6, to: 1)
            .Duration(220)));
        Spin = Recipe(nameof(Spin), Snippet.Of(Presto.Build()
            .Rotate(from: 0, to: 360)
            .Duration(700)
            .Ease(Eases.None)
            .Loop()));
        Succeed = Recipe(nameof(Succeed), Snippet.Of(Presto.Build()
            .Background(Palette.Aqua)
            .Scale(k => k.To(1.15).To(1))
            .Duration(420)
            .Ease(Eases.OutQuad)));
        Fail = Recipe(nameof(Fail), Snippet.Of(Presto.Build()
            .Background(Palette.Coral)
            .X(k => k.To(-10).To(10).To(-6).To(6).To(0))
            .Duration(460)));
        DrawIn = Recipe(nameof(DrawIn), Snippet.Of(Presto.Build()
            .Draw(from: "0 0", to: "0 1")
            .Duration(420)
            .Ease(Eases.OutQuad)));
        Erase = Recipe(nameof(Erase), Snippet.Of(Presto.Build()
            .Draw("1 1")
            .Duration(260)
            .Ease(Eases.InQuad)));
        Expand = Recipe(nameof(Expand), Snippet.Of(Presto.Build()
            .Width(200)
            .CornerRadius(new CornerRadius(8))
            .Background(Palette.Accent)
            .Ease(Eases.Spring(bounce: .35, duration: 650))));
        (Celebrate, var celebrate) = Snippet.Of<Func<Control, ParticleEffect>>(button => Recipes.Confetti(button, count: 80));
        declarations.Add(Snippet.Declare("static readonly Func<Control, ParticleEffect> Celebrate", celebrate));
        Code = string.Join("\n\n", declarations);
    }

    public SubmitFlowPage()
    {
        InitializeComponent();
        RecipesCode.Code = Code;
    }

    /// <summary>Completes when the flow the last click started has sprung back; <see langword="null"/> before the first click.</summary>
    public Task? Flow { get; private set; }

    protected override void OnAttached()
    {
        PrestoAvalonia.CreateDrawable(Tick);
        PrestoAvalonia.CreateDrawable(Cross);
    }

    private void SubmitClicked(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        Flow = Send(FailBox.IsChecked == true);
    }

    private async Task Send(bool fail)
    {
        _busy = true;
        Label.Animate(Hide);
        await Submit.Animate(Collapse);
        Spinner.Animate(Show);
        var spin = Scope.Execute(_ => Spinner.Animate(Spin));

        await Presto.CreateTimer(new() { Duration = 1400 });

        spin.Pause();
        Spinner.Animate(Hide);
        var mark = fail ? Cross : Tick;
        Submit.Animate(fail ? Fail : Succeed);
        if (!fail) _ = Celebrate(Submit);
        await mark.Animate(DrawIn);
        await Presto.CreateTimer(new() { Duration = 700 });

        await mark.Animate(Erase);
        Label.Text = fail ? "Try again" : "Send another";
        Label.Animate(Show);
        await Submit.Animate(Expand);
        _busy = false;
    }
}
