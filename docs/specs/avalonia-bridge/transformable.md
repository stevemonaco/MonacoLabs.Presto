---
id: BRIDGE-TRANSFORMABLE
title: Transformable (pan, pinch-zoom, rotate)
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge.Gestures
sources:
  - Monaco.Presto.AvaloniaBridge/Gestures/Transformable.cs
  - Monaco.Presto.AvaloniaBridge/Gestures/TransformableParams.cs
  - Monaco.Presto.AvaloniaBridge/Gestures/PanBounds.cs
  - Monaco.Presto.AvaloniaBridge/Gestures/WheelZoom.cs
  - Monaco.Presto.AvaloniaBridge/Gestures/TransformMath.cs
  - Monaco.Presto.AvaloniaBridge/Gestures/GestureOwners.cs
  - Monaco.Presto.AvaloniaBridge/PrestoAvalonia.cs
  - Monaco.Presto.AvaloniaBridge/AnimateExtensions.cs
types:
  - Transformable
  - TransformableParams
  - PanBounds
  - WheelZoom
tests:
  - TransformableTests
  - TransformMathTests
  - AllocationTests
upstream: []
depends:
  - CORE-ANIMATABLE
  - CORE-INERTIA
  - CORE-SCOPE
  - CORE-REDUCED
  - BRIDGE-TRANSFORMS
  - BRIDGE-HELPERS
  - BRIDGE-TOKENS
  - BRIDGE-DRAGGABLE
---

# Transformable (pan, pinch-zoom, rotate)

## Purpose

Direct manipulation of one control: one pointer pans it, two pan, pinch-zoom and rotate it about their midpoint, and the mouse wheel, double-tap and macOS trackpad zoom it. The transform is written through an `Animatable` (CORE-ANIMATABLE) over the `X`, `Y`, `Scale` and `Rotate` shorthands (BRIDGE-TRANSFORMS); on release pan and rotation coast through `Inertia` (CORE-INERTIA) while scale springs back into its limits. It is the multi-touch counterpart of `Draggable` (BRIDGE-DRAGGABLE), sharing its verbs and callback names. Also specifies `PrestoAvalonia.CreateTransformable` and its `CreateTransformable` extension.

## Requirements

### Creation and ownership

- **BRIDGE-TRANSFORMABLE-001** — When `CreateTransformable` is called, the bridge shall ensure the frame pump is initialized, create the transformable with `new TransformableParams()` when `parameters` is null, and start listening on the trigger at once (enabled).
  - Tests: `TransformableTests.SpreadingTwoFingersDoublesTheScaleAndKeepsTheirMidpointFixed`
- **BRIDGE-TRANSFORMABLE-002** — When the target is already driven by a live `Draggable`, `CreateTransformable` shall throw `InvalidOperationException` whose message names both `Draggable` and `Transformable`; likewise `CreateDraggable` on a target driven by a live `Transformable`.
  - Tests: `TransformableTests.ADraggableAndATransformableCannotShareATargetUntilOneIsReverted`
- **BRIDGE-TRANSFORMABLE-003** — When a transformable is reverted, the target shall be released so that a `Draggable` can then be created on it.
  - Tests: `TransformableTests.ADraggableAndATransformableCannotShareATargetUntilOneIsReverted`
- **BRIDGE-TRANSFORMABLE-004** — When created inside a `Scope`, the transformable shall register with that scope so reverting the scope reverts it.
  - Tests: untested
- **BRIDGE-TRANSFORMABLE-005** — The transformable shall capture `Engine.Current` at creation and use that engine for time, precision, defaults and reduced motion in every later pointer event.
  - Tests: untested

### Pointer tracking

- **BRIDGE-TRANSFORMABLE-006** — The transformable shall read pointer positions in the target's visual parent space, and shall ignore every pointer event while the target has no visual parent.
  - Tests: `TransformableTests.SpreadingTwoFingersDoublesTheScaleAndKeepsTheirMidpointFixed`
- **BRIDGE-TRANSFORMABLE-007** — While two pointers are tracked, a third pointer's press shall be ignored and left unhandled, and its moves shall change nothing.
  - Tests: `TransformableTests.AThirdFingerChangesNothing`
