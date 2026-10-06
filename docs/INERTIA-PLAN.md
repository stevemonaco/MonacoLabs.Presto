# Inertia — momentum from a velocity, with snapping and bounds

Phase 33 in [PLAN.md](PLAN.md). Status: **built**. This plan follows the post-Phase-27 gap review. Framer Motion's
`inertia` and GSAP's InertiaPlugin let any value coast to a stop from a measured velocity, snap where it lands, and bounce
off bounds. In Presto that motion exists only inside `Draggable`'s release, and even there it isn't a decay.

| Phase | Scope | Size |
|---|---|---|
| 33 | `Inertia` as a to-value (computed end, ease and duration), snapping, bounds handed over to a spring, `Animatable.Fling`, an opt-in inertia release on `Draggable`, a public velocity tracker | medium |

## 0. What exists today

- **`Draggable`'s release** (`Draggable.Release`, a port of `handleUp` in `tools/anime/src/draggable/draggable.js`) is a
  projection, not a decay:
  - The pointer speed is the maximum over three samples 17 ms apart, in units per ms, clamped to
    `[MinVelocity, MaxVelocity]`.
  - It is thrown `velocity * 150` units along the release angle, cushioned past the container by
    `1 - ReleaseContainerFriction`, snapped and clamped.
  - That destination is then eased with `ReleaseEase` (default `OutQuint`) over a spring's settling duration minus its
    200 ms rest, or with a supplied release spring.
  - An out-of-bounds throw plays two blended tweens on an overshoot target: out to the cushioned point in 65 % of the
    time, then back to the clamped destination.
  - The adapted `draggables.test.js` cases in `DraggableTests.cs` pin this: snap per step, out-of-bounds return, ease
    and spring settling at the same coordinates, and a mid-throw container resize retargeting the release.
- **`Spring`** is an ease with a computed `SettlingDuration`. The `Animation` constructor takes that as the tween's
  duration whenever the ease resolves to a spring (`keySpring != null`), and parents the spring so `OnComplete` can fire.
  This is the pattern inertia follows.
- **Phase 18** (`Animatable.CarryMomentum`, decision 20):
  - It measures a running tween's velocity in units per engine-time unit by a central difference of its ease.
  - It converts that to the solver's unit, `v * K * TimeScale / distance`, progress per solver second.
  - It starts a fresh `Spring` with it.
  - The bounds handover below reuses both conversions.
- **`Utils.Snap(increment)` / `Utils.Snap(points)`** already return a `ChainedUtil`, i.e. a `Func<double, double>`, so
  snapping needs no new type.

## 1. The motion

Time `t` is in the engine's time unit (ms by default) and velocity `v` in units per engine-time unit, which is what
`Draggable.Velocity` already reports. With time constant `τ` and power `P`:

```
A      = P · v · τ                     amplitude, the ideal travel
ideal  = from + A
target = modifyTarget(ideal)           snapping; A' = target − from
x(t)   = target − A' · e^(−t/τ)
x'(0)  = A' / τ                        equals P·v when nothing snapped
```

- **Power default.** With `P = 1` the motion starts at exactly the release velocity. Framer's `amplitude = power ·
  velocity` takes velocity in units per *second* and defaults to `power 0.8`, `timeConstant 700`. That is `P = 0.8 /
  0.7 ≈ 1.14` in this formulation, i.e. Framer starts slightly faster than the pointer. Presto defaults `P = 1` and
  `τ = 350` ms, which lands a 1 unit/ms fling 350 units away.
- **Duration.** The decay is at rest when `|A'| · e^(−t/τ) < RestDelta` (default 0.5 units, as Framer), which gives
  `D = τ · ln(|A'| / RestDelta)`, clamped to `[0, 60000]`. When `|A'| ≤ RestDelta`, `D` is `Constants.MinValue` and the
  value is set.
- **Ease.** As a tween from `from` to `target` over `D`, the ease is
  `e(u) = (1 − e^(−uD/τ)) / (1 − e^(−D/τ))`. The divisor makes `e(1) = 1` exactly, so the tween lands on `target` rather
  than `RestDelta` short of it.
- **Bounds.** `Min`/`Max` bounds, with the trajectory hitting one:
  - The crossing time is `t_c = −τ · ln(1 − (bound − from) / A')` and the speed there is `v_c = (A'/τ) · e^(−t_c/τ)`.
  - From `t_c` a spring (`BounceStiffness` default 500, `BounceDamping` default 10, as Framer) takes over at the bound
    with velocity `v_c` and settles on the bound.
  - A value that starts outside the bounds skips the decay and springs straight to the nearest bound with its velocity,
    as Framer does.

