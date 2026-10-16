---
id: CORE-TIMELINE
title: Timeline
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Timeline.cs
  - MonacoLabs.Presto/TimelineLabel.cs
  - MonacoLabs.Presto/TimelineParams.cs
  - MonacoLabs.Presto/TimelinePosition.cs
  - MonacoLabs.Presto/TimerChildren.cs
  - MonacoLabs.Presto/Presto.cs
types:
  - Timeline
  - TimelineLabel
  - TimelineParams
  - TimelinePosition
  - TimerChildren
tests:
  - TimelinesTests
  - TimelinePositionTests
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
- **CORE-TIMELINE-016** — When a position names a label the timeline does not have, string or `TimelineLabel`, with or without a relative operator or offset, adding the child shall throw `ArgumentException` naming the label; a bare operator (`"+=100"`) resolves against the timeline's current end.
  - Tests: `TimelinePositionTests.UnknownLabelsThrow`, `TimelinePositionTests.TypedPositionsResolveAsTheirStringTwins`
- **CORE-TIMELINE-017** — When a child's resolved duration (explicit, defaults or function) is 0 or `Constants.MinValue`, it shall be placed `Constants.MinValue` before its position, so its end value lands on the position.
  - Tests: `TimelinesTests.AddZeroDurationAnimation`, `TimelinesTests.AZeroDurationFromTheDefaultsOrAFunctionLandsOnThePosition`, `TimelineCallbacksTests.TimelineSet`
- **CORE-TIMELINE-044** — A `TimelineLabel` position shall resolve as its name does, and `label + n` / `label - n` as `"name+=n"` / `"name-=n"`.
  - Tests: `TimelinePositionTests.TypedPositionsResolveAsTheirStringTwins`
- **CORE-TIMELINE-045** — `TimelinePosition.End`, `PreviousEnd` and `PreviousStart` shall resolve as the default position, `"<"` and `"<<"`, and `+ n` / `- n` on them as that relative operator after the anchor.
  - Tests: `TimelinePositionTests.TypedPositionsResolveAsTheirStringTwins`
- **CORE-TIMELINE-046** — `+` and `-` applied to an absolute position shall shift it, applied to a string position shall add to the time the string resolves to, and applied again to a typed position shall fold into one offset.
  - Tests: `TimelinePositionTests.TypedPositionsResolveAsTheirStringTwins`

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
- **CORE-TIMELINE-047** — `Label(name, out label, position)` shall store the label as `Label(name, position)` does and give a `TimelineLabel` with that name.
  - Tests: `TimelinesTests.LabelWithAnOutHandleStoresTheSameTimeAsLabel`
- **CORE-TIMELINE-048** — `Label(label, position)` shall store the label under `label.Name` as `Label(name, position)` does.
  - Tests: `TimelinesTests.LabelWithADeclaredHandleStoresUnderItsName`
- **CORE-TIMELINE-049** — `Seek(label, muteCallbacks)` shall seek the timeline to the time stored under `label.Name` and return the timeline; when no label of that name exists, it shall throw `KeyNotFoundException`.
  - Tests: `TimelinesTests.SeekToALabelMatchesSeekToItsTime`, `TimelinesTests.SeekToAnUnknownLabelThrows`
- **CORE-TIMELINE-032** — `Sync` shall pause the synced timer and add a linear child that drives its `CurrentTime` from 0 to its duration over that duration.
  - Tests: `TimelineCallbacksTests.NestedTimelines`

### Controls

- **CORE-TIMELINE-033** — `Remove`, given a `PropertyKey` or null for every property, shall remove the targets' tweens from this timeline's children only (CORE-COMPOSITION), without notifying the adapters.
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

- **CORE-TIMELINE-041** — When `Composition` is not set, the timeline shall compose children against its state (true).
  - Tests: `TimelinesTests.TimelineValues`
- **CORE-TIMELINE-042** — When `PlaybackEase` is not set, the timeline shall use the engine defaults' `PlaybackEase`.
  - Tests: untested
- **CORE-TIMELINE-043** — When `OnRender` is not set, the timeline shall use the engine defaults' `OnRender`.
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

- A timeline has no reduced form of its own: each child picks its own `WhenReduced`, and `ReduceAll` collapses each animation child and places every child, `Call` and label at 0 (`ReducedMotionTests.ATimelineChildPicksItsOwnReducedForm`, `ReducedMotionTests.ReduceAllCollapsesTimelineChildren`, `ReducedMotionTests.ReduceAllCollapsesTimelinePositions`). Plain timers keep their durations.

## Allocation

- Adding children allocates. Ticking a timeline allocates nothing beyond its children's rendering; `Children` enumerates without allocating.

## Provenance

