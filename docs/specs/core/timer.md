---
id: CORE-TIMER
title: Timer timing and playback controls
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Timer.cs
  - MonacoLabs.Presto/TimerParams.cs
  - MonacoLabs.Presto/LoopCount.cs
  - MonacoLabs.Presto/Core/Renderer.cs
  - MonacoLabs.Presto/Core/Helpers.cs
types:
  - Timer
  - TimerParams
  - LoopCount
tests:
  - TimingsTests
  - ControlsTests
  - DirectionsTests
  - SecondsTests
  - LoopCountTests
  - ParametersTests
  - IntrospectionTests
  - CallbacksTests
  - EngineTimeTests
  - MotionValueDynamicsTests
  - SettleTests
upstream:
  - src/timer/timer.js
  - src/core/render.js
  - tests/suites/timings.test.js
  - tests/suites/controls.test.js
  - tests/suites/directions.test.js
  - tests/suites/seconds.test.js
depends:
  - CORE-ENGINE
  - CORE-THREADING
  - CORE-CALLBACKS
  - CORE-SCOPE
  - CORE-COMPOSITION
  - CORE-TIMELINE
  - CORE-MOTIONVALUE
  - CORE-DIAGNOSTICS
---

# Timer timing and playback controls

## Purpose

`Timer` is the delay/duration/loop/alternate/reverse state machine that `Animation` and `Timeline` extend, ported from upstream `timer.js` and `render.js`'s `tick()`. A plain timer animates nothing; it moves a playhead and fires callbacks (CORE-CALLBACKS) on the engine it is bound to (CORE-ENGINE, CORE-THREADING). This spec covers its parameters, playhead state and the playback controls. Also specifies `Presto.CreateTimer`.

## Requirements

### Creation

- **CORE-TIMER-001** — When `Presto.CreateTimer` creates a timer, it shall apply its frame and playback rates, render it at time 0 with callbacks muted, leave it paused, and resume it when autoplay is on.
  - Tests: `ControlsTests.ResetAPlayingTimer`, `ControlsTests.ResetACanceledTimer`
- **CORE-TIMER-002** — A timer's `Delay`, `LoopDelay`, `IsAlternating`, `Priority` and `Engine` shall reflect the parameters it was created with.
  - Tests: `IntrospectionTests.TimerGettersMatchTheParameters`
- **CORE-TIMER-003** — When a `TimerParams` value is null, the timer shall take it, once at construction, from its parent timeline's child defaults, or else from its engine's `Defaults`.
  - Tests: untested
- **CORE-TIMER-004** — When `Duration` is null, the timer shall have an infinite `IterationDuration` and a `Duration` clamped to 1e12; `Defaults.Duration` shall not apply.
  - Tests: untested
- **CORE-TIMER-005** (inherited) — When `Delay` is null and the default delay is function-based, the timer shall use a delay of 0.
  - Tests: untested
- **CORE-TIMER-006** — The timer's `Duration` shall be `(IterationDuration + LoopDelay) × IterationCount − LoopDelay`, clamped to ±1e12, and 1e-11 when that is 0 or NaN.
  - Tests: `TimingsTests.RaisingLoopMidPlayExtendsDurationAndPlaysTheExtraIterations`, `ControlsTests.StretchALoopedTimer`, `ParametersTests.ZeroDurationTimerWithInfiniteLoop`
- **CORE-TIMER-007** — `IterationCount` shall be `Loop + 1`, and +Infinity when `Loop` is infinite.
  - Tests: `TimingsTests.RaisingLoopMidPlayExtendsDurationAndPlaysTheExtraIterations`, `TimingsTests.SettingLoopToInfiniteNeverCompletes`
- **CORE-TIMER-008** — `LoopCount` shall be infinite for `LoopCount.Infinite`, `true` and any negative count, and finite for `false` (0) and non-negative counts.
  - Tests: `LoopCountTests.IsInfiniteFollowsTheImplicitConversion`
- **CORE-TIMER-009** — When `Id` is null, the timer's `Id` shall be the next value of a process-wide counter, as a string.
  - Tests: untested
