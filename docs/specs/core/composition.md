---
id: CORE-COMPOSITION
title: Composition and additive animation
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Composition.cs
  - MonacoLabs.Presto/Core/Composer.cs
  - MonacoLabs.Presto/Core/Additive.cs
  - MonacoLabs.Presto/Core/TweenList.cs
  - MonacoLabs.Presto/Core/TweenSiblings.cs
  - MonacoLabs.Presto/Core/TweenLink.cs
  - MonacoLabs.Presto/Core/Renderer.cs
  - MonacoLabs.Presto/Animation.cs
  - MonacoLabs.Presto/Timer.cs
  - MonacoLabs.Presto/Presto.cs
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

Composition decides what happens when a new tween animates a target property that other tweens already animate. `Replace`, the default, truncates or overrides the earlier tweens and cancels animations left with nothing to do; `Overlap` leaves them alone; `Additive` adds the new tween's motion on top through the engine's additive pass. This spec also covers the tween sibling lists composition walks, their integrity guarantees, and `Presto.Remove`, which takes targets out of composition. CORE-ANIMATION builds the tweens; CORE-TIMELINE adds the timeline-specific rules.

## Requirements

### Sibling registry

- **CORE-COMPOSITION-001** — Every tween whose composition is not `Overlap` shall be registered in its owner engine's replace sibling list for its target and canonical property, keyed weakly by target, so tweens on different engines never compose.
  - Tests: `CompositionTests.ReplacedTweensAreSortedByAbsoluteStartTime`, `EnginesTests.ReplaceTweensDoNotComposeAcrossEngines`
- **CORE-COMPOSITION-002** — The replace siblings shall be kept sorted by absolute start time, an inserted tween going before any overridden tail siblings.
  - Tests: `CompositionTests.ReplacedTweensAreSortedByAbsoluteStartTime`, `CompositionTests.OverriddenTweensAreSkippedWhenInsertingASibling`
- **CORE-COMPOSITION-003** — When a tween is built with a composition other than `Overlap`, every non-overridden registered sibling starting at or after its absolute start, following one that starts before or with it, shall be overridden, and the new tween shall chain its implicit from value off the latest non-overridden sibling before it.
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
- **CORE-COMPOSITION-039** — When a timeline child past its end renders after a later replace sibling of another child created before it, it shall not write once the timeline's playhead is past that sibling's takeover point, so a forward jump lands on the same values whatever path led there.
  - Tests: `TimelineCompositionTests.AForwardJumpPastALaterAddedSetAtZeroEndsOnTheLaterFade`
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
  - Tests: `TweenListIntegrityTests.CancelingAnAdditiveAnimationTwiceKeepsTheListsIntactForTheNextOne`, `TweenListIntegrityTests.CancelingACompletedAdditiveAnimationKeepsTheListsIntact`

### Overlap

- **CORE-COMPOSITION-018** — A `Overlap` tween shall not be registered, override nothing, never be overridden, and never cancel another animation, so tweens on the same property each write in turn.
  - Tests: `TweensTests.DoNotOverrideTweensWithCompositionOverlap`
- **CORE-COMPOSITION-019** — `Presto.Set` and animations of 1000 targets or more shall default to `Overlap` when `Composition` is unset.
  - Tests: untested

### Additive

- **CORE-COMPOSITION-020** — When the first `Additive` tween on a target property is built, the owner engine's additive state shall create one lookup tween for it, owned by the engine's additive animation, which is never scheduled on the engine.
  - Tests: `CompositionTests.AdditiveTweensShareOneLookupTweenPerProperty`
- **CORE-COMPOSITION-021** — Each `Additive` tween shall run from the lookup's previous base minus its own to value, to 0, and make its to value the lookup's new base.
  - Tests: `CompositionTests.AdditiveTweensShareOneLookupTweenPerProperty`
- **CORE-COMPOSITION-022** — On every engine update, after the timers have ticked, the additive pass shall write each lookup's base plus the current deltas of all its additive tweens, channel by channel for color and complex values.
  - Tests: `CompositionTests.AdditiveUpdateSumsTheAdditiveTweensIntoTheLookupTween`, `TweensTests.ProperlySumTweensWithCompositionAdditive`
