# Motion value time operators: Lag, Trail and Resist

## Why

`MotionValue` can map, combine, spring-follow and measure a source, but it cannot delay one. Cursor trails, follow-the-leader chains and the "tail" behind a dragged item have to be built by hand: the samples chain `Spring` calls in a loop, and nothing can replay a source's path a fixed time later. Separately, three controls each implement their own rubber band past a bound (`Draggable`'s `ContainerFriction`, `Sheet`'s friction 0.35, `PullToRefresh`'s exponential damping), and a gesture-driven value graph has no way to apply one.

This adds two time-driven operators (`Lag`, `Trail`) and one pure mapping (`Resist`) to CORE-MOTIONVALUE. It does not overlap `pointer-proximity-value.md`, which adds a bridge source; these operators work on any source, that one included.

## What

- **`Lag(double delay)`** returns a value that replays its source `delay` engine-time units late: at engine time `t` it holds the source's value at `t − delay`, interpolated linearly between recorded samples. It is time-driven like a spring follower: it stays linked to its source, is stepped by the engine while it has history to play out, and sleeps once it has caught up with the source.
- **`Trail(int count, Spring spring)`** and **`Trail(int count, double delay)`** return `count` followers chained one after another: the first follows this value, each next one follows the previous (react-spring's `useTrail`). The spring form chains `Spring(spring)`; the delay form chains `Lag(delay)`, so follower `i` replays the source `(i + 1) × delay` late. Feeding a set of controls is a loop over `Follow.Bind` or the `Follow.X`/`Follow.Y` shorthands, which already accept motion values (BRIDGE-FOLLOW-024, 042).
- **`Resist(double min, double max, double friction = 0.8)`** returns a `Map` that passes the source through inside `[min, max]` and scales the excess past either bound by `1 − friction`, the same curve and default as `Draggable`'s `ContainerFriction` (BRIDGE-DRAGGABLE-021, 064). It is a pure derived value, not time-driven; it lives in this change because it completes the gesture-graph toolkit and shares its tests.

What does not change: existing operators, the flush order (moving values step before sinks flush), reduced-motion handling (motion values are not animations, so `ReducedMotion` does not touch `Lag`/`Trail`; sinks keep deciding their own reduced form), and the three controls' own rubber bands (moving them onto `Resist` is out of scope).

## Decisions

- **`Lag` is a time-delay buffer, not a low-pass filter.** It reproduces the source's path exactly, overshoots included, which is what a trail or echo needs. Reason: smoothing already exists as `Spring`; a delay cannot be built from the existing operators. Rejected: an exponential "lerp toward" follower (frame-rate dependent unless solved in closed form, and `Spring` with high damping covers it).
- **Samples are recorded on engine steps and read with linear interpolation.** The lag records `(time, value)` on every step in which its source's version moved, and its output at `now` interpolates between the two samples bracketing `now − delay`, holding the oldest sample before the first and the newest after the last. Reason: the output is frame-rate independent and smooth when the reading and recording frame rates differ. Rejected: holding the last sample (stair-steps at uneven frame times).
- **The buffer is a growable ring of two parallel `double` arrays.** It starts at 16 samples, doubles when full, never shrinks, and drops samples older than the one bracketing `now − delay` on each step. Reason: the steady-state step allocates nothing (as `AllocationTests` already demands of springs); growth happens only while a source moves faster than the buffer has ever held, and at 60 fps a one-second lag needs 64 samples. Rejected: a fixed capacity (silently drops history at high frame rates or long delays).
- **A lag sleeps once caught up.** `Step` returns false when the buffer holds only the newest sample and the output equals it; a later source change reactivates it through the same `Invalidate` path that wakes spring followers, seeding the buffer with `(now, held value)` so time spent asleep does not stretch the first segment. Reason: the engine must sleep when nothing moves (CORE-MOTIONVALUE-022).
- **Pause and hide shift the buffer, freeze does not.** `Engine.Resume` already adds the paused interval to each moving value's `_time`; it will call an internal `Rebase(paused)` instead, which a lag implements by shifting every sample time. `Freeze` stops engine time itself, so samples need no shift, and `Engine.Step` advances a lag by exactly one frame. Reason: the replay continues where it was, as springs do (CORE-MOTIONVALUE-035, 036).
- **`delay` is in the engine's time unit**, like every duration, and is validated `> 0` and finite. `Lag(0)` throws rather than returning the source, so the result is always a Presto-assigned value with the same `Set` contract.
- **`Trail` always chains**, for both forms. Reason: chaining is `useTrail`'s behavior for springs, and for lags it bounds each buffer to one `delay` of history (fanning every follower off the leader would hold `i × delay` each). Rejected: per-index settings through a `Func<int, Spring>` until a sample needs it (see Open questions).
- **`Resist` is a `Map` with the library's existing linear curve.** Reason: one rubber-band definition across `Draggable` and the value graph; it needs no stepping. Rejected: `PullToRefresh`'s asymptotic `r(1 − e^(−d/r))` as the default (see Open questions).

## Spec changes

**CORE-MOTIONVALUE** (`docs/specs/core/motion-values.md`): extend Purpose to name `Lag`, `Trail` and `Resist`; add a "Lag and trail" section and a `Resist` requirement under "Range maps".

Added, draft wording:

- `Lag(delay)` shall return a value that, at engine time `t`, holds its source's value at `t − delay`, interpolated linearly between the samples recorded either side, starting at the source's value.
- When `delay` is not finite or not greater than 0, `Lag` shall throw `ArgumentOutOfRangeException`.
- A lag shall record its source's value, with the engine time, on each engine step in which the source's version moved since the last sample.
- While a lag holds more than one sample or its value differs from its newest sample, the engine shall step it every frame; once it holds one sample and equals it, it shall stop stepping, so the engine can sleep.
- When a sleeping lag's source moves, the lag shall restart its history from its held value at the current engine time, so time spent asleep does not count.
- A lag shall stay linked to its source whether or not it is observed (extends CORE-MOTIONVALUE-030 to lags).
- Stepping a lag shall allocate nothing once its buffer has grown to hold `delay` worth of samples at the current frame rate.
- `Trail(count, spring)` shall return `count` values, the first following this value through `Spring(spring)` and each next one following the previous the same way.
- `Trail(count, delay)` shall return `count` values, the first being `Lag(delay)` of this value and each next one `Lag(delay)` of the previous.
- When `count` is less than 1, `Trail` shall throw `ArgumentOutOfRangeException`.
- `Resist(min, max, friction)` shall return the source value inside `[min, max]`, and past a bound, the bound plus the excess scaled by `1 − friction` clamped to `[0, 1]`.
- When `min` is greater than `max`, `Resist` shall throw `ArgumentException`.
- When `friction` is not given, `Resist` shall use 0.8 (under Defaults).

Changed:

- CORE-MOTIONVALUE-002: add `Lag` and `Trail` results to the list of values whose `Set` throws.
- CORE-MOTIONVALUE-029: `IsAnimating` is true while a follower or lag is stepping (`FollowSpring`/`SpringTarget` stay spring-only).
- CORE-MOTIONVALUE-035: "moving values shall be rebased by the paused interval" now covers a lag's recorded samples.
- CORE-MOTIONVALUE-036: `Engine.Step` moves a follower or a lag by exactly one frame.
- Invariants: "a value is in the engine's active list only while it is a stepping spring, a lag with history to play, or a moving velocity sample."
- Allocation: add the lag step to the allocation-free list; add "a lag's buffer grows (doubling) while it must hold more samples than ever before".
- Threading and lifetime: a lag, like a spring follower, stays linked for its source's lifetime.

Struck: none.

**BRIDGE-FOLLOW**: no requirement changes; add a sentence to Purpose that trails reach controls through the existing motion value sinks.

## Tasks

1. `MotionValue`: generalize the time-driven flag (today `_spring != null`) to cover lags in `IsObserved`, `Invalidate` and `Step`; replace `Engine.Resume`'s `_time += paused` with an internal `Rebase(paused)`. Tests: existing `MotionValueDynamicsTests` stay green.
2. `Lag`: ring buffer, recording, interpolation, sleep and wake, validation. Tests in `MotionValueDynamicsTests`: replays the source `delay` late under `ManualFrameSource` at 60 and 144 fps with the same result at matching times; holds the initial value for the first `delay`; interpolates between uneven frames; sleeps once caught up and the engine idles; a change after a long sleep starts from now; pause/resume and hide/show continue without a jump; frozen `Step` advances one frame; seconds mode; invalid `delay` throws; `Set` throws.
3. Allocation: extend `AllocationTests` with a lag stepping a moving source after warm-up.
4. `Trail`: both overloads and validation. Tests: element `i` of a lag trail equals the source `(i + 1) × delay` late; a spring trail equals hand-chained `Spring` calls frame for frame; `count < 1` throws.
5. `Resist`: map and validation. Tests in `MotionValueTests`: identity inside the bounds, scaled excess on both sides, friction 1 pins, friction 0 passes through, out-of-range friction clamps, default 0.8, `min > max` throws.
6. Spec: apply the CORE-MOTIONVALUE changes above with `Tests:` lines; `SpecTests` green.
7. Sample: a "Trail" demo on the Animatable or Gestures showcase page: a row of dots following the pointer (`PointerValue` → `Trail(8, Springs.Gentle)` and a toggle to `Trail(8, 40)`), plus a draggable handle whose `XValue.Resist(...)` drives an indicator; its `#region snippet` code panel; a `*PageTests` smoke test.
8. Visual check on the Desktop sample: run `dotnet run --project samples/PrestoSample.Desktop -- --page <slug>`, move the pointer, confirm the spring and lag trails, that the engine idles once the dots settle (DevTools frame stats), and the resisted indicator past its bounds.

## Open questions

- Should `Trail` accept per-index settings (`Func<int, Spring>` or a `FunctionValue`-style delegate) so stiffness can fall off along the chain? Deferred unless the sample wants it.
- Should `Resist` also offer the asymptotic curve (`PullToRefresh`'s `r(1 − e^(−d/r))`, iOS-style) through a separate method or a `limit` parameter?
- Is a bridge convenience worth adding (`Follow.Trail(targets, x, y, …)` binding each control's `X`/`Y` to a trail), or is the explicit loop enough? Decide after the sample is written.
