using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Draggables;

namespace PrestoSample.Showcase.Pages;

public partial class CarouselPage : ShowcasePage
{
    private const double AutoplayInterval = 4000;
    private const double DotWidth = 8;
    private const double ActiveDotWidth = 24;
    private const double DotPitch = 16;
    private const double MarkDuration = 420;
    // Each layer's offset as a share of its slide's distance from centre: negative lags behind the strip, positive leads it.
    private const double BackgroundDepth = -.5;
    private const double ArtDepth = -.2;
    private const double TitleLead = .35;

    private static readonly (string Title, string Blurb, string Icon, ImmutableSolidColorBrush Tint)[] Slides =
    [
        ("Throw it", "An x-only draggable snaps to the slide width and settles on a spring.", "PlayIcon", Palette.Accent),
        ("Parallax", "Every layer reads the strip's X in OnUpdate and moves at its own rate.", "StarIcon", Palette.Aqua),
        ("Indicators", "The active dot widens through a Width tween while the ring draws the autoplay.", "HeartIcon", Palette.Coral),
        ("Autoplay", "A looping Draw animation advances every four seconds and holds while hovered.", "GearIcon", Palette.AccentBright),
        ("Keyboard", "Left and Right, or the arrows, spring the draggable's own X to a slide.", "StarIcon", Palette.AccentDim),
    ];

    private readonly List<(TranslateTransform Background, TranslateTransform Art, TranslateTransform Title)> _layers = [];
    private Draggable? _drag;
    private Animation? _autoplay;
    private Animation? _travel;
    private double _slideWidth;
    private int _active;
    private bool _hovered;
    private bool _dragging;

    public CarouselPage()
    {
        InitializeComponent();
        for (var i = 0; i < Slides.Length; i++)
        {
            Strip.Children.Add(BuildSlide(i));
            Dots.Children.Add(new Border());
        }
        var first = (Border)Dots.Children[0];
        first.Width = ActiveDotWidth;
        first.Background = Palette.Accent;
    }