- **CORE-COMPOSITION-023** — A `Additive` tween shall never write its target directly; only the additive pass does.
  - Tests: `TweensTests.ProperlySumTweensWithCompositionAdditive`
- **CORE-COMPOSITION-040** — When a timer is seeked, other than an internal render, the additive pass shall run right after the seek renders, so a paused timer shows its additive values at the new time.
  - Tests: `CompositionTests.SeekingAPausedAdditiveAnimationWritesTheSumWithoutAnEngineUpdate`
- **CORE-COMPOSITION-041** — When an `Additive` tween leaves its sibling list, it shall take back its own from and to values, so composing it again (CORE-COMPOSITION-016) applies CORE-COMPOSITION-021 to them rather than to its delta; and each composition shall start its current delta at its from value, so a keyframe that has not rendered since is summed where it starts.
  - Tests: `CompositionTests.SeekingACanceledAdditiveAnimationRecomposesItFromItsOwnValues`, `CompositionTests.SeekingACanceledAdditiveColorAnimationSumsTheKeyframeThatHasNotRenderedAtItsFrom`
- **CORE-COMPOSITION-024** — While a additive tween on a property is live, the additive pass shall overwrite a `Replace` tween's write to that property in the same frame.
  - Tests: untested
- **CORE-COMPOSITION-025** — A additive color tween shall interpolate with straight alpha.
  - Tests: `PremultipliedColorTests.AdditiveColorTweensKeepTheStraightBlend`
- **CORE-COMPOSITION-026** — When the last additive tween of a property leaves, its lookup tween shall leave the sibling list and the additive animation, and the target's entry shall go once none of its properties has additive tweens.
  - Tests: `CompositionTests.RemoveTweenSiblingsDropsTheLookupTweenWithTheLastAdditiveTween`, `TweenListIntegrityTests.RemovingATargetAfterRepeatedCancelsEmptiesTheLists`
- **CORE-COMPOSITION-027** — Additive state shall be per engine, so additive tweens on one engine are neither summed nor written by another.
  - Tests: `EnginesTests.AdditiveTweensAreInvisibleToAnotherEngine`
- **CORE-COMPOSITION-028** — A `Additive` tween shall still override the replace siblings registered at or after its start, as CORE-COMPOSITION-003 describes.
  - Tests: untested
- **CORE-COMPOSITION-029** — When a discrete property is given `Additive`, the animation constructor shall throw `ArgumentException`.
  - Tests: `DiscreteValuesTests.AdditiveThrows`

### List integrity

- **CORE-COMPOSITION-030** — When a tween that is not linked into a list is removed from it, the removal shall be a no-op, leaving the list's head and tail untouched.
  - Tests: `TweenListIntegrityTests.CancelingAnAdditiveAnimationTwiceKeepsTheListsIntactForTheNextOne`, `TweenListIntegrityTests.RemovingATargetAfterRepeatedCancelsEmptiesTheLists`
- **CORE-COMPOSITION-031** — In Debug builds, a walk of a tween list, and a sorted insert into one, shall throw `InvalidOperationException` when the list has a cycle or its head and tail disagree with its links; Release builds shall not check.
  - Tests: `TweenListIntegrityTests.CancelingAnAdditiveAnimationTwiceKeepsTheListsIntactForTheNextOne`
- **CORE-COMPOSITION-032** — Building and canceling a additive animation shall cost time linear in its tween count, Debug builds included.
  - Tests: `TweenListIntegrityTests.AnimatingAdditivelyAndCancelingTwentyThousandTargetsStaysFast`

### Remove

- **CORE-COMPOSITION-033** — When `Presto.Remove` takes a tween out of an animation, the tween shall also leave its sibling list, so later compositions on that target and property no longer see it. The rest of `Remove` is specified in CORE-PRESTO (028–037).
  - Tests: `UtilsTargetsTests.RemoveTarget`, `UtilsTargetsTests.RemoveTargetsFromMultipleAnimationsAtOnce`

### Defaults