## 2. Decisions

1. **`Inertia` is a to-value, shaped like `Spring` is an ease.** Its end value depends on the start value, so neither an
   ease nor a factory that reads the target up front fits. A sealed core class `Inertia` has `Velocity`,
   `Power`, `TimeConstant`, `RestDelta`, `ModifyTarget`, `Min`, `Max`, `BounceStiffness` and `BounceDamping`, and is
   passed where a `To` goes.
   - It gets implicit conversions to `TweenValue` (and so into function values and staggers).
   - The `Animation` constructor resolves it once the tween's from-number is known, at the point where relative `+=`
     values resolve today. It then sets the tween's to-number, ease and duration, as `keySpring` sets duration today.
   - Everything downstream is unchanged: callbacks, `Then()`, timelines, composition, `Revert`, `Stretch`, playback rate
     and `ManualFrameSource` determinism.
2. **One tween, even with a bounce.** The spring after the crossing is folded into the same tween's ease rather than a
   second keyframe:
   - For `u·D ≥ t_c` the ease is `1 + (S_v(s) − S_0(s))`, where `s` is solver time since the crossing.
   - `S_v` and `S_0` are `Spring.Solve` for the bounce spring with and without the crossing velocity. Normalized as
     Phase 18 does, that velocity is `v_c · K · TimeScale / (bound − from)`.
   - The spring is linear in its initial conditions, so the difference is exactly the response to the velocity alone.
     No new solver is written.
   - The tween's to-value is the bound it settles on, so readers of `To` (`Animatable.Get`, composition, `Complete`)
     see the true rest.
3. **The degenerate bounce, released on a bound moving outward** (`bound == from`), has no distance to normalize by:
   - The tween is built from the bound to the spring's first peak and back.
   - That is two keyframes: out with the impulse response up to its peak, back with a spring from rest.
   - The peak is found by sampling the response, which the solver makes cheap.
4. **Snapping is `ModifyTarget`, a `Func<double, double>`.** `Utils.Snap(40)` and `Utils.Snap(new[] { 0, 120, 240 })`
   already produce one, and any user function works. It applies to the ideal target before bounds; a snapped target
   beyond a bound still hits the bound and bounces. Snapping changes `A'`, so the start velocity changes with it, as in
   Framer. GSAP's alternative of stretching the duration to keep velocity continuous is rejected: it breaks the
   closed-form duration.
5. **Per-target velocity comes through function values.** `Velocity` is a `double` on the instance. A `FunctionValue`
   returning an `Inertia` per target covers staggers and multi-target flings with no new plumbing.
6. **`Animatable.Fling(property, velocity, Inertia? settings)`** retargets a property with an inertia. Omitting the
   velocity (`Fling(property)`) measures the running tween's velocity with Phase 18's `Velocity(tween, …)`, so a value
   that is already moving keeps coasting. The animatable's own ease is replaced for that run only, as `Set(…, ease)`
   does today.
7. **`Draggable` keeps its upstream release by default.** The ported parity tests pin the projection-and-ease release,
   and `PLAN.md`'s "rebuilt on it" is withdrawn.
   - A new opt-in `DraggableParams.ReleaseInertia` (an `Inertia` template) replaces the projection when set.
   - Each axis gets `v·cos(angle)` or `v·sin(angle)`.
   - The container bounds become `Min`/`Max`, and the axis snap becomes `ModifyTarget` unless the template sets one.
   - The overshoot animations, cushion and `ReleaseEase` are bypassed.
   - `Refresh`'s mid-throw retarget (`RetargetRelease`) re-resolves an inertia release from its current value and
     velocity instead of re-aiming over the remaining time.
8. **The velocity tracker is made public.** `Draggable`'s private sampler (three samples, 17 ms apart, max-of-three)
   becomes a core `VelocityTracker`:
   - `Add(double value, double time)`, `Velocity`, `Reset()`, with the interval and sample count configurable.
   - It is allocation-free.
   - `Draggable` uses it unchanged.
   - Apps with their own gestures (Avalonia `ScrollGestureRecognizer`, pinch, a custom swipe) feed it and pass
     `tracker.Velocity` to an `Inertia`. It is in the core because it only needs numbers and the frame source's `Now`.
9. **Reduced motion lands on the target at once.** `ReduceAll` already collapses every tween to `Constants.MinValue`, and
   since the target is resolved before the duration, the value snaps to the snapped, clamped rest. A `WhenReduced`
   alternative in a builder behaves as for any other property.
10. **Seconds mode.** Time-based fields are in the engine's unit, as `Spring.Duration` is, and velocity is per engine-time
    unit, so switching units converts `TimeConstant` and velocity together. The spring handover multiplies by
    `Globals.TimeScale` exactly as Phase 18 does.
