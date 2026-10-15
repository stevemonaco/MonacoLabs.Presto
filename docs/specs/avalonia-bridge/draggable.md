---
id: BRIDGE-DRAGGABLE
title: Draggable
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto.Draggables
sources:
  - MonacoLabs.Presto.Avalonia/Draggables
  - MonacoLabs.Presto.Avalonia/Gestures/GestureOwners.cs
  - MonacoLabs.Presto.Avalonia/PrestoAvalonia.cs
types:
  - Draggable
  - DraggableParams
  - DraggableAxisParams
  - DraggableCursorParams
tests:
  - DraggableTests
  - MotionValueSourceTests
  - DevToolsOriginTests
  - ReorderContainerTests
  - TransformableTests
  - TweenListIntegrityTests
upstream:
  - src/draggable/draggable.js
  - tests/suites/draggables.test.js
depends:
  - CORE-ANIMATABLE
  - CORE-INERTIA
  - CORE-SPRING
  - CORE-COMPOSITION
  - CORE-MOTIONVALUE
  - CORE-SCOPE
  - CORE-DIAGNOSTICS
  - BRIDGE-TRANSFORMS
  - BRIDGE-TARGETS
  - BRIDGE-HELPERS
---

# Draggable

## Purpose

`Draggable`, initially ported from anime.js `createDraggable`, is single-pointer dragging of a control, written through a core `Animatable` (CORE-ANIMATABLE) over two mapped axis properties, with a drag threshold, per-axis enabling, snapping, modifiers, container bounds and friction, edge auto-scroll of the nearest `ScrollViewer`, and a release that throws the target on with the pointer's velocity, plus an opt-in inertia release (CORE-INERTIA). `Transformable` (BRIDGE-TRANSFORMABLE) is its multi-touch counterpart; `Pager`, `Reorder` and other controls reuse its edge-scroll defaults, and its 3-DIP default threshold is one of the shared drag thresholds in BRIDGE-GESTURES. Also specifies `PrestoAvalonia.CreateDraggable`.

## Requirements

### Creation and ownership

- **BRIDGE-DRAGGABLE-001** — When `CreateDraggable` is called, the draggable shall capture the current engine and register itself with that engine's current `Scope`, so a scope revert reverts it.
  - Tests: untested
- **BRIDGE-DRAGGABLE-002** — When `CreateDraggable` is called on a target a live `Draggable`, `Transformable` or swipe tracker drives, it shall throw `InvalidOperationException` naming both types; once the other owner is reverted, creation shall succeed.
  - Tests: `TransformableTests.ADraggableAndATransformableCannotShareATargetUntilOneIsReverted`, `TransformableTests.ASecondGestureOfTheSameTypeCannotShareATargetUntilTheFirstIsReverted`
- **BRIDGE-DRAGGABLE-003** — The draggable shall write its axes to the `"X"` and `"Y"` transform shorthands unless an axis `MapTo` names another property, and those writes shall compose with other transform writers on the target (BRIDGE-TRANSFORMS).
  - Tests: `DraggableTests.DraggingMovesTheTargetByThePointerDelta`, `ReorderContainerTests.ADraggablesTranslationAddsToTheFlipThatIsEasingTheSameContainer`
- **BRIDGE-DRAGGABLE-004** — When an axis maps to a layout property such as `"Canvas.Left"`, the draggable shall start from that property's current value and drag it, leaving the other mapped property's value in place.
  - Tests: `DraggableTests.AnAxisCanBeMappedToALayoutProperty`
- **BRIDGE-DRAGGABLE-005** — When created, the draggable shall measure its bounds and clamp the current axis values into them without raising `OnUpdate`.
  - Tests: `DraggableTests.AContainerClampsToItsBoundsMinusPadding`
- **BRIDGE-DRAGGABLE-006** — The draggable's own timers and animations shall carry the diagnostics origin `"Draggable"`.
  - Tests: `DevToolsOriginTests.ADraggableReleaseAnimationCarriesDraggable`

### Grabbing and dragging

- **BRIDGE-DRAGGABLE-007** — When a pointer is pressed on the trigger and no drag is in progress, the draggable shall mark the press handled, capture the pointer on the trigger, stop any running release, re-measure the bounds, reset `Velocity` and `Angle` to 0 and raise `OnGrab` before any movement.
  - Tests: `DraggableTests.OnUpdateOnlyFiresWhenTheTargetMoves`
