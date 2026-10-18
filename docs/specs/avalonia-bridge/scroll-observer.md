---
id: BRIDGE-SCROLLOBSERVER
title: Scroll observer
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto.Events
sources:
  - MonacoLabs.Presto.Avalonia/Events/ScrollObserver.cs
  - MonacoLabs.Presto.Avalonia/Events/ScrollObserverParams.cs
  - MonacoLabs.Presto.Avalonia/Events/ScrollContainer.cs
  - MonacoLabs.Presto.Avalonia/Events/ScrollThreshold.cs
  - MonacoLabs.Presto.Avalonia/Events/ScrollSync.cs
  - MonacoLabs.Presto.Avalonia/Events/ScrollSnap.cs
  - MonacoLabs.Presto.Avalonia/Events/ScrollAxis.cs
  - MonacoLabs.Presto.Avalonia/Events/ScrollWriter.cs
  - MonacoLabs.Presto.Avalonia/PrestoAvalonia.cs
types:
  - ScrollObserver
  - ScrollObserverParams
  - ScrollAxis
  - ScrollContainer
  - ScrollSnap
  - ScrollSync
  - ScrollThreshold
tests:
  - ScrollObserverTests
  - ScrollPinTests
  - ScrollSnapTests
  - ScrollSpikeTests
  - MotionValueSourceTests
  - ScrollStoryPageTests
  - ScopePruningTests
upstream:
  - src/events/scroll.js
  - tests/suites/scroll.test.js
depends:
  - BRIDGE-INIT
  - BRIDGE-TRANSFORMS
  - BRIDGE-SCROLL
  - CORE-TIMER
  - CORE-ANIMATION
  - CORE-TIMELINE
  - CORE-UTILS
  - CORE-SCOPE
  - CORE-MOTIONVALUE
  - CORE-EASEPARSER
---

# Scroll observer

## Purpose

Initially ported from anime.js `onScroll`: a `ScrollObserver` watches a control crossing enter and leave thresholds of its `ScrollViewer`, reports the crossings through callbacks and drives a `Link`ed timer, animation or timeline by playback methods or by seeking it to the scroll progress. On top of that it adds GSAP ScrollTrigger's pin (with spacing) and snap. The observers of one scroller share a `ScrollContainer`, which `Scroll.Speed` parallax, the `Scroll` edge cues, `PrestoAvalonia.ScrollValue` and `PrestoAvalonia.ScrollRemainingValue` also use (BRIDGE-SCROLL, BRIDGE-FOLLOW). Also specifies `PrestoAvalonia.OnScroll` and `PrestoAvalonia.RefreshScroll`.

## Requirements

### Creation and resolution

- **BRIDGE-SCROLLOBSERVER-001** — When `OnScroll` is called with no parameters, the observer shall use the default of every parameter.
  - Tests: `ScrollObserverTests.AnObserverWithoutATargetTakesTheLinkedAnimationsControl`
- **BRIDGE-SCROLLOBSERVER-002** — When an observer is created inside an ambient scope, the scope shall register it, so reverting the scope reverts it.
  - Tests: `ScrollObserverTests.AnObserverCreatedInsideAScopeRevertsWithIt`
- **BRIDGE-SCROLLOBSERVER-003** — The observer's `Id` shall be `ScrollObserverParams.Id` when set, and otherwise the number of observers created in the process so far, counting this one.
  - Tests: `ScrollObserverTests.TheObserverKeepsTheSuppliedId`
- **BRIDGE-SCROLLOBSERVER-004** — When `Container` is set, or the target already has a `ScrollViewer` ancestor, the observer shall join that scroller's shared `ScrollContainer` inside `OnScroll`, before it is `Ready`.
  - Tests: `ScrollObserverTests.CompletingTheResizeTickerBeforeTheObserverResolvesDoesNotThrow`
- **BRIDGE-SCROLLOBSERVER-005** — The observer shall resolve its target and container and measure its bounds on the next engine tick, not inside `OnScroll`; until then `Ready` is false and `Refresh` does nothing.
  - Tests: `ScrollObserverTests.RefreshBeforeTheObserverResolvesDoesNothing`
