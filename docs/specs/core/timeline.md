---
id: CORE-TIMELINE
title: Timeline
package: Monaco.Presto
namespaces:
  - Monaco.Presto
sources:
  - Monaco.Presto/Timeline.cs
  - Monaco.Presto/TimelineParams.cs
  - Monaco.Presto/TimelinePosition.cs
  - Monaco.Presto/TimerChildren.cs
  - Monaco.Presto/Presto.cs
types:
  - Timeline
  - TimelineParams
  - TimelinePosition
  - TimerChildren
tests:
  - TimelinesTests
  - TimelineCallbacksTests
  - TimelineCompositionTests
  - ControlsTests
  - StaggerTests
  - IntrospectionTests
  - CallbacksTests
upstream:
  - src/timeline/timeline.js
  - src/timeline/position.js
  - tests/suites/timelines.test.js
depends:
  - CORE-TIMER
  - CORE-ANIMATION
  - CORE-COMPOSITION
  - CORE-STAGGER
  - CORE-SCOPE
  - CORE-BUILDER
---

# Timeline

## Purpose

A `Timeline` is a timer (CORE-TIMER) whose children are animations and plain timers placed at positions on a shared playhead. Its duration starts at 0 and grows to fit its children. Children inherit the timeline's `Defaults`, and with composition on, each child starts from the values the earlier children leave behind. `Stagger` positions (CORE-STAGGER) add one child per target. Also specifies `Timer.Children` and `Presto.CreateTimeline`.

## Requirements

### Creation and duration

- **CORE-TIMELINE-001** — When `Presto.CreateTimeline` is called, the timeline shall start with a duration of 0, ignore `TimerParams.Duration`, initialize, and raise the engine's `TimerCreated`.
  - Tests: `CallbacksTests.OnCompleteOnTimeline`, `HostHookTests.TimerCreatedFiresOncePerRootAndPerTimelineChild`
- **CORE-TIMELINE-002** — When a child is added, the timeline's `IterationDuration` shall grow to the latest child end (offset plus delay plus duration), and its `Duration` shall be the iterations with their loop delays, or `Constants.MinValue` when that is 0.
  - Tests: `TimelinesTests.BasicTimePositions`, `TimelinesTests.DurationIsTheSumOfEveryChildEndTimeTimesTheIterationCount`, `TimelineCallbacksTests.CallCallbacksOnAZeroDurationTimeline`
- **CORE-TIMELINE-003** — When a child's loop count is raised, the timeline shall grow to fit it; when it is lowered, the timeline shall keep its duration.
  - Tests: `TimelinesTests.RaisingAChildLoopGrowsTheTimelineToFitIt`, `TimelinesTests.LoweringAChildLoopKeepsTheTimelineDuration`
- **CORE-TIMELINE-004** — A child shall start at its position plus its own delay.
  - Tests: `TimelinesTests.ChildrenPositionShouldBeAddedToTheirDelay`
- **CORE-TIMELINE-005** — A child shall never autoplay; the timeline's playhead drives it.
  - Tests: untested
- **CORE-TIMELINE-006** — Each added child shall raise the engine's `TimerCreated`.
  - Tests: `HostHookTests.TimerCreatedFiresOncePerRootAndPerTimelineChild`

### Defaults

- **CORE-TIMELINE-007** — Children shall inherit `Defaults` merged over the engine's defaults, for their timer settings, tween settings and callbacks, while the timeline's own `Reversed`, `Loop` and other settings are not inherited.
  - Tests: `TimelinesTests.DefaultParametersAreInheritedByChildren`, `TimelinesTests.DurationIsTheSumOfEveryChildEndTimeTimesTheIterationCount`
- **CORE-TIMELINE-008** — While `PlaybackEase` is set, the timeline shall drive every child through its eased playhead.
  - Tests: `ParametersTests.PlaybackEaseOnTimeline`, `IntrospectionTests.TimelineChildOffsetAndPlaybackEaseMatchTheTimeline`
- **CORE-TIMELINE-009** — The timeline shall invoke `OnRender` after rendering its children each frame.
  - Tests: `CallbacksTests.OnRenderOnTimeline`