- **BRIDGE-DRAGGABLE-008** (inherited) — While a drag is in progress, the draggable shall ignore further presses and moves from any pointer other than the one that grabbed.
  - Tests: untested
- **BRIDGE-DRAGGABLE-009** (inherited) — When the trigger's press is already handled by the time it reaches the trigger (for example a `ListBoxItem`), the draggable shall never grab.
  - Tests: `ReorderContainerTests.ADraggableOnAListBoxItemNeverGrabsBecauseTheItemHandlesThePress`
- **BRIDGE-DRAGGABLE-010** — While the pointer has moved no more than `DragThreshold` from the grab point on every enabled axis, the draggable shall not move the target or raise `OnDrag`.
  - Tests: `DraggableTests.MovementBelowTheDragThresholdIsSuppressed`
- **BRIDGE-DRAGGABLE-011** — Once a move passes the threshold, the draggable shall raise `OnDrag` on that move and on every later move of the same drag.
  - Tests: `DraggableTests.OnUpdateOnlyFiresWhenTheTargetMoves`, `DraggableTests.MovementBelowTheDragThresholdIsSuppressed`
- **BRIDGE-DRAGGABLE-012** — While dragging, the draggable shall move each enabled axis by the pointer's delta since the grab, applied on the next engine frame.
  - Tests: `DraggableTests.DraggingMovesTheTargetByThePointerDelta`, `DraggableTests.OnUpdateFiresForEveryHorizontalStep`
- **BRIDGE-DRAGGABLE-013** — When an axis has `Enabled = false`, the draggable shall never write that axis's property, and its `X`/`Y` setter shall be ignored for that axis.
  - Tests: `DraggableTests.ADisabledAxisStaysPut`
- **BRIDGE-DRAGGABLE-014** — The draggable shall raise `OnUpdate` once per axis write that actually changed the rendered position, and never on a frame where the target did not move.
  - Tests: `DraggableTests.OnUpdateOnlyFiresWhenTheTargetMoves`, `DraggableTests.OnUpdateFiresForEveryHorizontalStep`
- **BRIDGE-DRAGGABLE-015** — While the trigger is not grabbed, the draggable shall show the hover cursor while the pointer is over the trigger and restore the trigger's own cursor when it leaves; while grabbed it shall show the grab cursor.
  - Tests: `DraggableTests.TheCursorFollowsTheDragAndRevertPutsItBack`
- **BRIDGE-DRAGGABLE-016** — When `Cursor.Enabled` is false, the draggable shall leave the trigger's cursor untouched.
  - Tests: untested
- **BRIDGE-DRAGGABLE-017** — When the grab happens, the draggable shall scroll the nearest `ScrollViewer` so the target is in view, over 100 ms with `Eases.Out(3)`.
  - Tests: untested

### Bounds and friction

- **BRIDGE-DRAGGABLE-018** — When `Container` is set, the bounds shall be the positions at which the target's box stays inside the container's box inset by `ContainerPadding`, measured from the target's layout offset within the container.
  - Tests: `DraggableTests.AContainerClampsToItsBoundsMinusPadding`
- **BRIDGE-DRAGGABLE-019** — When `ContainerBounds` is set, the bounds shall be the returned rect inset by `ContainerPadding`, re-read on every measurement, taking precedence over `Container`.
  - Tests: untested
- **BRIDGE-DRAGGABLE-020** — When neither `Container` nor `ContainerBounds` is set, the axes shall be unbounded.
  - Tests: `DraggableTests.ReleasingWithVelocityThrowsTheTargetOnAndSettles`
- **BRIDGE-DRAGGABLE-021** — While dragging past a bound, the draggable shall move the target by the excess scaled by `1 − ContainerFriction`, so friction 1 pins it to the bound and 0 drags straight through.
  - Tests: `DraggableTests.AContainerClampsToItsBoundsMinusPadding`, `DraggableTests.AnOutOfBoundsReleaseEasesBackIntoTheContainer`
- **BRIDGE-DRAGGABLE-022** — When a drag is grabbed while the target sits past a bound, the draggable shall continue from the raw coordinate the cushioned position came from, so the target does not jump.
  - Tests: untested
