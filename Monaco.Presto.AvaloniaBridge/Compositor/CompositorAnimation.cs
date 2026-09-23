using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Rendering.Composition;
using Monaco.Presto.AvaloniaBridge.Targets;
using Monaco.Presto.Targets;

namespace Monaco.Presto.AvaloniaBridge.Compositor;

/// <summary>
/// Upstream's <c>WAAPIAnimation</c>: a <see cref="CompositorAnimationParams"/> compiled into composition key frame
/// animations and started on each target's own composition visual, so it keeps running while the UI thread is busy.
/// </summary>
/// <remarks>
/// The paused Presto <see cref="Animation"/> the key frames were sampled from runs alongside as the playback model, so the
/// timing and the callbacks are anime.js's own. It is a model rather than the animation itself: while the UI thread is
/// blocked it falls behind what the render thread draws, and catches up once the thread is free again.
/// </remarks>
public sealed class CompositorAnimation : IRevertible, ICompositorTrackOwner
{
    private readonly TargetAnimation[] _targets;
    private readonly Animation? _control;

    internal CompositorAnimation(object? targets, CompositorAnimationParams parameters)
    {
        var parsed = TargetParser.Parse(targets);
        _targets = new TargetAnimation[parsed.Count];
        for (var i = 0; i < parsed.Count; i++)
        {
            var (mirror, tracks) = CompositorCompiler.Compile(i, parsed, parameters);
            _targets[i] = new TargetAnimation(this, (Visual)parsed[i], mirror, tracks);
            // Upstream reports the timing of the animation that finishes last, its controlAnimation.
            if (_control == null || mirror._delay + mirror.Duration > _control._delay + _control.Duration) _control = mirror;
        }

        foreach (var target in _targets)
        {
            if (target.Mirror == _control) continue;
            target.Mirror.OnBegin = null;
            target.Mirror.OnComplete = null;
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

    /// <inheritdoc cref="Timer.Completed"/>
    public bool Completed => _control?.Completed ?? true;

    /// <summary>How fast the animation plays (upstream <c>playbackRate</c>); setting it recompiles from the current position.</summary>
    public double Speed
    {
        get => _control?.Speed ?? 1;
        set
        {
            foreach (var target in _targets) target.Move(model => model.Speed = value);
        }
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
    public CompositorAnimation Alternate()
    {
        foreach (var target in _targets) target.Move(static model => model.Alternate());
        return this;
    }

    /// <inheritdoc cref="Timer.Seek"/>
    /// <inheritdoc cref="Pause" path="/remarks"/>
    public CompositorAnimation Seek(double time, bool muteCallbacks = false)
    {
        foreach (var target in _targets) target.Move(model => model.Seek(time, muteCallbacks));
        return this;
    }

    /// <inheritdoc cref="Timer.Restart"/>
    public CompositorAnimation Restart() => Seek(0, muteCallbacks: true).Resume();

    /// <summary>Seeks to the end, firing <see cref="CompositorAnimationParams.OnComplete"/> and resolving <see cref="Then"/> (upstream <c>waapi.complete</c>).</summary>
    public CompositorAnimation Complete() => Seek(_control?.Duration ?? 0);

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

    /// <inheritdoc cref="Animation.Then"/>
    public async Task<CompositorAnimation> Then(Action<CompositorAnimation>? callback = null)
    {
        if (_control is { } control) await control.Then(callback == null ? null : _ => callback(this));
        else callback?.Invoke(this);
        return this;
    }

    /// <inheritdoc cref="Timer.GetAwaiter"/>
    public TaskAwaiter<CompositorAnimation> GetAwaiter() => Then().GetAwaiter();

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

    private sealed class TargetAnimation(CompositorAnimation owner, Visual visual, Animation mirror, CompiledTrack[] tracks)
    {
        private const string Centre = "Vector3(this.Target.Size.X * 0.5, this.Target.Size.Y * 0.5, 0)";

        private static readonly Vector3D s_one = new(1, 1, 1);

        private CompositionVisual? _element;
        private AnimatedValue? _opacity;
        private double _opacityEnd;
        private bool _centred;
        private bool _moved;
        private int _running;

        public Animation Mirror => mirror;

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
                if (track.Kind == CompositorTrackKind.Opacity)
                {
                    _opacityEnd = track.Values[^1][0];
                    Own();
                }
                element.StartAnimation(track.Kind.ToString(), CompositorTracks.Build(element, track));
                _running |= 1 << (int)track.Kind;
                _centred |= track.Kind is CompositorTrackKind.Scale or CompositorTrackKind.RotationAngle;
            }

            // Keeps the origin of a rotation or a scale on the middle of the control as layout resizes it, as RenderTransformOrigin does.
            if (_centred) element.StartAnimation("CenterPoint", element.Compositor.CreateExpressionAnimation(Centre));
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
            mirror.Revert();
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
                if (track.Kind == CompositorTrackKind.Opacity) _opacityEnd = track.Values[^1][0];
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

            if (!_centred) return;
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
            var opacity = _opacity = new AnimatedValue(visual, Visual.OpacityProperty);
            opacity.Push(visual.Opacity);
            // A finished instance holds its last key frame, so pushing that same value back hands ownership over without a jump.
            _ = mirror.Then(_ => opacity.Push(_opacityEnd));
        }

        private void Stop()
        {
            visual.AttachedToVisualTree -= OnAttachedToVisualTree;
            if (_element is not { } element) return;
            foreach (var track in tracks) Release(track.Kind, handOver: false);
            if (_centred) element.StopAnimation("CenterPoint");
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

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            visual.AttachedToVisualTree -= OnAttachedToVisualTree;
            Start();
        }
    }
}
