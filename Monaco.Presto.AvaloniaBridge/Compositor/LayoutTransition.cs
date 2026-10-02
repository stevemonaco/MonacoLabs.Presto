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
    private readonly CompositorScratch _scratch = new() { Scale = 1, ScaleX = 1, ScaleY = 1 };

    private Rect? _first;
    private bool _firstOnScreen;
    private bool _armed = true;
    private Animation? _mirror;
    private bool _translating;
    private bool _scaling;
    private List<(Visual Child, CompositionVisual Element)>? _corrected;

    // An on-screen first is moved into this control's layout frame at its first pass, once its own scroll offset is known.
    // With recordFirst and no first, that pass only records; unscoped is for a transition whose owner reverts it.
    internal LayoutTransition(
        Control control, Rect? first, bool firstOnScreen, LayoutMotion motion, LayoutAnimateMode mode, bool once,
        bool recordFirst = false, bool scoped = true)
    {
        _control = control;
        _motion = motion;
        _mode = mode;
        Once = once;
        // Measuring now rather than on the first layout pass is what lets the very next move ease instead of only recording.
        _first = first ?? (recordFirst ? null : Measure(control));
        _firstOnScreen = first != null && firstOnScreen;

        s_live.TryGetValue(control, out var replaced);
        replaced?.Revert();
        s_live.AddOrUpdate(control, this);
        control.LayoutUpdated += OnLayoutUpdated;
        if (scoped) Engine.Current.CurrentScope?.Register(this);
        replaced?.Replaced?.Invoke(this);
    }

    /// <inheritdoc cref="Timer.Progress"/>
    public double Progress => _mirror?.Progress ?? 0;

    /// <inheritdoc cref="Timer.Completed"/>
    public bool Completed => _mirror?.Completed ?? true;

    /// <summary>True from creation until the first move it plays has finished, unlike <see cref="Completed"/>, which is true until that move starts.</summary>
    internal bool InFlight => _armed || _mirror is { Completed: false };

    /// <summary>Whether this is a one-shot that stops easing once its first move is under way, such as a shared element flight.</summary>
    internal bool Once { get; }

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
        _armed = false;
        _control.LayoutUpdated -= OnLayoutUpdated;
        if (s_live.TryGetValue(_control, out var live) && live == this) s_live.Remove(_control);
        return this;
    }

    void IRevertible.Revert() => Revert();

    /// <summary>Called with the transition that has just reverted this one by arming on the same control.</summary>
    internal Action<LayoutTransition>? Replaced { get; set; }

    /// <summary>
    /// The list of the item container this eases; a pass that scrolls it without a pending item change, or that scroll
    /// anchoring compensates, moves the container in one frame.
    /// </summary>
    internal ItemChanges? List { get; init; }

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
    internal static Rect? MeasureDrawn(Control control) => MeasureOnScreen(control) is { } slot ? Drawn(control, slot) : null;

    /// <summary>The layout slot <paramref name="control"/> has on screen, scroll offsets left in.</summary>
    internal static Rect? MeasureOnScreen(Control control) => Measure(control, onScreen: true);

    /// <summary>Where <paramref name="control"/> is drawn given its on-screen <paramref name="slot"/>: moved by its render transform and scaled by its live transition.</summary>
    internal static Rect Drawn(Control control, Rect slot)
    {
        if (control.RenderTransform is { } transform)
        {
            var matrix = transform.Value;
            slot = slot.Translate(new Vector(matrix.M31, matrix.M32));
        }
        if (!s_live.TryGetValue(control, out var live)) return slot;
        var scratch = live._scratch;
        return new Rect(slot.X + scratch.X, slot.Y + scratch.Y, slot.Width * scratch.ScaleX, slot.Height * scratch.ScaleY);
    }

    /// <summary>The transition last armed on <paramref name="control"/> and not reverted since, if any.</summary>
    internal static LayoutTransition? Live(Control control) => s_live.TryGetValue(control, out var live) ? live : null;

    /// <summary>Records where <paramref name="source"/> is drawn under <paramref name="key"/>, replacing any earlier entry.</summary>
    internal static bool Prepare(string key, Control source)
    {
        if (MeasureDrawn(source) is not { } drawn)
        {
            s_prepared.Remove(key);
            return false;
        }
        Prepare(key, drawn);
        return true;
    }

    /// <summary>Records <paramref name="drawn"/> under <paramref name="key"/>, replacing any earlier entry.</summary>
    internal static void Prepare(string key, Rect drawn) => s_prepared[key] = (drawn, Stopwatch.GetTimestamp());

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
        if (kind == CompositorTrackKind.Scale && _corrected?.FindIndex(c => c.Child == visual) is >= 0 and var i)
        {
            Restore(_corrected[i].Child, _corrected[i].Element);
            _corrected.RemoveAt(i);
            return;
        }
        if (!ReferenceEquals(visual, _control) || ElementComposition.GetElementVisual(_control) is not { } element) return;
        if (kind == CompositorTrackKind.Translation && _translating) _translating = false;
        else if (kind == CompositorTrackKind.Scale && _scaling)
        {
            _scaling = false;
            Uncorrect();
        }
        else return;

        element.StopAnimation(kind.ToString());
        CompositorRegistry.Drop(_control, kind, this);
        if (_translating || _scaling) return;
        _mirror?.Cancel();
        _mirror = null;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        // A transition reverted by an earlier handler of this pass still runs from the event's snapshot.
        if (Live(_control) != this || Measure(_control) is not { } last) return;
        var first = _first;
        // Adding this control's own scroll offsets moves an on-screen source into its layout frame.
        if (_firstOnScreen && first is { } onScreen && Measure(_control, onScreen: true) is { } screenSlot) first = onScreen.Translate(last.Position - screenSlot.Position);
        _firstOnScreen = false;
        _first = last;
        // A virtualizing panel also repositions realized rows as it corrects estimated sizes during a scroll,
        // and a content size resize beside the control already eases its move, which a flight would trail behind.
        if (List is { Scrolled: true, Pending: false } or { Anchoring: true } || ContentSizeTransition.ResizingBeside(_control))
        {
            Cancel();
            return;
        }
        if (first is not { } from || ElementComposition.GetElementVisual(_control) is not { } element) return;
        _armed = false;

        var sizable = _mode != LayoutAnimateMode.Position && !ContentSizeTransition.Eases(_control) && last.Width > 0 && last.Height > 0;
        var scaled = sizable && (from.Size != last.Size || _scratch.ScaleX != 1 || _scratch.ScaleY != 1);
        if (from.Position == last.Position && !scaled) return;

        Play(element, from, last, scaled);
        if (Once) _control.LayoutUpdated -= OnLayoutUpdated;
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
            OnComplete = _mode == LayoutAnimateMode.PositionAndSizeCorrected ? _ => Uncorrect() : null,
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

        var move = CompositorCompiler.Segment(CompositorTrackKind.Translation, translation, [0, 0, 0], mirror, [easeX, easeY]);
        // A zero-length compositor animation can still draw its first key for a frame, so an instant move is not started.
        if (move.Duration == TimeSpan.Zero && move.Delay == TimeSpan.Zero)
        {
            Cancel();
            return;
        }
        Start(element, move, ref _translating);
        if (!scaled) return;
        // The inverse scale is anchored on the corner the offset maths used, not on the middle a §4.3 rotation centres on.
        element.StopAnimation("CenterPoint");
        Anchor(element, default);
        Start(element, CompositorCompiler.Segment(CompositorTrackKind.Scale, scale, [1, 1, 1], mirror, [easeScale]), ref _scaling);
        if (_mode == LayoutAnimateMode.PositionAndSizeCorrected) Correct(element);
    }

    /// <remarks>
    /// Each child is anchored at the point of itself that sits as far across it as the child sits across the room the
    /// parent leaves it: a child flush top left stays there, a centred one stays centred, and a margin scales with the parent.
    /// </remarks>
    private void Correct(CompositionVisual parent)
    {
        var corrected = _corrected ??= [];
        for (var i = corrected.Count - 1; i >= 0; i--)
        {
            if (corrected[i].Child.GetVisualParent() == _control) continue;
            Restore(corrected[i].Child, corrected[i].Element);
            corrected.RemoveAt(i);
        }

        var room = _control.Bounds.Size;
        foreach (var child in _control.GetVisualChildren())
        {
            if (ElementComposition.GetElementVisual(child) is not { } element) continue;
            var slot = child.Bounds;
            element.StopAnimation("CenterPoint");
            Anchor(element, new Vector3D(Across(slot.X, slot.Width, room.Width), Across(slot.Y, slot.Height, room.Height), 0));
            // A child corrected by the move this one interrupts keeps its expression, which a restore's write would detach.
            if (CompositorRegistry.Holder(child, CompositorTrackKind.Scale) == this) continue;
            CompositorRegistry.WarnIfShorthandsDrive(child, CompositorTrackKind.Scale);
            CompositorRegistry.Take(child, CompositorTrackKind.Scale, this);
            var counter = element.Compositor.CreateExpressionAnimation("Vector3(1 / parent.Scale.X, 1 / parent.Scale.Y, 1)");
            counter.SetReferenceParameter("parent", parent);
            element.StartAnimation("Scale", counter);
            corrected.Add((child, element));
        }
    }

    private static double Across(double offset, double length, double room) => room > length ? offset / (room - length) * length : 0;

    private void Uncorrect()
    {
        if (_corrected == null) return;
        foreach (var (child, element) in _corrected) Restore(child, element);
        _corrected.Clear();
    }

    private void Restore(Visual child, CompositionVisual element)
    {
        element.StopAnimation("Scale");
        CompositorTracks.Write(element, CompositorTrackKind.Scale, [1, 1, 1]);
        Anchor(element, default);
        CompositorRegistry.Drop(child, CompositorTrackKind.Scale, this);
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
                Anchor(element, default);
            }
        }
        Uncorrect();

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
    private static void Anchor(CompositionVisual element, Vector3D at)
    {
        element.CenterPoint = at + s_one;
        element.CenterPoint = at;
    }
}
