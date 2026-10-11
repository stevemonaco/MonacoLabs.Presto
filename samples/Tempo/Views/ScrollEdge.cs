using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Tempo.Views;

/// <summary>
/// Cues that a scroller has more past an edge: a shadow element fades in as content scrolls under it
/// (<c>v:ScrollEdge.ShadowTop="{Binding #List}"</c>), and <see cref="FadeSidesProperty"/> fades a sideways scroller's ends.
/// </summary>
/// <remarks>Each cue ramps with the distance left to scroll, so it eases in and out with the scroll rather than popping.</remarks>
public static class ScrollEdge
{
    private const double Ramp = 24;
    private const double FadeWidth = 24;

    /// <summary>The scroller, or the control templating one, whose top this shadow cues.</summary>
    public static readonly AttachedProperty<Control?> ShadowTopProperty =
        AvaloniaProperty.RegisterAttached<Visual, Control?>("ShadowTop", typeof(ScrollEdge));

    /// <summary>The scroller, or the control templating one, whose bottom this shadow cues.</summary>
    public static readonly AttachedProperty<Control?> ShadowBottomProperty =
        AvaloniaProperty.RegisterAttached<Visual, Control?>("ShadowBottom", typeof(ScrollEdge));

    /// <summary>Whether the control's left and right ends fade out while its scroller has more that way.</summary>
    public static readonly AttachedProperty<bool> FadeSidesProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("FadeSides", typeof(ScrollEdge));

    private static readonly AttachedProperty<Watch?> WatchProperty =
        AvaloniaProperty.RegisterAttached<Visual, Watch?>("Watch", typeof(ScrollEdge));

    static ScrollEdge()
    {
        ShadowTopProperty.Changed.AddClassHandler<Visual>((shadow, e) => WatchShadow(shadow, e.NewValue as Control, top: true));
        ShadowBottomProperty.Changed.AddClassHandler<Visual>((shadow, e) => WatchShadow(shadow, e.NewValue as Control, top: false));
        FadeSidesProperty.Changed.AddClassHandler<Control>((control, e) => WatchSides(control, e.NewValue is true));
    }

    public static Control? GetShadowTop(Visual shadow) => shadow.GetValue(ShadowTopProperty);

    public static void SetShadowTop(Visual shadow, Control? value) => shadow.SetValue(ShadowTopProperty, value);

    public static Control? GetShadowBottom(Visual shadow) => shadow.GetValue(ShadowBottomProperty);

    public static void SetShadowBottom(Visual shadow, Control? value) => shadow.SetValue(ShadowBottomProperty, value);

    public static bool GetFadeSides(Control control) => control.GetValue(FadeSidesProperty);

    public static void SetFadeSides(Control control, bool value) => control.SetValue(FadeSidesProperty, value);

    private static void WatchShadow(Visual shadow, Control? source, bool top)
    {
        shadow.GetValue(WatchProperty)?.Dispose();
        shadow.Opacity = 0;
        shadow.SetValue(WatchProperty, source is null ? null : new Watch(source, scroller =>
            shadow.Opacity = Strength(top ? scroller.Offset.Y : scroller.Extent.Height - scroller.Viewport.Height - scroller.Offset.Y)));
    }

    private static void WatchSides(Control control, bool enabled)
    {
        control.GetValue(WatchProperty)?.Dispose();
        control.ClearValue(Visual.OpacityMaskProperty);
        if (!enabled)
        {
            control.ClearValue(WatchProperty);
            return;
        }
        GradientStop left = new() { Offset = 0 }, leftEnd = new() { Color = Colors.Black }, rightStart = new() { Color = Colors.Black }, right = new() { Offset = 1 };
        control.OpacityMask = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = { left, leftEnd, rightStart, right },
        };
        control.SetValue(WatchProperty, new Watch(control, scroller =>
        {
            var fade = scroller.Viewport.Width > 0 ? Math.Min(FadeWidth / scroller.Viewport.Width, .5) : 0;
            leftEnd.Offset = fade;
            rightStart.Offset = 1 - fade;
            left.Color = Mask(Strength(scroller.Offset.X));
            right.Color = Mask(Strength(scroller.Extent.Width - scroller.Viewport.Width - scroller.Offset.X));
        }));
    }

    private static double Strength(double beyond) => Math.Clamp(beyond / Ramp, 0, 1);

    private static Color Mask(double strength) => Color.FromArgb((byte)Math.Round(255 * (1 - strength)), 0, 0, 0);

    /// <summary>Updates a cue from the source's own scroller, ignoring scrollers nested in its content.</summary>
    private sealed class Watch : IDisposable
    {
        private readonly Control _source;
        private readonly Action<ScrollViewer> _update;

        public Watch(Control source, Action<ScrollViewer> update)
        {
            _source = source;
            _update = update;
            source.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
            if (Scroller() is { } scroller)
                update(scroller);
        }

        public void Dispose() => _source.RemoveHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);

        private ScrollViewer? Scroller() =>
            _source as ScrollViewer ?? _source.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(s => s.TemplatedParent == _source);

        private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (e.Source is ScrollViewer scroller && (scroller == _source || scroller.TemplatedParent == _source))
                _update(scroller);
        }
    }
}