- **BRIDGE-DRAGGABLE-023** (inherited) — When the container leaves the visual tree mid-drag, the draggable shall keep its last measured bounds and not throw, including on a later `Refresh` and `Revert`.
  - Tests: `DraggableTests.RemovingTheParentMidDragDoesNotThrow`
- **BRIDGE-DRAGGABLE-024** — When the target or the container changes size, the draggable shall wait 150 ms × `Globals.TimeScale` of quiet (restarting on each change), then raise `OnResize`, call `Refresh`, then raise `OnAfterResize`.
  - Tests: `DraggableTests.RevertingDetachesTheResizeAndPointerHandlers`, `DraggableTests.ResizeRaisesOnResizeWithinTheDebounceInSecondsMode`

### Snapping and modifiers

- **BRIDGE-DRAGGABLE-025** — The draggable shall snap each axis with its own `DraggableAxisParams.Snap` when set, otherwise with `DraggableParams.Snap`.
  - Tests: `DraggableTests.AnInertiaReleaseDecaysOntoEachAxisSnap`
- **BRIDGE-DRAGGABLE-026** — While dragging, the draggable shall not snap the drawn position, but shall raise `OnSnap` on each frame where the snapped value of a snapping axis changed.
  - Tests: `DraggableTests.SnapQuantizesTheReleaseAndReportsEveryStep`
- **BRIDGE-DRAGGABLE-027** — When released, the draggable shall snap and clamp the destination, rounded to 5 decimals, and raise `OnSnap` before `OnRelease` only if that destination differs from the last snapped value of a snapping axis.
  - Tests: `DraggableTests.SnapQuantizesTheReleaseAndReportsEveryStep`
- **BRIDGE-DRAGGABLE-028** — The draggable shall apply an axis `Modifier` to that axis in place of `DraggableParams.Modifier`, through the animatable's modifier; a modifier must be idempotent because it is re-applied to its own output from the release on.
  - Tests: untested

### Release (projection, the upstream default)

- **BRIDGE-DRAGGABLE-029** — When the grabbing pointer is released or its capture is lost, the draggable shall end the drag once, release the capture and show the hover cursor.
  - Tests: `DraggableTests.TheCursorFollowsTheDragAndRevertPutsItBack`
- **BRIDGE-DRAGGABLE-030** — The draggable shall measure `Velocity` as the largest of the last three accepted samples (taken at least 17 ms apart in dilated engine time) of path length per ms, scaled by `VelocityMultiplier` and clamped to `[MinVelocity, MaxVelocity]`.
  - Tests: `DraggableTests.ReleasingWithVelocityThrowsTheTargetOnAndSettles`, `DraggableTests.VelocityIsMeasuredInDilatedEngineTime`
- **BRIDGE-DRAGGABLE-031** — The velocity shall be the strongest of the last three samples, zero seeds included, so a negative multiplier with a negative `MinVelocity` reports a negative velocity from the first sample.
  - Tests: `DraggableTests.ANegativeMultiplierReportsANegativeVelocityAtOnce`
- **BRIDGE-DRAGGABLE-032** — When released without `ReleaseInertia`, the draggable shall throw each enabled axis `Velocity × 150` units along the release `Angle`, cushion the throw past the bounds by `1 − ReleaseContainerFriction`, then snap and clamp it as the destination.
  - Tests: `DraggableTests.ReleasingWithVelocityThrowsTheTargetOnAndSettles`
- **BRIDGE-DRAGGABLE-033** — When `ReleaseEase` is not a `Spring`, each axis shall ease to its destination with `ReleaseEase` over the settling duration of a spring (`ReleaseMass`, `ReleaseStiffness`, `ReleaseDamping`, launched at `Velocity`) minus the spring's rest duration.
  - Tests: `DraggableTests.ReleasingWithVelocityThrowsTheTargetOnAndSettles`
- **BRIDGE-DRAGGABLE-034** — When `ReleaseEase` is a `Spring`, it shall replace the release springs and be used as the ease over its full settling duration, and it shall land on the same coordinates an eased release does.
  - Tests: `DraggableTests.AReleaseEaseAndAReleaseSpringSettleAtTheSameCoordinates`