- **BRIDGE-SCROLLOBSERVER-006** — When no `Target` is set, the observer shall watch the first `Control` target of the linked `Animation`, or the first `Control` target among a linked `Timeline`'s child animations, read when `Link` is called, so children added to a linked `Timeline` afterwards are not seen.
  - Tests: `ScrollObserverTests.AnObserverWithoutATargetTakesTheLinkedAnimationsControl`, `ScrollObserverTests.AnObserverWithoutATargetTakesTheLinkedTimelinesFirstControl`
- **BRIDGE-SCROLLOBSERVER-007** — When the target is not loaded at resolution, the observer shall wait for its `Loaded` event and resolve then, finding its `ScrollViewer` ancestor at that point.
  - Tests: `ScrollObserverTests.AnObserverOfAnUnlaidOutTargetResolvesOnceItLoads`
- **BRIDGE-SCROLLOBSERVER-008** — The observer shall throw `InvalidOperationException` for a missing target or `ScrollViewer` from `OnScroll` when a loaded `Target` has no `ScrollViewer` ancestor and no `Container` is set, and from `Link` when an unresolved observer is left with no target or with such a target; otherwise from its resolution, at the target's `Loaded` or, for an observer never linked and without a target, on its first engine tick.
  - Tests: `ScrollObserverTests.ALoadedTargetOutsideAScrollViewerThrowsFromOnScrollAndLink`, `ScrollObserverTests.LinkingAnObjectWithNoControlThrowsFromLink`
- **BRIDGE-SCROLLOBSERVER-009** — When `Link` is called on an observer whose parameters name no target, resolved or not, the observer shall switch to the linked object's first control and refresh.
  - Tests: `ScrollObserverTests.LinkingAgainAfterResolutionSwitchesToTheNewLinkedControlAndRemeasures`

### Thresholds and bounds

- **BRIDGE-SCROLLOBSERVER-010** — The observer shall default to the vertical axis, `Repeat` true, `Enter` `"end start"` (the container's end meeting the target's start) and `Leave` `"start end"`.
  - Tests: `ScrollObserverTests.RefreshUpdatesTheBounds`, `ScrollObserverTests.RepeatFalseRevertsTheObserverAfterCompletion`
- **BRIDGE-SCROLLOBSERVER-011** — When `Axis` is `Horizontal`, the observer shall measure and read the horizontal offset, extent and sizes instead of the vertical ones.
  - Tests: `ScrollObserverTests.TheHorizontalAxisWatchesTheHorizontalOffset`
- **BRIDGE-SCROLLOBSERVER-012** — A threshold string shall be read as the container half followed by the target half; a missing target half shall default to the target's start for `Enter` and its end for `Leave`, and a `double` threshold shall be a container half in pixels.
  - Tests: `ScrollObserverTests.AKeywordThresholdIsMeasuredAgainstTheContainerSize`, `ScrollObserverTests.ABareNumberThresholdIsPixels`
- **BRIDGE-SCROLLOBSERVER-013** — Each half shall map `start`/`top`/`left`/`min` to 0, `end`/`bottom`/`right`/`max` to 100%, `center` to 50%, a percentage to that fraction of the container's viewport (container half) or the target's size (target half), and any other number to pixels, ignoring a unit other than `%`.
  - Tests: `ScrollObserverTests.AKeywordThresholdIsMeasuredAgainstTheContainerSize`, `ScrollObserverTests.APercentageThresholdIsThatFractionOfTheContainerSize`, `ScrollObserverTests.ABareNumberThresholdIsPixels`
- **BRIDGE-SCROLLOBSERVER-014** — When a half carries a relative operator (`"bottom-=100"`), the observer shall apply it to the value of the part before the operator.
  - Tests: `ScrollObserverTests.ARelativeOperatorMovesTheThresholdOffTheKeyword`