    protected override void OnAttached()
    {
        _autoplay = PrestoAvalonia.CreateDrawable(Ring).Animate(new()
        {
            ["Draw"] = TweenValue.FromTo("0 0", "0 1"),
            Duration = AutoplayInterval,
            Ease = Eases.None,
            Loop = true,
            OnLoop = _ => GoTo(_active + 1),
        });
        _drag = PrestoAvalonia.CreateDraggable(Strip, new DraggableParams
        {
            Y = new DraggableAxisParams { Enabled = false },
            ContainerBounds = _ => new Rect(-(Slides.Length - 1) * _slideWidth, 0, (Slides.Length - 1) * _slideWidth, 0),
            Snap = x => Utils.Snap(x, _slideWidth),
            ReleaseStiffness = 180,
            ReleaseDamping = 22,
            OnGrab = _ =>
            {
                _travel?.Pause();
                _dragging = true;
                Viewport.Focus();
                Hold();
            },
            OnUpdate = drag => Parallax(drag.X),
            OnSnap = drag => Mark((int)Math.Round(-drag.X / _slideWidth)),
            OnRelease = _ =>
            {
                _dragging = false;
                _autoplay.Restart();
                Hold();
            },
        });
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is not (Key.Left or Key.Right)) return;
        Step(e.Key == Key.Left ? -1 : 1);
        e.Handled = true;
    }

    private Panel BuildSlide(int index)
    {
        var (title, blurb, icon, tint) = Slides[index];
        var background = new TranslateTransform();
        var art = new TranslateTransform();
        var text = new TranslateTransform();
        _layers.Add((background, art, text));
        return new Panel
        {
            ClipToBounds = true,
            Children =
            {
                new Panel
                {
                    RenderTransform = background,
                    Background = new LinearGradientBrush
                    {
                        StartPoint = RelativePoint.TopLeft,
                        EndPoint = RelativePoint.BottomRight,
                        GradientStops = { new GradientStop(Color.FromArgb(0x55, tint.Color.R, tint.Color.G, tint.Color.B), 0), new GradientStop(Palette.Surface.Color, .75) },
                    },
                    Children =
                    {
                        new Ellipse { Width = 420, Height = 420, Margin = new Thickness(0, -140, -120, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Fill = tint, Opacity = .12 },
                        new Ellipse { Width = 160, Height = 160, Margin = new Thickness(-40, 0, 0, -60), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Fill = tint, Opacity = .18 },
                    },
                },
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    Margin = new Thickness(96, 0),
                    Children =
                    {
                        new StackPanel
                        {
                            RenderTransform = text,
                            Spacing = 10,
                            VerticalAlignment = VerticalAlignment.Center,
                            Children =
                            {
                                new TextBlock { Classes = { "eyebrow", "showcase" }, Text = $"0{index + 1} / 0{Slides.Length}" },
                                new TextBlock { Text = title, FontSize = 40, FontWeight = FontWeight.SemiBold, Foreground = Palette.Ink },
                                new TextBlock { Text = blurb, FontSize = 14, TextWrapping = TextWrapping.Wrap, Foreground = Palette.InkDim },
                            },
                        },
                        new Border
                        {
                            [Grid.ColumnProperty] = 1,
                            RenderTransform = art,
                            Width = 220,
                            Height = 160,
                            CornerRadius = new CornerRadius(16),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Background = new LinearGradientBrush
                            {
                                StartPoint = RelativePoint.TopLeft,
                                EndPoint = RelativePoint.BottomRight,
                                GradientStops = { new GradientStop(tint.Color, 0), new GradientStop(Palette.AccentDeep.Color, 1) },
                            },
                            Child = new PathIcon
                            {
                                Width = 56,
                                Height = 56,
                                Foreground = Palette.Ink,
                                Data = (Geometry)Application.Current!.FindResource(icon)!,
                            },
                        },
                    },
                },
            },
        };
    }

    private void Parallax(double x)
    {
        for (var i = 0; i < _layers.Count; i++)
        {
            var offset = x + (i * _slideWidth);
            var (background, art, title) = _layers[i];
            background.X = offset * BackgroundDepth;
            art.X = offset * ArtDepth;
            title.X = offset * TitleLead;
        }
    }

    private void Step(int step)
    {
        GoTo(_active + step);
        _autoplay?.Restart();
        Hold();
    }

    private void GoTo(int index)
    {
        if (_drag is null) return;
        index = (index + Slides.Length) % Slides.Length;
        Mark(index);
        _travel = Scope.Execute(_ => Presto.Animate(_drag, new()
        {
            ["X"] = -index * _slideWidth,
            Ease = Eases.Spring(bounce: .2, duration: 700),
        }));
    }

    private void Mark(int index)
    {
        if (index == _active) return;
        _active = index;
        Scope.Execute(_ =>
        {
            Presto.Animate(Dots.Children, new AvaloniaAnimationParams
            {
                ["Width"] = (FunctionValue)((_, i, _) => i == index ? ActiveDotWidth : DotWidth),
                ["Background"] = (FunctionValue)((_, i, _) => TweenValue.Of(i == index ? Palette.Accent : Palette.Border)),
                Duration = MarkDuration,
                Ease = Eases.OutQuint,
            });
            Ring.Animate(new() { ["X"] = index * DotPitch, Duration = MarkDuration, Ease = Eases.OutQuint });
        });
    }

    private void Hold()
    {
        if (_hovered || _dragging) _autoplay?.Pause();
        else _autoplay?.Resume();
    }

    private void ViewportResized(object? sender, SizeChangedEventArgs e)
    {
        _slideWidth = e.NewSize.Width;
        foreach (var slide in Strip.Children)
        {
            slide.Width = _slideWidth;
            slide.Height = e.NewSize.Height;
        }
        if (_drag is null) return;
        _travel?.Pause();
        _drag.Refresh();
        _drag.X = -_active * _slideWidth;
        Parallax(_drag.X);
    }

    private void ViewportEntered(object? sender, PointerEventArgs e)
    {
        _hovered = true;
        Hold();
    }

    private void ViewportExited(object? sender, PointerEventArgs e)
    {
        _hovered = false;
        Hold();
    }

    private void PreviousClicked(object? sender, RoutedEventArgs e) => Step(-1);

    private void NextClicked(object? sender, RoutedEventArgs e) => Step(1);
}
