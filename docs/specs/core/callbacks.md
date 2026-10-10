---
id: CORE-CALLBACKS
title: Callbacks and Then()
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Timer.cs
  - MonacoLabs.Presto/TimerParams.cs
  - MonacoLabs.Presto/Core/Renderer.cs
  - MonacoLabs.Presto/Animation.cs
  - MonacoLabs.Presto/AnimationParams.cs
  - MonacoLabs.Presto/Timeline.cs
  - MonacoLabs.Presto/TimelineParams.cs
  - MonacoLabs.Presto/PrestoDefaults.cs
  - MonacoLabs.Presto/DefaultsParams.cs
types: []
tests:
  - CallbacksTests
  - PromisesTests
  - TimelineCallbacksTests
upstream:
  - src/core/render.js
  - src/timer/timer.js
  - tests/suites/callbacks.test.js
  - tests/suites/promises.test.js
depends:
  - CORE-TIMER
  - CORE-ENGINE
  - CORE-ANIMATION
  - CORE-TIMELINE
  - CORE-SCOPE
---

# Callbacks and Then()

## Purpose

Timers, animations and timelines report their progress through seven callbacks, `OnBegin`, `OnBeforeUpdate`, `OnRender`, `OnUpdate`, `OnLoop`, `OnPause` and `OnComplete`, and through `Then()`, the awaitable stand-in for upstream's promise. All of them fire from the tick (`Renderer`) or from a control on the timer (CORE-TIMER). This spec fixes when each fires, in what order, and when they are muted. Also specifies the callback properties of `TimerParams`, `AnimationParams`, `TimelineParams`, `PrestoDefaults`, `DefaultsParams` and `Timer`, and `Timer.Then` and `Timer.GetAwaiter`.

## Requirements

### Assignment

- **CORE-CALLBACKS-001** — When a timer is constructed, each callback shall come from its parameters, or else from the defaults it inherits (its parent timeline's child defaults, otherwise its engine's `Defaults`) as they are at that moment.
  - Tests: untested
- **CORE-CALLBACKS-002** — When a callback property is assigned after creation, the timer shall use the new delegate from the next tick on.
  - Tests: `CallbacksTests.OnUpdateAssignedAfterCreationFiresOnTheNextTick`, `CallbacksTests.OnCompleteReplacedAfterCreationFiresTheNewCallback`
- **CORE-CALLBACKS-003** — Every callback shall receive the timer, animation or timeline it belongs to.
  - Tests: `PromisesTests.ThenOnTimer`, `TimingsTests.DelayShouldDelayTheStart`

### OnBegin

- **CORE-CALLBACKS-004** — When the playhead first moves past the delay, the timer shall fire `OnBegin` once, and fire it again only after the playhead has returned to the delay or before.
  - Tests: `CallbacksTests.OnBeginOnAnimation`, `CallbacksTests.OnBeginOnLoopedAnimation`
- **CORE-CALLBACKS-005** — A timeline child shall fire `OnBegin` and `OnComplete` only while its parent has begun and the playhead moves forwards.
  - Tests: `CallbacksTests.OnBeginOnTimeline`, `CallbacksTests.OnCompleteOnReversedTimelineShouldCompleteAndIgnoreChildren`

### OnBeforeUpdate, OnRender and OnUpdate

- **CORE-CALLBACKS-006** — On every tick that renders the timer with its playhead past the delay, the timer shall fire `OnBeforeUpdate` before writing its tweens and `OnUpdate` after.
  - Tests: `CallbacksTests.OnBeforeUpdateOnAnimation`, `CallbacksTests.OnUpdateOnAnimation`, `CallbacksTests.OnBeforeUpdateOnTimeline`, `CallbacksTests.OnUpdateOnTimeline`
- **CORE-CALLBACKS-007** — A timeline shall fire its own `OnBeforeUpdate` and `OnUpdate` before its children render on that tick.
  - Tests: untested