- **BRIDGE-DRAGGABLE-035** — When the throw lands past a bound, no release spring is supplied and the release friction is below 1, the draggable shall play two additive tweens on an internal target: out to the cushioned point over 65 % of the duration, and back to the clamped destination over the full duration.
  - Tests: `DraggableTests.AnOutOfBoundsReleaseEasesBackIntoTheContainer`
- **BRIDGE-DRAGGABLE-036** — When a release moves only one axis, or every axis duration is 0, the draggable shall raise `OnSettle` once, after both axes are written.
  - Tests: `DraggableTests.AReleaseThatOnlyMovesOneAxisSettlesOnceBothAreWritten`
- **BRIDGE-DRAGGABLE-037** — When released, the draggable shall raise `OnRelease` after the release animations exist, so they can be canceled from it, and shall scroll the nearest `ScrollViewer` so the destination sits `ScrollThreshold` inside its viewport over the longest axis duration.
  - Tests: `DraggableTests.ReleasingWithVelocityThrowsTheTargetOnAndSettles`
- **BRIDGE-DRAGGABLE-038** — While the release animates, `Velocity` and `Angle` shall report the target's own motion; once the axes have nothing left to play the draggable shall reset them to 0 and raise `OnSettle` once.
  - Tests: `DraggableTests.ReleasingWithVelocityThrowsTheTargetOnAndSettles`, `MotionValueSourceTests.DraggableValuesFollowADragAndTheReleaseSpring`
- **BRIDGE-DRAGGABLE-039** (inherited) — When `OnSettle` calls `Reset`, both axes shall end at 0 and a new drag begun from inside it shall not be taken for the tail of the previous one.
  - Tests: `DraggableTests.ResettingInsideOnSettleLeavesBothAxesAtZero`, `DraggableTests.AReleaseThatOnlyMovesOneAxisSettlesOnceBothAreWritten`

### Release (inertia, opt-in)

- **BRIDGE-DRAGGABLE-040** — When `ReleaseInertia` is set, the release shall fling each enabled axis through `Animatable.Fling` at `Velocity·cos(Angle)` and `Velocity·sin(Angle)`, with the bounds as `Min`/`Max` and the axis snap as `ModifyTarget` unless the template sets its own, ignoring the template's `Velocity`, `Min` and `Max`, `ReleaseEase`, the release springs and `ReleaseContainerFriction`.
  - Tests: `DraggableTests.AnInertiaReleaseDecaysOntoEachAxisSnap`
- **BRIDGE-DRAGGABLE-041** — When an inertia release crosses a bound, the axis shall bounce past it and settle on the bound.
  - Tests: `DraggableTests.AnInertiaReleaseBouncesOffTheContainerAndSettlesOnItsBound`
- **BRIDGE-DRAGGABLE-042** — When `ReleaseInertia` is set and an axis is disabled, the release shall leave that axis unwritten.
  - Tests: `DraggableTests.AnInertiaReleaseLeavesADisabledAxisAlone`

### Refresh, retargeting and auto-scroll

- **BRIDGE-DRAGGABLE-043** — When `Refresh` is called, the draggable shall re-read padding, snaps, threshold, frictions, velocity limits, scroll settings and cursors, re-measure the bounds and, with no release in flight, clamp both axes into them without raising `OnUpdate`.
  - Tests: `DraggableTests.AContainerClampsToItsBoundsMinusPadding`
- **BRIDGE-DRAGGABLE-044** — When `Refresh` runs while a projection release is in flight and the snapped, clamped destination changed, the release shall be re-aimed there over its remaining time (canceling any overshoot) and `OnSettle` shall still fire once.
  - Tests: `DraggableTests.AContainerResizedMidThrowRetargetsTheReleaseAndSettlesOnce`
- **BRIDGE-DRAGGABLE-045** — When `Refresh` runs mid inertia release and the resting point falls outside the new bounds, the release shall be re-flung from the current value and velocity and `OnSettle` shall fire once.
  - Tests: `DraggableTests.ARefreshMidInertiaReleaseResolvesItAgainAndSettlesOnce`
- **BRIDGE-DRAGGABLE-046** — While dragging with the pointer inside the `ScrollThreshold` band at an edge of the nearest `ScrollViewer`'s viewport, the draggable shall scroll it each frame by up to `ScrollSpeed × 10`, ramping across the band, clamped to the extent, and move the target with the content; it shall stop on release.
  - Tests: `DraggableTests.AutoScrollAdvancesTheScrollViewerWhileThePointerSitsInTheEdgeBand`
