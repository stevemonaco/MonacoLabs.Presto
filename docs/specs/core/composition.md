---
id: CORE-COMPOSITION
title: Composition and additive animation
package: Monaco.Presto
namespaces:
  - Monaco.Presto
sources:
  - Monaco.Presto/Composition.cs
  - Monaco.Presto/Core/Composer.cs
  - Monaco.Presto/Core/Additive.cs
  - Monaco.Presto/Core/TweenList.cs
  - Monaco.Presto/Core/TweenSiblings.cs
  - Monaco.Presto/Core/TweenLink.cs
  - Monaco.Presto/Core/Renderer.cs
  - Monaco.Presto/Animation.cs
  - Monaco.Presto/Timer.cs
  - Monaco.Presto/Presto.cs
types:
  - Composition
tests:
  - CompositionTests
  - TweenListIntegrityTests
  - TweensTests
  - TimelineCompositionTests
  - UtilsTargetsTests
  - EnginesTests
upstream:
  - src/animation/composition.js
  - src/animation/additive.js
  - tests/suites/tweens.test.js
  - tests/suites/timelines.test.js
depends:
  - CORE-ANIMATION
  - CORE-TIMER
  - CORE-ENGINE
  - CORE-TARGETS
---

# Composition and additive animation

## Purpose

Composition decides what happens when a new tween animates a target property that other tweens already animate. `Replace`, the default, truncates or overrides the earlier tweens and cancels animations left with nothing to do; `None` leaves them alone; `Blend` adds the new tween's motion on top through the engine's additive pass. This spec also covers the tween sibling lists composition walks, their integrity guarantees, and `Presto.Remove`, which takes targets out of composition. CORE-ANIMATION builds the tweens; CORE-TIMELINE adds the timeline-specific rules.

## Requirements

### Sibling registry

- **CORE-COMPOSITION-001** — Every tween whose composition is not `None` shall be registered in the replace sibling list of its target and canonical property, which is shared process-wide and keyed weakly by target.
  - Tests: `CompositionTests.ReplacedTweensAreSortedByAbsoluteStartTime`
- **CORE-COMPOSITION-002** — The replace siblings shall be kept sorted by absolute start time, an inserted tween going before any overridden tail siblings.
  - Tests: `CompositionTests.ReplacedTweensAreSortedByAbsoluteStartTime`, `CompositionTests.OverriddenTweensAreSkippedWhenInsertingASibling`
- **CORE-COMPOSITION-003** — When a tween is built with a composition other than `None`, every non-overridden registered sibling starting at or after its absolute start, following one that starts before or with it, shall be overridden, and the new tween shall chain its implicit from value off the latest non-overridden sibling before it.
  - Tests: `TimelineCompositionTests.CorrectTweenOverridingWhenAddingAnAnimationBeforeMultipleKeyframeStartTimeOnTheSameProperty`, `TimelineCompositionTests.AnImplicitFromChainsFromTheLatestLiveSiblingSkippingAnOverriddenMidChainKeyframe`
- **CORE-COMPOSITION-004** — An overridden tween shall no longer render, and its `IsOverridden` and `IsOverlapped` shall read true.
  - Tests: `IntrospectionTests.IsOverriddenFlipsWhenANewerAnimationTakesTheProperty`, `CompositionTests.OverlappedSiblingIsOverriddenWhenTruncatedToNothing`

### Replace

- **CORE-COMPOSITION-005** — When a `Replace` tween starts before its previous sibling ends, the sibling shall be truncated to end where the new tween's update starts and marked overlapped.
  - Tests: `CompositionTests.OverlappedSiblingIsTruncatedToTheNewTweenStart`, `AnimationsTests.CanceledTweenShouldUpdate`
- **CORE-COMPOSITION-006** — When the truncation leaves the sibling shorter than `Constants.MinValue`, the sibling shall be overridden.
  - Tests: `CompositionTests.OverlappedSiblingIsOverriddenWhenTruncatedToNothing`