11. **Out of scope:**
    - `ScrollViewer` flinging: Avalonia's `ScrollGestureRecognizer` has its own inertia (`IsScrollInertiaEnabled`), and
      replacing it is a control-level change.
    - Two-dimensional inertia with a shared direction (each axis decays on its own, as in Framer).
    - Compositor inertia. An inertia tween compiles to a sampled `KeyFrameAnimation` like any other ease, so
      `AnimateOnCompositor` accepts it for transform and opacity without new work; a test confirms it.

## 3. API

```csharp
public sealed class Inertia
{
    public Inertia(double velocity);
    public double Velocity { get; init; }                  // units per engine-time unit
    public double Power { get; init; } = 1;
    public double TimeConstant { get; init; } = 350;       // engine-time unit
    public double RestDelta { get; init; } = 0.5;
    public Func<double, double>? ModifyTarget { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public double BounceStiffness { get; init; } = 500;
    public double BounceDamping { get; init; } = 10;
    public static implicit operator TweenValue(Inertia inertia);
}

public sealed class VelocityTracker
{
    public VelocityTracker(double interval = 17, int samples = 3);
    public void Add(double value, double time);
    public double Velocity { get; }
    public void Reset();
}
```

```csharp
Presto.Animate(card, new AnimationParams { ["X"] = new Inertia(drag.VelocityX) { ModifyTarget = Utils.Snap(240), Min = -960, Max = 0 } });

carousel.Animate(b => b.X(new Inertia(v) { ModifyTarget = Utils.Snap(pageWidth) }));

follower.Fling("Y", tracker.Velocity, new Inertia(0) { Max = 0, Min = -contentHeight });

new Draggable(sheet, new DraggableParams { Y = true, ReleaseInertia = new Inertia(0) { TimeConstant = 250 } });
```

The builder needs nothing new: `TweenValue` accepts the `Inertia`, so the generated `X`, `Y`, `Property` overloads take
it. A `Duration` set beside an inertia tween is ignored for that tween, as it is for a spring ease; `Delay` applies.

## 4. Implementation steps

1. **`Inertia` math (internal, pure).** The target, duration and ease factories, the crossing solve, the ease that
   folds in the spring (decision 2), and the degenerate two-keyframe expansion (decision 3). Unit tests against a
   numerical integration before any wiring.
2. **`Animation` constructor.**
   - Recognize an `Inertia` to-value.
   - Resolve it after the from-number decomposes. Today the tween duration is computed before from is known, so for
     inertia tweens duration and ease are assigned after decomposition and before the tween's change end time.
   - Reject a non-number property (color, complex) with `ArgumentException`.
   - Honor `ReduceAll`.
3. **`VelocityTracker`**, then `Draggable.ComputeVelocity` moved onto it with the parity tests unchanged.
4. **`Animatable.Fling`**, reusing Phase 18's velocity measurement.
5. **`DraggableParams.ReleaseInertia`**, per decision 7, including the `Refresh` retarget.
6. **README:** a Framer `inertia` / GSAP InertiaPlugin row in the cheat sheet, and the units note (velocity per ms).

## 5. Tests (`InertiaTests`, `VelocityTrackerTests`, `AnimatablesTests`, `DraggableTests`)

- The math:
  - `x'(0)` equals `P·v` without snapping.
  - `x(D)` is within `RestDelta` of the target before normalization, and the eased tween lands exactly on it.
  - Doubling `τ` doubles the travel.
  - Negative velocity travels backward.
  - Velocity below `RestDelta / τ` sets the value at once.
- Snapping: `Utils.Snap(100)` lands on a multiple of 100 nearest the ideal target, and `Utils.Snap(points)` on the
  nearest point.
- Bounds:
  - Position and velocity are continuous at `t_c`: a central difference either side agrees within tolerance.
  - The value peaks past the bound and settles on it.
  - A start outside the bounds springs straight back.
  - A release exactly on the bound moving outward goes out and back (decision 3).
- The trajectory matches a 1 ms-step numerical integration of `x' = (target − x)/τ` and, after the crossing, of the
  spring ODE.
- Integration through `ManualFrameSource`:
  - The frame-by-frame values are identical across two runs.
  - `Then()` completes at `D`.
  - `Revert` restores the start.
  - `Seek` mid-flight reproduces `x(t)`.
  - Seconds mode gives the same trajectory scaled.
- A `FunctionValue` returning per-target inertias staggers correctly. `ReduceAll` lands on the snapped, clamped target
  in one frame.
- `Animatable.Fling` without a velocity continues a running `Set` with matching velocity, the Phase 18 assertion
  reused.
