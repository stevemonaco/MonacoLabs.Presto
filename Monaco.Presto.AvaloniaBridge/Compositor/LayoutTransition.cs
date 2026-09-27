using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge.Compositor;

/// <summary>
/// Upstream's <c>createLayout</c> as FLIP played on the compositor (COMPOSITOR-PLAN §4.4): where the control was and where
/// layout has just put it are measured on the UI thread, the move is inverted onto the composition <c>Translation</c>, and
/// the render thread eases that back to identity, so the transition keeps running while the UI thread is busy.
/// </summary>
/// <remarks>
/// Layout itself stays immediate — <see cref="Visual.Bounds"/> is right in the same pass and only the pixels lag behind —
/// so the control is hit where layout put it while it is still drawn on the way there. A paused Presto
/// <see cref="Animation"/> over a scratch object times the move, which is what lets an interrupted one invert from where
/// the control is drawn rather than from the slot it has left, and what <see cref="Then"/> resolves on.
/// </remarks>
public sealed class LayoutTransition : IRevertible, ICompositorTrackOwner
{
    // Matches the Toolkit's ConnectedAnimationService, which discards a prepared animation not started within about three seconds.
    private static readonly TimeSpan s_preparedLifetime = TimeSpan.FromSeconds(3);
    private static readonly ConditionalWeakTable<Control, LayoutTransition> s_live = new();
    private static readonly Dictionary<string, (Rect OnScreen, long At)> s_prepared = new();
    private static readonly Vector3D s_one = new(1, 1, 1);

    private readonly Control _control;
    private readonly LayoutMotion _motion;
    private readonly LayoutAnimateMode _mode;
    private readonly bool _once;
    private readonly CompositorScratch _scratch = new() { Scale = 1, ScaleX = 1, ScaleY = 1 };

    private Rect? _first;
    private bool _firstOnScreen;
    private Animation? _mirror;
    private bool _translating;
    private bool _scaling;

    // An on-screen first is moved into this control's layout frame at its first pass, once its own scroll offset is known.
    internal LayoutTransition(Control control, Rect? first, bool firstOnScreen, LayoutMotion motion, LayoutAnimateMode mode, bool once)
    {
        _control = control;
        _motion = motion;
        _mode = mode;
        _once = once;
        // Measuring now rather than on the first layout pass is what lets the very next move ease instead of only recording.
        _first = first ?? Measure(control);
        _firstOnScreen = first != null && firstOnScreen;

        if (s_live.TryGetValue(control, out var replaced)) replaced.Revert();
        s_live.AddOrUpdate(control, this);
        control.LayoutUpdated += OnLayoutUpdated;
        Engine.Current.CurrentScope?.Register(this);
    }

    /// <inheritdoc cref="Timer.Progress"/>
    public double Progress => _mirror?.Progress ?? 0;

    /// <inheritdoc cref="Timer.Completed"/>
    public bool Completed => _mirror?.Completed ?? true;

    /// <summary>Stops the move in flight, leaving the control in the slot layout gave it.</summary>
    public LayoutTransition Cancel()
    {
        _mirror?.Cancel();
        _mirror = null;
        Identity();
        return this;
    }

    /// <summary>Cancels the move and stops easing later layout changes, so the control moves in one frame again.</summary>
    public LayoutTransition Revert()
    {
        Cancel();
        _control.LayoutUpdated -= OnLayoutUpdated;
        if (s_live.TryGetValue(_control, out var live) && live == this) s_live.Remove(_control);
        return this;
    }

    void IRevertible.Revert() => Revert();

    /// <inheritdoc cref="Animation.Then"/>
    public async Task<LayoutTransition> Then(Action<LayoutTransition>? callback = null)
    {
        if (_mirror is { } mirror) await mirror.Then(callback == null ? null : _ => callback(this));
        else callback?.Invoke(this);
        return this;
    }

    /// <inheritdoc cref="Timer.GetAwaiter"/>
    public TaskAwaiter<LayoutTransition> GetAwaiter() => Then().GetAwaiter();