- **CORE-COMPOSITION-007** — When the previous sibling belongs to another animation with more than one iteration still running at the new tween's start, that sibling and every earlier sibling of the same animation shall be overridden.
  - Tests: `CompositionTests.LoopedPreviousAnimationIsOverriddenWithItsKeyframeChain`, `TweensTests.ProperlyOverrideLoopedTween`
- **CORE-COMPOSITION-008** — When every tween of the previous sibling's top-level animation is overlapped, that animation shall be canceled, which pauses it and fires `OnPause`.
  - Tests: `CompositionTests.PreviousAnimationIsCanceledOnlyOnceAllItsTweensAreOverlapped`, `CallbacksTests.OnPauseOnAnimationTweenOverride`
- **CORE-COMPOSITION-009** — While an animation still has a tween that is not overlapped, a newer animation shall not cancel it.
  - Tests: `TweensTests.DoNotPauseAnimationWithPartiallyOverriddenTweens`, `CompositionTests.PreviousAnimationIsCanceledOnlyOnceAllItsTweensAreOverlapped`
- **CORE-COMPOSITION-010** — When the previous animation is a timeline child, its timeline shall be canceled only once every child of it is fully overlapped.
  - Tests: `TweensTests.ProperlyOverrideTimelineTweens`, `TweensTests.DoNotPauseTimelineWithPartiallyOverriddenTweens`, `CallbacksTests.OnPauseOnTimelineTweenOverride`
- **CORE-COMPOSITION-011** — When the new and previous tweens belong to children of the same timeline, composition shall not cancel either animation or the timeline.
  - Tests: `TweensTests.DoNotPauseTimelineWithPartiallyOverriddenTweens`, `TimelineCompositionTests.CorrectTweenCompositionWithMultipleTimelines`
- **CORE-COMPOSITION-012** — While replace siblings overlap, each shall write only within its own window: up to its truncated end, and a following sibling from its takeover point, so seeking forwards or backwards across siblings writes the value of the one active at that time.
  - Tests: `TimelineCompositionTests.BackwardSeekWritesTheActiveChildValueNotTheStaleToValueOfAnEarlierPastEndSibling`, `TimelineCompositionTests.SeekIntoTheGapBetweenATruncatedChildAndADelayedChildWritesTheHoldValue`, `TimelineCompositionTests.BackwardSeekBeforeANegativeDelaySiblingStartRestoresThePreviousChildEndValue`, `TimelineCompositionTests.BackwardSeekReEnteringAnEarlierChildRangeKeepsTheActiveChildValue`, `TimelineCompositionTests.APastEndChildWhoseDelayExceedsItsDurationMustNotOverwriteALaterSiblingOnAForwardSeek`
- **CORE-COMPOSITION-038** — When the playhead moves back out of a tween with an explicit from value into the gap after a previous sibling of another child, that sibling's value at its truncated end shall be written once, so the gap shows the same value backwards as forwards.
  - Tests: `TimelineCompositionTests.BackwardSeekOutOfAnExplicitFromChildIntoTheGapWritesThePreviousChildHold`, `TimelineCompositionTests.PlayingBackwardOutOfAFadeIntoTheGapWritesALaterAddedSetAtZero`
- **CORE-COMPOSITION-013** — When looping or alternating timelines with replace siblings are rendered, each iteration shall start from the correct initial value and an earlier tween shall not render over a later one.
  - Tests: `TimelineCompositionTests.CorrectInitialTweenValueOnLoop`, `TimelineCompositionTests.PreviousTweenBeforeLastShouldNotRenderOnLoop`, `TimelineCompositionTests.AlternateDirectionTimelineChildrenShouldCorrectlyRenderOnSeekAfterTheAnimationEnd`, `TimelineCompositionTests.AlternateDirectionTimelineChildrenShouldCorrectlyRenderOnSeekMidway`, `TimelineCompositionTests.CorrectTweenPlaybackWithMultipleTimelines`
