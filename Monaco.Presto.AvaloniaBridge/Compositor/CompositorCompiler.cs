using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;

namespace Monaco.Presto.AvaloniaBridge.Compositor;

/// <summary>
/// Compiles <see cref="CompositorAnimationParams"/> into <see cref="CompiledTrack"/>s (COMPOSITOR-PLAN §4.3 steps 1-3): a
/// normal Presto <see cref="Animation"/> runs over a scratch mirror of the control's transform and opacity, and sampling its
/// <see cref="Timer.Seek"/> gives keyframes, per-keyframe eases, modifiers and springs for free.
/// </summary>
internal static class CompositorCompiler
{
    // Matches upstream easingToLinear's default.
    private const int SamplesPerIteration = 100;

    // A recompiled cycle covers every iteration that is left, so its sample count is bounded rather than per iteration.
    private const int MaxSamples = 1000;

    private static readonly Easing s_linear = new LinearEasing();

    // The default modifier is a cached static lambda, so a tween that inherited it is recognisable by reference.
    private static readonly Func<double, double> s_noModifier = new PrestoDefaults().Modifier;

    /// <summary>
    /// Builds the mirror animation for the target at <paramref name="index"/> of <paramref name="targets"/> — the position
    /// function values and staggers resolve against — and samples it. The mirror is paused: it writes only to its own scratch
    /// object, and the caller starts it alongside the composition animations it drives.
    /// </summary>
    /// <exception cref="NotSupportedException">A property has no composition counterpart.</exception>
    public static (Animation Mirror, CompiledTrack[] Tracks) Compile(
        int index,
        IReadOnlyList<object> targets,
        CompositorAnimationParams parameters)
    {
        var visual = (Visual)targets[index];
        var defaults = Engine.Current.Defaults;
        var resolved = Pure(parameters.Ease ?? defaults.Ease, visual, index, targets.Count, out var spring);

        var mirrorParams = new AnimationParams
        {
            Ease = resolved,
            Duration = spring != null ? (TweenValue)spring.SettlingDuration : Resolve(parameters.Duration ?? defaults.Duration, visual, index, targets),
            Delay = Resolve(parameters.Delay ?? defaults.Delay, visual, index, targets),
            Loop = parameters.Loop,
            LoopDelay = parameters.LoopDelay,
            Alternate = parameters.Alternate,
            Reversed = parameters.Reversed,
            PlaybackRate = parameters.PlaybackRate,
            OnBegin = parameters.OnBegin,
            Autoplay = false,
        };

        var kinds = new List<CompositorTrackKind>();
        foreach (var (name, value) in parameters.Properties)
        {
            var property = CompositorProperty.Parse(name);
            mirrorParams.Properties[property.Channel] = Resolve(value, visual, index, targets);
            if (!kinds.Contains(property.Kind)) kinds.Add(property.Kind);
        }

        var scratch = CompositorScratch.Seed(visual);
        var mirror = Presto.Animate(scratch, mirrorParams);
        return (mirror, Build(mirror, scratch, kinds));
    }

    /// <summary>
    /// Re-samples <paramref name="mirror"/> from where its playhead stands (COMPOSITOR-PLAN §4.3 step 6), so that starting
    /// the composition animations again carries on instead of jumping back to the beginning: a finite animation compiles
    /// into the single cycle it has left, an infinite one into one full period from the current phase, which rotates the
    /// cycle rather than restarting it.
    /// </summary>
    /// <remarks>
    /// The mirror has to be paused, since sampling seeks it; it is left where it was found. Everything the mirror knows is
    /// in the samples — the remaining iterations, the loop delays, the direction — so the track is one linear cycle
    /// playing forwards, and the exact single segment compile of <see cref="Compile"/> does not apply: half of an ease
    /// is not an ease.
    /// </remarks>
    public static CompiledTrack[] CompileFrom(Animation mirror, CompiledTrack[] tracks)
    {
        if (mirror._headTween is not { } head) return [];

        var kinds = new CompositorTrackKind[tracks.Length];
        for (var k = 0; k < tracks.Length; k++) kinds[k] = tracks[k].Kind;

        var scratch = (CompositorScratch)head.Target;
        var iteration = mirror.IterationDuration;
        var forever = double.IsPositiveInfinity(mirror.IterationCount);
        var position = mirror.CurrentTime;
        var start = Math.Max(position, 0);
        var span = forever
            ? (mirror._alternate ? 2 : 1) * (iteration + mirror._loopDelay)
            : Math.Max(mirror.Duration - start, 0);
        var segments = iteration > 0 ? (int)Math.Clamp(Math.Ceiling(span / iteration * SamplesPerIteration), 1, MaxSamples) : 1;

        var keys = new float[segments + 1];
        var easings = new Easing[segments + 1];
        var values = new double[kinds.Length][][];
        for (var k = 0; k < kinds.Length; k++) values[k] = new double[segments + 1][];
        for (var s = 0; s <= segments; s++)
        {
            keys[s] = (float)s / segments;
            easings[s] = s_linear;
            mirror.Seek(start + (span * s / segments), muteCallbacks: true);
            for (var k = 0; k < kinds.Length; k++) values[k][s] = Read(scratch, kinds[k]);
        }
        mirror.Seek(position, muteCallbacks: true);

        var timeScale = WallClock(mirror);
        var compiled = new CompiledTrack[kinds.Length];
        for (var k = 0; k < kinds.Length; k++)
        {
            compiled[k] = new CompiledTrack(
                kinds[k],
                keys,
                values[k],
                easings,
                TimeSpan.FromMilliseconds(span * timeScale),
                TimeSpan.FromMilliseconds(Math.Max(-position, 0) * timeScale),
                1,
                forever,
                PlaybackDirection.Normal);
        }
        return compiled;
    }