- **CORE-CALLBACKS-008** — An animation shall fire `OnRender` once on a tick on which, with its playhead past 0, at least one tween wrote a value to its target, after the writes and before `OnUpdate`.
  - Tests: `CallbacksTests.OnRenderOnAnimation`, `CallbacksTests.CallbacksOrderExecutionOnAnimation`
- **CORE-CALLBACKS-009** — A timeline shall fire `OnRender` after its children have rendered, on a tick on which any child rendered.
  - Tests: `CallbacksTests.OnRenderOnTimeline`
- **CORE-CALLBACKS-010** — When an animation is created with autoplay off and a tween with an explicit from value, it shall fire `OnRender` once during construction.
  - Tests: `CallbacksTests.OnRenderWithAutoplayFalse`
- **CORE-CALLBACKS-011** (inherited) — `OnRender` shall be fixed at construction; it has no public setter.
  - Tests: untested

### OnLoop

- **CORE-CALLBACKS-012** — When the playhead of a timer without children enters a new iteration, the timer shall fire `OnLoop` once.
  - Tests: `CallbacksTests.OnLoopOnAnimation`, `TimingsTests.RaisingLoopMidPlayExtendsDurationAndPlaysTheExtraIterations`
- **CORE-CALLBACKS-013** — When a timeline enters a new iteration, it shall first render the children the loop skipped to their end, firing their callbacks, then fire its `OnLoop`, then render its children for the new iteration.
  - Tests: `CallbacksTests.OnLoopOnTimeline`, `CallbacksTests.OnBeginAndOnCompleteOnLoopedTimeline`
- **CORE-CALLBACKS-014** — A zero-duration timer, animation or timeline shall never fire `OnLoop`.
  - Tests: `CallbacksTests.AllCallbacksButOnLoopTriggerOnZeroDurationAnimation`, `CallbacksTests.AllCallbacksButOnLoopTriggerOnZeroDurationTimeline`

### OnPause

- **CORE-CALLBACKS-015** — When `Pause` pauses a playing timer, directly or through `Cancel`, `Revert`, `Complete`, `Scrub`, the `CurrentTime` setter or a replacing animation's composition, the timer shall fire `OnPause`.
  - Tests: `CallbacksTests.OnPauseOnTimer`, `CallbacksTests.OnPauseOnAnimationTweenOverride`, `CallbacksTests.OnPauseOnTimelineTweenOverride`
- **CORE-CALLBACKS-016** — The timer shall not fire `OnPause` when it is already paused, when it completes on its own, or when its engine pauses, freezes or hides.
  - Tests: `CallbacksTests.OnPauseOnTimer`, `CallbacksTests.OnPauseDoesNotFireWhenATimerCompletesOnItsOwn`, `EngineTimeTests.FreezingNeitherPausesNorFiresOnPause`

### OnComplete

- **CORE-CALLBACKS-017** — When the playhead reaches the end of the last iteration, the timer shall fire `OnComplete` once, and fire it again only after the playhead has moved back below the end.
  - Tests: `CallbacksTests.OnCompleteOnAnimation`, `CallbacksTests.OnCompleteOnLoopedAnimation`, `CallbacksTests.OnCompleteOnLoopedAlternateAnimation`
- **CORE-CALLBACKS-018** — A reversed timer shall fire `OnComplete` when its playhead reaches the end of its run, whatever direction the iterations played in.
  - Tests: `CallbacksTests.OnCompleteOnReversedAnimation`, `CallbacksTests.OnCompleteAfterCallingAnimationReverse`, `CallbacksTests.OnCompleteOnLoopedReversedAlternateAnimation`
- **CORE-CALLBACKS-019** — A timer whose `Loop` is infinite shall never fire `OnComplete`.
  - Tests: `TimingsTests.SettingLoopToInfiniteNeverCompletes`
- **CORE-CALLBACKS-020** — A timeline shall fire `OnComplete` after its children and its `OnRender`, once its time has reached its duration and every child has completed or it is running backwards.
  - Tests: `CallbacksTests.OnCompleteOnTimeline`, `CallbacksTests.OnCompleteOnTimelineWithAnimationsOfTheSameDurationAsTheTimeline`, `CallbacksTests.OnCompleteOnTimelineAfterCallingReverse`