- **BRIDGE-TRANSFORMABLE-008** — A mouse press shall be tracked only with the left button pressed and no other pointer tracked; while a mouse is tracked a touch or pen press shall be ignored. Pen counts as touch.
  - Tests: `TransformableTests.AMouseDragPansOnceZoomedIn`
- **BRIDGE-TRANSFORMABLE-009** — When a second pointer lands, or a single pointer lands while `Pan` is on and the transform is settling, `PanAtRest` is on, or `Scale` exceeds `MinScale`, the transformable shall grab: stop running animations, set `IsManipulating`, capture every tracked pointer to the trigger, reset velocity and invoke `OnGrab` once.
  - Tests: `TransformableTests.SpreadingTwoFingersDoublesTheScaleAndKeepsTheirMidpointFixed`, `TransformableTests.OnceZoomedInAOneFingerDragPansRatherThanSwipingAnEnclosingCarousel`
- **BRIDGE-TRANSFORMABLE-010** — While `PanAtRest` is false and `Scale` is at `MinScale` with nothing settling, a single pointer's press and moves shall be neither handled nor captured, so ancestors receive them.
  - Tests: `TransformableTests.AtRestAOneFingerDragIsLeftToAncestors`
- **BRIDGE-TRANSFORMABLE-011** — When a pointer press or move is taken by the transformable, it shall mark the event handled and call `PreventGestureRecognition()`, so an ancestor's swipe recognizer (e.g. a `Carousel`) does not steal the capture.
  - Tests: `TransformableTests.OnceZoomedInAOneFingerDragPansRatherThanSwipingAnEnclosingCarousel`
- **BRIDGE-TRANSFORMABLE-012** — When a pointer touches the target during a release or settle, the transformable shall stop it where it is, grab (even at `MinScale` with `PanAtRest` off) and not invoke `OnSettle` for the interrupted release.
  - Tests: `TransformableTests.ATouchDuringAReleaseStopsItAndGrabsFromWhereItIs`

### Manipulation

- **BRIDGE-TRANSFORMABLE-013** — While one pointer is manipulating and `Pan` is on, each move shall translate the target by the pointer's movement in parent space.
  - Tests: `TransformableTests.AMouseDragPansOnceZoomedIn`, `TransformableTests.OnceZoomedInAOneFingerDragPansRatherThanSwipingAnEnclosingCarousel`
- **BRIDGE-TRANSFORMABLE-014** — While two pointers are manipulating, each move shall pan by half the moving pointer's displacement (when `Pan` is on), zoom by the ratio of the pointers' distance after to before (when `Zoom` is on) and turn by the change of their angle (when `Rotate` is on), all about the pointers' current midpoint, keeping the content point under that midpoint fixed.
  - Tests: `TransformableTests.SpreadingTwoFingersDoublesTheScaleAndKeepsTheirMidpointFixed`, `TransformableTests.MovingBothFingersTogetherPans`, `TransformableTests.TurningTwoFingersRotatesOnlyWhenRotateIsOn`, `TransformMathTests.FocalUpdateKeepsTheContentPointUnderTheFocalPointFixed`
- **BRIDGE-TRANSFORMABLE-015** — When `Pan` is false, the pointers' own movement shall not pan, but a zoom or turn shall still move the translation to keep its focal point fixed.
  - Tests: untested
- **BRIDGE-TRANSFORMABLE-016** — When one of two pointers lifts, the remaining pointer shall continue as a one-finger pan from its current position with no jump and reset velocity; when a second pointer lands mid-pan, a pinch shall start from the current state without a second `OnGrab`.
  - Tests: `TransformableTests.LiftingAFingerHandsOverToAPanAndASecondFingerPinchesFromThere`
- **BRIDGE-TRANSFORMABLE-017** — While manipulating past `MinScale`/`MaxScale`, the shown scale shall be rubber-banded in log space as `limit·(s/limit)^ScaleFriction`.
  - Tests: `TransformableTests.BeyondMaxScaleTheReleaseSpringsBackAboutTheLastFocalPoint`, `TransformMathTests.RubberBandDampsTheExcessBeyondTheLimits`, `TransformMathTests.RubberBandIsContinuousAtTheLimits`, `TransformMathTests.RubberBandIsTheIdentityInsideTheLimits`