- **CORE-COMPOSITION-034** — When `Composition` is set neither on the tween, keyframe or animation nor in `Presto.Defaults`, the tween shall compose with `Replace` (outside the CORE-COMPOSITION-019 cases). Which level wins when several are set is CORE-COMPOSITION-035.
  - Tests: `AnimationsTests.CanceledTweenShouldUpdate`
- **CORE-COMPOSITION-035** — The animation shall give each tween the first composition set among: the property's own `TweenParams.Composition` (a duration-based `Keyframe`'s `Composition` applies to the tweens it produces; a percentage-based keyframe's is ignored), the animation's `Composition`, `Overlap` in the CORE-COMPOSITION-019 cases, then the inherited defaults' `Composition` (a timeline's child defaults, else `Presto.Defaults`).
  - Tests: `TweensTests.ProperlyAssignSpecificTweenProperties`

## Invariants

- Each tween is in at most one replace list and at most one additive list, and every list is acyclic with its head's previous and its tail's next link null.
- A lookup tween is the head of its additive list, and a list holding only its lookup tween does not exist.
- A canceled timer's tweens are in no sibling list.

## Edge cases

- A tween inserted before every registered sibling overrides none of them; the later siblings keep rendering in their own windows.
- A plain timer or `Call` child has no tweens, so it counts as fully overlapped when composition decides whether to cancel its timeline.
- Restarting a canceled `Additive` animation recomposes it against the current base, so its from value can differ from its first run.

## Threading and lifetime

- All composition runs on the owner engine's thread; there are no locks.
- Each engine keeps its replace lists in a `ConditionalWeakTable` keyed by target, so composition never keeps a target alive. The additive lookups hold their targets strongly until the last additive tween leaves.

## Reduced motion

- Not affected: composition applies the same way to collapsed tweens; a `ReduceAll` tween still overrides and is overridden.

## Allocation

- Replace composition allocates nothing per frame. The additive pass allocates a channel array per additive color or complex property per frame; additive numbers allocate nothing.
- Registering the first tween on a target property allocates its sibling list.

## Provenance

- `Composition` is one value per call, where upstream resolves it per target (see "`Composition` is a plain enum, not a function value").
- Removing a tween from a list it is not in is a no-op, and a repeat cancel leaves composition alone, because upstream's double removal corrupts its lists (see "Sibling removal is idempotent").
- Debug builds check list integrity before walks and sorted inserts, so a corrupted list throws instead of hanging the thread (see "Debug integrity checks run only before walks and sorted inserts").
- Replace lists and additive state are per engine, where upstream has one global lookup table, because composition compares start times on one engine's clock and a cancel must stay on that engine's thread (see CORE-THREADING).
- Child lists survive a callback unlinking a sibling mid-loop, because upstream's loop could empty the engine's list for good (see CORE-ENGINE).
- `Remove` does not re-flag a previous transform tween, which upstream does; transform composition belongs to the bridge.
- Moving back out of an explicit-from tween into the gap after a sibling of another child writes that sibling's hold, where upstream leaves the tween's last frame in place (see "A backward exit from an explicit from hands the value back").
- A child past its end yields to a later sibling created before it by the timeline's playhead, where upstream gates on the child's clamped time and lets a later-added `.set` at 0 overwrite a later fade on a forward jump (see "A forward jump does not depend on the path").
- A seek runs the additive pass itself, where upstream runs it only in the engine update, which a paused timer has left (see "A seek shows additive values").
- An additive tween recomposes from its own values and starts its delta at its from value, where upstream recomposes from the delta and starts it at 0 (see "A revived additive tween recomposes from its own values").
- Additive color tweens keep straight alpha while other color tweens are premultiplied; the premultiplied-alpha decision exempts `Additive` without a recorded reason (see CORE-COLORS, "Colors interpolate with premultiplied alpha").

## Decisions

