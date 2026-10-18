using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Compositor;
using MonacoLabs.Presto.Targets;
using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto;

/// <summary>
/// Eases a host's real layout size whenever its content changes size (≈ Compose <c>animateContentSize</c>), clipping the
/// content meanwhile. The content is laid out at its final size from the first frame, so its text never reflows.
/// </summary>
/// <remarks>
/// The host's sole visual child is given a right and bottom margin at <see cref="BindingPriority.Animation"/> that makes up
/// the difference between the size drawn and the size laid out, so the host and its siblings follow the eased size while
/// the child keeps its natural constraint, and a change mid-flight is measured as it happens. Only an axis along which the
/// host's size follows its content eases; a stretched axis moves in one frame.
/// </remarks>
public sealed class ContentSizeTransition : IRevertible, IReleasable
{
    private static readonly ConditionalWeakTable<Control, ContentSizeTransition> s_live = new();
    private static readonly List<Control> s_resizing = [];

    private readonly Control _host;
    private readonly LayoutMotion _motion;
    private readonly Inset _inset = new();
    private readonly Action<Timer> _push;
    private readonly Action<Timer> _release;

    private Layoutable? _child;
    private Size? _natural;
    private Size _childNatural;
    private Thickness _margin;
    private AnimatedValue<Thickness>? _held;
    private AnimatedValue<Geometry?>? _clip;
    private Animation? _mirror;
    private bool _reverted;

    // With record false, the host's first layout pass only records its size; unscoped is for a transition whose owner reverts it.
    internal ContentSizeTransition(Control host, LayoutMotion motion, bool record, bool scoped = true)
    {
        _host = host;
        _motion = motion;
        _push = _ => Push();
        _release = _ => Release();
        if (record) Record(SoleChild());

        s_live.TryGetValue(host, out var replaced);
        replaced?.Revert();
        s_live.AddOrUpdate(host, this);
        host.LayoutUpdated += OnLayoutUpdated;
        if (scoped) Engine.Current.CurrentScope?.Register(this);
    }

    /// <inheritdoc cref="Timer.Progress"/>
    public double Progress => _mirror?.Progress ?? 0;

    /// <inheritdoc cref="Timer.Completed"/>
    public bool Completed => _mirror?.Completed ?? true;

    /// <summary>Stops the resize in flight, leaving the host at its natural size.</summary>
    public ContentSizeTransition Cancel()
    {
        _mirror?.Cancel();
        _mirror = null;
        _inset.Right = _inset.Bottom = 0;
        Release();
        return this;
    }

    /// <summary>Cancels the resize and stops easing later size changes, so the host resizes in one frame again.</summary>
    public ContentSizeTransition Revert()
    {
        Cancel();
        _host.LayoutUpdated -= OnLayoutUpdated;
        if (s_live.TryGetValue(_host, out var live) && live == this) s_live.Remove(_host);
        _reverted = true;
        return this;
    }

    void IRevertible.Revert() => Revert();

    bool IReleasable.IsReleased => _reverted;

    /// <inheritdoc cref="Animation.Then"/>
    public async Task<ContentSizeTransition> Then(Action<ContentSizeTransition>? callback = null)
    {
        if (_mirror is { } mirror) await mirror.Then(callback == null ? null : _ => callback(this));
        else callback?.Invoke(this);
        return this;
    }

    /// <inheritdoc cref="Timer.GetAwaiter"/>
    public TaskAwaiter<ContentSizeTransition> GetAwaiter() => Then().GetAwaiter();

    /// <summary>Whether a content size transition eases <paramref name="host"/>'s size.</summary>
    internal static bool Eases(Control host) => s_live.TryGetValue(host, out _);

    /// <summary>Whether <paramref name="visual"/> is a host whose content a resize under way clips to its drawn box.</summary>
    internal static bool Clips(Visual visual) => visual is Control host && s_resizing.Contains(host);

    /// <summary>Whether a host in <paramref name="control"/>'s top level is resizing that is neither <paramref name="control"/>, inside it, nor its ancestor.</summary>
    internal static bool ResizingBeside(Control control)
    {
        if (TopLevel.GetTopLevel(control) is not { } top) return false;
        foreach (var host in s_resizing)
        {
            if (host != control && !control.IsVisualAncestorOf(host) && !host.IsVisualAncestorOf(control) && TopLevel.GetTopLevel(host) == top) return true;
        }
        return false;
    }

    /// <summary>The builder's timing and ease, which is all a content size transition takes.</summary>
    /// <exception cref="NotSupportedException">The builder animates a property, or sets anything else a layout transition cannot honor.</exception>
    internal static LayoutMotion Motion(AnimationBuilder builder) =>
        LayoutMotion.Timing(builder, "Layout decides the size a content size transition eases to, so give it only timing and an ease.");

    private Layoutable? SoleChild() =>
        _host.GetVisualChildren() is IReadOnlyList<Visual> { Count: 1 } children ? children[0] as Layoutable : null;

