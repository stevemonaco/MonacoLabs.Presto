# Settling a scrubbed timer

## Why

`Timer.Scrub(progress)` (CORE-MOTIONVALUE-039) lets a gesture drive an authored animation or timeline, but `Scrub(null)` leaves the timer paused wherever the gesture let go. Nothing hands control back from the gesture to time: finishing the motion toward open or closed, or the nearest label, with the speed the finger had. Every drag-to-open menu, drag-to-dismiss card or swipe-between-states built on a timeline needs that last step, and today it has to be hand-written (a spring over a `MotionValue`, then `Scrub` on that, then cleanup).

`Sheet`, `Pager`, `Swipe` and `PullToRefresh` each settle after a drag, but they settle property values (offsets, extents) through `Inertia` or a spring, not a timer's progress. They are not timeline-driven, so this change does not migrate them (see Decisions).

## What

A new `Timer.Settle` finishes a timer from its current progress to a chosen resting progress, driven by a spring that starts with the release velocity:

```csharp
var open = Presto.CreateTimeline(...);            // authored open motion, staggered children
var drag = Presto.CreateMotionValue();            // fed from a Draggable or pointer, 0..1
open.Scrub(drag);
// on release:
var landed = await open.Settle();                 // reads drag's velocity, lands on 0 or 1
```

```csharp
public Task<double> Settle(SettleParams? settings = null);

public sealed record SettleParams
{
    public double? Velocity { get; init; }        // progress per engine time unit; null reads the scrub source's Velocity
    public double[]? Points { get; init; }        // resting progresses; null = { 0, 1 }
    public bool Labels { get; init; }             // a Timeline also rests on each label (time ÷ Duration)
    public double Projection { get; init; } = 350;  // ms (engine unit), as Inertia.TimeConstant
    public double? FlickVelocity { get; init; }   // past this speed, go to the next point in the velocity's direction
    public Spring? Spring { get; init; }          // null = Springs.Gentle
}
```

Target selection: the projected progress is `Progress + Velocity × Projection`; the target is the point nearest it. When `|Velocity| ≥ FlickVelocity`, the target is instead the nearest point strictly beyond the current progress in the velocity's direction (or the last point that way). The spring then moves the playhead from the current progress to the target, starting with the release velocity converted to progress per second, and the timer is seeked to `value × Duration` every frame, as a scrub is. Progress is clamped to [0, 1], so overshoot flattens at the ends.

The returned task completes with the landed progress when the spring rests on the target, or with the progress at the moment the settle is interrupted. Seeks fire callbacks exactly as `Seek` does, so landing on 1 sets `Completed`, fires `OnComplete` and resolves `Then()`; landing on 0 fires none of them.

Unchanged: `Scrub`, `Seek` and play controls behave as today, except that each now also ends a running settle.

## Decisions