- **BRIDGE-TRANSFORMABLE-018** — While manipulating past the pan bounds, the shown translation shall keep `ScaleFriction` of its linear excess beyond the bounds.
  - Tests: `TransformableTests.UnderContainThePanClampsAndAFlickCoastsIntoTheEdgeAndSettlesOnIt`
- **BRIDGE-TRANSFORMABLE-019** — When a grab begins on a rubber-banded state, the transformable shall invert the band (friction `1/ScaleFriction`, or 1 when friction is 0) so the target does not jump.
  - Tests: untested
- **BRIDGE-TRANSFORMABLE-020** — Every write of the transform (manipulation moves, unanimated moves, `Refresh`) shall apply all four channels at once, sample the velocity trackers and invoke `OnUpdate`; while settling, `OnUpdate` shall also run on every animation update.
  - Tests: `TransformableTests.SpreadingTwoFingersDoublesTheScaleAndKeepsTheirMidpointFixed`, `TransformableTests.UnanimatedMovesApplyAtOnceAndResetRestoresIdentity`
- **BRIDGE-TRANSFORMABLE-021** — `X`, `Y`, `Scale` and `Rotation` shall report the animatable's values rounded to the engine's precision; `Velocity` shall report the translation speed over recent samples in parent units per millisecond.
  - Tests: untested

### Pan bounds

- **BRIDGE-TRANSFORMABLE-022** — The pan bounds shall be computed from the axis-aligned box of the target scaled and rotated about its `RenderTransformOrigin`, against `Container`'s box translated into the parent (the parent's own bounds when `Container` is null or cannot be translated).
  - Tests: `TransformMathTests.RotatedBoundsUseTheAxisAlignedBox`
- **BRIDGE-TRANSFORMABLE-023** — Under `PanBounds.Contain`, content larger than the container shall be kept covering it, and on an axis where it is smaller it shall be centered.
  - Tests: `TransformMathTests.ContainKeepsLargerContentCoveringTheContainer`, `TransformMathTests.ContainCentersContentSmallerThanTheContainer`
- **BRIDGE-TRANSFORMABLE-024** — Under `PanBounds.Inside`, content shall be kept within the container; on an axis where it is larger it shall fall back to covering the container.
  - Tests: `TransformMathTests.InsideKeepsContentWithinTheContainer`
- **BRIDGE-TRANSFORMABLE-025** — Under `PanBounds.Free`, the pan shall be unbounded.
  - Tests: `TransformMathTests.FreeIsUnbounded`

### Release

- **BRIDGE-TRANSFORMABLE-026** — When the last pointer lifts (released or capture lost), the transformable shall clear `IsManipulating`, release the capture, start the release and invoke `OnRelease`.
  - Tests: `TransformableTests.OnReleaseAndOnSettleFireOncePerGesture`
- **BRIDGE-TRANSFORMABLE-027** — When `Rotate` is on, the release shall resolve rotation first by flinging it with the release inertia (unbounded), landing on a multiple of `RotationSnap` when it is positive, or on `ReleaseInertia.ModifyTarget` when that is set (which takes precedence).
  - Tests: `TransformableTests.ARotationSnapLandsOnAMultiple`
- **BRIDGE-TRANSFORMABLE-028** — When the released scale is outside `[MinScale, MaxScale]`, scale and translation shall ease to the clamped scale about the last focal point, with the translation's end clamped into the pan bounds at that scale, through `Settle`'s duration and ease; the pan shall not coast.
  - Tests: `TransformableTests.BeyondMaxScaleTheReleaseSpringsBackAboutTheLastFocalPoint`, `TransformableTests.ACustomSettleDurationIsHonored`
- **BRIDGE-TRANSFORMABLE-029** — When the released scale is inside its limits, the pan shall coast with the release inertia from its tracked velocity (zero velocity when `Pan` is off), with `Min`/`Max` taken from the pan bounds at the landed scale and angle, so a coast into an edge bounces and settles on it.
  - Tests: `TransformableTests.UnderContainThePanClampsAndAFlickCoastsIntoTheEdgeAndSettlesOnIt`
- **BRIDGE-TRANSFORMABLE-030** — Scale shall never coast on release.
  - Tests: untested