- **BRIDGE-SCROLLOBSERVER-015** — A container half of `max` shall move the threshold so the target never enters before scroll 0, and `min` so it never leaves past the last scroll position; with a relative operator, `min` and `max` shall keep whichever of the moved and the clamped value lies further inside.
  - Tests: `ScrollObserverTests.MaxClampsTheEnterThresholdToWhatTheContainerCanScroll`, `ScrollObserverTests.MinClampsTheLeaveThresholdToWhatTheContainerCanScroll`
- **BRIDGE-SCROLLOBSERVER-016** — The observer shall set `Offset` to the target's layout position in the scrolled content, `OffsetStart` to enter-target + `Offset` − enter-container, `OffsetEnd` to leave-target + `Offset` − leave-container, and `Distance` to `max(OffsetEnd − OffsetStart, 0)`, each threshold half rounded to a whole pixel.
  - Tests: `ScrollObserverTests.RefreshUpdatesTheBounds`
- **BRIDGE-SCROLLOBSERVER-017** — While measuring, the observer shall hold the linked object at time 0 without callbacks and restore its time afterwards, so the animation it drives cannot move its own thresholds.
  - Tests: `ScrollObserverTests.MeasuringHoldsTheLinkedAnimationAtItsStartWithoutCallbacksAndRestoresItsTime`
- **BRIDGE-SCROLLOBSERVER-018** — `Refresh` shall re-read `Axis`, `Repeat`, `Enter`, `Leave` and `Pin`, measure again and re-evaluate the scroll position.
  - Tests: `ScrollObserverTests.RefreshUpdatesTheBounds`
- **BRIDGE-SCROLLOBSERVER-019** — When the scroller's viewport or extent changes, its container shall refresh every `Ready` observer 250 ms (× `Globals.TimeScale`) after the last such change, invoking each one's `OnResize` after its refresh.
  - Tests: `ScrollObserverTests.OnResizeFiresOnEveryContainerRefresh`, `ScrollStoryPageTests.AResizeMidScrollReMeasuresThePinWithoutMovingTheScrubOffTheScroll`
- **BRIDGE-SCROLLOBSERVER-020** — `RefreshScroll` shall perform that refresh at once, and do nothing for a scroller no observer watches.
  - Tests: `ScrollObserverTests.RefreshScrollRemeasuresEveryObserverOfTheScrollerAtOnce`

### Scroll state

- **BRIDGE-SCROLLOBSERVER-021** — The container shall hold the scroller's offset rounded to whole pixels in `ScrollX`/`ScrollY`, updated in every offset change notification.
  - Tests: `ScrollObserverTests.TheHorizontalAxisWatchesTheHorizontalOffset`
- **BRIDGE-SCROLLOBSERVER-022** — The container shall sample `Velocity` (pixels per millisecond, rounded to 5 decimals) and `BackwardX`/`BackwardY` at 30 fps, changing an axis's backward flag only when that axis moved.
  - Tests: `ScrollObserverTests.TheEngineIdlesAfterTheWakeWindowAndWakesOnTheNextScroll`, `ScrollObserverTests.TheEnterAndLeaveCallbacksFireInBothDirections`
- **BRIDGE-SCROLLOBSERVER-023** — `Progress` shall be `(Scroll − OffsetStart) / Distance` clamped to 0–1 and rounded to 6 decimals, and 0 when `Distance` is 0.
  - Tests: `ScrollObserverTests.TheHorizontalAxisWatchesTheHorizontalOffset`, `ScrollObserverTests.ASyncedObserverSeeksTheLinkedAnimationToTheScrollProgress`
- **BRIDGE-SCROLLOBSERVER-024** — The container shall evaluate its observers every frame for 500 ms (× `Globals.TimeScale`) after the last offset change, then pause both the evaluation ticker and the 30 fps data timer so the engine can idle.
  - Tests: `ScrollObserverTests.TheEngineIdlesAfterTheWakeWindowAndWakesOnTheNextScroll`
- **BRIDGE-SCROLLOBSERVER-025** — While either axis is moving backward, the container shall evaluate its observers in reverse registration order.
  - Tests: `ScrollObserverTests.ObserversAreEvaluatedInReverseOrderWhileScrollingBackward`