    /// <summary>
    /// The layout slot <paramref name="control"/> has in its window with every scroll offset taken back out, so neither a
    /// scroll nor a render transform reads as a layout move; this is also the frame a move between two parents is measured in.
    /// </summary>
    internal static Rect? Measure(Control control) => Measure(control, onScreen: false);

    /// <summary>
    /// Where <paramref name="control"/> is drawn on screen: its slot with scrolling left in, plus its render transform's
    /// translation and its live transition. This is what a shared element flies from, whatever scrolls around it.
    /// </summary>
    internal static Rect? MeasureDrawn(Control control)
    {
        if (Measure(control, onScreen: true) is not { } slot) return null;
        if (control.RenderTransform is { } transform)
        {
            var matrix = transform.Value;
            slot = slot.Translate(new Vector(matrix.M31, matrix.M32));
        }
        if (!s_live.TryGetValue(control, out var live)) return slot;
        var scratch = live._scratch;
        return new Rect(slot.X + scratch.X, slot.Y + scratch.Y, slot.Width * scratch.ScaleX, slot.Height * scratch.ScaleY);
    }

    /// <summary>Records where <paramref name="source"/> is drawn under <paramref name="key"/>, replacing any earlier entry.</summary>
    internal static bool Prepare(string key, Control source)
    {
        if (MeasureDrawn(source) is not { } drawn)
        {
            s_prepared.Remove(key);
            return false;
        }
        s_prepared[key] = (drawn, Stopwatch.GetTimestamp());
        return true;
    }

    /// <summary>Removes and returns the rect prepared under <paramref name="key"/>, unless there is none or it has expired.</summary>
    internal static Rect? TakePrepared(string key)
    {
        if (!s_prepared.Remove(key, out var prepared)) return null;
        return Stopwatch.GetElapsedTime(prepared.At) <= s_preparedLifetime ? prepared.OnScreen : null;
    }

    private static Rect? Measure(Control control, bool onScreen)
    {
        if (TopLevel.GetTopLevel(control) is null) return null;
        var position = default(Point);
        for (Visual? visual = control; visual is not null and not TopLevel; visual = visual.GetVisualParent())
        {
            position += visual.Bounds.Position;
            if (!onScreen && visual.GetVisualParent() is ScrollContentPresenter scroller) position += scroller.Offset;
        }
        return new Rect(position, control.Bounds.Size);
    }

    /// <summary>
    /// Lets go of a property another owner is taking over, or that <see cref="Presto.Remove"/> has taken away. The stopped
    /// animation leaves its value where it stands, so the control freezes mid-move rather than jumping to its slot; with
    /// nothing left to drive, the move is off.
    /// </summary>
    void ICompositorTrackOwner.Release(Visual visual, CompositorTrackKind kind)
    {
        if (!ReferenceEquals(visual, _control) || ElementComposition.GetElementVisual(_control) is not { } element) return;
        if (kind == CompositorTrackKind.Translation && _translating) _translating = false;
        else if (kind == CompositorTrackKind.Scale && _scaling) _scaling = false;
        else return;

        element.StopAnimation(kind.ToString());
        CompositorRegistry.Drop(_control, kind, this);
        if (_translating || _scaling) return;
        _mirror?.Cancel();
        _mirror = null;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (Measure(_control) is not { } last) return;
        var first = _first;
        // Adding this control's own scroll offsets moves an on-screen source into its layout frame.
        if (_firstOnScreen && first is { } onScreen && Measure(_control, onScreen: true) is { } screenSlot) first = onScreen.Translate(last.Position - screenSlot.Position);
        _firstOnScreen = false;
        _first = last;
        if (first is not { } from || ElementComposition.GetElementVisual(_control) is not { } element) return;

        var sizable = _mode == LayoutAnimateMode.PositionAndSize && last.Width > 0 && last.Height > 0;
        var scaled = sizable && (from.Size != last.Size || _scratch.ScaleX != 1 || _scratch.ScaleY != 1);
        if (from.Position == last.Position && !scaled) return;

        Play(element, from, last, scaled);
        if (_once) _control.LayoutUpdated -= OnLayoutUpdated;
    }