- **BRIDGE-DRAGGABLE-047** — When `ScrollSpeed` or `ScrollThreshold` is 0, or a `ScrollInView` animation is running, the draggable shall not auto-scroll.
  - Tests: untested
- **BRIDGE-DRAGGABLE-048** — When `ScrollInView` is called and the target's destination is less than `gap` inside the viewport, the draggable shall animate the scroller's offset to bring it in, clamped to the extent; otherwise it shall do nothing.
  - Tests: untested
- **BRIDGE-DRAGGABLE-049** — When `AnimateInView` is called, the draggable shall stop, re-measure and move the target itself until it is `gap` inside the scroller's viewport, snapped and clamped like a release.
  - Tests: `DraggableTests.AnimateInViewMovesTheTargetBackIntoTheScrollViewersViewport`

### Escape

- **BRIDGE-DRAGGABLE-050** (inherited) — When `CancelOnEscape` is true and Escape is pressed mid-drag, the draggable shall handle the key, end the drag, spring the target back to its value at the grab, raise `OnRelease` once and `OnSettle` once, and ignore the later pointer release.
  - Tests: `DraggableTests.EscapeMidDragSpringsTheTargetBackToWhereItWasGrabbed`
- **BRIDGE-DRAGGABLE-051** (inherited) — When `CancelOnEscape` is false, Escape shall not affect the drag.
  - Tests: `DraggableTests.EscapeIsIgnoredWithoutCancelOnEscape`

### Values and progress

- **BRIDGE-DRAGGABLE-052** — `X` and `Y` shall read the animatable's current value rounded to the engine's precision; setting them shall write that axis at once (rounded to 5 decimals), unclamped, and raise `OnUpdate` if it moves.
  - Tests: `DraggableTests.EscapeMidDragSpringsTheTargetBackToWhereItWasGrabbed`, `ReorderContainerTests.ADraggablesTranslationAddsToTheFlipThatIsEasingTheSameContainer`
- **BRIDGE-DRAGGABLE-053** — `ProgressX`/`ProgressY` shall map the axis between its start and end bound to 0..1, and setting them shall map back.
  - Tests: untested
- **BRIDGE-DRAGGABLE-054** — `XValue`/`YValue` shall mirror `X`/`Y` on every render of the drag and the release, and shall freeze once the draggable reverts.
  - Tests: `MotionValueSourceTests.DraggableValuesFollowADragAndTheReleaseSpring`, `MotionValueSourceTests.ObserverAndDraggableValuesFreezeOnRevert`

### Stop, reset, enable, revert

- **BRIDGE-DRAGGABLE-055** — When `Stop` is called, the draggable shall pause the drag and every animation on its axes, cancel its own overshoot and scroll animations and drop its references to them, and leave animations on other targets running, also when called from another animation's callback mid-frame.
  - Tests: `DraggableTests.ResetMidOvershootLeavesAnAnimationOnAnotherControlRunning`
- **BRIDGE-DRAGGABLE-056** — Grabbing an overshooting throw repeatedly (canceling the overshoot each time, including after its out leg completed) shall leave every engine and additive tween list acyclic and intact.
  - Tests: `DraggableTests.RegrabbingAnOvershootingThrowRepeatedlyKeepsTheTweenListsAcyclic`, `TweenListIntegrityTests.CancelingAnAdditiveAnimationTwiceKeepsTheListsIntactForTheNextOne`, `TweenListIntegrityTests.CancelingACompletedAdditiveAnimationKeepsTheListsIntact`
- **BRIDGE-DRAGGABLE-057** — When `Reset` is called, the draggable shall stop, cancel a pending resize, end any drag state and Escape listener, write both axes to 0 without raising `OnUpdate`, and clear the velocity.
  - Tests: `DraggableTests.ResetMidOvershootLeavesAnAnimationOnAnotherControlRunning`, `DraggableTests.ResettingInsideOnSettleLeavesBothAxesAtZero`