### Positions

- **CORE-TIMELINE-010** — When no position is given, the child shall be placed at the timeline's current end, a `Constants.MinValue` end counting as 0.
  - Tests: `TimelinesTests.BasicTimePositions`
- **CORE-TIMELINE-011** — A number, or a string that parses as one, shall place the child at that absolute time.
  - Tests: `TimelinesTests.AbsoluteTimePositions`, `TimelinesTests.MixedTimePositionTypes`
- **CORE-TIMELINE-012** — `"+=N"`, `"-=N"` and `"*=N"` shall apply to the timeline's current end.
  - Tests: `TimelinesTests.RelativeTimePositions`
- **CORE-TIMELINE-013** — A string starting with `"<<"` shall resolve to the last added child's start (offset plus delay), and one starting with a single `"<"` to its end; with no child yet, either shall resolve to 0.
  - Tests: `TimelinesTests.PreviousAnimationEndPosition`, `TimelinesTests.PreviousAnimationStartPosition`
- **CORE-TIMELINE-014** — A relative operator after `"<"` or `"<<"` shall apply to that resolved time.
  - Tests: `TimelinesTests.PreviousOperatorWithRelativeValues`, `TimelinesTests.PreviousAnimationEndPositionWithRelativeValue`, `TimelinesTests.PreviousAnimationStartPositionWithRelativeValues`
- **CORE-TIMELINE-015** — A label name shall resolve to the label's time, and a relative operator after a label name to that time plus the operation.
  - Tests: `TimelineCallbacksTests.TimelineLabelsPositions`, `StaggerTests.StaggersTimelinePositionsStartingAtALabel`
- **CORE-TIMELINE-016** (inherited) — An unknown label, with or without a relative operator, shall resolve against the timeline's current end.
  - Tests: untested
- **CORE-TIMELINE-017** — When a child's `Duration` parameter is 0 or `Constants.MinValue`, it shall be placed `Constants.MinValue` before its position, so its end value lands on the position.
  - Tests: `TimelinesTests.AddZeroDurationAnimation`, `TimelineCallbacksTests.TimelineSet`

### Adding children

- **CORE-TIMELINE-018** — While `Composition` is true, adding a child shall first render the timeline at the child's position without callbacks, so the child's implicit from values are those the earlier children leave there.
  - Tests: `TimelinesTests.TimelineValues`, `TimelineCallbacksTests.TimelineMixSetAndAdd`, `FunctionBasedValuesTests.ChainedFunctionValuesOnTimelineChildrenReadThePreviousChildEndValue`
- **CORE-TIMELINE-019** — While `Composition` is false, adding a child shall neither render the timeline nor initialize the child.
  - Tests: untested
- **CORE-TIMELINE-020** — `Add` with targets shall add an animation child; its targets may be timers or animations, whose properties such as `CurrentTime` it then animates.
  - Tests: `TimelinesTests.AddAnimationsAndTimersAsTargets`
- **CORE-TIMELINE-021** — `Add(TimerParams)` shall add a plain timer child.
  - Tests: `TimelinesTests.AddTimers`
- **CORE-TIMELINE-022** — The builder overloads of `Add` and `Set` shall add the child a snapshot of the builder's parameters describes.
  - Tests: `AnimationBuilderTests.TimelinesBuiltWithBuildersLayOutLikeTimelinesBuiltWithParams`, `AnimationBuilderTests.TimelinesBuiltWithLambdasLayOutLikeTimelinesBuiltWithBuilders`
- **CORE-TIMELINE-023** — When `Add` is given a `Stagger` position, the timeline shall add one child per parsed target, each at the time the stagger gives that target, all positioned against the timeline as it was before the group.
  - Tests: `StaggerTests.StaggersTimelinePositions`, `StaggerTests.StaggersTimelinePositionsWithANumericStart`
- **CORE-TIMELINE-024** — When a staggered group's parameters have an `Id`, child `i` shall be given `"{Id}-{i}"`.
  - Tests: `StaggerTests.StaggersTimelinePositions`
