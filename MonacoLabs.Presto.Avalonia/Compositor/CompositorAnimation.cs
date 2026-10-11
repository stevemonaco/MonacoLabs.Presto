using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Rendering.Composition;
using MonacoLabs.Presto.Targets;

namespace MonacoLabs.Presto.Compositor;

/// <summary>
/// A <see cref="CompositorAnimationParams"/> compiled into composition key frame
/// animations and started on each target's own composition visual, so it keeps running while the UI thread is busy.
/// </summary>
/// <remarks>
/// The paused Presto <see cref="Animation"/> the key frames were sampled from runs alongside as the playback model, so the
/// timing and the callbacks match the same animation played on the UI thread. It is a model rather than the animation itself: while the UI thread is
/// blocked it falls behind what the render thread draws, and catches up once the thread is free again.
/// </remarks>
public sealed class CompositorAnimation : IRevertible, ICompositorTrackOwner
{
    private readonly TargetAnimation[] _targets;
    private readonly Animation? _control;
    private readonly Action<Timer>? _onComplete;
    private TaskCompletionSource<CompositorAnimation>? _completion;
    private Action<CompositorAnimation>? _onResolve;
    private bool _moving;
    private Timer? _finishedWhileMoving;

    internal CompositorAnimation(object? targets, CompositorAnimationParams parameters)
    {
        _onComplete = parameters.OnComplete;
        var parsed = TargetParser.Parse(targets);
        _targets = new TargetAnimation[parsed.Count];
        var compiled = CompositorCompiler.Compile(parsed, parameters);
        for (var i = 0; i < parsed.Count; i++)
        {
            var (mirror, tracks) = compiled[i];
            _targets[i] = new TargetAnimation(this, (Visual)parsed[i], mirror, tracks);
            // Upstream reports the timing of the animation that finishes last, its controlAnimation.
            if (_control == null || mirror._delay + mirror.Duration > _control._delay + _control.Duration) _control = mirror;
        }

        foreach (var target in _targets)
        {
            target.Mirror.OnComplete = target.OnComplete;
            if (target.Mirror != _control) target.Mirror.OnBegin = null;
        }

        Engine.Current.CurrentScope?.Register(this);
        if (parameters.Autoplay ?? Engine.Current.Defaults.Autoplay)
        {
            foreach (var target in _targets) target.Start();
        }
    }

    /// <inheritdoc cref="Timer.CurrentTime"/>
    public double CurrentTime => _control?.CurrentTime ?? 0;

    /// <inheritdoc cref="Timer.Progress"/>
    public double Progress
    {
        get => _control?.Progress ?? 0;
        set => Seek((_control?.Duration ?? 0) * value);
    }

    /// <inheritdoc cref="Timer.Began"/>
    public bool Began => _control?.Began ?? false;

    /// <summary>True once every target has finished in the current direction (upstream <c>_completed</c> reaching <c>animations.length</c>).</summary>
    public bool Completed
    {
        get
        {
            foreach (var target in _targets)
            {
                if (!target.Mirror.Completed) return false;
            }
            return true;
        }
    }

    /// <summary>How fast the animation plays (upstream <c>playbackRate</c>); setting it recompiles from the current position.</summary>
    public double Speed
    {
        get => _control?.Speed ?? 1;
        set => Move(model => model.Speed = value);
    }

    /// <summary>
    /// Stops every composition property the animation drives, holding the mirror's exact current value (upstream
    /// <c>waapi.pause</c>).
    /// </summary>
    /// <remarks>
    /// A <c>KeyFrameAnimation</c> has no pause, so pausing detaches the server animations and <see cref="Resume"/> starts
    /// freshly compiled ones. The mirror moves now and the render thread follows at the next commit, so the two are at most
    /// one frame apart, and the next control operation puts them back in step.
    /// </remarks>
    public CompositorAnimation Pause()
    {
        foreach (var target in _targets) target.Pause();
        return this;
    }

    /// <summary>Carries on from the current position in the current direction (upstream <c>waapi.resume</c>).</summary>
    /// <inheritdoc cref="Pause" path="/remarks"/>
    public CompositorAnimation Resume()
    {
        foreach (var target in _targets) target.Resume();
        return this;
    }

    /// <inheritdoc cref="Timer.Play"/>
    public CompositorAnimation Play()
    {
        if (_control is { Reversed: true }) Alternate();
        return Resume();
    }

    /// <inheritdoc cref="Timer.Reverse"/>
    public CompositorAnimation Reverse()
    {
        if (_control is { Reversed: false }) Alternate();
        return Resume();
    }

    /// <inheritdoc cref="Timer.Alternate"/>
    public CompositorAnimation Alternate() => Move(static model => model.Alternate());

    /// <inheritdoc cref="Timer.Seek"/>
    /// <inheritdoc cref="Pause" path="/remarks"/>
    public CompositorAnimation Seek(double time, bool muteCallbacks = false) => Move(model => model.Seek(time, muteCallbacks));

    /// <inheritdoc cref="Timer.Restart"/>
    public CompositorAnimation Restart() => Seek(0, muteCallbacks: true).Resume();