- **BRIDGE-SCROLLOBSERVER-026** — `IsInView` shall be true only while `OffsetStart < Scroll < OffsetEnd`.
  - Tests: `ScrollObserverTests.TheEnterAndLeaveCallbacksFireInBothDirections`
- **BRIDGE-SCROLLOBSERVER-027** — `ProgressValue` shall be a `MotionValue` assigned `Progress` on every evaluation and left at its last value once the observer reverts.
  - Tests: `MotionValueSourceTests.ProgressValueMatchesTheObserverAcrossItsThresholds`, `MotionValueSourceTests.ObserverAndDraggableValuesFreezeOnRevert`

### Callbacks

- **BRIDGE-SCROLLOBSERVER-028** — When the target comes into view, the observer shall invoke `OnEnter` and then `OnEnterForward` or, while moving backward, `OnEnterBackward`.
  - Tests: `ScrollObserverTests.TheEnterAndLeaveCallbacksFireInBothDirections`, `ScrollStoryPageTests.EveryNamedThresholdReportsBothCrossingsInBothDirections`
- **BRIDGE-SCROLLOBSERVER-029** — When the target goes out of view, the observer shall invoke `OnLeave` and then `OnLeaveForward` or, while moving backward, `OnLeaveBackward`.
  - Tests: `ScrollObserverTests.TheEnterAndLeaveCallbacksFireInBothDirections`
- **BRIDGE-SCROLLOBSERVER-030** — When the scroll position lands exactly on a threshold before the target has ever entered, the observer shall run the enter callbacks once without setting `IsInView`.
  - Tests: `ScrollObserverTests.LandingExactlyOnAThresholdBeforeEverEnteringRunsTheEnterCallbacksOnceWithoutBeingInView`
- **BRIDGE-SCROLLOBSERVER-031** — The observer shall invoke `OnUpdate` on every evaluation while in view, on the evaluation that leaves the view, and on any evaluation that changes a seeking sync's progress.
  - Tests: `ScrollObserverTests.TheEnterAndLeaveCallbacksFireInBothDirections`
- **BRIDGE-SCROLLOBSERVER-032** — Within one evaluation, the observer shall run the enter callbacks, then the seek and `OnUpdate`, then the leave callbacks, then the completion check.
  - Tests: `ScrollObserverTests.AnEvaluationRunsTheEnterCallbacksThenUpdateThenTheLeaveCallbacksThenTheCompletion`, `ScrollObserverTests.ASeekingSyncSeeksTheLinkedAnimationBeforeOnUpdate`
- **BRIDGE-SCROLLOBSERVER-033** — When progress reaches 1 after rising above 0 since the scroll was last before `OffsetStart`, and the sync has completed (the linked progress was 1 for a seeking sync, the target left the view for a method or eased sync, at once with no sync), the observer shall complete once, invoking `OnSyncComplete` when a sync mode is in force, and complete again only after progress drops below 1.
  - Tests: `ScrollObserverTests.RepeatFalseRevertsTheObserverAfterCompletion`
- **BRIDGE-SCROLLOBSERVER-034** — When `Repeat` is false and the observer completes with no linked object or a completed one, the observer shall revert itself.
  - Tests: `ScrollObserverTests.RepeatFalseRevertsTheObserverAfterCompletion`

### Link and sync

- **BRIDGE-SCROLLOBSERVER-035** — `Link` shall pause the given object, keep it as `Linked` and return the observer.
  - Tests: `ScrollObserverTests.LinkPausesWhatItIsGivenAndKeepsIt`
- **BRIDGE-SCROLLOBSERVER-036** — When the linked object reverts, the observer shall revert with it.
  - Tests: `ScrollObserverTests.RevertingTheLinkedAnimationRevertsTheObserver`, `ScrollPinTests.RevertingTheLinkedAnimationReleasesThePin`
- **BRIDGE-SCROLLOBSERVER-037** — By default (`"play pause"`), the observer shall call the linked object's `Play` on enter and `Pause` on leave.
  - Tests: `ScrollObserverTests.TheDefaultSyncPlaysTheLinkedObjectOnEnterAndPausesItOnLeave`