- **`Settle` lives on `Timer`, beside `Scrub`.** It works for an `Animation` and a `Timeline` alike, since both seek by progress. Rejected: a `MotionValue` operator (`drag.SettleTo(...)`), because the resting points (labels, `Duration`) belong to the timer, and the caller would still need `Scrub` plumbing.
- **Velocity defaults to the scrub source's `Velocity`.** `MotionValue.Velocity` is already in value units per engine time unit, so a scrub source in 0..1 gives progress velocity directly, and the common case needs no arguments. Rejected: requiring a velocity in DIPs, which would need the caller's DIP-to-progress mapping inside the core.
- **Target by projection plus an optional flick threshold.** Projection with `Inertia`'s default 350 ms time constant matches how `Sheet` and `Pager` pick a rest today; the flick threshold covers "a quick swipe always advances" without making the projection huge. Rejected: a fixed 50 % threshold only, which ignores velocity.
- **Labels are opt-in and live in time, not eased progress.** Labels are raw timeline times (CORE-TIMELINE), and the settle seeks in time, so a label resolves to `time ÷ Duration` and lands exactly on it whatever `PlaybackEase` does. Rejected: always including labels, which would surprise a timeline using labels only for layout.
- **The spring is the core `MotionValue` spring stepper, reused internally.** The settle is an internal source holding the target, a spring follower seeded with the current progress and the release velocity, and an internal scrub on that follower. Reason: it is already frame-rate independent, engine-time based, sleeps at rest and rebases on pause/hide (CORE-MOTIONVALUE-025…036). Rejected: a tween with a spring ease, which cannot start with a velocity without the numeric derivative CORE-SPRING declines to expose.
- **Progress is clamped to [0, 1]; the default spring does not bounce.** A timer cannot render outside its duration, so a bouncy spring would flatten against an end. `Springs.Gentle` (bounce 0) is the default; a bouncy spring is allowed and clamps.
- **The task never faults and never hangs.** Interrupted settles complete with the progress where they stopped, so `await` always continues. Rejected: cancelling the task (callers would need try/catch for a routine re-grab), and returning `Timer` for chaining (its `Then()` only completes at the end, never at 0).
- **Any playhead control ends the settle.** `Scrub`, `Seek`, `Play`, `Resume`, `Reverse`, `Restart`, `Pause`, `Cancel`, `Revert`, `Complete` and a new `Settle` end it first. Reason: the last caller wins, as with a new `Scrub` (CORE-MOTIONVALUE-041).
- **Reduced motion lands at once.** When `ReducedMotion` is set on the timer's engine at the call, the target is still chosen from the velocity, and the timer is seeked to it in one call. Reason: the destination is meaningful (open or closed), the travel is not; this matches `Inertia` under `ReduceAll` (CORE-INERTIA-030).
- **Migrating `Sheet`, `Pager`, `Swipe` and `PullToRefresh` is out of scope.** They settle property values, not timers; adopting `Settle` would mean restructuring each around a timeline. A follow-up can revisit once a control is authored as a timeline.

## Spec changes

**CORE-MOTIONVALUE** — the Timer sources section owns `Settle` (it already owns `Scrub`); add `SettleParams` to `types` and `Monaco.Presto/SettleParams.cs` to `sources`. Requirements added:

- `Timer.Settle` shall end any running scrub and settle, pause the timer, and move its playhead from `Progress` to a target progress by a spring, seeking to `value × Duration` on every frame the spring moves.
- When `SettleParams.Velocity` is null, the settle shall use the `Velocity` of the value the timer was scrubbed by when `Settle` was called, or 0 when it was not scrubbed.
- The settle shall choose the target as the resting point nearest `Progress + Velocity × Projection`.
- When `FlickVelocity` is set and `|Velocity|` is at least `FlickVelocity`, the settle shall choose the nearest resting point strictly beyond `Progress` in the velocity's direction, or the last point in that direction when none lies beyond.
- The resting points shall be `Points`, or 0 and 1 when null, plus, when `Labels` is set on a `Timeline`, each label's time divided by `Duration`, each clamped to [0, 1].
- The spring shall start at `Progress` with the velocity converted to progress per second, keep the position and velocity hand-off rules of a `MotionValue` spring follower, and land exactly on the target at rest.
- The progress the settle seeks to shall be clamped to [0, 1].
- Each seek of a settle shall fire callbacks as `Seek` does, so landing on 1 sets `Completed`, fires `OnComplete` and completes `Then()`.
- When the spring rests on the target, the returned task shall complete with the target.
- When `Scrub`, `Seek`, `Play`, `Resume`, `Reverse`, `Restart`, `Pause`, `Cancel`, `Revert`, `Complete` or another `Settle` is called during a settle, the settle shall stop and its task shall complete with `Progress` at that moment.
- While a settle runs, the engine shall stay awake; once it rests, the engine shall be free to sleep.
- When `ReducedMotion` is set on the timer's engine when `Settle` is called, the settle shall choose its target as above and seek to it at once, completing the task before `Settle` returns.
- When the timer's `Duration` is infinite (an infinite loop), `Settle` shall throw `InvalidOperationException`.
- When `Points` is empty or contains a non-finite value, or `Projection` or `FlickVelocity` is negative or non-finite, `Settle` shall throw `ArgumentException`.
- (Defaults) When not given, `Points` shall be 0 and 1, `Projection` 350 ms in the engine unit, `FlickVelocity` off, and `Spring` a new `Springs.Gentle`.