- **CORE-TIMER-010** — A root timer's `Offset` shall be the engine's last tick time at creation, measured from the engine's start and expressed in the current time unit.
  - Tests: `EngineTests.InitialOffsetPositionShouldBeProperlyCalculatedOnColdStart`, `SecondsTests.TimersOffsetTimeShouldBeProperlyScaledWhenNotControlledByATimeline`
- **CORE-TIMER-011** — A timer added to a timeline shall never autoplay, whatever `Autoplay` says.
  - Tests: untested
- **CORE-TIMER-068** — When `Duration` is null, the timer shall reach its end only after 1e12 time units, so in practice it never completes or fires `OnComplete`.
  - Tests: untested

### Playhead

- **CORE-TIMER-012** — `CurrentTime` shall report the playhead across every iteration, rounded to the engine `Precision` and clamped to `[−Delay, Duration]`.
  - Tests: `TimingsTests.AnimationIterationCurrentTimeShouldBeNegativeWhenADelayIsDefined`, `TimingsTests.GetAndSetCurrentTimeOnALoopedTimerWithPreDefinedIterations`, `EngineTests.DefaultPrecisionShouldBe4`
- **CORE-TIMER-013** — Setting `CurrentTime` shall seek there and leave the timer playing or paused as it was.
  - Tests: `TimingsTests.GetAndSetCurrentTimeOnALoopedTimerWithPreDefinedIterations`
- **CORE-TIMER-014** — `Progress` shall be `CurrentTime / Duration` rounded to 10 decimals and clamped to `[0, 1]`, and setting it shall seek to `Duration × value`.
  - Tests: `ControlsTests.SeekATimer`
- **CORE-TIMER-015** — `IterationProgress` and `IterationCurrentTime` shall report the position inside the current iteration, and setting either shall seek within the current iteration, measured as `(IterationDuration + LoopDelay) × CurrentIteration` plus the value.
  - Tests: `TimingsTests.GetAndSetIterationProgressOnNonLoopedTimer`, `TimingsTests.GetAndSetIterationProgressOnALoopedTimerWithPreDefinedIterations`, `TimingsTests.GetAndSetIterationCurrentTimeOnALoopedTimerWithPreDefinedIterations`, `TimingsTests.IterationSettersStepOverTheLoopDelay`
- **CORE-TIMER-016** — Setting `CurrentIteration` shall clamp it to `[0, IterationCount − 1]` and seek to `(IterationDuration + LoopDelay) ×` that index, the iteration's start.
  - Tests: `TimingsTests.GetAndSetCurrentIterationOnALoopedTimerWithPreDefinedIterations`, `TimingsTests.IterationSettersStepOverTheLoopDelay`
- **CORE-TIMER-017** — `IterationTime` shall report the rendered position inside the iteration after `Reversed`, alternation and the playback ease are applied.
  - Tests: `TimingsTests.IterationTimeReflectsPlaybackEaseAndReversed`
- **CORE-TIMER-018** — When the playhead moves past the delay, `Began` shall become true, and when it returns to the delay or before, `Began` shall become false.
  - Tests: `ControlsTests.SeekATimer`, `CallbacksTests.OnBeginOnAnimation`
- **CORE-TIMER-019** — When the playhead reaches the end of the last iteration, the timer shall set `Completed` and `Paused`; when it is moved back below `Duration`, `Completed` shall become false.
  - Tests: `ControlsTests.SeekATimer`, `CallbacksTests.OnCompleteOnAnimation`
- **CORE-TIMER-020** — `Backwards` shall be true while the playhead moves towards 0, combining `Reversed`, the alternating iteration and, for a child, its parent's direction.
  - Tests: untested
- **CORE-TIMER-021** — `ProgressValue` shall be a `MotionValue` on the timer's engine, created on first read and assigned `Progress` on every render.
  - Tests: `MotionValueDynamicsTests.ProgressValueTracksAPlayingTimer`

### Iterations and direction

- **CORE-TIMER-022** — When `Loop` is set, each iteration after the first shall start `LoopDelay` after the previous one ends, and the `Delay` shall not repeat.
  - Tests: `TimingsTests.SpecifiedTimingsParametersOnAnimation`, `CallbacksTests.OnLoopOnAnimation`
- **CORE-TIMER-023** — When `Reversed` is true, the timer shall play each iteration from its end towards 0.
  - Tests: `DirectionsTests.DirectionReverseShouldUpdateFrom1To0`