    /// <summary>
    /// The one segment a layout transition plays (COMPOSITOR-PLAN §4.4 step 4): <paramref name="from"/> to
    /// <paramref name="to"/> over <paramref name="mirror"/>'s iteration, after its delay. <paramref name="eases"/> gives
    /// each channel its ease, the last one covering the rest. A shared ease is compiled exactly rather than sampled, since
    /// a FLIP move is a single two-value tween; channels eased differently, which is what makes an arc, are sampled.
    /// </summary>
    public static CompiledTrack Segment(CompositorTrackKind kind, double[] from, double[] to, Animation mirror, Ease[] eases)
    {
        var timeScale = WallClock(mirror);
        var duration = TimeSpan.FromMilliseconds(mirror.IterationDuration * timeScale);
        var delay = TimeSpan.FromMilliseconds(mirror._delay * timeScale);
        if (eases.All(ease => ease == eases[0]))
            return new(kind, [0, 1], [from, to], [s_linear, new PrestoEasing(eases[0])], duration, delay, 1, false, PlaybackDirection.Normal);

        var keys = new float[SamplesPerIteration + 1];
        var values = new double[SamplesPerIteration + 1][];
        var easings = new Easing[SamplesPerIteration + 1];
        for (var s = 0; s <= SamplesPerIteration; s++)
        {
            var progress = (double)s / SamplesPerIteration;
            keys[s] = (float)progress;
            easings[s] = s_linear;
            values[s] = new double[from.Length];
            for (var c = 0; c < from.Length; c++)
                values[s][c] = from[c] + ((to[c] - from[c]) * eases[Math.Min(c, eases.Length - 1)](progress));
        }
        return new(kind, keys, values, easings, duration, delay, 1, false, PlaybackDirection.Normal);
    }

    // Wall-clock ms per mirror ms, the scale Timer.ResetTime rebases on; the engine's own rate already carries the time unit.
    private static double WallClock(Animation mirror) => 1 / (mirror._speed * mirror.OwnerEngine._speed);

    private static CompiledTrack[] Build(Animation mirror, CompositorScratch scratch, List<CompositorTrackKind> kinds)
    {
        var iteration = mirror.IterationDuration;
        var loops = mirror.IterationCount > 1;
        var loopDelay = loops ? mirror._loopDelay : 0;
        // A KeyFrameAnimation has no loop delay, so an alternating one becomes a cycle holding both iterations.
        var expanded = loopDelay > 0 && mirror._alternate;
        var cycle = expanded ? 2 * (iteration + loopDelay) : iteration + loopDelay;

        var exact = new Tween?[kinds.Count];
        var allExact = true;
        for (var k = 0; k < kinds.Count; k++)
        {
            exact[k] = expanded ? null : ExactTween(mirror, kinds[k]);
            allExact &= exact[k] != null;
        }

        var segments = allExact ? 1 : SamplesPerIteration;
        var samples = new double[segments + 1][][];
        for (var s = 0; s <= segments; s++)
        {
            var forward = iteration * s / segments;
            var time = mirror._reversed ? iteration - forward : forward;
            // A looping mirror renders the start of the next iteration at the boundary rather than the end of this one.
            mirror.Seek(loops && time >= iteration ? Math.BitDecrement(iteration) : time, muteCallbacks: true);
            samples[s] = new double[kinds.Count][];
            for (var k = 0; k < kinds.Count; k++) samples[s][k] = Read(scratch, kinds[k]);
        }

        var timeScale = WallClock(mirror);
        var forever = double.IsPositiveInfinity(mirror.IterationCount);
        var iterations = forever ? 1 : (int)mirror.IterationCount;
        if (expanded) iterations = (int)Math.Ceiling(iterations / 2d);
        var direction = mirror._alternate && !expanded
            ? mirror._reversed ? PlaybackDirection.AlternateReverse : PlaybackDirection.Alternate
            : mirror._reversed ? PlaybackDirection.Reverse : PlaybackDirection.Normal;

        var tracks = new CompiledTrack[kinds.Count];
        for (var k = 0; k < kinds.Count; k++)
        {
            var keys = new List<float>();
            var values = new List<double[]>();
            var easings = new List<Easing>();

            void Add(double time, double[] value, Easing easing)
            {
                keys.Add((float)(time / cycle));
                values.Add(value);
                easings.Add(easing);
            }

            if (exact[k] is { } tween)
            {
                Add(0, samples[0][k], s_linear);
                Add(iteration, samples[segments][k], new PrestoEasing(tween._ease));
            }
            else
            {
                for (var s = 0; s <= segments; s++) Add(iteration * s / segments, samples[s][k], s_linear);
            }

            if (loopDelay > 0) Add(iteration + loopDelay, values[^1], s_linear);
            if (expanded)
            {
                for (var s = segments - 1; s >= 0; s--) Add(cycle - loopDelay - (iteration * s / segments), samples[s][k], s_linear);
                Add(cycle, samples[0][k], s_linear);
            }

            tracks[k] = new CompiledTrack(
                kinds[k],
                [.. keys],
                [.. values],
                [.. easings],
                TimeSpan.FromMilliseconds(cycle * timeScale),
                TimeSpan.FromMilliseconds(mirror._delay * timeScale),
                iterations,
                forever,
                direction);
        }
        return tracks;
    }

