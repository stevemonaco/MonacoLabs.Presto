using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Monaco.Presto.Core;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Events;

/// <summary>
/// Port of anime.js <c>onScroll</c>: watches a control crossing a <see cref="ScrollViewer"/>'s enter and leave
/// thresholds, reporting it through callbacks and, once <see cref="Link"/>ed to a timer, animation or timeline, driving
/// that object either by playback methods or by seeking it to the scroll position.
/// </summary>
/// <remarks>
/// Upstream's <c>debug</c> overlay draws DOM elements and is not ported, nor are the function forms of <c>axis</c>,
/// <c>repeat</c>, <c>enter</c> and <c>leave</c> — the values are read again on every <see cref="Refresh"/> instead.
/// Upstream also links through <c>autoplay: onScroll()</c>; here the linked object is created paused and handed to
/// <see cref="Link"/>, which still ties the two lifetimes together. A control pinned between the thresholds is the
/// caller's overlay, as <see cref="PrestoAvalonia.OnScroll"/> describes.
/// </remarks>
public sealed class ScrollObserver : IRevertible
{
    private const string DefaultSync = "play pause";

    private static readonly DecomposedValue s_bound = new();
    private static readonly Dictionary<string, Action<Timer>> s_syncMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        ["play"] = timer => timer.Play(),
        ["pause"] = timer => timer.Pause(),
        ["resume"] = timer => timer.Resume(),
        ["restart"] = timer => timer.Restart(),
        ["reverse"] = timer => timer.Reverse(),
        ["alternate"] = timer => timer.Alternate(),
        ["complete"] = timer => timer.Complete(),
        ["cancel"] = timer => timer.Cancel(),
        ["revert"] = timer => timer.Revert(),
        ["reset"] = timer => timer.Reset(),
    };

    private static int s_observerId;

    private readonly ScrollObserverParams _parameters;
    private readonly Action<Timer>?[] _syncMethods = new Action<Timer>?[4];
    private readonly bool _biDirSync;
    private ScrollThreshold _enter;
    private ScrollThreshold _leave;
    private double _prevProgress;
    private bool _began;
    private bool _completed;
    private bool _hasEntered;
    private bool _forceEnter;

    internal ScrollObserver(ScrollObserverParams parameters)
    {
        Engine.Current.CurrentScope?.Register(this);
        _parameters = parameters;

        var mode = parameters.Sync?.Raw ?? DefaultSync;
        var ease = mode switch { string name => EaseParser.Parse(name), Ease raw => raw, _ => null };
        var isLinear = mode is "linear" || (mode is Ease && ease == Eases.None);
        var isEase = ease != null && (ease != Eases.None || isLinear);
        var isSmooth = mode is double or true || isLinear;
        var isMethods = mode is string && !isEase && !isSmooth;
        if (isMethods)
        {
            var names = ((string)mode).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            _biDirSync = names.Length > 2;
            for (var i = 0; i < names.Length && i < _syncMethods.Length; i++)
            {
                _syncMethods[i] = s_syncMethods.GetValueOrDefault(names[i]);
            }
        }

        Id = parameters.Id ?? (++s_observerId).ToString(CultureInfo.InvariantCulture);
        Sync = isEase || isSmooth || isMethods;
        if (isEase) SyncEase = ease;
        if (isSmooth) SyncSmooth = isLinear || mode is not double smoothing ? 1 : smoothing;
        Target = parameters.Target;

        if ((parameters.Container ?? Target?.FindAncestorOfType<ScrollViewer>()) is { } scroller)
        {
            Container = ScrollContainer.Register(scroller, this);
        }
        // Resolution waits a frame so an observer created before the animation it watches can still take its target.
        Utils.Sync(_ => Resolve());
    }

    /// <summary>The observer's name, taken from the parameters or from the number of observers created before it.</summary>
    public string Id { get; }

    /// <summary>The state shared with the other observers of the same scroller; null until the observer has resolved.</summary>
    public ScrollContainer? Container { get; private set; }

    /// <summary>The watched control, taken from <see cref="Linked"/> when the parameters name none.</summary>
    public Control? Target { get; private set; }

    /// <summary>What <see cref="Link"/> attached, driven by the sync mode.</summary>
    public Timer? Linked { get; private set; }

    /// <summary>Whether the observer keeps watching once the target has passed its leave threshold.</summary>
    public bool Repeat { get; private set; } = true;

    /// <summary>Whether the observer watches the horizontal axis.</summary>
    public bool Horizontal { get; private set; }

    /// <summary>Whether a sync mode is in force at all.</summary>
    public bool Sync { get; }

    /// <summary>The ease the scroll progress is mapped through before seeking, when the sync mode is an eased one.</summary>
    public Ease? SyncEase { get; }

    /// <summary>How closely the linked object follows the scroll position, 1 being exactly; null unless the sync mode is a smooth one.</summary>
    public double? SyncSmooth { get; }

    /// <summary>Whether the target and container have been resolved and the bounds measured.</summary>
    public bool Ready { get; private set; }

    /// <summary>Whether <see cref="Revert"/> has detached the observer from its container.</summary>
    public bool Reverted { get; private set; }

    /// <summary>Whether the scroll position currently sits between the two thresholds.</summary>
    public bool IsInView { get; private set; }

    /// <summary>The target's distance from the start of the scrolled content, along the watched axis.</summary>
    public double Offset { get; private set; }

    /// <summary>The scroll position at which the target enters the view.</summary>
    public double OffsetStart { get; private set; }

    /// <summary>The scroll position at which the target leaves the view.</summary>
    public double OffsetEnd { get; private set; }

    /// <summary>How far the scroller travels between the two thresholds; 0 when they cross.</summary>
    public double Distance { get; private set; }

    /// <inheritdoc cref="ScrollContainer.Velocity"/>
    public double Velocity => Container?.Velocity ?? 0;

    /// <summary>Whether the scroller is moving towards the start of the content along the watched axis.</summary>
    public bool Backward => Container != null && (Horizontal ? Container.BackwardX : Container.BackwardY);

    /// <summary>The scroll position along the watched axis.</summary>
    public double Scroll => Container == null ? 0 : Horizontal ? Container.ScrollX : Container.ScrollY;

    /// <summary>How far the scroll position has travelled between the two thresholds, 0 to 1.</summary>
    public double Progress
    {
        get
        {
            var progress = (Scroll - OffsetStart) / Distance;
            return double.IsInfinity(progress) || double.IsNaN(progress) ? 0 : Utils.Round(Utils.Clamp(progress, 0, 1), 6);
        }
    }

    /// <summary>
    /// Attaches the object the observer drives, pausing it and — when the parameters name no target — watching the first
    /// control it animates.
    /// </summary>
    /// <remarks>Reverting <paramref name="linked"/> reverts the observer with it, as upstream's <c>autoplay: onScroll()</c> does.</remarks>
    public ScrollObserver Link(Timer linked)
    {
        linked.Pause();
        Linked = linked;
        linked._onRevert += HandleLinkedRevert;
        if (_parameters.Target == null && Ready && LinkedTarget() is { } target)
        {
            Target = target;
            Refresh();
        }
        return this;
    }

    /// <summary>Re-reads the parameters that can change and measures the target against the container again.</summary>
    /// <remarks>A no-op until the observer has resolved, a frame after it is created, and once it has reverted.</remarks>
    public ScrollObserver Refresh() => Ready ? Measure() : this;

    private ScrollObserver Measure()
    {
        // Upstream's flag: HandleScroll does nothing until a refresh has measured the bounds it reads.
        Ready = true;
        Repeat = _parameters.Repeat ?? true;
        Horizontal = (_parameters.Axis ?? ScrollAxis.Vertical) == ScrollAxis.Horizontal;
        _enter = _parameters.Enter ?? "end start";
        _leave = _parameters.Leave ?? "start end";
        UpdateBounds();
        HandleScroll();
        return this;
    }

    /// <summary>Detaches the observer from its container, which reverts with its last observer.</summary>
    public ScrollObserver Revert()
    {
        if (Reverted) return this;
        if (Linked is { } linked) linked._onRevert -= HandleLinkedRevert;
        if (Target is { } target) target.Loaded -= HandleTargetLoaded;
        Container?.Remove(this);
        Reverted = true;
        Ready = false;
        return this;
    }

    void IRevertible.Revert() => Revert();

    internal void RaiseResize() => _parameters.OnResize?.Invoke(this);

    internal void HandleScroll()
    {
        if (!Ready) return;
        var linked = Linked;
        var smooth = SyncSmooth ?? 0;
        var shouldSeek = linked != null && (SyncEase != null || smooth != 0);
        var scroll = Scroll;
        var isBefore = scroll <= OffsetStart;
        var isAfter = scroll >= OffsetEnd;
        var isInView = !isBefore && !isAfter;
        var isOnTheEdge = scroll == OffsetStart || scroll == OffsetEnd;
        var forceEnter = !_hasEntered && isOnTheEdge;
        var hasUpdated = false;
        var syncCompleted = false;
        var progress = Progress;

        if (isBefore && _began) _began = false;
        if (progress > 0 && !_began) _began = true;

        if (shouldSeek)
        {
            var linkedProgress = linked!.Progress;
            if (smooth != 0)
            {
                if (smooth < 1)
                {
                    const double step = .0001;
                    var snap = linkedProgress < progress && progress == 1 ? step
                        : linkedProgress > progress && progress == 0 ? -step
                        : 0;
                    progress = Utils.Round(Utils.Lerp(linkedProgress, progress, Utils.Lerp(.01, .2, smooth)) + snap, 6);
                }
            }
            else if (SyncEase is { } ease)
            {
                progress = ease(progress);
            }
            hasUpdated = progress != _prevProgress;
            syncCompleted = linkedProgress == 1;
            // A smoothed progress keeps converging after the scroll stopped, so the ticker is kept awake for it.
            if (hasUpdated && !syncCompleted && smooth != 0 && linkedProgress != 0) Container!.WakeTicker.Restart();
        }

        if ((isInView && !IsInView) || (forceEnter && !_forceEnter && !_hasEntered))
        {
            if (isInView) IsInView = true;
            if (!_forceEnter || !_hasEntered)
            {
                if (!_biDirSync) InvokeSyncMethod(0);
                _parameters.OnEnter?.Invoke(this);
                if (Backward)
                {
                    if (_biDirSync) InvokeSyncMethod(2);
                    _parameters.OnEnterBackward?.Invoke(this);
                }
                else
                {
                    if (_biDirSync) InvokeSyncMethod(0);
                    _parameters.OnEnterForward?.Invoke(this);
                }
                _hasEntered = true;
                if (forceEnter) _forceEnter = true;
            }
            else if (isInView)
            {
                _forceEnter = false;
            }
        }

        if (isInView || IsInView) hasUpdated = true;

        if (hasUpdated)
        {
            if (shouldSeek) linked!.Seek(linked.Duration * progress);
            _parameters.OnUpdate?.Invoke(this);
        }

        if (!isInView && IsInView)
        {
            IsInView = false;
            if (!_biDirSync) InvokeSyncMethod(1);
            _parameters.OnLeave?.Invoke(this);
            if (Backward)
            {
                if (_biDirSync) InvokeSyncMethod(3);
                _parameters.OnLeaveBackward?.Invoke(this);
            }
            else
            {
                if (_biDirSync) InvokeSyncMethod(1);
                _parameters.OnLeaveForward?.Invoke(this);
            }
            if (Sync && smooth == 0) syncCompleted = true;
        }

        if (progress >= 1 && _began && !_completed && (!Sync || syncCompleted))
        {
            if (Sync) _parameters.OnSyncComplete?.Invoke(this);
            _completed = true;
            if (!Repeat && (linked == null || linked.Completed)) Revert();
        }

        if (progress < 1 && _completed) _completed = false;

        _prevProgress = progress;
    }

    // Upstream convertValueToPx.
    private static double ConvertValueToPx(string text, double size, double under, double over)
    {
        ValueParser.Decompose(text switch
        {
            "top" or "left" or "start" or "min" => "0",
            "bottom" or "right" or "end" or "max" => "100%",
            "center" => "50%",
            _ => text,
        }, s_bound);
        var parsed = s_bound.Unit == "%" ? s_bound.Number / 100 * size : s_bound.Number;
        if (text is "max" && under < 0) parsed += under;
        if (text is "min" && over > 0) parsed += over;
        return parsed;
    }

    // Upstream parseBoundValue.
    private static double ParseBoundValue(object value, double size, double under = 0, double over = 0)
    {
        if (value is not string text) return Utils.Round((double)value, 0);
        var at = ValueParser.IndexOfRelativeOperator(text);
        if (at < 0) return Utils.Round(ConvertValueToPx(text, size, under, over), 0);
        var head = text[..at];
        var clamped = ConvertValueToPx(head, size, under, over);
        // The move starts from the unclamped bound; "min" and "max" then keep whichever of the two lies further inside.
        var moved = ValueParser.GetRelativeValue(
            ConvertValueToPx(head, size, 0, 0),
            ConvertValueToPx(text[(at + 2)..], size, under, over),
            text[at]);
        return Utils.Round(head switch { "min" => Math.Max(moved, clamped), "max" => Math.Min(moved, clamped), _ => moved }, 0);
    }

    private static Control? FirstControl(Animation animation)
    {
        foreach (var target in animation.Targets)
        {
            if (target is Control control) return control;
        }
        return null;
    }

    private void Resolve()
    {
        if (Reverted) return;
        Target ??= LinkedTarget();
        // A target created during the layout pass this frame renders has neither its ScrollViewer ancestor nor bounds yet.
        if (Target is { IsLoaded: false } pending)
        {
            pending.Loaded += HandleTargetLoaded;
            return;
        }
        if (Container == null && Target?.FindAncestorOfType<ScrollViewer>() is { } scroller)
        {
            Container = ScrollContainer.Register(scroller, this);
        }
        if (Target == null || Container == null)
        {
            throw new InvalidOperationException(
                "A ScrollObserver watches a control inside a ScrollViewer: set ScrollObserverParams.Target, or link an animation of one.");
        }
        Measure();
    }

    private void UpdateBounds()
    {
        var container = Container!;
        var target = Target!;
        var scroller = container.Scroller;
        var isHorizontal = Horizontal;
        var linked = Linked;
        var linkedTime = linked?.CurrentTime ?? 0;
        // Measured with the linked object at its start, so the animation it drives does not move its own thresholds.
        linked?.Seek(0, true);

        var origin = target.TranslatePoint(default, scroller) ?? default;
        var offset = isHorizontal ? origin.X + container.ScrollX : origin.Y + container.ScrollY;
        var targetSize = isHorizontal ? target.Bounds.Width : target.Bounds.Height;
        var containerSize = isHorizontal ? scroller.Viewport.Width : scroller.Viewport.Height;
        var scrollSize = isHorizontal ? scroller.Extent.Width : scroller.Extent.Height;

        var enterTarget = ParseBoundValue(_enter.Target ?? "start", targetSize);
        var leaveTarget = ParseBoundValue(_leave.Target ?? "end", targetSize);
        var under = enterTarget + offset - containerSize;
        var over = leaveTarget + offset - (scrollSize - containerSize);
        var enterContainer = ParseBoundValue(_enter.Container ?? "end", containerSize, under, over);
        var leaveContainer = ParseBoundValue(_leave.Container ?? "start", containerSize, under, over);

        Offset = offset;
        OffsetStart = enterTarget + offset - enterContainer;
        OffsetEnd = leaveTarget + offset - leaveContainer;
        Distance = Math.Max(OffsetEnd - OffsetStart, 0);

        linked?.Seek(linkedTime, true);
    }

    private void InvokeSyncMethod(int index)
    {
        if (Linked is { } linked) _syncMethods[index]?.Invoke(linked);
    }

    private Control? LinkedTarget()
    {
        if (Linked is Animation animation) return FirstControl(animation);
        if (Linked is not Timeline timeline) return null;
        Control? found = null;
        Helpers.ForEachChildren(timeline, child => found ??= child is Animation animated ? FirstControl(animated) : null);
        return found;
    }

    private void HandleTargetLoaded(object? sender, RoutedEventArgs e)
    {
        Target!.Loaded -= HandleTargetLoaded;
        Resolve();
    }

    private void HandleLinkedRevert(Timer linked) => Revert();
}