- **CORE-TIMER-024** — When `Alternate` is true, the timer shall play every odd iteration in the opposite direction to the even ones.
  - Tests: `DirectionsTests.DirectionAlternateShouldUpdateFrom0To1Then1To0WhenALoopParameterIsSpecified`, `DirectionsTests.DirectionAlternateReverseShouldUpdateFrom1To0Then0To1WhenALoopParameterIsSpecified`
- **CORE-TIMER-025** — When `Alternate` is true and the timer does not loop, it shall play its one iteration in its normal direction.
  - Tests: `DirectionsTests.DirectionAlternateShouldUpdateFrom0To1WhenNoLoopParameterIsSpecified`, `DirectionsTests.DirectionAlternateReverseShouldUpdateFrom1To0WhenNoLoopParameterIsSpecified`
- **CORE-TIMER-026** — While `Loop` is infinite, the timer shall never complete, wrapping its start time by `Duration` whenever the playhead reaches it.
  - Tests: `TimingsTests.SettingLoopToInfiniteNeverCompletes`, `DirectionsTests.InfiniteLoopWithDirectionAlternateShouldUpdateFrom0To1Then1To0`, `DirectionsTests.InfiniteLoopWithDirectionAlternateReverseShouldUpdateFrom1To0Then0To1`
- **CORE-TIMER-027** — When `Delay` is negative, the timer shall render the position that far into its run as soon as it is seeked to 0.
  - Tests: `TimingsTests.NegativeDelayOnAlternateLoopedAnimationsShouldRenderTheValueInAdvance`
- **CORE-TIMER-028** — When `Loop` is raised mid-play, the timer shall recompute `IterationCount` and `Duration` in place and play the extra iterations.
  - Tests: `TimingsTests.RaisingLoopMidPlayExtendsDurationAndPlaysTheExtraIterations`
- **CORE-TIMER-029** (inherited) — When `Loop` is lowered below the current iteration of a running timer, it shall complete on the next tick.
  - Tests: `TimingsTests.LoweringLoopBelowTheCurrentIterationCompletesOnTheNextTick`
- **CORE-TIMER-030** (inherited) — When `Loop` is lowered on a paused timer whose playhead is past the new `Duration`, the timer shall seek just short of the end with callbacks muted, so playing it renders the end state.
  - Tests: `TimingsTests.LoweringLoopOnAPausedTimerRendersTheEndStateOnceItPlays`
- **CORE-TIMER-031** (inherited) — When `Loop` is set on a timeline child, the parent timeline shall grow to fit a child that now runs past its end, and keep its duration when the child gets shorter.
  - Tests: untested
- **CORE-TIMER-069** — When `Loop` is lowered, the timer shall recompute `IterationCount` and `Duration` in place, as it does when `Loop` is raised.
  - Tests: `TimingsTests.LoweringLoopBelowTheCurrentIterationCompletesOnTheNextTick`

### Rates

- **CORE-TIMER-032** — `PlaybackRate` shall multiply how fast the timer's own time advances.
  - Tests: `ParametersTests.PlaybackRateOnTimer`
- **CORE-TIMER-033** — When `Speed` is set, the timer shall clamp it to at least 1e-11 and rebase its start time so the playhead does not jump.
  - Tests: `ClockTests.SpeedClampsToMinValue`
- **CORE-TIMER-034** — `FrameRate` shall cap how often the timer renders without changing how fast its time advances; zero or less shall mean uncapped.
  - Tests: `ParametersTests.FrameRateThrottlesUpdates`, `ParametersTests.FrameRateZeroOrNegativeRendersEveryFrame`
- **CORE-TIMER-035** — When a timer runs in seconds mode, its durations, delays and times shall be read in seconds.
  - Tests: `SecondsTests.ATimerPlaysInSeconds`
- **CORE-TIMER-070** — When `PlaybackRate` or `Speed` is 0 or negative, the timer shall clamp it to 1e-11, so it never stops or runs backwards; reversing is done with `Reverse` or `Alternate`.
  - Tests: `ClockTests.SpeedClampsToMinValue`
- **CORE-TIMER-071** — When a timer's `FrameRate` or `Fps` is set above the engine's `Defaults.FrameRate`, that default shall rise to it (CORE-ENGINE-049).
  - Tests: `ClockTests.FpsHigherThanDefaultFrameRateRaisesDefault`