- **BRIDGE-SCROLLOBSERVER-038** — A method sync shall accept `play`, `pause`, `resume`, `restart`, `reverse`, `alternate`, `complete`, `cancel`, `revert` and `reset` in any case, with one or two names meaning enter and leave, three or four meaning enter-forward, leave-forward, enter-backward and leave-backward, and an unknown or missing name doing nothing.
  - Tests: `ScrollObserverTests.AFourNameSyncRunsItsBackwardPairWhenTheScrollTurnsAround`
- **BRIDGE-SCROLLOBSERVER-039** — When `Sync` is `true`, `"linear"` or `Eases.None`, the observer shall have `SyncSmooth` 1 and seek the linked object to `Duration × Progress` on each update.
  - Tests: `ScrollObserverTests.SyncTrueIsASmoothSyncOfOne`, `ScrollObserverTests.SyncLinearIsASmoothSyncOfOne`, `ScrollObserverTests.ASyncedObserverSeeksTheLinkedAnimationToTheScrollProgress`
- **BRIDGE-SCROLLOBSERVER-040** — When `Sync` is a number below 1, the observer shall move the linked progress each evaluation by `Lerp(.01, .2, smoothing)` of the way to the scroll progress, nudged by .0001 at the ends, and keep the container awake while it converges.
  - Tests: `ScrollObserverTests.SyncANumberIsThatSmoothing`
- **BRIDGE-SCROLLOBSERVER-041** — When `Sync` is an `Ease` or a string `EaseParser.TryParse` accepts as an ease other than `Eases.None`, the observer shall set `SyncEase`, leave `SyncSmooth` null and seek the linked object to the eased progress.
  - Tests: `ScrollObserverTests.SyncAnEaseNameIsAnEasedSync`
- **BRIDGE-SCROLLOBSERVER-042** — When `Sync` is `false`, the observer shall report `Sync` false and never drive the linked object.
  - Tests: `ScrollObserverTests.SyncFalseNeverDrivesTheLinkedObject`

### Pin

- **BRIDGE-SCROLLOBSERVER-043** — When `Pin` is true, the observer shall write `Clamp(Scroll − OffsetStart, 0, Distance)` to the target's `"Y"` shorthand (`"X"` when horizontal) inside the scroller's offset notification, so the target stays put through its range without a frame of lag.
  - Tests: `ScrollPinTests.APinnedTargetStaysPutBetweenItsThresholdsWithoutAFrameOfLag`, `ScrollPinTests.BeforeTheRangeThePinnedTargetScrollsNormally`, `ScrollPinTests.AfterTheRangeThePinnedTargetRidesAtThePinnedDistance`, `ScrollPinTests.TheHorizontalAxisPinsAlongX`
- **BRIDGE-SCROLLOBSERVER-044** — While measuring, the observer shall lift the pin to 0 and re-apply it afterwards; when a refresh changes the axis, it shall revert the old shorthand.
  - Tests: `ScrollPinTests.ARefreshAfterAResizeKeepsThePinWithoutAccumulatingTheSpacer`
- **BRIDGE-SCROLLOBSERVER-045** — While pinned with `PinSpacing` not false, the observer shall set the target's trailing margin (bottom, or right when horizontal) at `Animation` priority to the margin it had when pinning began plus `Distance`.
  - Tests: `ScrollPinTests.PinSpacingPushesTheFollowingContentBackByTheDistance`, `ScrollPinTests.WithoutPinSpacingTheFollowingContentStays`, `ScrollPinTests.ARefreshAfterAResizeKeepsThePinWithoutAccumulatingTheSpacer`
- **BRIDGE-SCROLLOBSERVER-046** — A linked animation of a descendant of the pinned target shall still be seeked by the scroll.
  - Tests: `ScrollPinTests.ALinkedAnimationOfAChildOfThePinnedTargetStillScrubs`, `ScrollStoryPageTests.ThePinnedScrubIsSeekedToTheScrollPositionInBothDirections`

### Snap