- **CORE-CALLBACKS-021** — A zero-duration timeline child shall fire `OnComplete` when the parent, having begun, plays forwards past it, and again when the parent plays backwards over it after it completed.
  - Tests: `TimelineCallbacksTests.CallCallbacks`, `TimelineCallbacksTests.CallCallbacksMultipleTimesViaSeek`, `TimelineCallbacksTests.SeekDoesNotRefireACallItDoesNotCross`

### Order and muting

- **CORE-CALLBACKS-022** — Within one tick of an animation, callbacks shall fire in the order `OnBegin`, `OnLoop`, `OnBeforeUpdate`, `OnRender`, `OnUpdate`, `OnComplete`.
  - Tests: `CallbacksTests.CallbacksOrderExecutionOnAnimation`
- **CORE-CALLBACKS-023** — When a zero-duration timer, animation or timeline plays, it shall fire `OnBegin`, `OnBeforeUpdate`, `OnRender` (when it wrote), `OnUpdate` and `OnComplete` once each, synchronously, in the order of CORE-CALLBACKS-022 for a timer or animation; a timeline fires `OnUpdate` before its `OnRender` (CORE-CALLBACKS-007, -009).
  - Tests: `CallbacksTests.AllCallbacksButOnLoopTriggerOnZeroDurationAnimation`, `CallbacksTests.AllCallbacksButOnLoopTriggerOnZeroDurationTimeline`
- **CORE-CALLBACKS-024** — While a render is muted (`Seek` with `muteCallbacks`, `Complete(true)`, `Reset`, `Revert`, and the initial render of `Init`), no tick callback shall fire, but `Began` and `Completed` shall still update.
  - Tests: untested
- **CORE-CALLBACKS-025** — A callback shall be able to pause, seek, reverse or alternate its own timer, and create other timers, from inside the tick.
  - Tests: `TimingsTests.DelayShouldDelayTheStart`, `ControlsTests.AlternateTheDirectionOfAnAnimation`, `EngineTests.InitialOffsetPositionShouldBeProperlyCalculatedOnColdStart`
- **CORE-CALLBACKS-026** — Callbacks shall run synchronously on the thread that ticks the timer's engine.
  - Tests: untested

### Then and await

- **CORE-CALLBACKS-027** — When `Then(callback)` is called on a timer that has not completed, it shall return a task that completes with the timer the next time the timer fires `OnComplete` unmuted, invoking `callback` synchronously just before.
  - Tests: `PromisesTests.ThenOnTimer`, `PromisesTests.ThenOnAnimation`, `PromisesTests.ThenOnTimeline`, `EngineHiddenTests.AnAnimationStartedBeforeHidingCompletesAfterShowing`
- **CORE-CALLBACKS-028** — When `Then(callback)` is called on a completed timer, it shall invoke `callback` synchronously and return an already completed task.
  - Tests: `PromisesTests.ThenOnAnAlreadyCompletedTimerResolvesImmediately`
- **CORE-CALLBACKS-029** — The `Then` callbacks shall run after `OnComplete`.
  - Tests: untested
- **CORE-CALLBACKS-030** (inherited) — When `Then` is called several times before completion, every call shall return the same task and every callback shall run, in registration order.
  - Tests: untested
- **CORE-CALLBACKS-031** — Once a timer has resolved, a `Then` after `Restart` shall return a new task that completes at the next completion.
  - Tests: `PromisesTests.ARestartedTimerResolvesANewThen`
- **CORE-CALLBACKS-032** — The task shall run its continuations asynchronously, so an `await` never resumes inside the tick.
  - Tests: untested
- **CORE-CALLBACKS-033** — Awaiting a timer, animation or timeline directly shall behave as awaiting `Then()`.
  - Tests: `PromisesTests.AwaitATimer`, `PromisesTests.AwaitAnAnimation`