- **`Composition` is a plain enum, not a function value.** It is set per animation, keyframe, tween or animatable property, never per target. Reason: upstream resolves it per target through `getFunctionValue`, but nothing needs targets of one call to compose differently, two `Animate` calls (or two timeline `Add`s) express it, and a `CompositionValue` union across five parameter types and the builder costs more than it gives. Rejected: a function-based `CompositionValue`.
- **Sibling removal is idempotent.** Removing a tween that is not in a list is a no-op, and a repeat cancel, complete, revert or engine detach leaves composition alone; `Reset` and `Seek` recompose and clear the flag. Reason: upstream's `removeChild` assumes membership, and a double removal (Draggable canceling its overshoot animations on every grab) orphaned additive tweens, spliced one animation's list into another and hung the UI thread in a reverse walk; upstream 4.5.0 has the same shape.
- **Debug integrity checks run only before walks and sorted inserts.** Debug builds check a tween list for cycles and inconsistent head and tail with a bounded, allocation-free tortoise-and-hare, and throw instead of hanging; Release builds compile the check out. Reason: checking on every add and remove made a 20 000-tween `Additive` build quadratic (6.5 s in Debug).
- **A forward jump does not depend on the path.** A timeline child past its end gates its next replace sibling's takeover on the timeline's playhead, not its own clamped end time, when that sibling's tween was created first and so has already rendered this frame. Reason: anime.js 4.5.0 renders children in insertion order and clamps a past-end child's time to its end, so a `.Set` at 0 added after a fade at 1000 rendered last on a jump from 0 to the end and left opacity 0, while seeking through 1500 first left 1 (the Title sequence cards ended hidden); limited to siblings created first because a sibling rendering after the child overwrites its end anyway, and skipping that end would change what the child's `OnComplete` sees. Rejected: upstream parity; gating on the playhead for every past-end child; rendering a jump's children in start-time order.
- **A backward exit from an explicit from hands the value back.** Leaving an explicit-from tween backwards into the gap after a sibling of another child writes that sibling's value at its truncated end, once. Reason: upstream's gate keeps an explicit from from writing before its own start, and the sibling's timer is past its end so it never renders again, which left the tween's last frame on the target (the Title sequence cards played in reverse held at 0.4–4% opacity until time 0); the forward pass shows the sibling's hold in that gap, so the backward pass now matches it.
- **A seek shows additive values.** `Seek` runs the engine's additive pass after it renders (CORE-COMPOSITION-040). Reason: a paused timer leaves the engine, so upstream's only additive pass, in the engine update, never ran for it; a paused, scrubbed timeline with additive children (the Title sequence dot ripple) showed the values of the last frame it played. Rejected: waking the engine for a frame after each seek, which shows the seek one frame late.
- **A revived additive tween recomposes from its own values.** An `Additive` tween keeps its own from and to when composing turns them into a delta, takes them back when it leaves its list, and starts its delta at its from value instead of 0 (CORE-COMPOSITION-041). Reason: the engine cancels a timeline once it completes, and a seek back revives and recomposes it; upstream composes the delta again, so the recreated lookup took a delta as its base, and its keyframes that had not rendered since added nothing, which left the Title sequence dots at Scale −1.2 to 0 through the ripple after one play. Rejected: keeping additive tweens in their lists across the engine's cancel, which CORE-COMPOSITION-015 and the idempotent removal rely on.
- **Property keys are canonicalized, not keyed on accessors.** See CORE-TARGETS.
- **Defaults, scope and composition state are per engine; a few statics are process-wide and documented, not locked.** See CORE-THREADING.
- **Discrete values reject relative values and `Additive`.** See CORE-TARGETS.
- **Colors interpolate with premultiplied alpha.** See CORE-COLORS.
- **The modes are `Replace`, `Overlap` and `Additive`.** Reason: `None` read as "no animation" rather than "tweens overlap without composing", and `Additive` is the standard term for summed animation. Rejected: keeping the upstream names `none` and `blend`.
- **Unset composition replaces.** With no `Composition` set anywhere, a tween composes with `Replace` (CORE-COMPOSITION-034). Reason: a new animation of a property then takes it over from the running one, so the latest call wins without the caller canceling anything; `Overlap` and `Additive` are opt-ins for effects that want both.

## Non-goals

- No per-frame weighting between additive tweens; deltas are summed.
- Discrete values cannot be additive.

## Open items

- CORE-COMPOSITION-024 (the additive pass overwriting a `Replace` write) is documented on `Composition.Additive` but has no core test.
- `Additive` tweens overriding later replace siblings (CORE-COMPOSITION-028) and the 1000-target `Overlap` default have no test.