- **BRIDGE-DRAGGABLE-058** — When `Disable` is called, the draggable shall drop the drag in progress and the pointer capture, restore the cursor and stop listening for pointers, leaving the target where it is; `Enable` shall attach the listeners again and is a no-op while enabled.
  - Tests: untested
- **BRIDGE-DRAGGABLE-059** — When `Revert` is called, the draggable shall reset, disable, revert its timers and animatable (restoring the axis properties and the cursor), detach its size handlers and release target ownership, after which pointer input and resizes have no effect.
  - Tests: `DraggableTests.RevertingDetachesTheResizeAndPointerHandlers`, `DraggableTests.TheCursorFollowsTheDragAndRevertPutsItBack`, `TransformableTests.ADraggableAndATransformableCannotShareATargetUntilOneIsReverted`

### Defaults

- **BRIDGE-DRAGGABLE-060** — When `Trigger` is not set, the draggable shall listen for pointers on the target itself.
  - Tests: `DraggableTests.DraggingMovesTheTargetByThePointerDelta`
- **BRIDGE-DRAGGABLE-061** — When an axis's `Enabled` is not set, or the axis has no `DraggableAxisParams`, that axis shall be enabled.
  - Tests: `DraggableTests.DraggingMovesTheTargetByThePointerDelta`
- **BRIDGE-DRAGGABLE-062** — When an axis's `Composition` is not set, the axis shall write with `Composition.Replace`.
  - Tests: untested
- **BRIDGE-DRAGGABLE-063** — When `ContainerPadding` is not set, the bounds shall have no padding.
  - Tests: untested
- **BRIDGE-DRAGGABLE-064** — When `ContainerFriction` is not set, the drag friction past a bound shall be 0.8.
  - Tests: `DraggableTests.AnOutOfBoundsReleaseEasesBackIntoTheContainer`
- **BRIDGE-DRAGGABLE-065** — When `ReleaseContainerFriction` is not set, the release friction shall equal the drag friction.
  - Tests: untested
- **BRIDGE-DRAGGABLE-066** (inherited) — When `CancelOnEscape` is not set, Escape shall not cancel a drag.
  - Tests: `DraggableTests.EscapeIsIgnoredWithoutCancelOnEscape`
- **BRIDGE-DRAGGABLE-067** — When `DragThreshold` is not set, the drag threshold shall be 3.
  - Tests: `DraggableTests.MovementBelowTheDragThresholdIsSuppressed`
- **BRIDGE-DRAGGABLE-068** — When `ReleaseEase` is not set, the release shall ease with `Eases.OutQuint`.
  - Tests: untested
- **BRIDGE-DRAGGABLE-069** — When `ReleaseMass`, `ReleaseStiffness` or `ReleaseDamping` is not set, the release spring shall use mass 1, stiffness 80 and damping 20 respectively.
  - Tests: untested
- **BRIDGE-DRAGGABLE-070** — When `MinVelocity`, `MaxVelocity` or `VelocityMultiplier` is not set, the velocity shall be clamped to at least 0 and at most 50 and scaled by 1 respectively.
  - Tests: untested
- **BRIDGE-DRAGGABLE-071** — When `ScrollSpeed` is not set, the edge auto-scroll speed shall be 1.5, and when `ScrollThreshold` is not set, the edge band shall be 20 wide.
  - Tests: untested
- **BRIDGE-DRAGGABLE-072** (inherited) — When `Cursor` is not set, cursors shall be enabled, and when `OnHover` or `OnGrab` is not set, the draggable shall show `StandardCursorType.Hand` on hover and `StandardCursorType.SizeAll` while grabbed.
  - Tests: untested
- **BRIDGE-DRAGGABLE-073** — When `ScrollInView` or `AnimateInView` is called without a duration, gap or ease, it shall animate over 350 ms × `Globals.TimeScale` with a gap of 0 and `Eases.InOutQuad`.
  - Tests: untested

## Invariants

- At most one pointer drives the draggable at a time, and the trigger holds that pointer's capture until release.
- An axis with `Enabled = false` is never written, by the drag, the release, the setters or `Reset`.
- `OnSettle` fires at most once per release, after both axes have been written; `Velocity` and `Angle` are 0 when it fires.
- After `Stop`, the draggable holds no reference to a canceled overshoot or scroll animation, so no animation is canceled twice through it.
- A target is driven by at most one gesture type (`Draggable` or `Transformable`) at a time.