- `VelocityTracker` reproduces the values the current `Draggable` sampler gives for the same input, and every existing
  `DraggableTests` case passes untouched.
- `ReleaseInertia`:
  - A flick decays and snaps per axis.
  - An out-of-bounds flick bounces off the container.
  - `Refresh` mid-throw re-resolves and settles once.
- `AnimateOnCompositor` with an inertia `X` samples the same curve as the UI-thread animation.
- `AllocationTests`: a running inertia tween holds the core's zero-allocation tick budget.

## 6. Samples

- **Draggable page:** a *Release* picker (upstream, inertia) on the existing demo, with the builder printed. The
  inertia release shows bounds bouncing and a snap grid.
- **Animatable page:** a *Fling* button that coasts the follower from its current velocity.
- **Carousel showcase:** the swipe settles through `Inertia` with `ModifyTarget = Utils.Snap(pageWidth)`, so a hard
  flick skips several pages and a soft one moves one.
- **Gestures showcase:** a bottom sheet dragged with `ReleaseInertia` and bounds at its open and closed stops.

## 7. Open questions (confirm before the phase)

- The `TimeConstant` and `Power` defaults (`350`, `1`) versus Framer's (`700`, `0.8` in per-second units). Decide by
  feel on the sample; only the defaults change.
- Whether `Inertia` should also accept the velocity as a `FunctionValue` field instead of relying on a `FunctionValue`
  that returns whole inertias. It is not needed unless a sample shows the object form is clumsy.
- Whether keyframe arrays may contain an `Inertia` (a fling as the first keyframe, then a settle). Rejected with
  `ArgumentException` until a use appears, because its duration is only known after the previous keyframe resolves.
- Whether the degenerate bounce (decision 3) is worth its two-keyframe special case, or a release on a bound moving
  outward should just stop there. Framer bounces, so the plan bounces.

## 8. Deviations as built

- §7's open questions were settled as planned; defaults are unchanged.
- **The solver** is `Core/InertiaSolver`, returning a `Plan(To, Duration, Ease, Then?)`. The decay duration is clamped to
  `[0, 60000]`; a bounce lasts the crossing time plus the thrown spring's settling duration. A start outside the bounds
  springs to the nearest bound with the spring's side-effect-free `PureEase`.
- **The degenerate bounce** (decision 3) is taken not only at `bound == from` but whenever the crossing velocity
  normalized by `bound − from` exceeds `K · 10`, i.e. a release just short of a bound. Its peak is found by stepping the
  impulse response in `1e-3` s steps. `Plan.Then` carries the way back:
  - The `Animation` constructor turns it into two consecutive tweens, as planned.
  - `Animatable.Fling` has one tween per target, so it folds both parts into a single ease from the bound out to the
    peak and back.
- **`VelocityTracker`** takes values, not displacements: the first `Add` after `Reset` is only the baseline, and
  `Velocity` is the signed sample of largest magnitude. `Reset` keeps the last sample time, as upstream does. `Draggable`
  measures path length rather than a value, so it uses an internal `AddDisplacement(distance, time)` that reports
  whether the sample was accepted. It keeps its own multiplier, `[MinVelocity, MaxVelocity]` clamp and 5-digit rounding,
  applied only on an accepted sample, so the parity tests pass untouched.
- **`Animatable.Fling`**:
  - `Fling(property)` measures with Phase 18's velocity helper, and gives 0 when the animation is paused or finished.
    The helper now handles any ease: a spring's ease is swapped for its `PureEase` by checking the delegate's `Target`,
    which removed `CarryMomentum`'s `declared` parameter.
  - When a property's targets resolve to different durations, every tween is sampled over the longest.
  - The ease and duration from before the fling are saved and restored by the next scalar `Set`.
- **`ReleaseInertia`** ignores the template's own `Velocity`, `Min` and `Max`. `Refresh` mid-throw re-resolves only when
  the resting point falls outside the new bounds, through `Fling(property, settings)` from the current value and the
  measured velocity. `ScrollInView` uses the longest fling's duration.
- **No per-axis velocity on `Draggable`.** §3's `drag.VelocityX` does not exist: `Draggable` reports a speed and an
  `Angle`, and `ReleaseInertia` splits them into `Velocity·cos(Angle)` and `Velocity·sin(Angle)` itself.
- **Carousel sample.** It snaps through the draggable's `Snap` lambda, which `ReleaseInertia` uses as its
  `ModifyTarget`, rather than a `ModifyTarget` on the template. The active dot is marked in `OnSettle` from the resting
  `X` instead of in `OnSnap`, because `OnSnap` fires at release, before the inertia lands.