- **BRIDGE-SCROLLOBSERVER-047** — When `Snap` is set, every offset change the snap did not write shall cancel a running snap and restart the wait, and the snap shall start once the offset has not changed for `Delay` (default 150 ms × `Globals.TimeScale`).
  - Tests: `ScrollSnapTests.AnIncrementSnapEasesToTheNearestStepOnceSettledLandsExactlyAndLetsTheEngineIdle`, `ScrollSnapTests.AUserScrollMidSnapStopsItAndANewSnapStartsFromThereOnceSettled`
- **BRIDGE-SCROLLOBSERVER-048** — The snap shall not start while a pointer is pressed on the scroller, scrollbars included, and the pointer's release shall restart the wait.
  - Tests: `ScrollSnapTests.TheSnapWaitsWhileAPointerIsPressedOnTheScroller`
- **BRIDGE-SCROLLOBSERVER-049** — The snap shall not start while the offset lies outside `[OffsetStart, OffsetEnd]`, while `Distance` is 0, or while a `"ScrollX"`/`"ScrollY"` tween holds the observer's axis (see BRIDGE-SCROLL).
  - Tests: `ScrollSnapTests.NothingSnapsOutsideTheThresholds`, `ScrollToTests.ASnapWaitsForTheScrollToAndSnapsAfterItsDelayOnceItEnds`
- **BRIDGE-SCROLLOBSERVER-050** — The snap target shall be `OffsetStart + map(progress) × Distance` clamped to the scrollable range, with progress taken from the unrounded offset; a double maps to its nearest multiple, a function or `ChainedUtil` to what it returns.
  - Tests: `ScrollSnapTests.AnIncrementSnapEasesToTheNearestStepOnceSettledLandsExactlyAndLetsTheEngineIdle`, `ScrollSnapTests.AFunctionSnapLandsOnWhatTheFunctionReturns`
- **BRIDGE-SCROLLOBSERVER-051** — `ScrollSnap.Labels` shall snap to 0, 1 and each label of a linked `Timeline` as a fraction of its duration, and leave the offset alone when the linked object is not a timeline with a positive duration.
  - Tests: `ScrollSnapTests.LabelSnappingLandsOnTheLinkedTimelinesLabelsFromEitherSide`, `ScrollStoryPageTests.ScrollingThatStopsBetweenTwoLabelsSettlesOnTheNearerOne`
- **BRIDGE-SCROLLOBSERVER-052** — The snap shall ease the offset over `Duration` (default 400 ms × `Globals.TimeScale`) on `Ease` (default `OutQuad`) with `SetCurrentValue`, land exactly on its last frame, and let the engine idle afterwards.
  - Tests: `ScrollSnapTests.AnIncrementSnapEasesToTheNearestStepOnceSettledLandsExactlyAndLetsTheEngineIdle`
- **BRIDGE-SCROLLOBSERVER-053** — When an extent shrink clamps the snap's own write, the snap shall treat the change as foreign, stop and snap again within the new range once settled.
  - Tests: `ScrollSpikeTests.ASnapTreatsAClampOfItsOwnWriteAsForeignAndSnapsAgainWithinTheNewRange`
- **BRIDGE-SCROLLOBSERVER-059** — While the observer's engine has `ReducedMotion` or `ReduceAll` set when a snap starts, the snap shall write its target at once, after the usual `Delay`, instead of easing, and let the engine idle afterwards.
  - Tests: `ScrollSnapTests.AReducedSnapJumpsToItsStepOnceSettledAndLetsTheEngineIdle`

### Revert and lifetime

- **BRIDGE-SCROLLOBSERVER-054** — `Revert` shall release the pin and its spacer, cancel the snap, leave its container, set `Reverted` and clear `Ready`; a second call shall do nothing.
  - Tests: `ScrollPinTests.RevertRestoresTheUsersTransformAndMargin`, `ScrollSnapTests.RevertMidSnapStopsIt`
- **BRIDGE-SCROLLOBSERVER-055** — `Revert` shall leave the linked object as it is.
  - Tests: `ScrollObserverTests.RevertLeavesTheLinkedAnimationAsItIs`
- **BRIDGE-SCROLLOBSERVER-060** — Once reverted, the observer shall count as released to the scope it registered with (CORE-SCOPE).
  - Tests: `ScopePruningTests.RevertedBridgeRevertiblesArePrunedFromTheirScope`