## Edge cases

- A drag threshold of 0 starts the drag on any movement; the comparison is strictly greater than the threshold.
- A `ContainerFriction` or `ReleaseContainerFriction` outside `[0, 1]` is clamped. Friction 1 (`1 − f == 0`) skips both the drag cushion inversion at grab and the release overshoot.
- `MaxVelocity = 0` makes every release a pure snap with no throw.
- `Reset` writes 0 even when 0 lies outside the container bounds; it does not clamp.
- The `X`/`Y` setters do not clamp to the bounds; only `Refresh`, a drag and a release do.
- Creating a second `Draggable` on a target that already has one does not throw (ownership only rejects a different gesture type); both then listen to the trigger.
- A press already handled by a child or by the trigger itself (as `ListBoxItem` does) never grabs.
- Pointer capture loss (for example the window losing capture) ends the drag exactly like a release, including the throw.

## Threading and lifetime

- UI thread only. The draggable captures `Engine.Current` at construction and uses that engine for pointer-time work (velocity timing, precision), since pointer events arrive when another engine may be ambient.
- It registers with the engine's current `Scope`; the scope's revert, or `Revert`, releases every handler. Until then the trigger's and container's event handlers keep the draggable alive as long as those controls are.
- Velocity uses dilated engine time (`Engine.Now`), so `TimeDilation` and DevTools freezes apply.

## Reduced motion

- The drag itself is direct manipulation and is never reduced. The release, overshoot and scroll animations are ordinary engine animations, so `ReduceAll` collapses them to their end values like any other animation (CORE-REDUCED); the draggable has no reduced form of its own. Untested.

## Allocation

- The per-frame drag update and the render callback allocate nothing of their own; axis writes go through `Animatable.Set` (CORE-ANIMATABLE). A release allocates its animations (and, out of bounds, two overshoot animations); `XValue`/`YValue` are created once on first read.

## Provenance

- No `dragSpeed`; `DragThreshold` is one number with no mouse/touch split (upstream: 3 mouse, 7 touch). Reason: not recorded.
- Parameters are plain values, not function-based (`parseDraggableFunctionParameter`), except `ContainerBounds`, which is a delegate read on every measurement; `ContainerPadding` is a `Thickness`. Reason: not recorded.
- No container defaults to unbounded; upstream defaults to the document body, which has no counterpart here.
- The grab does not raise the target's z-index, and there is no touch-scroll pass-through inside scrollable children of the trigger. Reason: not recorded.
- Cursors are set on the trigger (Hand / SizeAll), not on the document body (grab / grabbing). Reason: not recorded.
- Not upstream: `CancelOnEscape` (reason not recorded), `ReleaseInertia` (see Decisions), `XValue`/`YValue` (MotionValue sources, CORE-MOTIONVALUE), and `Refresh` re-aiming a release in flight, because a container resized mid-throw otherwise replaced the release with a muted zero-duration write, leaving the target stopped mid-air with no `OnSettle`.
- `Stop` cancels the overshoot and scroll animations and drops its references instead of removing the overshoot target, because canceling the overshoot on every grab removed additive tweens twice and corrupted the tween lists; the idempotent sibling removal underneath is a core deviation (CORE-COMPOSITION).
- The release's zero-duration axis writes rely on the core's top-level `Stretch` keeping its offset (CORE-TIMER), because stretching a 1e-11 setter otherwise scaled the offset by ~10¹⁴ and started the release late or never; release springs set their own velocity, which is why `Animatable` momentum is opt-in (CORE-ANIMATABLE).

## Decisions