    // Unless held, the child has no margin of this transition's yet, so the sizes laid out are already natural.
    private void Record(Layoutable? child, bool held = true)
    {
        _child = child;
        var inset = held ? new Thickness(0, 0, _inset.Right, _inset.Bottom) : default;
        _natural = child is null || !_host.IsArrangeValid ? null : _host.Bounds.Size.Deflate(inset);
        if (child is not null) _childNatural = child.DesiredSize.Deflate(inset);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        // A transition reverted by an earlier handler of this pass still runs from the event's snapshot.
        if (!s_live.TryGetValue(_host, out var live) || live != this) return;
        var child = SoleChild();
        // A swapped child takes over the resize from the size drawn now.
        var swapped = child != _child;
        if (swapped) Release();
        if (child is null) Cancel();
        var last = _natural;
        var lastChild = _childNatural;
        Record(child, held: !swapped);
        if (child is null || last is not { } was || _natural is not { } now) return;

        // An axis eases only when the host grew by as much as its content did, which a stretched axis does not.
        var width = now.Width - was.Width;
        var height = now.Height - was.Height;
        var easeWidth = Math.Abs(_childNatural.Width - lastChild.Width) > 1 && Math.Abs(width - (_childNatural.Width - lastChild.Width)) <= 1;
        var easeHeight = Math.Abs(_childNatural.Height - lastChild.Height) > 1 && Math.Abs(height - (_childNatural.Height - lastChild.Height)) <= 1;
        if (easeWidth || easeHeight || swapped && !Completed) Play(child, easeWidth ? _inset.Right - width : _inset.Right, easeHeight ? _inset.Bottom - height : _inset.Bottom);
    }

    private void Play(Layoutable child, double right, double bottom)
    {
        _inset.Right = right;
        _inset.Bottom = bottom;
        _mirror?.Cancel();
        using var origin = PrestoDiagnostics.Origin("ContentSizeTransition");
        var motion = _motion.Current;
        var mirror = _mirror = Presto.Animate(_inset, new AnimationParams
        {
            ["Right"] = 0d,
            ["Bottom"] = 0d,
            Duration = motion.Duration,
            Delay = motion.Delay,
            Ease = motion.Ease,
            OnRender = _push,
            OnComplete = _release,
        });
        if (mirror.Duration <= Constants.MinValue)
        {
            Cancel();
            return;
        }
        if (_held is null)
        {
            _margin = child.Margin;
            _held = new AnimatedValue<Thickness>(child, Layoutable.MarginProperty);
            _clip = new AnimatedValue<Geometry?>(child, Visual.ClipProperty);
            s_resizing.Add(_host);
        }
        // Written now rather than at the mirror's first frame, so the pass this layout change triggers already draws the old size.
        Push();
    }

    private void Push()
    {
        if (_held is null || _child is null) return;
        _held.Push(new Thickness(_margin.Left, _margin.Top, _margin.Right + _inset.Right, _margin.Bottom + _inset.Bottom));
        _clip!.Push(InnerBox(_child));
    }

    // The content is clipped rather than the host, whose ClipToBounds would also clip its own shadow.
    private Geometry InnerBox(Layoutable child)
    {
        var (inset, radius) = _host switch
        {
            Border border => (border.BorderThickness + border.Padding, border.CornerRadius),
            ContentPresenter presenter => (presenter.BorderThickness + presenter.Padding, presenter.CornerRadius),
            Decorator decorator => (decorator.Padding, default),
            _ => (default(Thickness), default(CornerRadius)),
        };
        // The size the pass after this push lays the host out at.
        var drawn = (_natural ?? _host.Bounds.Size) + new Size(_inset.Right, _inset.Bottom);
        var box = new Rect(drawn).Deflate(inset).Translate(-(Vector)child.Bounds.Position);
        var corners = new CornerRadius(
            Math.Max(0, radius.TopLeft - Math.Max(inset.Left, inset.Top)),
            Math.Max(0, radius.TopRight - Math.Max(inset.Right, inset.Top)),
            Math.Max(0, radius.BottomRight - Math.Max(inset.Right, inset.Bottom)),
            Math.Max(0, radius.BottomLeft - Math.Max(inset.Left, inset.Bottom)));
        return corners.IsUniform ? new RectangleGeometry(box, corners.TopLeft, corners.TopLeft) : Rounded(box, corners);
    }

    private static StreamGeometry Rounded(Rect box, CornerRadius corners)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(box.Left + corners.TopLeft, box.Top), true);
        Corner(context, box.TopRight, corners.TopRight, new Vector(-1, 0), new Vector(0, 1));
        Corner(context, box.BottomRight, corners.BottomRight, new Vector(0, -1), new Vector(-1, 0));
        Corner(context, box.BottomLeft, corners.BottomLeft, new Vector(1, 0), new Vector(0, -1));
        Corner(context, box.TopLeft, corners.TopLeft, new Vector(0, 1), new Vector(1, 0));
        context.EndFigure(true);
        return geometry;
    }

    private static void Corner(StreamGeometryContext context, Point corner, double radius, Vector into, Vector outOf)
    {
        context.LineTo(corner + into * radius);
        if (radius > 0) context.ArcTo(corner + outOf * radius, new Size(radius, radius), 0, false, SweepDirection.Clockwise);
    }

    private void Release()
    {
        if (_held is null) return;
        _held.Dispose();
        _held = null;
        _clip!.Dispose();
        _clip = null;
        // Kept through the pass the last frame lays out, so the moves it makes beside the host land with it.
        if (TopLevel.GetTopLevel(_host) is { } top) ItemChanges.AfterLayout(top, () => s_resizing.Remove(_host));
        else s_resizing.Remove(_host);
    }

    private sealed class Inset : IMemberTarget
    {
        private static readonly Dictionary<string, IPropertyAccessor> s_members = new()
        {
            [nameof(Right)] = ObjectTargetAdapter.MemberAccessor.Of<Inset, double>(inset => inset.Right, (inset, value) => inset.Right = value),
            [nameof(Bottom)] = ObjectTargetAdapter.MemberAccessor.Of<Inset, double>(inset => inset.Bottom, (inset, value) => inset.Bottom = value),
        };

        public double Right { get; set; }

        public double Bottom { get; set; }

        IReadOnlyDictionary<string, IPropertyAccessor> IMemberTarget.Members => s_members;
    }
}