- **CORE-TIMELINE-025** — In a staggered group, function-based and staggered values shall see the whole group: the child's index and the group's target count.
  - Tests: `StaggerTests.StaggersATweenValueInATimeline`

### Set, Call, Label, Sync

- **CORE-TIMELINE-026** — `Set` shall add a child with a `Constants.MinValue` duration and `Replace` composition, without modifying the parameters it is given.
  - Tests: `TimelineCallbacksTests.TimelineSet`, `TimelineCallbacksTests.SetDoesNotMutateTheParametersItIsGiven`
- **CORE-TIMELINE-027** — The values a `Set` child writes shall be written again every time the playhead crosses its position, loops included.
  - Tests: `TimelineCallbacksTests.TimelineMixSetAndAdd`, `TimelineCallbacksTests.SetValuesUpdateOnLoop`
- **CORE-TIMELINE-028** — `Call` shall invoke its callback with the timeline each time the playhead crosses its position, forwards or backwards.
  - Tests: `TimelineCallbacksTests.CallCallbacks`, `TimelineCallbacksTests.CallCallbacksMultipleTimesViaSeek`, `TimelineCallbacksTests.CallCallbacksWithAlternateLoops`, `TimelineCallbacksTests.MixedCallAndAdd`
- **CORE-TIMELINE-029** — A seek that does not cross a `Call`'s position, including repeated seeks to one time, shall not invoke it.
  - Tests: `TimelineCallbacksTests.SeekDoesNotRefireACallItDoesNotCross`, `TimelineCallbacksTests.RepeatedSeekToTheSamePositionDoesNotRefireACall`, `TimelineCallbacksTests.SeekDirectionReversalPastACallInTheMiddleOfATimelineDoesNotRefireIt`
- **CORE-TIMELINE-030** — `Call`s at fractional or coinciding positions, and on a timeline whose duration is 0, shall each fire once per crossing.
  - Tests: `TimelineCallbacksTests.CallWithOddFloatingPointValuesA`, `TimelineCallbacksTests.CallWithOddFloatingPointValuesB`, `TimelineCallbacksTests.CallCallbacksOnAZeroDurationTimeline`
- **CORE-TIMELINE-031** — `Label` shall store the time its position resolves to under its name, replacing any earlier label of that name; without a position, the timeline's current end.
  - Tests: `TimelineCallbacksTests.TimelineLabelsPositions`
- **CORE-TIMELINE-032** — `Sync` shall pause the synced timer and add a linear child that drives its `CurrentTime` from 0 to its duration over that duration.
  - Tests: `TimelineCallbacksTests.NestedTimelines`

### Controls

- **CORE-TIMELINE-033** — `Remove` shall remove the targets' tweens from this timeline's children only (CORE-COMPOSITION), without notifying the adapters.
  - Tests: `TimelineCallbacksTests.RemoveNestedAnimations`, `UtilsTargetsTests.RemoveTargetsFromTimeline`
- **CORE-TIMELINE-034** — When `Remove` unlinks children, the timeline's `IterationDuration` shall shrink to the survivors while its `Duration` stays as it was.
  - Tests: `TimelineCallbacksTests.RemoveNestedAnimations`
- **CORE-TIMELINE-035** — `Stretch` shall stretch every child by the same factor and scale every label.
  - Tests: `ControlsTests.StretchATimeline`, `ControlsTests.StretchATimelineScalesKeyframeDelaysOfAnimationChildren`
- **CORE-TIMELINE-036** — `Refresh` shall refresh every animation child (CORE-ANIMATION).
  - Tests: `ControlsTests.RefreshATimeline`
- **CORE-TIMELINE-037** — `Revert` shall revert the timeline and then every child, last first, so each target gets its original values back.
  - Tests: `ControlsTests.RevertATimeline`
- **CORE-TIMELINE-038** — Canceling, completing or resetting the timeline shall apply to every child.
  - Tests: `ControlsTests.CancelATimeline`, `ControlsTests.CompleteATimeline`, `ControlsTests.ResettingATimelineShouldSetItsValuesBackToTheirInitialStates`
