using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Builder;

namespace PrestoSample.Showcase.Pages;

public partial class MicroInteractionsPage : ShowcasePage
{
    #region snippet MicroInteractionsBuilders
    public static readonly AnimationBuilder PressIn = Presto.Build().Scale(.92).Duration(120).Ease(Eases.OutQuad);
    public static readonly AnimationBuilder Pop = Presto.Build().Scale(k => k.To(1.1, duration: 110).To(1, duration: 420, ease: Eases.OutBack()));
    public static readonly AnimationBuilder TabLift = Presto.Build().Y(-2).Duration(180).Ease(Eases.OutQuad);
    #endregion

    #region snippet MicroInteractionsTabIndicator
    public static readonly AnimationBuilder TabGlide = Presto.Build().Duration(490).Ease(Eases.OutCubic);
    #endregion

    private int _likes = 1203;
    private bool _liked;
    private bool _saving;
    private int _notices;

    public MicroInteractionsPage()
    {
        InitializeComponent();
        LikeCount.Text = Count(_likes);
        Code.Xaml = string.Join("\n\n", Snippets.MicroInteractionsRipple, Snippets.MicroInteractionsSubmitButton, Snippets.MicroInteractionsSaveButton, Snippets.MicroInteractionsLike, Snippets.MicroInteractionsTabs,
            Snippets.MicroInteractionsAccordion, Snippets.MicroInteractionsToasts, Snippets.MicroInteractionsMagnetic);
        Code.CSharp = string.Join("\n\n", Snippets.MicroInteractionsBuilders, Snippets.MicroInteractionsSubmit, Snippets.MicroInteractionsSave, Snippets.MicroInteractionsLikeHandler,
            Snippets.MicroInteractionsTabIndicator, Snippets.MicroInteractionsToastShow, Snippets.MicroInteractionsValidation);
    }

    private static string Count(double likes) => likes.ToString("N0", CultureInfo.InvariantCulture);

    #region snippet MicroInteractionsSubmit
    private void SubmitClicked(object? sender, RoutedEventArgs e)
    {
        if (SubmitButton.GetState(Submit) != SubmitState.Idle) return;
        // A Presto timer stands in for the request.
        _ = SubmitButton.Run(Submit, Presto.CreateTimer(new() { Duration = 1100 }).Then());
    }
    #endregion

    #region snippet MicroInteractionsSave
    private void SaveClicked(object? sender, RoutedEventArgs e)
    {
        if (!_saving) _ = SaveAsync();
    }

    // Inter has ✓, so the label's outlines morph into it; under reduced motion MorphGlyphs crossfades.
    private async Task SaveAsync()
    {
        _saving = true;
        await Presto.CreateTimer(new() { Duration = 600 });
        await Recipes.MorphGlyphs(SaveLabel, "✓");
        await Presto.CreateTimer(new() { Duration = 1400 });
        await Recipes.MorphGlyphs(SaveLabel, "Save");
        _saving = false;
    }
    #endregion

    #region snippet MicroInteractionsLikeHandler
    private void LikeClicked(object? sender, RoutedEventArgs e)
    {
        var from = _likes;
        _liked = !_liked;
        _likes += _liked ? 1 : -1;
        Heart.Foreground = _liked ? Palette.Coral : Palette.InkDim;
        Scope.Execute(_ =>
        {
            LikeCount.Animate(new()
            {
                ["Text"] = new TweenParams { From = from, To = _likes, TextModifier = Count },
                Duration = 400,
                Ease = Eases.OutQuad,
            });
            if (_liked) Recipes.Burst(Heart, color: Palette.Coral.Color, reduced: Recipes.Fade());
        });
    }
    #endregion

    #region snippet MicroInteractionsValidation
    private void SubscribeClicked(object? sender, RoutedEventArgs e)
    {
        var text = Email.Text ?? "";
        var at = text.IndexOf('@');
        var valid = at > 0 && text.IndexOf('.', at) > at + 1;
        Email.Classes.Set("invalid", !valid);
        Hint.Classes.Set("error", !valid);
        Hint.Text = valid ? $"Subscribed {text}." : "That doesn't look like an email address.";
        if (!valid) Scope.Execute(_ => Email.Animate(Recipes.Shake()));
    }
    #endregion

    #region snippet MicroInteractionsToastShow
    private void NotifyClicked(object? sender, RoutedEventArgs e) => Toasts.Show(Notice.Sample(_notices++));
    #endregion

    private void EmailChanged(object? sender, TextChangedEventArgs e) => Email.Classes.Remove("invalid");
}