- **BRIDGE-TRANSFORMABLE-031** — When the release, or an animated programmatic move, comes to rest, the transformable shall invoke `OnSettle` exactly once; `OnRelease` and `OnSettle` shall fire once per gesture, in that order.
  - Tests: `TransformableTests.OnReleaseAndOnSettleFireOncePerGesture`
- **BRIDGE-TRANSFORMABLE-032** (inherited) — When nothing is left running after the release starts (e.g. everything landed at once), `OnSettle` shall fire synchronously after `OnRelease`; when `OnRelease` calls `Stop`, no `OnSettle` shall follow.
  - Tests: untested
- **BRIDGE-TRANSFORMABLE-033** — When `ReleaseInertia` is null, the release shall use `new Inertia(0)` as its template.
  - Tests: untested

### Double-tap, wheel and trackpad

- **BRIDGE-TRANSFORMABLE-034** — When the trigger is double-tapped (touch or mouse double-click) with `Zoom` on and `DoubleTapScale` set, the transformable shall mark it handled and, at a scale at or below 1.01, zoom to `DoubleTapScale` (clamped to the limits) about the tap point through `Settle`; otherwise it shall `Reset()` animated.
  - Tests: `TransformableTests.ADoubleTapZoomsToDoubleTapScaleAboutTheTapAndTogglesBack`
- **BRIDGE-TRANSFORMABLE-035** — When `DoubleTapScale` is null or `Zoom` is off, a double-tap shall be left unhandled and change nothing.
  - Tests: `TransformableTests.WithZoomOffTheWheelAndADoubleTapLeaveTheScale`
- **BRIDGE-TRANSFORMABLE-036** — When a wheel event arrives with `Zoom` on and either `WheelZoom.Always`, or `WheelZoom.WithControl` with Ctrl held, the transformable shall mark it handled and set the scale at once to `Scale · WheelZoomStep^Delta.Y`, clamped to the limits, about the cursor, with no rubber band or animation.
  - Tests: `TransformableTests.ControlWheelZoomsAboutTheCursorAtOnce`, `TransformableTests.APlainWheelZoomsOnlyUnderAlwaysAndOtherwiseScrollsTheAncestor`
- **BRIDGE-TRANSFORMABLE-037** — Otherwise (`WheelZoom.None`, a plain wheel under `WithControl`, or `Zoom` off) the wheel event shall be left unhandled, so an ancestor `ScrollViewer` scrolls.
  - Tests: `TransformableTests.APlainWheelZoomsOnlyUnderAlwaysAndOtherwiseScrollsTheAncestor`, `TransformableTests.WithZoomOffTheWheelAndADoubleTapLeaveTheScale`
- **BRIDGE-TRANSFORMABLE-038** — When a touchpad magnify gesture arrives with `Zoom` on, the transformable shall mark it handled and set the scale at once to `Scale · (1 + Delta.X)`, clamped, about the pointer.
  - Tests: manual — macOS trackpad pinch on the Photo viewer page (Avalonia.Native only).
- **BRIDGE-TRANSFORMABLE-039** — When a touchpad rotate gesture arrives with `Rotate` on, the transformable shall mark it handled and rotate at once by `-Delta.X` degrees about the pointer.
  - Tests: manual — macOS trackpad rotate on the Photo viewer page; sign unverified on hardware.

### Programmatic moves

- **BRIDGE-TRANSFORMABLE-040** — `ZoomTo`, `PanTo`, `RotateTo` and `Reset` shall first drop any gesture in progress without a release (no `OnRelease`) and stop running animations.
  - Tests: untested