    /// <remarks>
    /// The invert is measured against where the control is <em>drawn</em> — its slot plus whatever the mirror still holds —
    /// so a second layout change mid-move carries on from there instead of snapping back to the slot it was leaving.
    /// </remarks>
    private void Play(CompositionVisual element, Rect from, Rect last, bool scaled)
    {
        double[] translation = [from.X - last.X + _scratch.X, from.Y - last.Y + _scratch.Y, 0];
        double[] scale = [from.Width * _scratch.ScaleX / last.Width, from.Height * _scratch.ScaleY / last.Height, 1];

        _scratch.X = translation[0];
        _scratch.Y = translation[1];
        if (scaled)
        {
            _scratch.ScaleX = scale[0];
            _scratch.ScaleY = scale[1];
        }

        var ease = CompositorCompiler.Pure(_motion.Ease ?? Engine.Current.Defaults.Ease, _control, 0, 1, out var spring);
        var easeX = Axis(_motion.EaseX, ease);
        var easeY = Axis(_motion.EaseY, ease);
        var easeScale = Axis(_motion.EaseScale, ease);
        var mirrorParams = new AnimationParams
        {
            Duration = spring != null ? (TweenValue)spring.SettlingDuration : _motion.Duration,
            Delay = _motion.Delay,
            Ease = ease,
        };
        mirrorParams.Properties["X"] = new TweenParams { To = 0d, Ease = easeX };
        mirrorParams.Properties["Y"] = new TweenParams { To = 0d, Ease = easeY };
        if (scaled)
        {
            mirrorParams.Properties["ScaleX"] = new TweenParams { To = 1d, Ease = easeScale };
            mirrorParams.Properties["ScaleY"] = new TweenParams { To = 1d, Ease = easeScale };
        }

        _mirror?.Cancel();
        var mirror = _mirror = Presto.Animate(_scratch, mirrorParams);

        Start(element, CompositorCompiler.Segment(CompositorTrackKind.Translation, translation, [0, 0, 0], mirror, [easeX, easeY]), ref _translating);
        if (!scaled) return;
        // The inverse scale is anchored on the corner the offset maths used, not on the middle a §4.3 rotation centres on.
        element.StopAnimation("CenterPoint");
        Corner(element);
        Start(element, CompositorCompiler.Segment(CompositorTrackKind.Scale, scale, [1, 1, 1], mirror, [easeScale]), ref _scaling);
    }

    private Ease Axis(EaseValue? value, Ease fallback) =>
        value is { } axis ? CompositorCompiler.Pure(axis, _control, 0, 1, out _) : fallback;

    private void Start(CompositionVisual element, CompiledTrack track, ref bool holding)
    {
        // The transition keeps the property across later layout passes, so the collision is worth saying once.
        if (!holding) CompositorRegistry.WarnIfShorthandsDrive(_control, track.Kind);
        CompositorRegistry.Take(_control, track.Kind, this);
        holding = true;
        element.StartAnimation(track.Kind.ToString(), CompositorTracks.Build(element, track));
    }

    private void Identity()
    {
        if (ElementComposition.GetElementVisual(_control) is { } element)
        {
            if (_translating) Stop(element, CompositorTrackKind.Translation, [0, 0, 0], ref _translating);
            if (_scaling)
            {
                Stop(element, CompositorTrackKind.Scale, [1, 1, 1], ref _scaling);
                Corner(element);
            }
        }

        _scratch.X = _scratch.Y = 0;
        _scratch.ScaleX = _scratch.ScaleY = 1;
    }

    private void Stop(CompositionVisual element, CompositorTrackKind kind, double[] identity, ref bool holding)
    {
        holding = false;
        element.StopAnimation(kind.ToString());
        CompositorTracks.Write(element, kind, identity);
        CompositorRegistry.Drop(_control, kind, this);
    }

    // The client setter's equality guard drops an unchanged write, so the discarded first write is what puts the real one on the wire.
    private static void Corner(CompositionVisual element)
    {
        element.CenterPoint = s_one;
        element.CenterPoint = default;
    }
}