### Controls

- **CORE-TIMER-036** — When `Pause` is called on a playing timer, it shall set `Paused` and fire `OnPause`; on a paused timer it shall do nothing.
  - Tests: `CallbacksTests.OnPauseOnTimer`
- **CORE-TIMER-037** — When `Resume` is called on a paused timer with a duration, a delay or children, it shall clear `Paused`, link into its engine's child list if not linked, rebase its start time to carry on from `CurrentTime` backdated by 12 ms, and wake the engine.
  - Tests: `EngineTimeTests.ATimerCreatedWhileFrozenStartsFromFrozenTime`, `EnginesTests.ResumingAfterTheBlockEndsReRegistersWithTheEngineTheTimerWasCreatedUnder`, `IntrospectionTests.TimerGettersMatchTheParameters`
- **CORE-TIMER-038** — When `Resume` is called on a paused zero-duration timer without a delay or children, it shall render to its end synchronously, without linking into the engine; with a delay it plays through CORE-TIMER-037 and lands when the delay ends.
  - Tests: `CallbacksTests.AllCallbacksButOnLoopTriggerOnZeroDurationAnimation`, `ParametersTests.ZeroDurationAnimation`, `ParametersTests.ZeroDurationAnimationWaitsOutItsDelay`
- **CORE-TIMER-039** — `Seek(time)` shall revive a canceled timer, clear `Completed`, render at `time + Delay` and leave the timer playing or paused as it was.
  - Tests: `ControlsTests.SeekATimer`, `ControlsTests.ResetAPlayingTimer`
- **CORE-TIMER-040** — When `Seek` is called with `muteCallbacks`, it shall fire no callback, including those of iterations skipped over; with `internalRender`, it shall update timing state without writing to targets.
  - Tests: untested
- **CORE-TIMER-041** — `Alternate()` shall flip the playback direction in place: a finite timer seeks to the mirrored position, an infinite one mirrors its iteration progress, and both rebase their start time; it shall not resume a paused timer.
  - Tests: `ControlsTests.AlternateTheDirectionOfAnAnimation`, `ControlsTests.AlternateTheDirectionOfAReversedAnimation`, `ControlsTests.AlternateTheDirectionOfALoopedAnimation`, `ControlsTests.AlternateTheDirectionOfALoopedAlternateAnimation`, `ControlsTests.AlternateTheDirectionOfALoopedAlternateReversedAnimationWithOddIterationCount`, `DirectionsTests.ManuallyReversedInfiniteAnimationShouldUpdate`
- **CORE-TIMER-042** — When `Alternate()` is called on an alternating timer with an even iteration count, its `Reversed` flag shall stay as it was and only the mirrored seek shall turn it round.
  - Tests: `ControlsTests.AlternateTheDirectionOfALoopedAlternateReversedAnimationWithEvenIterationCount`
- **CORE-TIMER-043** — `Play` shall flip a reversed timer forwards with `Alternate()` and then `Resume`.
  - Tests: `ControlsTests.ResetACanceledTimer`
- **CORE-TIMER-044** — `Reverse` shall flip a forward timer with `Alternate()` and then `Resume`.
  - Tests: `CallbacksTests.OnCompleteAfterCallingAnimationReverse`
- **CORE-TIMER-045** — Setting `Reversed` shall call `Reverse` for true and `Play` for false, so it also resumes the timer.
  - Tests: untested
- **CORE-TIMER-046** — `Reset(softReset)` shall revive a canceled timer, restore its initial direction, render it at 0 with callbacks muted (without writing targets when soft), and leave it and its children paused, not began and not completed.
  - Tests: `ControlsTests.ResetAPlayingTimer`, `ControlsTests.ResetACanceledTimer`
- **CORE-TIMER-047** — `Restart` shall `Reset` and then `Resume`.
  - Tests: `PromisesTests.ARestartedTimerResolvesANewThen`
- **CORE-TIMER-048** — `Cancel` shall end any scrub, cancel the children in reverse order or detach the tweens from their composition siblings, set `Canceled`, and `Pause`, leaving animated values where they stand.
  - Tests: `ControlsTests.CancelATimer`, `ControlsTests.CancelAnAnimation`