Requirement changed: **CORE-MOTIONVALUE-040** — "`Scrub(null)`, `Cancel`, `Revert` and `Settle` shall end the scrub; the engine's cancel of a completed timer shall not."

**CORE-TIMER** — add one requirement: "`Seek`, `Play`, `Resume`, `Reverse`, `Restart`, `Pause`, `Cancel`, `Revert` and `Complete` shall end a running `Settle` (CORE-MOTIONVALUE)." Add CORE-MOTIONVALUE to its `depends` if absent.

## Tasks

1. `SettleParams` record and validation. Tests: `SettleTests.InvalidParamsThrow`, `SettleTests.InfiniteTimerThrows`.
2. Target selection (projection, flick, points, labels, clamping) as a pure internal function. Tests: `SettleTests.ProjectionPicksTheNearestPoint`, `FlickAdvancesToTheNextPointInTheVelocityDirection`, `LabelsBecomeRestingPointsOnATimeline`, `PointsAreClampedToTheUnitRange`.
3. Seed a `MotionValue` spring follower with a position and velocity (internal), and drive the settle through it with an internal scrub. Tests: `SettleTests.ReleaseVelocityCarriesIntoTheSpring` (first-frame slope matches the velocity), `LandsExactlyOnTheTargetAndCompletesTheTask`, `TrajectoryIsFrameRateIndependent`, `ProgressIsClampedWhenTheSpringOvershoots`.
4. Velocity default from the scrub source. Tests: `SettleTests.UsesTheScrubSourceVelocityWhenNoneIsGiven`, `UnscrubbedTimerSettlesWithZeroVelocity`.
5. Callbacks and completion. Tests: `SettleTests.LandingOnOneCompletesAndResolvesThen`, `LandingOnZeroFiresNoCompletion`.
6. Interruption from every playhead control, and `Scrub`/`Cancel`/`Revert` ending a settle. Tests: `SettleTests.ANewScrubEndsTheSettleWithTheCurrentProgress` (theory over each control), `ASecondSettleReplacesTheFirst`.
7. Engine time: sleep at rest, pause/hide rebasing, `Engine.Step` while frozen, seconds mode. Tests: `SettleTests.EngineSleepsOnceTheSettleRests`, `ResumesWithoutAJumpAfterAPause`, `SecondsModeGivesTheSameTrajectoryScaledInTime`.
8. Reduced motion. Tests: `SettleTests.ReducedMotionLandsOnTheChosenTargetAtOnce`.
9. Update CORE-MOTIONVALUE (Purpose names `Settle`) and CORE-TIMER as above, with `Tests:` lines; `SpecTests` green.
10. Sample: on the Gestures page, add a drag-to-open card whose opening is a staggered timeline scrubbed by `Draggable.YValue.Map(...)` and released with `Settle(new() { FlickVelocity = ..., Labels = true })` (one mid label for a half-open rest), with a `#region snippet`. Test: `GesturesPageTests` covers scrub then settle landing on each rest. Visual check on the Desktop sample (`--page gestures`): slow drags rest on the nearest point, flicks advance, re-grabbing mid-settle continues without a jump, and `--motion reduced` lands at once.

## Open questions

- Re-grab continuity: `Scrub(source)` seeks straight to `source.Value`, so a caller re-grabbing mid-settle must first `Set` its source to `timer.Progress` or the playhead jumps. Should `Scrub` gain an option (or `Settle` a documented pattern) that rebases the source for the caller?
- Should a child timer of a timeline be settleable, or should `Settle` (and `Scrub`) throw for a timer with a `Parent`?