- **One live gesture per control.** `GestureOwners.Claim` throws when the target already has a live `Draggable`, `Transformable` or swipe tracker, the same type included, until that owner is reverted. Reason: two gesture objects on one control both handle its pointer events and move it twice, and a second claim of the same type used to overwrite the owner, so its `Revert` freed a control the first still drove; in practice this is a forgotten `Revert` before rebuilding, which should fail where it happens. Multi-touch on one element is a `Transformable`'s job, and separate controls dragged at once are unaffected. Rejected: handing over by reverting the old owner, which snaps the element back to its origin; allowing it.
- **Draggable reads engine time.** Velocity timing reads the engine's public `Now`, not the frame source's. Reason: the frame source keeps wall time while the engine is frozen or dilated.
- **Draggable samples velocity through `VelocityTracker` with upstream's clamping.** The draggable feeds the core `VelocityTracker` through an internal overload that clamps and rounds each sample when taken, from a zero-seeded three-sample stack as `draggable.js` does, and reports the strongest sample as the public tracker does. Reason: clamping each sample once means a mid-drag `Refresh` that changes the limits applies only to later samples, and the overload allocates nothing; the draggable's samples are speeds, never negative under the default `MinVelocity` of 0, so the strongest sample is upstream's plain maximum, and one selection rule serves both. Only a negative `MinVelocity` with a negative `VelocityMultiplier` tells them apart, where the throw now runs backwards instead of upstream's 0. Rejected: upstream's plain maximum as a second rule; feeding the public tracker unchanged.
- **The projection release stays the default; inertia is opt-in.** Without `ReleaseInertia` the release projects the throw and eases to the snapped, clamped destination; with it, each axis is flung at `Velocity·cos/sin(Angle)` with the bounds as `Min`/`Max` and the axis snap as `ModifyTarget` unless set, bypassing overshoot, cushion and `ReleaseEase`, ignoring the template's `Velocity`, `Min` and `Max`, and re-flinging on `Refresh` only when the rest point leaves the new bounds. Reason: the projection's feel is tuned through `ReleaseEase` or a release spring and the container cushion, all of which an inertia release bypasses, so existing draggables keep their tuning; a decay suits content flicked like a page, which is where `Pager` opts in. Rejected: rebuilding the release on inertia, as first planned.
- **Unset options drag the target freely; the feel constants are upstream's.** Without `Trigger`, an axis `Enabled` or `Composition`, `ContainerPadding` or `ReleaseContainerFriction`, the target is its own handle, both axes move and write with the core default `Replace`, the bounds are the container's box and the release cushions as the drag does (BRIDGE-DRAGGABLE-060 to 063, 065). Reason: `CreateDraggable(control)` then drags the control, and each option narrows that. The 3-DIP threshold (067) is the one the other Presto drags share on their scroll axis (BRIDGE-GESTURES). The drag friction 0.8, `Eases.OutQuint`, the release spring (mass 1, stiffness 80, damping 20), the velocity limits 0 to 50, the scroll speed 1.5 and 20-wide band, and `ScrollInView`'s 350 ms `InOutQuad` (064, 068 to 071, 073) are kept from upstream; no other reason recorded.
- **VelocityTracker is a public core type.** See CORE-INERTIA.
- **Sibling removal is idempotent.** See CORE-COMPOSITION.
- **Spring momentum on retarget is opt-in.** See CORE-ANIMATABLE.
- **One owner per element.** See BRIDGE-TRANSFORMABLE.
- **The axis `Modifier` stays re-applied on release, documented as needing idempotence.** From the release on, the animatable re-applies an axis modifier to its own output, and `DraggableAxisParams.Modifier` documents that it must be idempotent. Reason: the only feature it hurt, pull-to-refresh, damps its travel itself instead (BRIDGE-PULL). Rejected: changing how the release applies the modifier.

## Non-goals

- Multi-touch pan, pinch or rotate (BRIDGE-TRANSFORMABLE).
- Per-axis velocity: `Velocity` is a speed with an `Angle`.
- Reordering items in a list (BRIDGE-REORDER owns its own pointer).
- A XAML surface: draggables are created in code.

## Open items

- `DraggableParams.OnDrag` is documented as running "on the first movement past DragThreshold", but the code raises it on every move of the drag after the threshold (pinned by `OnUpdateOnlyFiresWhenTheTargetMoves`, which expects 3); the XML doc is wrong.
- The manual Desktop check of flinging and re-grabbing cards on the Draggable page, is still open.
- `Reset` writes 0 unclamped even with a container that excludes 0; kept from upstream, unclear whether intended or a bug.
- No tests for `ContainerBounds`, `ProgressX`/`ProgressY`, `Disable`/`Enable`, `ScrollInView`, `Cursor.Enabled = false`, axis `Modifier`/`Composition`, `ReleaseContainerFriction`, scope registration, the grab-time scroll-into-view, or reduced-motion behavior of the release.