- **CORE-TIMER-049** — When `Cancel` is called on an already canceled timer, it shall leave composition untouched.
  - Tests: `TweenListIntegrityTests.CancelingAnAdditiveAnimationTwiceKeepsTheListsIntactForTheNextOne`, `TweenListIntegrityTests.RemovingATargetAfterRepeatedCancelsEmptiesTheLists`
- **CORE-TIMER-050** — Setting `Canceled` to true shall `Cancel`, and setting it to false shall soft-`Reset` and `Play`.
  - Tests: `ControlsTests.GetAndSetCanceledOnATimer`
- **CORE-TIMER-051** — `Complete(muteCallbacks)` shall seek to `Duration` and then `Cancel`.
  - Tests: `ControlsTests.CompleteATimer`, `ControlsTests.CompleteAnAnimation`
- **CORE-TIMER-052** — `Revert` shall render at 0 with callbacks muted, run the timer's revert hook, and `Cancel`.
  - Tests: `ControlsTests.RevertATimer`, `ControlsTests.RevertAnAnimation`
- **CORE-TIMER-053** — `Stretch(d)` shall set `Duration` to `d` and scale `IterationDuration`, `Delay` and `LoopDelay` by the same factor, leaving `CurrentTime` where it was.
  - Tests: `ControlsTests.StretchATimer`, `ControlsTests.StretchALoopedTimer`, `SecondsTests.StretchALoopedTimerWithZeroLoops`, `SecondsTests.StretchALoopedTimer`
- **CORE-TIMER-054** — When `Stretch` is given 0 or less, the timer shall become a setter with `Duration` and `IterationDuration` of 1e-11.
  - Tests: `ControlsTests.StretchATimer`, `SecondsTests.StretchALoopedTimerWithZeroLoops`
- **CORE-TIMER-055** — `Stretch` shall scale a timeline child's `Offset` and leave a root timer's `Offset` unchanged (see "Stretch keeps a root timer's offset").
  - Tests: `AnimatablesTests.RetargetingAZeroDurationPropertyLongAfterTheEngineStartedStillEases`
- **CORE-TIMER-056** — `Scrub(progress)` shall pause the timer, seek it to `progress × Duration`, and seek again whenever the value changes, until `Scrub(null)`, `Cancel` or `Revert`.
  - Tests: `MotionValueDynamicsTests.AScrubbedAnimationSeeksToValueTimesDuration`, `MotionValueDynamicsTests.AScrubbedTimelineSeeksToValueTimesDuration`, `MotionValueDynamicsTests.ScrubNullAndRevertEndTheScrub`
- **CORE-TIMER-057** — When a timer completes on its own, the engine shall unlink it on its next tick and mark it canceled, without firing `OnPause`.
  - Tests: `CallbacksTests.OnPauseDoesNotFireWhenATimerCompletesOnItsOwn`, `LeaksTests.EngineShouldNotContainAnyActiveTickable`
- **CORE-TIMER-058** — `IsScheduled` shall be true while the timer is linked into its engine's child list and false otherwise.
  - Tests: `IntrospectionTests.TimerGettersMatchTheParameters`
- **CORE-TIMER-072** — After `Complete`, the timer shall report `Completed`, `Paused` and `Canceled`, with `CurrentTime` at `Duration`.
  - Tests: untested
- **CORE-TIMER-073** — `Seek`, `Play`, `Resume`, `Reverse`, `Restart`, `Pause`, `Cancel`, `Revert` and `Complete` shall end a running `Settle` (CORE-MOTIONVALUE).
  - Tests: `SettleTests.APlayheadControlEndsTheSettleWithTheCurrentProgress`
- **CORE-TIMER-074** — When `Complete` is called, the timer shall fire `OnComplete` unless callbacks are muted, and shall not fire `OnPause`, whether it was playing or paused.
  - Tests: `CallbacksTests.CompleteFiresOnCompleteAndNotOnPause`

### Defaults

- **CORE-TIMER-059** — When `Delay` is not set and the engine defaults are unchanged, the timer shall have a delay of 0.
  - Tests: untested
- **CORE-TIMER-060** — When `LoopDelay` is not set and the engine defaults are unchanged, the timer shall have a loop delay of 0.
  - Tests: untested