- **BRIDGE-SCROLLOBSERVER-056** — When its last observer, scroll value and parallax element have left, a container shall detach from its scroller, cancel its timers and leave the registry, so the next user creates a fresh one.
  - Tests: `ScrollObserverTests.RepeatFalseRevertsTheObserverAfterCompletion`, `ScrollObserverTests.RevertingTheLinkedAnimationRevertsTheObserver`
- **BRIDGE-SCROLLOBSERVER-061** — The scroller-to-container registry shall not keep a scroller alive: a scroller that leaves the tree with unreverted observers shall be collectable with its container and observers.
  - Tests: `ScrollObserverTests.AScrollerDroppedWithAnUnrevertedObserverIsCollectable`

### Defaults

- **BRIDGE-SCROLLOBSERVER-057** (inherited) — When `Pin` is not set, the observer shall not pin its target.
  - Tests: `ScrollObserverTests.WithoutPinOrSnapTheObserverLeavesTheTargetAndTheOffsetAlone`
- **BRIDGE-SCROLLOBSERVER-058** (inherited) — When `Snap` is not set, the observer shall never move the scroller's offset.
  - Tests: `ScrollObserverTests.WithoutPinOrSnapTheObserverLeavesTheTargetAndTheOffsetAlone`

## Invariants

- At most one `ScrollContainer` exists per `ScrollViewer`.
- `Distance >= 0`, `0 <= Progress <= 1`, and `IsInView` implies `OffsetStart < Scroll < OffsetEnd`.
- A pinned target's pin shorthand lies in `[0, Distance]` after every offset notification.
- Snap writes go through the shared per-scroller `ScrollWriter`, which records the owner and the clamped value of each write.

## Edge cases

- A target created during the layout pass of the frame the observer resolves in waits for `Loaded` (007).
- Completing the container's resize ticker before the observer resolves does not throw (`ScrollObserverTests.CompletingTheResizeTickerBeforeTheObserverResolvesDoesNotThrow`).
- An observer can revert itself from inside a callback; the container's loop re-reads its list, so no observer is skipped or run twice.
- Thresholds that cross give `Distance` 0, so `Progress` is 0 and no snap runs.
- Linking a `Timeline` that has no children yet throws from `Link` (008), since its first control is read then.
- `Sync = 0d` reports `Sync` true but seeks nothing.
- A sync string that is neither a method name nor an ease (for example `"none"`) is a method sync that does nothing.

## Threading and lifetime

- UI thread only. Resolution, evaluation and snapping run on the engine tick; pin updates run in the scroller's `Offset` property-changed notification.
- The container keeps its observers alive through the scroller's event handlers until they revert, but only while something else holds the scroller: the registry holds it weakly (061). A linked object's revert reverts the observer; the observer never reverts the linked object.

## Reduced motion

- The observer follows the user's own scroll and is not affected. Under `ReducedMotion` or `ReduceAll`, read as each snap starts, the snap jumps to its target after its `Delay` (059).

## Allocation

- Evaluation (`HandleScroll`) and pin updates allocate nothing per frame; thresholds are parsed into a shared static `DecomposedValue`. Not covered by an allocation test.

## Provenance

- `Link` replaces upstream's `autoplay: onScroll()`: the linked object is created paused and handed over, with the lifetimes still tied, because `TimerParams.Autoplay` is a `bool?` and cannot take an observer.
- Upstream's `debug` overlay and the function forms of `axis`, `repeat`, `enter` and `leave` are not ported; `Refresh` re-reads the values instead.
- The data timer pauses with the wake window instead of running forever, so the engine can stop the frame source while an idle observer is alive.
- Pin, `PinSpacing` and `Snap` have no upstream counterpart; they follow GSAP ScrollTrigger, since anime.js 4.5.0 has no pinning or snapping. `PinSpacing` grows a margin instead of inserting a spacer element, so the tree is untouched.
- CSS units other than `%` have no equivalent and are read as their bare number.

## Decisions