- **BRIDGE-TRANSFORMABLE-041** — `ZoomTo` shall clamp `scale` to the limits, `PanTo` shall clamp the translation into the pan bounds at the current scale and angle, and `ZoomTo`/`RotateTo` shall keep the content under `focal` (parent space; the container's center when null) fixed with the resulting translation clamped into the pan bounds.
  - Tests: `TransformableTests.ZoomPanAndRotateToClampAndPlayThroughSettle`, `TransformableTests.UnanimatedMovesApplyAtOnceAndResetRestoresIdentity`
- **BRIDGE-TRANSFORMABLE-042** — `RotateTo` shall not clamp or snap the angle.
  - Tests: `TransformableTests.ZoomPanAndRotateToClampAndPlayThroughSettle`
- **BRIDGE-TRANSFORMABLE-043** — When `animate` is true (the default), a programmatic move shall ease all four channels to their ends with `Settle`'s duration and ease and end with `OnSettle`; when false, it shall write at once, invoke `OnUpdate` once and not invoke `OnSettle`.
  - Tests: `TransformableTests.ZoomPanAndRotateToClampAndPlayThroughSettle`, `TransformableTests.UnanimatedMovesApplyAtOnceAndResetRestoresIdentity`
- **BRIDGE-TRANSFORMABLE-044** — `Reset` shall return to translation (0, 0), scale 1 and rotation 0.
  - Tests: `TransformableTests.UnanimatedMovesApplyAtOnceAndResetRestoresIdentity`
- **BRIDGE-TRANSFORMABLE-045** (inherited) — `ZoomTo`, `PanTo`, `RotateTo` and `Refresh` shall do nothing when the target has no visual parent.
  - Tests: untested
- **BRIDGE-TRANSFORMABLE-046** — When `Settle` is null, the settle timing shall be `Recipes.Tokens.Medium` with `Recipes.Tokens.Ease`, duration 0 under reduced motion; when set, only its resolved `Duration` and `Ease` (after `WhenReduced`) shall be used, falling back to the engine defaults.
  - Tests: `TransformableTests.ACustomSettleDurationIsHonored`

### Playback verbs

- **BRIDGE-TRANSFORMABLE-047** — `Stop` shall pause every animation on the transform, leaving the target where it is, and cancel the pending `OnSettle`; it does not drop pointers.
  - Tests: untested
- **BRIDGE-TRANSFORMABLE-048** — When not settling, `Refresh` shall clamp scale into its limits and the translation into freshly measured pan bounds at once (one write, `OnUpdate`).
  - Tests: untested
- **BRIDGE-TRANSFORMABLE-049** — While settling, `Refresh` shall re-aim the pan's coast at the new bounds from its current value and velocity only when the planned rest point falls outside them.
  - Tests: untested
- **BRIDGE-TRANSFORMABLE-050** — `Disable` shall detach every handler from the trigger and drop the gesture in progress without a release, leaving the target where it is; `Enable` shall reattach them and be a no-op while enabled.
  - Tests: untested
- **BRIDGE-TRANSFORMABLE-051** — `Revert` shall disable, revert the animatable so the transform returns to what it was before, and release the target's gesture ownership.
  - Tests: `TransformableTests.ADraggableAndATransformableCannotShareATargetUntilOneIsReverted`

### Reduced motion

- **BRIDGE-TRANSFORMABLE-052** — While `PrestoDefaults.ReducedMotion` is on, manipulation shall still track the pointers unchanged.
  - Tests: `TransformableTests.UnderReducedMotionTheReleaseLandsOnItsClampedTargetAtOnce`
- **BRIDGE-TRANSFORMABLE-053** — While `PrestoDefaults.ReducedMotion` is on, the release shall not coast: rotation lands on its snapped angle and the pan on its clamped translation at once, and an out-of-limits scale settles through `Settle`'s reduced form (instant by default).
  - Tests: `TransformableTests.UnderReducedMotionTheReleaseLandsOnItsClampedTargetAtOnce`

### Allocation

- **BRIDGE-TRANSFORMABLE-054** — A two-finger move shall allocate no more than 600 bytes beyond Avalonia's event routing, with diagnostics on or off.
  - Tests: `AllocationTests.APinchMoveAllocatesOnlyItsWrites`

### Defaults

- **BRIDGE-TRANSFORMABLE-055** — When `Trigger` is not set, the target itself shall receive the input.
  - Tests: `TransformableTests.SpreadingTwoFingersDoublesTheScaleAndKeepsTheirMidpointFixed`
- **BRIDGE-TRANSFORMABLE-056** — When `Pan` or `Zoom` is not set, it shall be on.
  - Tests: `TransformableTests.SpreadingTwoFingersDoublesTheScaleAndKeepsTheirMidpointFixed`, `TransformableTests.AMouseDragPansOnceZoomedIn`
- **BRIDGE-TRANSFORMABLE-057** — When `Rotate` is not set, it shall be off.
  - Tests: untested
- **BRIDGE-TRANSFORMABLE-058** — When `PanAtRest` is not set, it shall be false.
  - Tests: `TransformableTests.AtRestAOneFingerDragIsLeftToAncestors`
- **BRIDGE-TRANSFORMABLE-059** — When `PanBounds` is not set, it shall be `Contain`.
  - Tests: `TransformableTests.UnderContainThePanClampsAndAFlickCoastsIntoTheEdgeAndSettlesOnIt`
- **BRIDGE-TRANSFORMABLE-060** — When `MinScale` is not set, it shall be 1.
  - Tests: `TransformableTests.AtRestAOneFingerDragIsLeftToAncestors`
- **BRIDGE-TRANSFORMABLE-061** — When `MaxScale` is not set, it shall be 4.
  - Tests: untested
- **BRIDGE-TRANSFORMABLE-062** — When `ScaleFriction` is not set, it shall be 0.35.
  - Tests: `TransformableTests.BeyondMaxScaleTheReleaseSpringsBackAboutTheLastFocalPoint`, `TransformableTests.UnderContainThePanClampsAndAFlickCoastsIntoTheEdgeAndSettlesOnIt`
- **BRIDGE-TRANSFORMABLE-063** — When `RotationSnap` is not set, it shall be 0, so a released rotation is not snapped.
  - Tests: untested
- **BRIDGE-TRANSFORMABLE-064** — When `DoubleTapScale` is not set, a double-tap shall zoom to 2.5.
  - Tests: `TransformableTests.ADoubleTapZoomsToDoubleTapScaleAboutTheTapAndTogglesBack`
- **BRIDGE-TRANSFORMABLE-065** — When `WheelZoom` is not set, it shall be `WithControl`.
  - Tests: `TransformableTests.ControlWheelZoomsAboutTheCursorAtOnce`
- **BRIDGE-TRANSFORMABLE-066** — When `WheelZoomStep` is not set, each wheel notch shall scale by 1.15.
  - Tests: `TransformableTests.ControlWheelZoomsAboutTheCursorAtOnce`

## Invariants

- The transform's state is exactly four numbers — translation `X`/`Y` (parent units), `Scale`, `Rotate` (degrees clockwise) — applied by the composer about `RenderTransformOrigin`; a user `RenderTransform` stays underneath and is not part of the math.
- At most two pointers are tracked; mouse and touch never mix in one gesture.
- After a release or settle completes, scale lies in `[MinScale, MaxScale]` and the translation lies in the pan bounds at that scale and angle.
- A control is driven by at most one gesture type at a time (`Draggable` or `Transformable`).

## Edge cases

- A press while the target has no visual parent is ignored; a parentless target cannot be zoomed, panned or rotated programmatically, but `Reset` still writes.
- Two pointers whose start distance is 0 give a zoom ratio of 1.
- The turn between moves is taken as the IEEE remainder of the angle change by 360, so crossing ±180° never spins the target.
- Losing a pointer's capture is treated as its release (the last one coasts and fires `OnRelease`).
- A wheel with only a horizontal delta, under a zooming mode, is still marked handled and leaves the scale unchanged.
- `ScaleFriction = 0` pins the target to the limits during a gesture.
- `DoubleTapScale` above `MaxScale` lands at `MaxScale`.

## Threading and lifetime

- UI thread only; every write goes through the `Animatable` on the engine captured at creation. The compositor is not involved (see Decisions: UI thread only).
- Handlers hold the transformable alive through the trigger's events until `Disable`/`Revert`. Gesture ownership is a weak table keyed by the target, so it does not keep the target alive.

## Reduced motion

- Manipulation is unchanged (the user drives it). The release lands at once on its clamped, snapped targets (BRIDGE-TRANSFORMABLE-053). `Settle` uses its `WhenReduced` form, instant for the default. `ReduceAll` makes an `Inertia` land in one frame (CORE-INERTIA).

## Allocation

- Pointer moves allocate nothing in the transformable itself; the budget is Avalonia's property change args plus `Animatable.Set` composing values (BRIDGE-TRANSFORMABLE-054).

## Deviations from anime.js

- No upstream counterpart: anime.js has no multi-touch transform. It follows the idioms of the `Draggable` port (BRIDGE-DRAGGABLE).

## Decisions

- **A new `Transformable`, not an extended `Draggable`.** Pan, pinch-zoom and rotate live in their own type that shares Draggable's idioms (params record, a duration-0 `Animatable` over the shorthands, per-channel `VelocityTracker`s, an `Inertia` release, the same verbs and callback names) and includes one-finger pan, so it replaces `Draggable` wherever an element pans and zooms. Reason: `Draggable` is a parity port whose suite pins single-pointer behavior, its axes are independent, and zooming about a point needs translation and scale solved together; the name says it also pans. Rejected: extending `Draggable`; the name `Pinchable`.
- **Raw pointers in parent space.** The transformable tracks up to two pointers itself on its trigger, reading positions in the target's parent space; a third is ignored. Reason: Avalonia's recognizer reads positions relative to the element being transformed, which moves with the transform, freezes its origin at the start so a two-finger pan cannot move the focal point, and ends the pinch when one finger lifts. Rejected: `PinchGestureRecognizer`.
- **One state of four numbers over the shorthands.** The state is translation `X`/`Y` in parent units, scale and rotation in degrees clockwise, composed by the shorthands about `RenderTransformOrigin`; a user `RenderTransform` stays underneath, outside the math, and the docs say so. Reason: it matches the composer's order (scale and rotate about the origin, then translate).
- **Focal-point zoom and rotate.** Every two-finger move and every wheel or trackpad step keeps the content point under the focal point fixed (`t′ = c − k·Rot(Δθ)·(c − t)`); for two fingers the focal point is the current midpoint, so its movement pans and one drag pans, zooms and rotates at once. Reason: zooming about a point needs translation and scale solved together.
- **Limits rubber-band, then settle; three pan-bound modes.** Beyond `MinScale`/`MaxScale` the scale follows through `ScaleFriction` in log space and springs back on release about the last focal point; past the pan bounds the translation follows through the same friction, and grabbing again inverts the band. `PanBounds` is `Contain` (default), `Inside` (falling back to covering on an oversized axis) or `Free`, from the scaled, rotated axis-aligned box, and `Container` defaults to the parent. Reason: it mirrors Draggable's `ContainerFriction` and `Container`; log space makes pinching past either limit equally stiff; `Inside` cannot hold oversized content. Rejected: the linear excess damping of Draggable's `Cushion` for scale; stopping the pan hard at the edge.
- **Release coasts pan and rotation, not scale.** The pan flings through an `Inertia` from the `ReleaseInertia` template with `Min`/`Max` from the pan bounds at the scale and angle the release lands on (rotation's fling resolved first); rotation coasts only when `Rotate` is on, landing on `RotationSnap` multiples. Reason: zoom momentum is rare in native viewers and makes the focal point drift, and bounds at the landed state let a coast bounce off the edge. Rejected: scale inertia, to add in log space only if a release feels dead.
- **The scale spring-back interpolates endpoints, and the pan does not coast during it.** `X`, `Y` and `Scale` ease to their ends with one `Settle` duration and ease, the translation's end clamped into the bounds at the settled scale. Reason: about a fixed focal point translation is affine in scale, so shared timing keeps the focal point still every frame with no per-frame recompute, and a coast would also write `X`/`Y`.
- **Lifting one finger hands over to a pan.** The remaining pointer continues as a one-finger pan re-based on its position with its trackers reset, and a second finger mid-pan starts a pinch from the current state. Reason: nothing jumps.
- **Double-tap toggles zoom through `Settle`.** `DoubleTapScale` (2.5, null disables) zooms about the tap at a scale at or below 1.01 and resets otherwise; it listens to `Gestures.DoubleTapped`. Reason: the routed event makes mouse double-clicks work too.
- **Wheel and trackpad zoom.** `WheelZoom` is `None`, `WithControl` (default) or `Always`; the scale multiplies by `WheelZoomStep^Delta.Y` (1.15 per notch) about the cursor, clamped at once with no rubber band or animation; macOS magnify and rotate apply about the pointer; unhandled wheels pass through. Reason: Ctrl+wheel covers trackpad pinch on Windows and Browser and is the conventional zoom in viewers, maps and design tools; the default leaves a plain wheel to an ancestor `ScrollViewer`; an exponent of the delta is the same per notch and proportional for fractional trackpad deltas.
- **Panning at rest is opt-in.** With `PanAtRest` false at `MinScale`, a one-finger or mouse drag is neither captured nor handled; two fingers always engage, a zoomed-in finger pans, and a touch during a release always grabs. Reason: the photo-gallery convention, so an ancestor `ScrollViewer`, `Carousel` or `Draggable` swipe still works at rest, and a coast can always be caught.
- **One owner per element.** Creating a `Transformable` on a target a live `Draggable` drives, or the reverse, throws `InvalidOperationException` naming both; `Motion` states or recipes on the same channels compose by replace, and the docs recommend hover motion on an inner element. Reason: both write the same shorthands.
- **UI thread only.** Everything runs through the `Animatable` on the UI thread; the compositor is not involved. Reason: direct manipulation needs the latest pointer position every frame.
- **Reduced motion reduces only the release.** Manipulation is unchanged; the release does not coast (pan and rotation land on clamped, snapped targets at once), and `Settle` uses its `WhenReduced` form, instant by default. Reason: the user drives the manipulation; only system-driven motion is reduced.
- **Pen counts as touch; the mouse is one pointer; no mixing.** Mouse left-drag pans (subject to `PanAtRest`), Ctrl+wheel zooms and double-click toggles; mouse rotation is only through `RotateTo` or the macOS trackpad; a mouse press while a finger holds the target is ignored and vice versa, and only the left button grabs. Reason: not recorded.
- **Programmatic moves settle like a release.** `ZoomTo`, `PanTo`, `RotateTo`, `Reset`, double-tap, wheel and trackpad drop any gesture without a release; animated ones ease all four channels to clamped ends through `Settle` and end with `OnSettle`, and `Settle` contributes only its duration and ease, resolved so `WhenReduced` applies. Reason: not recorded; an animated `RotateTo` interpolates endpoints, so its focal point is exact only at the ends.
- **`Pan = false` still keeps the focal point.** The pointers' own movement does not pan, but a zoom or turn still moves the translation, and the release settles the pan into its bounds. Reason: focal-point zoom requires the translation to change.
- **`Refresh` during a release re-aims the coast.** The pan's coast is re-aimed at the new bounds from its current value and velocity instead of clamping at once. Reason: not recorded; it matches Draggable's mid-throw retarget.
- **Grabbed pointers skip gesture recognition.** Every press and move the transformable takes calls `PreventGestureRecognition()`. Reason: Avalonia's recognizers see handled events too, so a swipe-enabled `Carousel` around a zoomed photo stole the pan's capture.
- **Linux trackpad pinch is unsupported.** It is documented as such; Ctrl+wheel still zooms. Reason: X11 raises no touchpad gestures, and XInput2 gestures are not an Avalonia API. Rejected: reading XInput2 gestures directly.
- **Draggable reads engine time.** See BRIDGE-DRAGGABLE.

## Non-goals

- Scale momentum on release (see Decisions: release coasts pan and rotation, not scale).
- Using Avalonia's `PinchGestureRecognizer` (see Decisions: raw pointers in parent space).
- Transforming `CompositorCanvas` scene elements; Linux trackpad pinch (X11 raises no gestures; Ctrl+wheel still zooms).
- Mouse rotation other than through `RotateTo` or the macOS trackpad.

## Open items

- `GestureOwners.Claim` rejects only a different gesture type, so a second `Transformable` on the same target is allowed and overwrites the owner; the first one's `Revert` then no longer releases it. Untested; decide whether that should throw.
- Capture loss on the last pointer runs a full release (coast, `OnRelease`) rather than a drop; intended or not, it is untested.
- A wheel with only a horizontal delta is marked handled under a zooming mode, which blocks an ancestor's horizontal scroll; untested.
- Under reduced motion a custom `Settle` without `WhenReduced` still animates the scale spring-back; consistent with the reduced-motion decision but untested.
- The macOS trackpad rotate sign (`-Delta.X`) is unverified on hardware.
- Android and Browser real-touch smoke runs are still to do, including a check that an ancestor `ScrollViewer`'s recognizer does not capture the first touch before the second lands.
- Untested behaviors that should have tests: `Stop`, `Refresh` (both paths), `Disable`/`Enable`, `Trigger`, `Container`, `Pan = false`, `ReleaseInertia` template and `ModifyTarget` precedence, scope registration, `Velocity`, end-to-end `PanBounds.Inside`.