- **CORE-TIMER-061** — When `Loop` is not set and the engine defaults are unchanged, the timer shall play one iteration (`Loop` 0).
  - Tests: untested
- **CORE-TIMER-062** — When `Reversed` is not set and the engine defaults are unchanged, the timer shall play forwards.
  - Tests: untested
- **CORE-TIMER-063** — When `Alternate` is not set and the engine defaults are unchanged, the timer shall not alternate.
  - Tests: untested
- **CORE-TIMER-064** — When `Autoplay` is not set and the engine defaults are unchanged, a root timer shall start playing on creation.
  - Tests: `ParametersTests.PlaybackRateOnTimer`
- **CORE-TIMER-065** — When `FrameRate` is not set and the engine defaults are unchanged, the timer shall render at up to 240 fps.
  - Tests: `ClockTests.FpsHigherThanDefaultFrameRateRaisesDefault`
- **CORE-TIMER-066** — When `PlaybackRate` is not set and the engine defaults are unchanged, the timer shall advance at a rate of 1.
  - Tests: `ParametersTests.PlaybackRateOnTimer`
- **CORE-TIMER-067** — When `Priority` is not set, the timer shall have priority 1.
  - Tests: untested

## Invariants

- `Duration` is always finite and at least 1e-11; `IterationDuration` is +Infinity only for a timer created with a null duration.
- `CurrentTime` stays within `[−Delay, Duration]`, `Progress` and `IterationProgress` within `[0, 1]`.
- `Completed` implies `Paused` until the timer is seeked, reset or resumed.
- A canceled timer is never linked into its engine.

## Edge cases

- `Complete()` on a playing timer fires `OnComplete` only, though it ends paused and canceled (see "`Complete()` does not fire `OnPause`").
- `Scrub` and `Cancel` on a playing timer fire `OnPause`.
- An infinite timer's `Progress` is `CurrentTime / 1e12`, effectively 0.
- A time jump bigger than the tick threshold (200 ms, 0.2 s in seconds mode) forces every tween to re-render.
- `Reset` on a timer that `Reverse` flipped calls `Play` internally before rendering, then leaves it paused.

## Threading and lifetime

- Every control runs on the thread that ticks the timer's engine (CORE-THREADING); `Reset`, `Pause`, `Resume` and `Seek` assert it in Debug.
- A playing timer is held by its engine's child list; a paused, completed or canceled one is held only by its owner and a scope that registered it.

## Reduced motion

- Not affected: a plain timer animates nothing, so it has no reduced form; `ReduceAll` leaves it alone (CORE-REDUCED).

## Allocation

- Ticking a timer allocates nothing; pinned by `AllocationTests.LoopingTimerTicksWithoutAllocating`.
- `ProgressValue`, `Scrub` and `Then` allocate once when first used.

## Provenance

- The iteration setters include `LoopDelay` in their step, where upstream leaves it out (see "The iteration setters step over the loop delay").
- `Autoplay` takes no scroll observer; `ScrollObserver.Link` stands in for `autoplay: onScroll()` (see "`Autoplay` is a `bool?`; a scroll observer links the timer instead").
- `Stretch` leaves a root timer's offset unchanged: scaling the creation-time offset lost precision when stretching a setter (see "Stretch keeps a root timer's offset").
- A repeat `Cancel` leaves composition alone: upstream's double removal corrupted the sibling lists (see CORE-COMPOSITION, "Sibling removal is idempotent").
- A zero-duration timer with a delay waits it out on `Resume`, where upstream renders it once inside the delay and never links it into the engine (see CORE-ANIMATION, "A zero-duration animation honors its delay").
- `Complete()` never fires `OnPause`, where upstream's seek resumes a playing timer and its cancel then pauses it, firing `onPause` after `onComplete` (see "`Complete()` does not fire `OnPause`").
- `FrameRate` of zero or less is uncapped, where upstream clamps it to a rate that never renders, because there was otherwise no way to ask for every frame.
- A function-based default delay gives a plain timer a delay of 0; `Duration` and `Delay` are numbers, not function values.
- `Loop` is settable after creation, `Scrub`, `ProgressValue`, `IsScheduled` and the introspection getters have no upstream counterpart.
- `Revert` runs an internal hook instead of reverting a linked scroll observer passed as `autoplay`.
- Time is `double` milliseconds with seconds as an engine scale, like upstream's `timeUnit` (project-wide decision "Time is double milliseconds", ARCHITECTURE.md §4).

