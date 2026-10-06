using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering;

namespace Monaco.Presto.AvaloniaBridge.Controls;

/// <summary>
/// Shows where a <see cref="Pager"/> is: a row of dots whose selected one widens, the change staggering out from it, or a
/// progress bar that follows the strip as it moves. Tapping a dot, or a stretch of the bar, sends the pager to that slide.
/// </summary>
/// <remarks>Under <see cref="PrestoDefaults.ReducedMotion"/>, read at each selection change, the dots change at once.</remarks>
public sealed class PagerIndicator : Control, ICustomHitTest
{
    private const double Dot = 8;
    private const double ActiveDot = 24;
    private const double HitHeight = 24;

    /// <summary>The pager the indicator follows.</summary>
    public static readonly StyledProperty<Pager?> PagerProperty =
        AvaloniaProperty.Register<PagerIndicator, Pager?>(nameof(Pager));

    /// <summary>Dots or a progress bar; dots by default.</summary>
    public static readonly StyledProperty<PagerIndicatorMode> ModeProperty =
        AvaloniaProperty.Register<PagerIndicator, PagerIndicatorMode>(nameof(Mode));

    /// <summary>The selected dot and the filled part of the bar; white by default.</summary>
    public static readonly StyledProperty<IBrush> FillProperty =
        AvaloniaProperty.Register<PagerIndicator, IBrush>(nameof(Fill), Brushes.White);

    /// <summary>The other dots and the bar's track; 40 % white by default.</summary>
    public static readonly StyledProperty<IBrush> TrackProperty =
        AvaloniaProperty.Register<PagerIndicator, IBrush>(nameof(Track), new ImmutableSolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)));

    private Pager? _connected;

    static PagerIndicator() => AffectsMeasure<PagerIndicator>(ModeProperty);

    /// <summary>Creates an indicator that pages its pager when tapped.</summary>
    public PagerIndicator()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
        Tapped += HandleTapped;
    }

    /// <inheritdoc cref="PagerProperty"/>
    public Pager? Pager
    {
        get => GetValue(PagerProperty);
        set => SetValue(PagerProperty, value);
    }

    /// <inheritdoc cref="ModeProperty"/>
    public PagerIndicatorMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <inheritdoc cref="FillProperty"/>
    public IBrush Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <inheritdoc cref="TrackProperty"/>
    public IBrush Track
    {
        get => GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    private int Count => _connected?.Slides.Count ?? 0;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PagerProperty && TopLevel.GetTopLevel(this) is not null) Connect(Pager);
        else if (change.Property == ModeProperty || change.Property == FillProperty || change.Property == TrackProperty) Build();
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Connect(Pager);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Connect(null);
    }

    // The dots take no hits, so the indicator owns the hand cursor and the tap across its whole strip, in a band taller than the dots.
    bool ICustomHitTest.HitTest(Point point) =>
        new Rect(Bounds.Size).Inflate(new Thickness(0, Math.Max(0, (HitHeight - Bounds.Height) / 2))).Contains(point);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0d;
        foreach (var child in VisualChildren)
        {
            ((Control)child).Measure(availableSize);
            width += ((Control)child).DesiredSize.Width;
        }
        return Mode == PagerIndicatorMode.Progress
            ? new Size(double.IsInfinity(availableSize.Width) ? Count * ActiveDot : availableSize.Width, Dot / 2)
            : new Size(width + (Math.Max(0, Count - 1) * Dot), Dot);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Mode == PagerIndicatorMode.Progress)
        {
            if (VisualChildren is [Control track, Control bar])
            {
                track.Arrange(new Rect(finalSize));
                bar.Arrange(new Rect(0, 0, finalSize.Width * Progress(), finalSize.Height));
            }
            return finalSize;
        }
        var x = 0d;
        foreach (var child in VisualChildren)
        {
            var dot = (Control)child;
            dot.Arrange(new Rect(x, (finalSize.Height - Dot) / 2, dot.DesiredSize.Width, Dot));
            x += dot.DesiredSize.Width + Dot;
        }
        return finalSize;
    }

    private void Connect(Pager? pager)
    {
        if (pager == _connected) return;
        if (_connected is { } old)
        {
            old.PropertyChanged -= HandlePagerChanged;
            old.Slides.CollectionChanged -= HandleSlidesChanged;
            old.Position.Changed -= HandleMoved;
        }
        _connected = pager;
        if (pager is not null)
        {
            pager.PropertyChanged += HandlePagerChanged;
            pager.Slides.CollectionChanged += HandleSlidesChanged;
            pager.Position.Changed += HandleMoved;
        }
        Build();
    }

    private void Build()
    {
        VisualChildren.Clear();
        if (Mode == PagerIndicatorMode.Progress)
        {
            VisualChildren.Add(new Border { IsHitTestVisible = false, Background = Track, CornerRadius = new CornerRadius(Dot / 4) });
            VisualChildren.Add(new Border { IsHitTestVisible = false, Background = Fill, CornerRadius = new CornerRadius(Dot / 4) });
        }
        else
        {
            var selected = _connected?.SelectedIndex;
            for (var i = 0; i < Count; i++)
            {
                VisualChildren.Add(new Border
                {
                    IsHitTestVisible = false,
                    Width = i == selected ? ActiveDot : Dot,
                    Height = Dot,
                    CornerRadius = new CornerRadius(Dot / 2),
                    Background = i == selected ? Fill : Track,
                });
            }
        }
        InvalidateMeasure();
    }

    private double Progress()
    {
        if (Count == 0) return 0;
        var page = _connected!.Position.Value;
        page = _connected.Loop ? ((page % Count) + Count) % Count : Math.Clamp(page, 0, Count - 1);
        return Math.Min(1, (page + 1) / Count);
    }

    private void HandlePagerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Pager.SelectedIndexProperty || Mode != PagerIndicatorMode.Dots) return;
        var index = _connected!.SelectedIndex;
        var reduced = Engine.Current.Defaults.ReducedMotion;
        Presto.Animate(VisualChildren.ToArray(), new AvaloniaAnimationParams
        {
            ["Width"] = (FunctionValue)((_, i, _) => i == index ? ActiveDot : Dot),
            ["Background"] = (FunctionValue)((_, i, _) => TweenValue.Of(i == index ? Fill : Track)),
            Delay = reduced ? 0 : Presto.Stagger(Recipes.Tokens.StaggerStep / 2, new StaggerParams { From = index }),
            Duration = reduced ? 0 : Recipes.Tokens.Medium,
            Ease = Recipes.Tokens.Ease,
        });
    }

    private void HandleTapped(object? sender, TappedEventArgs e)
    {
        if (_connected is null || Count == 0) return;
        var x = e.GetPosition(this).X;
        _connected.GoTo(Mode == PagerIndicatorMode.Progress ? Math.Min(Count - 1, (int)(x / Bounds.Width * Count)) : DotAt(x));
        e.Handled = true;
    }

    // Each dot owns half the gap on either side of it.
    private int DotAt(double x)
    {
        var i = 0;
        foreach (var dot in VisualChildren)
        {
            if (x < dot.Bounds.Right + (Dot / 2)) return i;
            i++;
        }
        return i - 1;
    }

    private void HandleSlidesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Build();

    private void HandleMoved(MotionValue position)
    {
        if (Mode == PagerIndicatorMode.Progress) InvalidateArrange();
    }
}