- **CORE-COMPOSITION-036** — When the new and previous tweens belong to children of the same timeline, the previous tween shall still be truncated or overridden as CORE-COMPOSITION-005 to -007 describe; only the cancel is skipped.
  - Tests: `TimelineCompositionTests.SeekIntoTheGapBetweenATruncatedChildAndADelayedChildWritesTheHoldValue`
- **CORE-COMPOSITION-037** — When CORE-COMPOSITION-007 overrides every tween of a looping animation, that animation shall be canceled as CORE-COMPOSITION-008 describes, so it stops without completing.
  - Tests: `TweensTests.ProperlyOverrideLoopedTween`

### Lifecycle of siblings

- **CORE-COMPOSITION-014** — Pausing an animation shall leave its tweens in their sibling lists.
  - Tests: `TweensTests.DoNotRemoveTweenSiblingsOnAnimationPause`
- **CORE-COMPOSITION-015** — When an animation completes and the engine releases it, or it is canceled, completed or reverted, its tweens shall leave their sibling lists.
  - Tests: `TweensTests.RemoveTweenSiblingsOnAnimationComplete`, `CompositionTests.CancelRemovesTweenSiblingsAndSeekReAddsThem`
- **CORE-COMPOSITION-016** — When a canceled animation is reset, restarted or seeked, its tweens shall be composed into their sibling lists again.
  - Tests: `TweensTests.ReAddTweenSiblingsOnRestartAfterAnimationCompletes`, `CompositionTests.CancelRemovesTweenSiblingsAndSeekReAddsThem`
- **CORE-COMPOSITION-017** — When an already canceled animation is canceled, completed or reverted again, composition shall be left alone.
  - Tests: `TweenListIntegrityTests.CancelingABlendedAnimationTwiceKeepsTheListsIntactForTheNextBlend`, `TweenListIntegrityTests.CancelingACompletedBlendedAnimationKeepsTheListsIntact`

### None

- **CORE-COMPOSITION-018** — A `None` tween shall not be registered, override nothing, never be overridden, and never cancel another animation, so tweens on the same property each write in turn.
  - Tests: `TweensTests.DoNotOverrideTweensWithCompositionNone`
- **CORE-COMPOSITION-019** — `Presto.Set` and animations of 1000 targets or more shall default to `None` when `Composition` is unset.
  - Tests: untested

### Blend

- **CORE-COMPOSITION-020** — When the first `Blend` tween on a target property is built, the owner engine's additive state shall create one lookup tween for it, owned by the engine's additive animation, which is never scheduled on the engine.
  - Tests: `CompositionTests.BlendedTweensShareOneLookupTweenPerProperty`
- **CORE-COMPOSITION-021** — Each `Blend` tween shall run from the lookup's previous base minus its own to value, to 0, and make its to value the lookup's new base.
  - Tests: `CompositionTests.BlendedTweensShareOneLookupTweenPerProperty`
- **CORE-COMPOSITION-022** — On every engine update, after the timers have ticked, the additive pass shall write each lookup's base plus the current deltas of all its blended tweens, channel by channel for color and complex values.
  - Tests: `CompositionTests.AdditiveUpdateSumsTheBlendedTweensIntoTheLookupTween`, `TweensTests.ProperlyBlendTweensWithCompositionBlend`
- **CORE-COMPOSITION-023** — A `Blend` tween shall never write its target directly; only the additive pass does.
  - Tests: `TweensTests.ProperlyBlendTweensWithCompositionBlend`
- **CORE-COMPOSITION-024** — While a blended tween on a property is live, the additive pass shall overwrite a `Replace` tween's write to that property in the same frame.
  - Tests: untested
- **CORE-COMPOSITION-025** — A blended color tween shall interpolate with straight alpha.
  - Tests: `PremultipliedColorTests.BlendedColorTweensKeepTheStraightBlend`