## Decisions

- **The iteration setters step over the loop delay.** `CurrentIteration`, `IterationCurrentTime` and `IterationProgress` seek in steps of `IterationDuration + LoopDelay`, the period the renderer counts iterations by. Reason: upstream steps by `IterationDuration` alone, so with a loop delay `CurrentIteration = 2` landed inside an earlier iteration; without one the result is unchanged. Rejected: upstream's step.
- **`Autoplay` is a `bool?`; a scroll observer links the timer instead.** Upstream's `autoplay: onScroll(...)` is `ScrollObserver.Link(timer)` (BRIDGE-SCROLLOBSERVER), which pauses the timer and ties their lifetimes the same way. Reason: the observer is a bridge type the core cannot name, and `Link` gives the same behavior without a core driver interface and an `Autoplay` union across `TimerParams`, `DefaultsParams` and `PrestoDefaults` that only scroll would use. Rejected: an `ITimerDriver` interface with `Autoplay` accepting a driver.
- **`tick()` is ported structurally, first.** `Renderer.Tick` keeps the structure of upstream `render.js` `tick()`, and was ported and tested (timings, controls, callbacks, directions) before tweens existed. Reason: it is the subtlest, highest-value part of the timing model, and a structural port lets the upstream suites be ported one-to-one.
- **`LoopCount` converts from `int` and `bool`.** `Loop = true` is infinite, `false` is 0. Reason: one property then holds both a count and "forever", and `Loop = true` reads as looping without a sentinel number.
- **Stretch keeps a root timer's offset.** For a root timer `Stretch` leaves the offset alone and rebuilds the tweens' absolute times from scaled local times; timeline children keep upstream's arithmetic. Reason: multiplying the creation-time offset by ~10¹⁴ when stretching from a setter's 1e-11 duration lost precision, so Draggable release tweens started late or never; upstream has the same arithmetic.
- **Unset timer parameters are the plain choices.** With nothing set a timer starts at once, plays one iteration forwards at rate 1 with no delays or alternation, renders at up to 240 fps and has priority 1 (CORE-TIMER-059 to 067). Reason: `Presto.CreateTimer` or `Presto.Animate` with only a duration then does what the call reads as; 240 fps is above common display refresh rates, so the default cap never drops a frame the display could show; priority 1 leaves 0 for a timer that must tick before the default ones.
- **Introspection is read-only and allocation-free.** The introspection surface is public getters only; `Children` and `Tweens` are struct enumerators over the linked lists; `IsAlternating` names the flag because `Alternate` is the method; `TweenSnapshot` methods allocate and are for tools only. Reason: inspection must not allocate on enumeration nor add mutation surface.
- **Sibling removal is idempotent.** See CORE-COMPOSITION.
- **`Complete()` does not fire `OnPause`.** `Complete` pauses the timer silently before seeking to the end, so neither the seek nor the cancel that follows fires `OnPause`; a plain `Pause` or `Cancel` still does. Reason: a completion is not a pause, and a handler that resumes or saves state on pause would misfire. Rejected: upstream's order (`OnComplete`, then `OnPause`).
- **`Presto.Delay` is the idiomatic wait; awaiting a timer stays supported.** `Then` and `GetAwaiter` still complete when the timer does, but a hold between animations is written `await Presto.Delay(ms)` (CORE-PRESTO). Reason: `Task.Delay` ignores engine time, so a hold would desync under DevTools dilation and never complete in headless tests driven by `Frames.Advance`, and a named wait says what it is. Rejected: keeping `await Presto.CreateTimer(...)` as the documented wait, which reads as a workaround.

## Non-goals

- No public constructor: timers come from `Presto.CreateTimer`, `Presto.Animate` or a timeline.
- No function-based `Duration` or `Delay` on a plain timer.

## Open items

- Untested: the defaults fallback at construction, a null `Duration`, function-based default delay, generated ids, `Backwards`, child-never-autoplays, the `Reversed` setter, `Seek` with `muteCallbacks`/`internalRender`, and `Loop` set on a timeline child.