- **CORE-CALLBACKS-034** — A completion reached with callbacks muted, a `Cancel` and a `Revert` shall leave a pending `Then` task incomplete.
  - Tests: untested
- **CORE-CALLBACKS-035** — On the default frame pump, `Then` shall resolve without any manual ticking.
  - Tests: `PromisesTests.ThenResolvesOnTheDefaultFramePump`

### Defaults

- **CORE-CALLBACKS-036** — When a callback is set neither in the parameters nor in the inherited defaults, the timer shall fire nothing for it, as anime.js's `noop` default does.
  - Tests: untested
- **CORE-CALLBACKS-037** — When `Then` is called without a callback, it shall only return the task, as anime.js's `then(callback = noop)` does.
  - Tests: `PromisesTests.AwaitATimer`

## Invariants

- `OnBegin` and `OnComplete` alternate with the playhead crossing the start and the end; neither fires twice for one crossing.
- `OnBeforeUpdate` and `OnUpdate` fire in pairs.
- At most one `Then` task is pending per timer, and it is cleared when it completes.

## Edge cases

- `Complete()` on a playing timer fires `OnComplete` and then `OnPause` (CORE-TIMER).
- Setting `CurrentTime` on a playing timer fires `OnPause`, because the setter pauses, seeks and resumes.
- `OnRender` does not fire on a frame that writes only at time 0 or when every tween was skipped.
- A timer canceled by its engine after completing still reports `Completed`, so a later `Then` resolves at once.
- An exception from a callback escapes the tick; one from a `Then` callback leaves the pending task incomplete for good, since it was cleared before the callbacks ran.
- A timeline's `OnUpdate` sees its children in last frame's state, since they render after it.

## Threading and lifetime

- Callbacks run on the engine's tick thread (CORE-THREADING). `Then` continuations are queued, never inline: awaiting from a thread with a synchronization context resumes there, otherwise on the thread pool.
- A pending `Then` task holds the timer only through the timer's own field; a timer dropped while pending leaves its awaiters waiting forever.

## Reduced motion

- Not affected: reduced forms fire the same callbacks; a form collapsed by `ReduceAll` completes at once and fires them synchronously (CORE-REDUCED).

## Allocation

- Firing callbacks allocates nothing beyond what the delegates do.
- `Then` allocates one `TaskCompletionSource` per pending completion, and `Task.FromResult` for a completed timer.

## Deviations from anime.js

- `Then` returns a `Task<Timer>` with asynchronous continuations, and `GetAwaiter` makes timers awaitable, standing in for upstream's thenable so C# callers can `await` (see "Every timer is awaitable" and "`Then()` completes asynchronously").
- Several `Then` calls before completion share one task and all run their callbacks; upstream overwrites its resolver on each call, so only the last promise resolves. This follows from keeping one pending task per timer; no decision records it.
- Callbacks are null when unset, not upstream's `noop`, since a nullable delegate needs no placeholder; nothing fires either way.

## Decisions

- **Every timer is awaitable.** Every `Timer` has `GetAwaiter` as well as `Then()`, and `Animation` narrows both to itself. Reason: anime.js `.then()` parity plus idiomatic `await`.
- **`Then()` completes asynchronously.** The task is created with `RunContinuationsAsynchronously`, so an `await` never resumes inside the tick, and awaiting a non-UI engine's timer resumes on the thread pool. Reason: not recorded; implied, continuations must not run inside a tick.

## Non-goals

- No events or multicast subscription beyond the delegate itself (a caller can combine delegates).
- No cancellation of a pending `Then`.

## Open items

- A `Then` task never completes after `Cancel`, `Revert` or a muted `Complete(true)`, so `await` on a canceled animation hangs (upstream parity, untested).
- An exception from a `Then` callback leaves the task incomplete and later `Then` calls return a new task that the already-completed timer resolves at once; untested.
- Untested: callbacks from inherited defaults, timeline `OnUpdate` before children, muted renders still updating `Began`/`Completed`, multiple `Then` callbacks, asynchronous continuations.