- **CORE-COMPOSITION-026** — When the last blended tween of a property leaves, its lookup tween shall leave the sibling list and the additive animation, and the target's entry shall go once none of its properties has blended tweens.
  - Tests: `CompositionTests.RemoveTweenSiblingsDropsTheLookupTweenWithTheLastBlendedTween`, `TweenListIntegrityTests.RemovingATargetAfterRepeatedCancelsEmptiesTheLists`
- **CORE-COMPOSITION-027** — Additive state shall be per engine, so blended tweens on one engine are neither summed nor written by another.
  - Tests: `EnginesTests.BlendedTweensAreInvisibleToAnotherEngine`
- **CORE-COMPOSITION-028** — A `Blend` tween shall still override the replace siblings registered at or after its start, as CORE-COMPOSITION-003 describes.
  - Tests: untested
- **CORE-COMPOSITION-029** — When a discrete property is given `Blend`, the animation constructor shall throw `ArgumentException`.
  - Tests: `DiscreteValuesTests.BlendingThrows`

### List integrity

- **CORE-COMPOSITION-030** — When a tween that is not linked into a list is removed from it, the removal shall be a no-op, leaving the list's head and tail untouched.
  - Tests: `TweenListIntegrityTests.CancelingABlendedAnimationTwiceKeepsTheListsIntactForTheNextBlend`, `TweenListIntegrityTests.RemovingATargetAfterRepeatedCancelsEmptiesTheLists`
- **CORE-COMPOSITION-031** — In Debug builds, a walk of a tween list, and a sorted insert into one, shall throw `InvalidOperationException` when the list has a cycle or its head and tail disagree with its links; Release builds shall not check.
  - Tests: `TweenListIntegrityTests.CancelingABlendedAnimationTwiceKeepsTheListsIntactForTheNextBlend`
- **CORE-COMPOSITION-032** — Building and canceling a blended animation shall cost time linear in its tween count, Debug builds included.
  - Tests: `TweenListIntegrityTests.BlendingAndCancelingTwentyThousandTargetsStaysFast`

### Remove

- **CORE-COMPOSITION-033** — When `Presto.Remove` takes a tween out of an animation, the tween shall also leave its sibling list, so later compositions on that target and property no longer see it. The rest of `Remove` is specified in CORE-PRESTO (028–037).
  - Tests: `UtilsTargetsTests.RemoveTarget`, `UtilsTargetsTests.RemoveTargetsFromMultipleAnimationsAtOnce`

### Defaults

- **CORE-COMPOSITION-034** — When `Composition` is set neither on the tween, keyframe or animation nor in `Presto.Defaults`, the tween shall compose with `Replace` (outside the CORE-COMPOSITION-019 cases), as anime.js does. Which level wins when several are set is CORE-COMPOSITION-035.
  - Tests: `AnimationsTests.CanceledTweenShouldUpdate`
- **CORE-COMPOSITION-035** — The animation shall give each tween the first composition set among: the property's own `TweenParams.Composition` (a duration-based `Keyframe`'s `Composition` applies to the tweens it produces; a percentage-based keyframe's is ignored), the animation's `Composition`, `None` in the CORE-COMPOSITION-019 cases, then the inherited defaults' `Composition` (a timeline's child defaults, else `Presto.Defaults`).
  - Tests: `TweensTests.ProperlyAssignSpecificTweenProperties`

## Invariants

- Each tween is in at most one replace list and at most one additive list, and every list is acyclic with its head's previous and its tail's next link null.
- A lookup tween is the head of its additive list, and a list holding only its lookup tween does not exist.
- A canceled timer's tweens are in no sibling list.

## Edge cases

- A tween inserted before every registered sibling overrides none of them; the later siblings keep rendering in their own windows.
- A plain timer or `Call` child has no tweens, so it counts as fully overlapped when composition decides whether to cancel its timeline.
- Replace lists are process-wide while additive state is per engine, so `Replace` tweens on two engines animating one target still override each other.
- Restarting a canceled `Blend` animation recomposes it against the current base, so its from value can differ from its first run.

## Threading and lifetime

