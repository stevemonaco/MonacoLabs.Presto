using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Draggables;
using PrestoSample.Builder;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Showcase.Pages;

public partial class MicroInteractionsPage : ShowcasePage
{
    #region snippet MicroInteractionsBuilders
    public static readonly AnimationBuilder PressIn = Presto.Build().Scale(.92).Duration(120).Ease(Eases.OutQuad);
    public static readonly AnimationBuilder Pop = Presto.Build().Scale(k => k.To(1.1, duration: 110).To(1, duration: 420, ease: Eases.OutBack()));
    public static readonly AnimationBuilder TabLift = Presto.Build().Y(-2).Duration(180).Ease(Eases.OutQuad);
    #endregion

    private const double TabInset = 10;
    private const double StretchDuration = 170;
    private const double ContractDuration = 320;
    private const double ToastLife = 4000;
    private const double SwipeThreshold = 90;

    private static readonly Geometry Arc = Geometry.Parse("M 10,2 A 8,8 0 1 1 2,10");
    private static readonly Geometry Check = Geometry.Parse("M 3,10.5 L 8,15 L 17,5");

    private Timeline? _submitting;
    private Animatable? _underline;
    private Timer? _contract;
    private int _likes = 1203;
    private bool _liked;

    public MicroInteractionsPage()
    {
        InitializeComponent();
        LikeCount.Text = Count(_likes);
        Code.Xaml = string.Join("\n\n", Snippets.MicroInteractionsRipple, Snippets.MicroInteractionsLike, Snippets.MicroInteractionsTabs,
            Snippets.MicroInteractionsAccordion, Snippets.MicroInteractionsToasts);
        Code.CSharp = string.Join("\n\n", Snippets.MicroInteractionsBuilders, Snippets.MicroInteractionsSubmit, Snippets.MicroInteractionsLikeHandler,
            Snippets.MicroInteractionsTabIndicator, Snippets.MicroInteractionsAccordionHandler, Snippets.MicroInteractionsToastSwipe, Snippets.MicroInteractionsValidation);
    }

    protected override void OnAttached()
    {
        _underline = Underline.CreateAnimatable(new() { ["X"] = StretchDuration, ["Width"] = StretchDuration, Ease = Eases.OutCubic });
        _contract = Presto.CreateTimer(new TimerParams { Duration = StretchDuration, Autoplay = false, OnComplete = _ => Contract() });
        AfterFirstArrange(Tabs, () =>
        {
            if (SelectedTab() is { } tab) _underline!.Set("X", tab.X, 0).Set("Width", tab.Width, 0);
        });
    }

    private static string Count(double likes) => likes.ToString("N0", CultureInfo.InvariantCulture);

    #region snippet MicroInteractionsSubmit
    private void SubmitClicked(object? sender, RoutedEventArgs e)
    {
        if (_submitting is { Completed: false }) return;
        var width = Submit.Width;
        var round = Submit.Height / 2;
        Spinner.Data = Arc;
        _submitting = Scope.Execute(_ => Presto.CreateTimeline()
            .Add(SubmitLabel, new() { ["Opacity"] = 0, Duration = 120, Ease = Eases.OutQuad })
            .Add(Submit, new() { ["Width"] = Submit.Height, ["CornerRadius"] = TweenValue.Of(new CornerRadius(round)), Duration = 320, Ease = Eases.InOutQuint }, "<<")
            .Add(Spinner, new() { ["Opacity"] = 1, Duration = 160 }, "-=120")
            .Add(Spinner, new() { ["Rotate"] = TweenValue.FromTo(0, 720), Duration = 1100, Ease = Eases.InOutSine }, "<<")
            .Add(Spinner, new() { ["Data"] = PrestoAvalonia.MorphTo(Check), Duration = 280, Ease = Eases.OutQuad })
            .Add(Submit, new() { ["Background"] = TweenValue.Of(Palette.Aqua), Duration = 280 }, "<<")
            .Add(Spinner, new() { ["Opacity"] = 0, Duration = 160 }, "+=700")
            .Add(Submit, new() { ["Background"] = TweenValue.Of(Palette.Accent), Duration = 300 }, "<<")
            .Add(Submit, new() { ["Width"] = width, ["CornerRadius"] = TweenValue.Of(new CornerRadius(8)), Ease = Eases.Spring(bounce: .4, duration: 600) }, "<<")
            .Add(SubmitLabel, new() { ["Opacity"] = 1, Duration = 240 }, "-=400"));
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

    #region snippet MicroInteractionsTabIndicator
    private Rect? SelectedTab() =>
        Tabs.ContainerFromIndex(Tabs.SelectedIndex) is { } tab ? TabIndicator.Under(tab, Rail, TabInset) : null;

    // Both edges first reach out to cover the old tab and the new one, then the trailing edge catches up.
    private void TabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_underline is null || _contract is null || SelectedTab() is not { } tab) return;
        var left = _underline.Get("X");
        var right = left + _underline.Get("Width");
        var from = Math.Min(left, tab.Left);
        _underline.Set("X", from).Set("Width", Math.Max(right, tab.Right) - from);
        _contract.Restart();
    }

    private void Contract()
    {
        if (SelectedTab() is { } tab) _underline?.Set("X", tab.X, ContractDuration).Set("Width", tab.Width, ContractDuration);
    }
    #endregion

    #region snippet MicroInteractionsAccordionHandler
    private void Expanding(object? sender, CancelRoutedEventArgs e)
    {
        foreach (var section in Accordion.Children)
        {
            if (section != sender && section is Expander expander) expander.IsExpanded = false;
        }
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

    #region snippet MicroInteractionsToastSwipe
    private void ToastLoaded(object? sender, RoutedEventArgs e)
    {
        var toast = (Control)sender!;
        Scope.Execute(_ =>
        {
            toast.Tag = toast.FindDescendantOfType<ProgressBar>()!.Animate(countdown => countdown
                .Property("Value", from: 1, to: 0)
                .Duration(ToastLife)
                .Ease(Eases.None)
                .OnComplete(_ => Dismiss(toast)));
            PrestoAvalonia.CreateDraggable(toast, new DraggableParams
            {
                Y = new DraggableAxisParams { Enabled = false },
                Snap = _ => 0,
                OnRelease = drag =>
                {
                    if (Math.Abs(drag.X) < SwipeThreshold) return;
                    drag.Disable();
                    if (drag.X < 0) Presence.SetExit((Control)toast.Parent!, Recipes.SlideOut(Edge.Left, 320, 260, ease: Eases.InQuad));
                    Dismiss(toast);
                },
            });
        });
    }

    // Held through Tag because GetAnimations drops a paused animation.
    private void HoldToast(object? sender, PointerEventArgs e) => (((Control)sender!).Tag as Animation)?.Pause();

    private void ReleaseToast(object? sender, PointerEventArgs e) => (((Control)sender!).Tag as Animation)?.Resume();
    #endregion

    private void Dismiss(Control toast) => ((ToastsViewModel)Toasts.DataContext!).Toasts.Remove((Toast)toast.DataContext!);

    private void EmailChanged(object? sender, TextChangedEventArgs e) => Email.Classes.Remove("invalid");
}