- An unknown label throws, where upstream places a bare one at the timeline's end and one before an operator at NaN (see "Unknown labels throw").
- `Set` copies its params, where upstream writes `duration` and `composition` into the caller's object (see "`Set` never mutates its params").
- `Set` copies its parameters instead of mutating them, because parameters are immutable init-only records.
- `Revert` reverts its children; upstream passes the method without calling it, so its children keep their animated values.
- `Sync` accepts Presto timers only, not WAAPI animations, which the core has no counterpart for.
- The zero-duration shift reads the resolved duration, where upstream reads only a numeric `duration` parameter, so a zero through the defaults or a function also lands on its position (see "A zero-duration animation is shifted once built").
- Child lists survive a callback unlinking a sibling: upstream's loop can drop every remaining child when a callback unlinks the cached next one (see CORE-ENGINE).

## Decisions

- **Child loops survive a callback unlinking a sibling.** See CORE-ENGINE.
- **Params are init-only records with a key indexer.** See CORE-ANIMATION.
- **Labels are handles, positions stay one type.** `TimelineLabel` is its own type with a `Name` and an implicit conversion to `TimelinePosition`; arithmetic yields a `TimelinePosition`. Reason: a handle reads as a label at call sites, can be stored and seeked to and passed as a stagger start (`Start = label.Name`), while `Add` keeps one position parameter type. Rejected: making a label a bare `TimelinePosition` (loses the name); a `CallerArgumentExpression`-named label (unreliable on `out var`).
- **The `out` overload places and names a label in one call.** `Label(name, out label, position)` keeps the fluent chain unbroken; the declared-first `Label(TimelineLabel, position)` serves labels stored as fields. There is no implicit `string` → `TimelineLabel` conversion. Reason: it would make `Label("x", 0)` ambiguous between the two overloads.
- **Typed positions resolve exactly as their string twins.** `label + n` is `"label+=n"`, `PreviousEnd - n` is `"<-=n"`, `End + n` is `"+=n"`, and a typed label not on the timeline throws like an unknown string label (CORE-TIMELINE-016). Successive `+`/`-` fold into one offset. Reason: one resolution rule; the `out` overload makes an unplaced label rare.
- **`+`/`-` on a string position add to its resolved time.** `(TimelinePosition)"L" + 10` resolves the string as before, then adds the folded offset, so it equals `"L+=10"`. Reason: one rule for every anchor (resolve, then add the offset), and the string parse path stays untouched. Rejected: rewriting the string (allocates, and has no meaning for `"*=2"`).
- **Unknown labels throw.** A position naming a label the timeline does not have throws `ArgumentException` when the child is added, in every form. Reason: a mistyped label otherwise placed the child at the end silently (or at NaN upstream, before an operator), a mistake that showed only as a mistimed sequence. Rejected: upstream's end fallback, kept earlier for the operator form too.
- **`Set` never mutates its params.** `Timeline.Set` adds a copy with the setter's duration and `Replace` composition. Reason: upstream's `.set()` writes both into the caller's object, so reusing it for a later `.add()` quietly made that a setter too. Rejected: upstream's mutation.
- **`Seek(TimelineLabel)` seeks to the label's raw time and throws for an unknown label.** It is `Seek(Labels[label.Name], muteCallbacks)` returning the timeline, and a label not on the timeline throws `KeyNotFoundException`, as the `Labels` lookup it replaces does. Reason: seeking to a section is what labels are kept for, and a seek has no upstream end-fallback to stay consistent with; silently seeking to the end would hide the mistake. Rejected: `TimeOf(TimelinePosition)` (resolving relative anchors like `PreviousEnd` at seek time has no clear meaning once children are added); seeking to the end for an unknown label.
- **Unset timeline parameters compose and inherit.** Without `Composition` a timeline composes its children, and without `PlaybackEase` or `OnRender` it takes the engine defaults' (CORE-TIMELINE-041 to 043). Reason: overlapping children that animate one property then hand it over in order, as separate animations would; the ease and render callback fall back to the defaults like every other timer parameter.
- **`Timeline.Remove` keeps `Duration`.** `Remove` shrinks `IterationDuration` to the surviving children but leaves `Duration` as it was, so the timeline still runs to its old end (CORE-TIMELINE-034). Reason: kept from upstream and pinned by the ported `TimelineCallbacksTests.RemoveNestedAnimations`; changing it rewrites a ported timing test, which belongs in a timing change of its own. Rejected: shrinking `Duration` too.
- **A zero-duration animation is shifted once built.** An animation whose resolved duration is zero (explicit, from the defaults, a function, or collapsed by `ReduceAll`) is shifted by `Constants.MinValue` once built; a plain timer is shifted before. Reason: a function's value is per target and only the built child knows it, and calling it early would run user code twice.
- **`ReduceAll` collapses timeline positions.** See CORE-REDUCED.
- **No typed `*=`.** Multiplication stays string-only. Reason: rarely used, and it composes poorly with folded offsets (`(label + 100) * 2` has no string twin).

## Non-goals

- No XAML authoring of timelines: multi-step sequences usually need code-side targets and state that markup cannot see.
- No nesting of a `Timeline` as a direct child; a timeline is synced in through `Sync`.

## Open items

- `TimelineParams.Composition = false` and children never autoplaying have no test.