- **CORE-TIMELINE-039** — `Children` shall enumerate the children in the order they were added, without allocating, and be empty on a timer that is not a timeline.
  - Tests: `IntrospectionTests.ChildrenAndTweensEnumerateInOrderWithoutAllocating`
- **CORE-TIMELINE-040** — When a child's callback removes a later child's targets mid-tick, the remaining children shall keep rendering that frame.
  - Tests: `TimelinesTests.RemovingTheNextChildsTargetFromAChildCallbackKeepsTheRestRendering`

### Defaults

- **CORE-TIMELINE-041** — When `Composition` is not set, the timeline shall compose children against its state (true), as anime.js does.
  - Tests: `TimelinesTests.TimelineValues`
- **CORE-TIMELINE-042** — When `PlaybackEase` is not set, the timeline shall use the engine defaults' `PlaybackEase`, as anime.js does.
  - Tests: untested
- **CORE-TIMELINE-043** — When `OnRender` is not set, the timeline shall use the engine defaults' `OnRender`, as anime.js does.
  - Tests: untested

## Invariants

- `IterationDuration` is at least the end of every child added since the last `Remove`.
- `Labels` hold raw times on the timeline: seeking to one lands on it only when no `PlaybackEase` or `Reversed` reshapes the playhead.
- Children are linked in insertion order, not in time order.

## Edge cases

- Any string starting with `"<"` reads as a previous-child position, whatever follows the angle brackets.
- A child placed earlier than existing children still extends nothing; one placed past the end extends the timeline.
- A `Stagger` used as a group position caches its start offset on first use, so reusing it on another timeline or group keeps the first offset (CORE-STAGGER).
- `Refresh` reaches animation children only; a synced timer is driven through an animation child, so it is covered.

## Threading and lifetime

- The timeline and its children share the timeline's engine and thread. Children are not scheduled on the engine; the timeline ticks them.
- A child holds its parent; revert and cancel cascade to children.

## Reduced motion

- A timeline has no reduced form of its own: each child picks its own `WhenReduced`, and `ReduceAll` collapses each animation child (`ReducedMotionTests.ATimelineChildPicksItsOwnReducedForm`, `ReducedMotionTests.ReduceAllCollapsesTimelineChildren`). Positions, plain timers and `Call`s are not collapsed.

## Allocation

- Adding children allocates. Ticking a timeline allocates nothing beyond its children's rendering; `Children` enumerates without allocating.

## Deviations from anime.js

- An unknown label before a relative operator resolves against the timeline's end instead of NaN, matching upstream's own fallback for a bare unknown label.
- `Set` copies its parameters instead of mutating them, because parameters are immutable init-only records.
- `Revert` reverts its children; upstream passes the method without calling it, so its children keep their animated values.
- `Sync` accepts Presto timers only, not WAAPI animations, which the core has no counterpart for.
- Child lists survive a callback unlinking a sibling: upstream's loop can drop every remaining child when a callback unlinks the cached next one (see CORE-ENGINE).

## Decisions

- **Child loops survive a callback unlinking a sibling.** See CORE-ENGINE.
- **Params are init-only records with a string indexer.** See CORE-ANIMATION.

## Non-goals

- No XAML authoring of timelines: multi-step sequences usually need code-side targets and state that markup cannot see.
- No nesting of a `Timeline` as a direct child; a timeline is synced in through `Sync`.

## Open items

- `Remove` shrinks `IterationDuration` but keeps `Duration`, so the timeline still runs to its old end (pinned by `TimelineCallbacksTests.RemoveNestedAnimations`); upstream shares it, but no decision records it.
- `TimelineParams.Composition = false`, unknown labels, and children never autoplaying have no test.
- The zero-duration shift (CORE-TIMELINE-017) reads only an explicit numeric `Duration` parameter, so a child whose duration is 0 through `Defaults` or a function is not shifted.
- Under `ReduceAll`, children keep their positions, so a timeline with spaced positions still takes its full length; the decision that `ReduceAll` collapses everything without an alternative (CORE-REDUCED) says timeline children collapse but not whether positions do.