- **Pin, spacing and snap follow GSAP ScrollTrigger, layered on the ported observer.** They sit on top of `ScrollObserver` without changing its observer behavior, so the observer tests pass unchanged; `Scroll.Speed` and `Scroll.Smooth` likewise follow ScrollSmoother (BRIDGE-SCROLL). Reason: ScrollTrigger and ScrollSmoother are the established models for pinning, snapping, parallax and wheel smoothing, which the observer lacked.
- **The data timer pauses with the wake window.** Reason: the engine then has no children and can stop the frame source while an idle observer is alive. Rejected: upstream's always-running data timer.
- **The pin writes its own shorthand inside the `Offset` notification.** The pin owns the target's `"Y"` (or `"X"`) shorthand, so a linked animation should drive the target's descendants, and it is lifted while bounds are measured. Reason: written synchronously in the notification rather than on the next scroll tick, it never trails the content by a frame; it is lifted because `TranslatePoint` includes the render transform.
- **`PinSpacing` is a trailing margin, not a spacer element.** It is recomputed from the margin the target had when pinning began. Reason: the tree is untouched, the margin lies outside `Bounds` so it never moves the thresholds it is sized from, and recomputing keeps a refresh from accumulating it. Rejected: GSAP's spacer element wrapping the target.
- **The snap eases `Offset` with `SetCurrentValue` after a settle wait.** It starts after `Delay` with no offset change and no pointer down, inside the thresholds, and any offset change it did not write cancels it and restarts the wait. Reason: the write stays at the offset's local priority, below `Animation`, so the scroller's own input still moves it, and restarting on foreign changes means a snap starts only once a smooth scroll settles.
- **A reduced scroll snap jumps.** Under `ReducedMotion` or `ReduceAll` the snap writes its target at once after the settle wait. Reason: the snap moves the viewport on the user's behalf, the motion reduced motion exists to stop, and jumping keeps the snap's position contract. Rejected: dropping the snap; a configurable reduced duration.
- **The snap waits on stillness alone.** It starts after `Delay` with no offset change; it does not also wait for `Velocity` to settle. Reason: every offset change restarts the wait, so `Delay` of stillness already means the velocity is zero. Rejected: a velocity threshold on top.
- **A missing target or `ScrollViewer` throws where it is first known.** `OnScroll` throws for a loaded `Target` in no `ScrollViewer` without a `Container`, and `Link` throws on an unresolved observer left with no target or with such a target; only an unloaded target (at its `Loaded`) or an observer never linked and without a target (on its first tick) throws at resolution. Reason: an exception from an engine tick points nowhere near the mistake. Rejected: resolving inside `OnScroll`, since an observer created before the animation it watches takes its target from `Link`.
- **`ScrollSnap.Labels` maps labels linearly onto progress.** A timeline lands on a label exactly only under a linear `Sync`. Reason: not recorded.
- **A snap waits for a running scroll-to.** A scroll tween's first write cancels a running snap, the snap does not start while a tween holds the axis, and the tween restarts every observer's wait when it lets go, so the snap comes its `Delay` after the scroll-to ends. Reason: not recorded.
- **Scroll velocity needs no API of its own.** It is `PrestoAvalonia.ScrollValue(scroller).VelocityOf()`. Reason: the motion value graph already provides it.
- **One `ScrollWriter` tells own writes, clamps and foreign writes apart.** See BRIDGE-SCROLL.
- **The core owns the cell, the bridge owns the sources.** See CORE-MOTIONVALUE.
- **The container registry is weak.** Scroller to container is a `ConditionalWeakTable`, like the bridge's other per-visual tables. Reason: an observer has no detach handling and leaves only on `Revert`, so one unreverted observer created outside a scope rooted its scroller and the tree under it for the life of the process. Rejected: reverting observers on detach, which needs a behavior decision of its own.

## Non-goals

- Smoothed wheel scrolling and parallax (BRIDGE-SCROLL), scroll reveals (BRIDGE-REVEAL), scroll `MotionValue`s (BRIDGE-FOLLOW).
- Observing anything but a `ScrollViewer`.

## Open items

None.