    private static Tween? ExactTween(Animation mirror, CompositorTrackKind kind)
    {
        Tween? only = null;
        for (var tween = mirror._headTween; tween != null; tween = tween._next)
        {
            if (CompositorProperty.Parse(tween.Property).Kind != kind) continue;
            if (only != null) return null;
            only = tween;
        }
        return only != null
            && only._startTime == 0
            && only._changeDuration == mirror.IterationDuration
            && only._textModifier == null
            && ReferenceEquals(only._modifier, s_noModifier)
            && (kind != CompositorTrackKind.Opacity || StaysInRange(only._ease))
                ? only
                : null;
    }

    // Opacity samples are clamped, but an exact track eases on the server, where an overshoot would get past the clamp.
    private static bool StaysInRange(Ease ease)
    {
        for (var s = 0; s <= SamplesPerIteration; s++)
        {
            if (ease((double)s / SamplesPerIteration) is < 0 or > 1) return false;
        }
        return true;
    }

    private static double[] Read(CompositorScratch scratch, CompositorTrackKind kind) => kind switch
    {
        CompositorTrackKind.Translation => [scratch.X, scratch.Y, 0],
        // The uniform scale multiplies the per axis ones, as RenderTransformComposer does on the UI thread.
        CompositorTrackKind.Scale => [scratch.Scale * scratch.ScaleX, scratch.Scale * scratch.ScaleY, 1],
        CompositorTrackKind.RotationAngle => [scratch.Rotate * Math.PI / 180],
        _ => [Math.Clamp(scratch.Opacity, 0, 1)],
    };

    private static TweenValue? Resolve(TweenValue? value, object target, int index, IReadOnlyList<object> targets)
    {
        if (value is not { } present) return null;
        return Resolve(present, target, index, targets);
    }

    private static TweenValue Resolve(TweenValue value, object target, int index, IReadOnlyList<object> targets)
    {
        switch (value.Raw)
        {
            case FunctionValue or ChainedFunctionValue or Stagger:
                return Resolve(Animation.GetFunctionValue(value, target, index, targets, null)!.Value, target, index, targets);
            case TweenValue[] frames:
                return Array.ConvertAll(frames, frame => Resolve(frame, target, index, targets));
            case TweenParams key:
                Spring? keySpring = null;
                EaseValue? keyEase = null;
                if (key.Ease is { } declared) keyEase = (EaseValue)Pure(declared, target, index, targets.Count, out keySpring);
                return new TweenParams
                {
                    From = Resolve(key.From, target, index, targets),
                    To = Resolve(key.To, target, index, targets),
                    Duration = keySpring != null ? (TweenValue)keySpring.SettlingDuration : Resolve(key.Duration, target, index, targets),
                    Delay = Resolve(key.Delay, target, index, targets),
                    Ease = keyEase,
                    Modifier = key.Modifier,
                    TextModifier = key.TextModifier,
                };
            default:
                return value;
        }
    }

    /// <summary>
    /// Resolves <paramref name="value"/> for one target the way the core does, narrowed as upstream's
    /// <c>parseWAAPIEasing</c> is: a spring contributes its pure solver, never the <see cref="Ease"/> that mutates it as it
    /// is sampled, and its <see cref="Spring.SettlingDuration"/> becomes the duration the caller must apply.
    /// </summary>
    internal static Ease Pure(EaseValue value, object target, int index, int total, out Spring? spring)
    {
        var ease = value.Resolve(target, index, total, out spring);
        return spring?.PureEase ?? ease;
    }
}