    /// <summary>Seeks every target to its end, firing <see cref="CompositorAnimationParams.OnComplete"/> and resolving <see cref="Then"/> (upstream <c>waapi.complete</c>).</summary>
    public CompositorAnimation Complete() => Move(static model => model.Seek(model.Duration));

    /// <summary>Detaches the animation from the render thread, leaving every property it animated where it stands.</summary>
    public CompositorAnimation Cancel()
    {
        foreach (var target in _targets) target.Cancel();
        return this;
    }

    /// <summary>Cancels the animation and puts the composition properties it took over back the way it found them.</summary>
    public CompositorAnimation Revert()
    {
        foreach (var target in _targets) target.Revert();
        return this;
    }

    void IRevertible.Revert() => Revert();

    /// <summary>Completes when the target that finishes last in the current direction finishes; an already completed animation resolves immediately.</summary>
    /// <inheritdoc cref="Timer.Then" path="/param"/>
    public Task<CompositorAnimation> Then(Action<CompositorAnimation>? callback = null)
    {
        if (Completed)
        {
            callback?.Invoke(this);
            return Task.FromResult(this);
        }
        _completion ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
        _onResolve += callback;
        return _completion.Task;
    }

    /// <inheritdoc cref="Timer.GetAwaiter"/>
    public TaskAwaiter<CompositorAnimation> GetAwaiter() => Then().GetAwaiter();

    /// <summary>Where the playback mirror of <paramref name="visual"/> stands, or null when the animation does not drive it.</summary>
    internal CompositorScratch? ScratchOf(Visual visual)
    {
        foreach (var target in _targets)
        {
            if (ReferenceEquals(target.Visual, visual)) return target.Mirror._headTween?.Target as CompositorScratch;
        }
        return null;
    }

    /// <summary>
    /// Upstream's <c>removeWAAPIAnimation</c>. Losing the last track leaves nothing to drive, so the animation cancels, as
    /// upstream's <c>_completed</c> counter reaching <c>animations.length</c> does; being replaced is a cancellation rather
    /// than a completion, so <see cref="Then"/> stays unresolved.
    /// </summary>
    void ICompositorTrackOwner.Release(Visual visual, CompositorTrackKind kind)
    {
        foreach (var target in _targets) target.Release(visual, kind);
        foreach (var target in _targets)
        {
            if (target.Running) return;
        }
        Cancel();
    }

    private CompositorAnimation Move(Action<Animation> move)
    {
        _moving = true;
        foreach (var target in _targets) target.Move(move);
        _moving = false;
        if (_finishedWhileMoving is { } finished)
        {
            _finishedWhileMoving = null;
            OnTargetComplete(finished);
        }
        return this;
    }

    private void OnTargetComplete(Timer mirror)
    {
        // Mid-move, a target yet to be moved may still read completed from the other direction.
        if (_moving)
        {
            _finishedWhileMoving = mirror;
            return;
        }
        if (!Completed) return;
        _onComplete?.Invoke(mirror);
        var completion = _completion;
        if (completion == null) return;
        var onResolve = _onResolve;
        _completion = null;
        _onResolve = null;
        onResolve?.Invoke(this);
        completion.SetResult(this);
    }

    private sealed class TargetAnimation(CompositorAnimation owner, Visual visual, Animation mirror, CompiledTrack[] tracks)
    {
        private static readonly Vector3D s_one = new(1, 1, 1);

        private CompositionVisual? _element;
        private AnimatedValue<double>? _opacity;
        private bool _centered;
        private bool _moved;
        private int _running;

        public Animation Mirror => mirror;

        public Visual Visual => visual;

        /// <summary>Whether any of this target's tracks is still on its composition visual.</summary>
        public bool Running => _running != 0;

        public void Start()
        {
            if (ElementComposition.GetElementVisual(visual) is not { } element)
            {
                // Resuming before the control is in the tree asks for the same subscription again.
                visual.AttachedToVisualTree -= OnAttachedToVisualTree;
                visual.AttachedToVisualTree += OnAttachedToVisualTree;
                return;
            }

            _element = element;
            // Sampling left the mirror sitting completed at the end of its last iteration, unless playback has since moved it.
            if (!_moved) mirror.Reset();
            foreach (var track in _moved ? CompositorCompiler.CompileFrom(mirror, tracks) : tracks)
            {
                CompositorRegistry.WarnIfShorthandsDrive(visual, track.Kind);
                // Taking the property over stops whoever held it, which has to happen before this one starts.
                CompositorRegistry.Take(visual, track.Kind, owner);
                if (track.Kind == CompositorTrackKind.Opacity) Own();
                element.StartAnimation(track.Kind.ToString(), CompositorTracks.Build(element, track));
                _running |= 1 << (int)track.Kind;
                _centered |= track.Kind is CompositorTrackKind.Scale or CompositorTrackKind.RotationAngle;
            }

            if (_centered)
            {
                Pivot(element);
                visual.PropertyChanged -= OnPropertyChanged;
                visual.PropertyChanged += OnPropertyChanged;
            }
            mirror.Resume();
        }

        public void Pause()
        {
            if (mirror.Paused) return;
            mirror.Pause();
            Recompile(playing: false);
        }