- All composition runs on the owner engine's thread; there are no locks.
- Replace lists live in a `ConditionalWeakTable` keyed by target, so composition never keeps a target alive. The additive lookups hold their targets strongly until the last blended tween leaves.

## Reduced motion

- Not affected: composition applies the same way to collapsed tweens; a `ReduceAll` tween still overrides and is overridden.

## Allocation

- Replace composition allocates nothing per frame. The additive pass allocates a channel array per blended color or complex property per frame; blended numbers allocate nothing.
- Registering the first tween on a target property allocates its sibling list.

## Deviations from anime.js

- Removing a tween from a list it is not in is a no-op, and a repeat cancel leaves composition alone, because upstream's double removal corrupts its lists (see "Sibling removal is idempotent").
- Debug builds check list integrity before walks and sorted inserts, so a corrupted list throws instead of hanging the thread (see "Debug integrity checks run only before walks and sorted inserts").
- Additive state is per engine, replace lists stay process-wide, because replace lookups are keyed by target and so cannot collide across engines (see CORE-THREADING).
- Child lists survive a callback unlinking a sibling mid-loop, because upstream's loop could empty the engine's list for good (see CORE-ENGINE).
- `Remove` does not re-flag a previous transform tween as upstream does; transform composition belongs to the bridge.
- Moving back out of an explicit-from tween into the gap after a sibling of another child writes that sibling's hold, where upstream leaves the tween's last frame in place (see "A backward exit from an explicit from hands the value back").
- Blended color tweens keep straight alpha while other color tweens are premultiplied; the premultiplied-alpha decision exempts `Blend` without a recorded reason (see CORE-COLORS, "Colors interpolate with premultiplied alpha").

## Decisions

- **Sibling removal is idempotent.** Removing a tween that is not in a list is a no-op, and a repeat cancel, complete, revert or engine detach leaves composition alone; `Reset` and `Seek` recompose and clear the flag. Reason: upstream's `removeChild` assumes membership, and a double removal (Draggable canceling its overshoot animations on every grab) orphaned blended tweens, spliced one animation's list into another and hung the UI thread in a reverse walk; upstream 4.5.0 has the same shape.
- **Debug integrity checks run only before walks and sorted inserts.** Debug builds check a tween list for cycles and inconsistent head and tail with a bounded, allocation-free tortoise-and-hare, and throw instead of hanging; Release builds compile the check out. Reason: checking on every add and remove made a 20 000-tween `Blend` build quadratic (6.5 s in Debug).
- **A backward exit from an explicit from hands the value back.** Leaving an explicit-from tween backwards into the gap after a sibling of another child writes that sibling's value at its truncated end, once. Reason: upstream's gate keeps an explicit from from writing before its own start, and the sibling's timer is past its end so it never renders again, which left the tween's last frame on the target (the Title sequence cards played in reverse held at 0.4–4% opacity until time 0); the forward pass shows the sibling's hold in that gap, so the backward pass now matches it.
- **Property keys are canonicalized, not keyed on accessors.** See CORE-TARGETS.
- **Defaults, scope and additive state are per engine; a few statics are process-wide and documented, not locked.** See CORE-THREADING.
- **Discrete values reject relative values and `Blend`.** See CORE-TARGETS.
- **Colors interpolate with premultiplied alpha.** See CORE-COLORS.

## Non-goals

- No per-frame weighting between blended tweens; deltas are summed.
- Discrete values cannot blend.

## Open items

- One record of the integrity-check decision says the check runs only before sorted inserts; the code (`TweenList.ForEachChildren`) also checks before every walk, and the decision above follows the code.
- CORE-COMPOSITION-024 (the additive pass overwriting a `Replace` write) is documented on `Composition.Blend` but has no core test.
- `Blend` tweens overriding later replace siblings (CORE-COMPOSITION-028) and the 1000-target `None` default have no test.
- Cross-engine `Replace` overriding through the process-wide replace lists is unspecified by any plan and untested.
