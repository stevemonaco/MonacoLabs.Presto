using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.AvaloniaBridge.Draggables;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Showcase.Pages;

public partial class MicroInteractionsPage : ShowcasePage
{
    public static readonly AnimationBuilder PressIn = Presto.Build().Scale(.92).Duration(120).Ease(Eases.OutQuad);
    public static readonly AnimationBuilder Pop = Presto.Build().Scale(k => k.To(1.1, duration: 110).To(1, duration: 420, ease: Eases.OutBack()));
    public static readonly AnimationBuilder TabLift = Presto.Build().Y(-2).Duration(180).Ease(Eases.OutQuad);

    private const int BurstCount = 8;
    private const double BurstRadius = 34;
    private const double TabInset = 10;
    private const double StretchDuration = 170;
    private const double ContractDuration = 320;
    private const double RestackDuration = 380;
    private const double ToastLife = 4000;
    private const double ToastTravel = 320;
    private const double SwipeThreshold = 90;

    private static readonly Geometry Arc = Geometry.Parse("M 10,2 A 8,8 0 1 1 2,10");
    private static readonly Geometry Check = Geometry.Parse("M 3,10.5 L 8,15 L 17,5");
    private static readonly string[] Messages = ["Order #1042 shipped", "Backup finished", "3 new comments", "Invite accepted"];

    private Timeline? _submitting;
    private Animatable? _underline;
    private Timer? _contract;
    private int _likes = 1203;
    private bool _liked;
    private int _notified;

    public MicroInteractionsPage()
    {
        InitializeComponent();
        LikeCount.Text = Count(_likes);
        for (var i = 0; i < BurstCount; i++)
        {
            Burst.Children.Add(new Border
            {
                Width = 7,
                Height = 7,
                CornerRadius = new CornerRadius(3.5),
                Background = Palette.Coral,
                Opacity = 0,
                [Canvas.LeftProperty] = -3.5d,
                [Canvas.TopProperty] = -3.5d,
            });
        }
    }

    protected override void OnAttached()
    {
        _underline = Underline.CreateAnimatable(new() { ["X"] = StretchDuration, ["Width"] = StretchDuration, Ease = Eases.OutCubic });
        _contract = Presto.CreateTimer(new TimerParams { Duration = StretchDuration, Autoplay = false, OnComplete = _ => Contract() });
        Tabs.LayoutUpdated += PlaceUnderline;

        foreach (var section in Accordion.Children) section.AnimateLayout(RestackDuration, Eases.OutQuint, LayoutAnimateMode.PositionAndSize);
    }

    private static string Count(double likes) => likes.ToString("N0", CultureInfo.InvariantCulture);

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
            if (!_liked) return;
            Presto.Animate(Burst.Children, new AvaloniaAnimationParams
            {
                ["X"] = (FunctionValue)((_, i, total) => TweenValue.FromTo(0, BurstRadius * Math.Cos(i * 2 * Math.PI / total))),
                ["Y"] = (FunctionValue)((_, i, total) => TweenValue.FromTo(0, BurstRadius * Math.Sin(i * 2 * Math.PI / total))),
                ["Scale"] = TweenValue.FromTo(1.4, .2),
                ["Opacity"] = new TweenParams { From = 1, To = 0, Ease = Eases.InQuad },
                Duration = 560,
                Delay = Presto.Stagger(18),
                Ease = Eases.OutQuint,
            });
        });
    }

    private Rect? SelectedTab() =>
        Tabs.ContainerFromIndex(Tabs.SelectedIndex) is { } tab && tab.TranslatePoint(default, Rail) is { } at
            ? new Rect(at.X + TabInset, 0, tab.Bounds.Width - (2 * TabInset), 0)
            : null;

    private void PlaceUnderline(object? sender, EventArgs e)
    {
        if (SelectedTab() is not { } tab || _underline is null) return;
        Tabs.LayoutUpdated -= PlaceUnderline;
        _underline.Set("X", tab.X, 0).Set("Width", tab.Width, 0);
    }

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

    private void Expanding(object? sender, CancelRoutedEventArgs e)
    {
        foreach (var section in Accordion.Children)
        {
            if (section != sender && section is Expander expander) expander.IsExpanded = false;
        }
    }

    private void NotifyClicked(object? sender, RoutedEventArgs e) => Scope.Execute(_ =>
    {
        var bar = new ProgressBar { Maximum = 1, Value = 1 };
        var toast = new Border
        {
            Classes = { "toast" },
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = Messages[_notified++ % Messages.Length], FontWeight = FontWeight.SemiBold, Foreground = Palette.Ink },
                    new TextBlock { Classes = { "caption" }, Text = "Hover to hold it, swipe it away" },
                    bar,
                },
            },
        };
        // The slot carries the restack and the toast its own entry, so a toast pushed down mid-entry keeps sliding in.
        var slot = new Panel { Children = { toast } };
        slot.AnimateLayout(RestackDuration, Eases.OutQuint);
        Toasts.Children.Insert(0, slot);
        toast.AnimateOnCompositor(new CompositorAnimationParams
        {
            ["X"] = TweenValue.FromTo(ToastTravel, 0),
            ["Opacity"] = TweenValue.FromTo(0, 1),
            Duration = 420,
            Ease = Eases.OutQuint,
        });

        var gone = false;
        var life = Presto.CreateTimer(new TimerParams
        {
            Duration = ToastLife,
            OnUpdate = timer => bar.Value = 1 - timer.Progress,
            OnComplete = _ => Dismiss(1),
        });
        PrestoAvalonia.CreateDraggable(toast, new DraggableParams
        {
            Y = new DraggableAxisParams { Enabled = false },
            Snap = _ => 0,
            OnRelease = drag =>
            {
                if (Math.Abs(drag.X) < SwipeThreshold) return;
                drag.Disable();
                Dismiss(Math.Sign(drag.X));
            },
        });
        toast.PointerEntered += (_, _) => life.Pause();
        toast.PointerExited += (_, _) =>
        {
            if (!gone) life.Resume();
        };

        void Dismiss(double direction)
        {
            if (gone) return;
            gone = true;
            Scope.Execute(_ => toast.Animate(new()
            {
                ["X"] = direction * ToastTravel,
                ["Opacity"] = 0,
                Duration = 260,
                Ease = Eases.InQuad,
                OnComplete = _ => Toasts.Children.Remove(slot),
            }));
        }
    });

    private void SubscribeClicked(object? sender, RoutedEventArgs e)
    {
        var text = Email.Text ?? "";
        var at = text.IndexOf('@');
        var valid = at > 0 && text.IndexOf('.', at) > at + 1;
        Email.Classes.Set("invalid", !valid);
        Hint.Classes.Set("error", !valid);
        Hint.Text = valid ? $"Subscribed {text}." : "That doesn't look like an email address.";
        if (!valid)
        {
            Scope.Execute(_ => Email.Animate(new()
            {
                ["X"] = TweenValue.Keyframes(-10, 10, -7, 7, -3, 0),
                Duration = 420,
                Ease = Eases.InOutSine,
            }));
        }
    }

    private void EmailChanged(object? sender, TextChangedEventArgs e) => Email.Classes.Remove("invalid");
}