        public void Resume()
        {
            if (!mirror.Paused) return;
            if (_element == null) Start();
            else if (_running != 0)
            {
                Recompile(playing: true);
                mirror.Resume();
            }
        }

        public void Cancel()
        {
            Stop();
            mirror.Cancel();
        }

        public void Revert()
        {
            Stop();
            _opacity?.Dispose();
            if (_element is { } element) Restore(element);
            // Timer.Revert would leave the playhead at minus the delay; the scratch it restores is never read again.
            mirror.Seek(0, muteCallbacks: true).Cancel();
        }

        public void OnComplete(Timer finished)
        {
            // A finished instance holds its last key frame, which the mirror has just rendered, so pushing it hands ownership over without a jump.
            _opacity?.Push(Math.Clamp(((CompositorScratch)mirror._headTween!.Target).Opacity, 0, 1));
            owner.OnTargetComplete(finished);
        }

        /// <summary>Lets go of <paramref name="kind"/> on <paramref name="target"/>, which another animation is taking over.</summary>
        public void Release(Visual target, CompositorTrackKind kind)
        {
            if (ReferenceEquals(target, visual)) Release(kind, handOver: true);
        }

        /// <summary>
        /// Moves the mirror where <paramref name="move"/> puts it, then recompiles the composition animations around its
        /// new position. Sampling seeks the mirror, so it has to be paused for the whole of it.
        /// </summary>
        /// <remarks>
        /// Before the first start there is nothing on the server to recompile, so the move lives on in the mirror alone and
        /// <see cref="Start"/> compiles from there instead of winding back to zero.
        /// </remarks>
        public void Move(Action<Animation> move)
        {
            var playing = !mirror.Paused;
            mirror.Pause();
            move(mirror);
            _moved = true;
            Recompile(playing);
            if (playing) mirror.Resume();
        }

        private void Recompile(bool playing)
        {
            if (_element is not { } element || _running == 0) return;
            foreach (var track in CompositorCompiler.CompileFrom(mirror, tracks))
            {
                if ((_running & (1 << (int)track.Kind)) == 0) continue;
                element.StopAnimation(track.Kind.ToString());
                if (playing) element.StartAnimation(track.Kind.ToString(), CompositorTracks.Build(element, track));
                else Write(element, track.Kind, track.Values[0]);
            }
        }

        private void Restore(CompositionVisual element)
        {
            foreach (var track in tracks)
            {
                // The binding is already gone, so the control's own opacity goes back through the composition visual.
                CompositorTracks.Write(element, track.Kind, track.Kind switch
                {
                    CompositorTrackKind.Opacity => [visual.Opacity],
                    CompositorTrackKind.Scale => [1, 1, 1],
                    _ => [0, 0, 0],
                });
            }

            if (!_centered) return;
            element.CenterPoint = s_one;
            element.CenterPoint = default;
        }

        private void Write(CompositionVisual element, CompositorTrackKind kind, double[] value)
        {
            // Writing the composition visual's own Opacity is what a later property sync would overwrite.
            if (kind == CompositorTrackKind.Opacity) _opacity?.Push(value[0]);
            else CompositorTracks.Write(element, kind, value);
        }

        private void Own()
        {
            // Avalonia syncs Visual.Opacity to the server every dirty pass; holding it stops a real change removing the animation.
            _opacity = new AnimatedValue<double>(visual, Visual.OpacityProperty);
            _opacity.Push(visual.Opacity);
        }

        private void Stop()
        {
            visual.AttachedToVisualTree -= OnAttachedToVisualTree;
            visual.PropertyChanged -= OnPropertyChanged;
            if (_element is not { } element) return;
            foreach (var track in tracks) Release(track.Kind, handOver: false);
            if (_centered) element.StopAnimation("CenterPoint");
        }

        private void Release(CompositorTrackKind kind, bool handOver)
        {
            if ((_running & (1 << (int)kind)) == 0) return;
            _running &= ~(1 << (int)kind);
            _element!.StopAnimation(kind.ToString());
            // A cancel keeps Visual.Opacity held so nothing writes over the frozen pixels; a hand over lets the next owner have it.
            if (handOver && kind == CompositorTrackKind.Opacity) _opacity?.Dispose();
            CompositorRegistry.Drop(visual, kind, owner);
        }

        private void Pivot(CompositionVisual element)
        {
            var origin = visual.RenderTransformOrigin;
            var (x, y) = origin.Point;
            // An expression on Target.Size keeps a relative origin correct across layout resizes.
            var expression = origin.Unit == RelativeUnit.Relative
                ? FormattableString.Invariant($"Vector3(this.Target.Size.X * {x:0.#########}, this.Target.Size.Y * {y:0.#########}, 0)")
                : FormattableString.Invariant($"Vector3({x:0.#########}, {y:0.#########}, 0)");
            element.StartAnimation("CenterPoint", element.Compositor.CreateExpressionAnimation(expression));
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Visual.RenderTransformOriginProperty) Pivot(_element!);
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            visual.AttachedToVisualTree -= OnAttachedToVisualTree;
            Start();
        }
    }
}
