---
id: BRIDGE-LAYOUT
title: Layout transitions (FLIP on the compositor)
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
  - MonacoLabs.Presto.Compositor
sources:
  - MonacoLabs.Presto.Avalonia/Compositor/LayoutTransition.cs
  - MonacoLabs.Presto.Avalonia/Compositor/LayoutAnimateMode.cs
  - MonacoLabs.Presto.Avalonia/Compositor/LayoutMotion.cs
  - MonacoLabs.Presto.Avalonia/Compositor/CompositorCompiler.cs
  - MonacoLabs.Presto.Avalonia/Compositor/CompositorRegistry.cs
  - MonacoLabs.Presto.Avalonia/PrestoAvalonia.cs
  - MonacoLabs.Presto.Avalonia/AnimateExtensions.cs
types:
  - LayoutTransition
  - LayoutAnimateMode
tests:
  - LayoutTransitionTests
  - CorrectedLayoutTransitionTests
  - CompositorRegistryTests
  - AnimationBuilderBridgeTests
  - WhenDetachedTests
  - ConnectedTests
  - ReferencePagesTests
upstream:
  - src/layout/layout.js
depends:
  - BRIDGE-THREAD
  - BRIDGE-INIT
  - BRIDGE-TRANSFORMS
  - BRIDGE-CONTENTSIZE
  - CORE-ANIMATION
  - CORE-BUILDER
  - CORE-SCOPE
  - CORE-REDUCED
  - CORE-SPRING
---

# Layout transitions (FLIP on the compositor)

## Purpose

Eases a control into the place layout gives it instead of letting it jump: where it was (First) and where layout put it (Last) are measured on the UI thread, the difference is inverted onto the composition `Translation` (and `Scale`), and the render thread plays it back to identity, so the move survives a blocked UI thread. Layout itself stays immediate. The same type flies a shared element from another control or from a prepared rectangle. XAML arming is BRIDGE-DECLLAYOUT, XAML keys are BRIDGE-CONNECTED, and real-size easing is BRIDGE-CONTENTSIZE. Also specifies `PrestoAvalonia.AnimateLayout`, `AnimateSharedElement` and `PrepareSharedElement`, and `AnimateExtensions.AnimateLayout`.

## Requirements

### Arming

- **BRIDGE-LAYOUT-001** — `AnimateLayout` shall arm a transition that eases every later layout move of the control until it is reverted.
  - Tests: `LayoutTransitionTests.ALayoutMoveEasesIntoPlaceInsteadOfJumping`
- **BRIDGE-LAYOUT-002** — A control with no live transition shall move to its new slot in one frame.
  - Tests: `LayoutTransitionTests.AControlWithNoLiveTransitionJumpsInOneFrame`
- **BRIDGE-LAYOUT-003** — When the control is in a window at creation, the transition shall measure First at once, so a layout change in the same pass already eases; otherwise the control's first laid-out pass shall only record where it is.
  - Tests: `LayoutTransitionTests.ALayoutMoveEasesIntoPlaceInsteadOfJumping`, `LayoutTransitionTests.ARecordFirstTransitionDoesNotEaseItsFirstPass`
- **BRIDGE-LAYOUT-004** — Arming a transition on a control that already has a live one shall revert the earlier one.
  - Tests: `LayoutTransitionTests.ASecondTransitionReplacesTheFirst`
- **BRIDGE-LAYOUT-005** — Every public factory shall register the transition with `Engine.Current.CurrentScope` when there is one.
  - Tests: untested

### Measuring

- **BRIDGE-LAYOUT-006** — A layout slot shall be the control's position in its window with every `ScrollContentPresenter` offset taken back out, so scrolling an armed control is not a layout move.
  - Tests: `LayoutTransitionTests.ScrollingAnArmedControlIsNotALayoutMove`
- **BRIDGE-LAYOUT-007** — The render transform shall not count toward the slot, so a transform shorthand's translation is not inverted.
  - Tests: `LayoutTransitionTests.ALayoutPassDoesNotInvertAShorthandTranslation`
- **BRIDGE-LAYOUT-008** — When a pass leaves the slot's position unchanged and nothing scales, the transition shall do nothing.
  - Tests: untested

### Playing a move

- **BRIDGE-LAYOUT-009** — When layout moves the control, the transition shall invert the move onto the composition `Translation` in the same pass and ease it to zero on the render thread, while `Bounds` already holds the new slot.
  - Tests: `LayoutTransitionTests.ALayoutMoveEasesIntoPlaceInsteadOfJumping`, `ReferencePagesTests.LayoutShufflesAndCloses`
- **BRIDGE-LAYOUT-010** — The frame rendered right after a layout change shall still show the control at its old position.
  - Tests: `LayoutTransitionTests.TheFrameAfterALayoutChangeStillShowsTheOldPosition`
- **BRIDGE-LAYOUT-011** — When layout moves the control again mid-move, the new move shall start from where the control is drawn (its slot plus what the mirror still holds), not from the slot it left.
  - Tests: `LayoutTransitionTests.ASecondLayoutChangeRetargetsFromWhereTheControlIsDrawn`
- **BRIDGE-LAYOUT-012** — The move shall use the motion's duration, delay and ease, with the engine's default ease when none is given; a spring ease shall play over its settling duration through its pure solver.
  - Tests: `AnimationBuilderBridgeTests.ALambdaGivesALayoutTransitionTheMotionItsBuilderGives`
- **BRIDGE-LAYOUT-013** — When X and Y share one ease the translation shall play as one exactly eased segment; when their eases differ it shall be sampled at 100 segments, curving the flight into an arc.
  - Tests: `LayoutTransitionTests.DifferentAxisEasesFlyTheSharedElementInAnArc`
- **BRIDGE-LAYOUT-014** — During the delay the control shall be held where it was drawn before the move.
  - Tests: `LayoutTransitionTests.ABuilderDelayHoldsTheControlWhereItWasBeforeItMoves`
- **BRIDGE-LAYOUT-015** — When a move's compiled duration and delay are both zero, the transition shall start no composition animation, cancel any move in flight and let the control land in one frame.
  - Tests: `LayoutTransitionTests.AReducedOrZeroDurationMoveLandsInOneFrame`
- **BRIDGE-LAYOUT-016** — `Then` shall resolve with the transition when the move in flight completes, and at once when no move has started.
  - Tests: `LayoutTransitionTests.ThenResolvesWhenTheMoveCompletes`
- **BRIDGE-LAYOUT-017** — A move in flight shall keep running and complete after its control leaves the visual tree.
  - Tests: `WhenDetachedTests.ALayoutTransitionFinishesWhenItsControlDetaches`

### Modes

- **BRIDGE-LAYOUT-018** — Under `Position`, a resize shall land in one frame while the move eases.
  - Tests: `LayoutTransitionTests.AResizeUnderPositionAloneLandsAtItsNewSizeInOneFrame`
- **BRIDGE-LAYOUT-019** — Under `PositionAndSize`, when the size changes to a positive size, or a scale from an interrupted move is still held, the transition shall invert the size ratio onto the composition `Scale` anchored at the top-left corner and ease it to 1.
  - Tests: `LayoutTransitionTests.AResizeUnderPositionAndSizeGrowsIntoPlace`
- **BRIDGE-LAYOUT-020** — Under `PositionAndSizeCorrected`, while a resize scales the control, each direct visual child shall be counter-scaled on the render thread by `1 / parent scale` so it keeps its size.
  - Tests: `CorrectedLayoutTransitionTests.APlainGrowShrinksTheChildMidFlight`, `ReferencePagesTests.TheCorrectedAvatarKeepsItsSizeWhileItsCardGrows`
- **BRIDGE-LAYOUT-021** — Each counter-scaled child shall be anchored at the point as far across itself as it sits across the room the control leaves it, so it keeps its alignment.
  - Tests: `CorrectedLayoutTransitionTests.TheChildKeepsItsAlignmentInsideTheCardAsItIsDrawn`
- **BRIDGE-LAYOUT-022** — When the move completes, is canceled or is reverted, every counter-scaled child shall be put back at identity scale and anchor with nothing driving it.
  - Tests: `CorrectedLayoutTransitionTests.TheChildIsBackAtIdentityWithNothingDrivingItOnceTheMoveLands`, `CorrectedLayoutTransitionTests.CancelingOrRevertingMidFlightRestoresTheChild`
- **BRIDGE-LAYOUT-023** — A resize interrupted mid-flight shall keep the counter-scale already on each child, and a child no longer a direct child shall be restored.
  - Tests: `CorrectedLayoutTransitionTests.AGrowInterruptedMidFlightKeepsTheCorrection`
- **BRIDGE-LAYOUT-024** — A move without a resize shall leave the children alone.
  - Tests: `CorrectedLayoutTransitionTests.APositionOnlyMoveLeavesTheChildAlone`
- **BRIDGE-LAYOUT-025** — The counter-scale shall take each child's composition `Scale` through the compositor registry, and when another owner takes it back the child shall be restored to identity.
  - Tests: `CorrectedLayoutTransitionTests.ACorrectedGrowTakesTheScaleOverFromACompositorAnimationOnTheChild`
- **BRIDGE-LAYOUT-026** — What the control draws itself (background, border, shadow, corner radius) shall still scale under both size modes.
  - Tests: manual — watch the growing panel and avatar card on the sample's Layout page on the Desktop head.

### Ending and ownership

- **BRIDGE-LAYOUT-027** — `Cancel` shall stop the move in flight and leave the control in its layout slot, still armed for later moves.
  - Tests: `LayoutTransitionTests.EndingATransitionLeavesTheControlInItsLayoutSlot`
- **BRIDGE-LAYOUT-028** — `Revert` shall cancel and stop easing later moves, so the control moves in one frame again.
  - Tests: `LayoutTransitionTests.EndingATransitionLeavesTheControlInItsLayoutSlot`
- **BRIDGE-LAYOUT-029** — When a compositor animation takes the control's `Translation` or `Scale`, the transition shall release it, leaving the control frozen where it is drawn, and stop the move once it drives nothing.
  - Tests: `CompositorRegistryTests.ACompositorAnimationTakesTheTranslationFromALayoutTransition`, `CompositorRegistryTests.ALayoutTransitionTakesTheTranslationFromACompositorAnimation`
- **BRIDGE-LAYOUT-030** — `Presto.Remove(control)` shall stop the move in flight, freezing the control where it is drawn.
  - Tests: `CompositorRegistryTests.RemoveEndsALayoutTransition`
- **BRIDGE-LAYOUT-031** — In a Debug build, a transition that starts holding a property of a control whose transform shorthands are animating shall log one warning, however often layout moves it afterwards.
  - Tests: `CompositorRegistryTests.MixingAShorthandAndALayoutTransitionWarnsOnceHoweverOftenLayoutMoves`, `CompositorRegistryTests.AShorthandThatHasEndedDoesNotWarnWhenALayoutTransitionMoves`

### Shared elements

- **BRIDGE-LAYOUT-032** — `AnimateSharedElement(source, target, …)` shall fly `target` from where `source` is drawn now (its on-screen slot, its render-transform translation, and the offset and scale of its own live transition) into the slot layout gives `target` next.
  - Tests: `LayoutTransitionTests.ASharedElementMoveStartsWhereTheSourceWasAndLandsInTheTargetSlot`, `LayoutTransitionTests.ASharedElementMoveStartsWhereTheSourceIsDrawnByItsRenderTransform`, `LayoutTransitionTests.ReArmingAMoveMidFlightAsASharedElementCarriesOnFromWhereItIsDrawn`
- **BRIDGE-LAYOUT-033** — The on-screen start shall be moved into `target`'s layout frame at `target`'s first layout pass by adding `target`'s own scroll offsets, so scrolled sources and targets land right.
  - Tests: `LayoutTransitionTests.ASharedElementOutOfAScrolledListStartsWhereItIsDrawn`, `LayoutTransitionTests.ASharedElementIntoAScrolledListStartsWhereTheSourceIsDrawn`
- **BRIDGE-LAYOUT-034** — With `once` true (the default) the transition shall stop listening to layout once its move has started; with `once` false it shall keep easing later moves as `AnimateLayout` does.
  - Tests: untested
- **BRIDGE-LAYOUT-035** (inherited) — When `source` is not in a window, the transition shall measure `target` itself as First, so nothing flies.
  - Tests: untested
- **BRIDGE-LAYOUT-036** — `PrepareSharedElement` shall record only where `source` is drawn under the key, replacing any earlier entry, and return true; when `source` is not on screen it shall remove the key's entry and return false.
  - Tests: `LayoutTransitionTests.AKeyedSharedElementFliesFromWhereItsSourceWasPreparedAfterTheSourceIsGone`
- **BRIDGE-LAYOUT-037** — A keyed `AnimateSharedElement` shall take the key's entry and fly `target` from it, even after the source has left the tree.
  - Tests: `LayoutTransitionTests.AKeyedSharedElementFliesFromWhereItsSourceWasPreparedAfterTheSourceIsGone`
- **BRIDGE-LAYOUT-038** — When the key has no entry, or its entry is more than three seconds old, a keyed `AnimateSharedElement` shall return null, discard the entry and leave `target` alone.
  - Tests: `LayoutTransitionTests.AKeyedSharedElementWithNothingPreparedReturnsNullAndLeavesTheTargetAlone`, `ConnectedTests.APreparedRectIsTakenWithinThreeSecondsAndExpiresAfter`

### Builder motion

- **BRIDGE-LAYOUT-039** — The builder overloads shall take the builder's `Duration` (the engine default when unset), `Delay` (0 when unset) and `Ease`, and per-axis eases given as `.X(ease:)`, `.Y(ease:)` and `.Scale(ease:)`; the lambda overloads shall behave as the builder they build.
  - Tests: `AnimationBuilderBridgeTests.ALambdaGivesALayoutTransitionTheMotionItsBuilderGives`, `LayoutTransitionTests.DifferentAxisEasesFlyTheSharedElementInAnArc`
- **BRIDGE-LAYOUT-040** — When the builder animates a property other than X, Y or Scale, gives one of those anything but an ease, sets `Keyframes`, a loop, direction, autoplay, frame rate, playback rate or ease, composition, modifier, id, priority or any callback, or gives a per-target duration or delay, the factory shall throw `NotSupportedException` naming it.
  - Tests: `LayoutTransitionTests.ABuilderThatGivesALayoutAxisAValueOrSetsAnythingElseThrows`

### Reduced motion

- **BRIDGE-LAYOUT-041** — For the `(duration, ease)` overloads, while `PrestoDefaults.ReducedMotion` is set as a move starts, the move shall land in one frame; a transition created while the flag was clear lands once it is set.
  - Tests: `LayoutTransitionTests.ADurationLayoutMoveLandsInOneFrameOnlyUnderReducedMotion`, `LayoutTransitionTests.APersistentDurationLayoutTransitionLandsOnceReducedMotionIsSet`, `LayoutTransitionTests.ADurationSharedElementMoveLandsInOneFrameOnlyUnderReducedMotion`, `LayoutTransitionTests.ADurationKeyedSharedElementLandsInOneFrameOnlyUnderReducedMotion`
- **BRIDGE-LAYOUT-042** — For the builder overloads, the builder's reduced form shall be read once, when the transition is created, and `ReduceAll` shall make each move land in one frame.
  - Tests: `LayoutTransitionTests.AReducedOrZeroDurationMoveLandsInOneFrame`

### Defaults

- **BRIDGE-LAYOUT-043** — When `AnimateLayout` is given no mode, the transition shall use `Position`.
  - Tests: `LayoutTransitionTests.AResizeUnderPositionAloneLandsAtItsNewSizeInOneFrame`
- **BRIDGE-LAYOUT-044** — When `AnimateSharedElement` is given no mode, the flight shall use `PositionAndSize`.
  - Tests: untested
- **BRIDGE-LAYOUT-045** (inherited) — Before its first move, a transition's `Progress` shall be 0.
  - Tests: untested
- **BRIDGE-LAYOUT-046** (inherited) — Before its first move, a transition's `Completed` shall be true.
  - Tests: `LayoutTransitionTests.ScrollingAnArmedControlIsNotALayoutMove`

## Invariants

- At most one live transition per control.
- Layout is never delayed: `Bounds` is correct in the pass that moved the control, and only the pixels lag.
- With no move in flight, the transition's composition `Translation` is zero and its `Scale` one.

## Edge cases

- A control is hit-tested at its new slot while it is still drawn on the way there.
- A control flying into a clipping ancestor (a `ScrollViewer`) is clipped by it until it lands.
- Passing the same control as `source` and `target` flies it from where it is drawn now into its next slot (a reparent).
- After `Presto.Remove`, the transition stays armed and keeps the frozen offset in its mirror, so the next move starts from where the control is drawn.
- A control whose new size is zero in either axis never scales; it only moves.
- A ContentSize host never scales, whatever the mode (see BRIDGE-CONTENTSIZE), and moves beside a resizing host land in one frame.

## Threading and lifetime

- Created and measured on the UI thread on `LayoutUpdated`; the move plays on the render thread and survives a blocked UI thread except on Browser and headless (BRIDGE-THREAD: Browser's blocked-UI-thread limitation is documented, not worked around). Freeze-proofing is checked by hand on the Layout page with the freeze button.
- The transition subscribes to the control's `LayoutUpdated` and is kept in a weak per-control table; `Revert` unsubscribes. Prepared entries hold a rectangle and a timestamp, never a control.
- Headless green is not acceptance for layout motion: new motion is watched on the Desktop head.

## Reduced motion

- BRIDGE-LAYOUT-041 and -042. The `(duration, ease)` overloads read the flag per move (CORE-REDUCED: the flag has no change notification); builder overloads only at creation, through the builder's `WhenReduced`.

## Allocation

- A pass that does not move the control measures one parent walk and allocates nothing. Each move allocates a mirror animation and composition animations.

## Provenance

- Upstream `createLayout` records and animates a root's subtree on demand (`record()`, `animate()`), animating real CSS size and transform with enter, leave and swap states. Presto arms per control, reacts to every layout pass automatically, and inverts onto composition `Translation`/`Scale` instead of an implicit `Offset` animation, because an implicit `Offset` animation snaps on the Windows desktop backend while an explicit one on `Translation` survives a blocked UI thread. Enter and leave belong to `Presence`, because they need a presenter hook rather than a layout hook.
- Shared elements and keyed preparation follow the Windows Community Toolkit's connected animations, not anime.js, because upstream has no shared-element API and framework-driven navigation needs one.

## Decisions

- **Layout transitions are FLIP inverted onto `Translation`.** First and Last are measured on the UI thread, the inverse is written to the element visual's `Translation` (and `Scale` for size), and an explicit `StartAnimation` plays it to identity on the render thread; enter and leave states stay out. Reason: Avalonia never writes `Translation`, the explicit path survives a blocked UI thread, and it also covers size changes and moves between parents; enter and leave need a presenter hook, not a layout hook. Rejected: an implicit animation on `Offset`, built and measured: it interpolates headless but snaps on the Win32 backend, as a bare Avalonia implicit animation does.
- **Size inversion is anchored at the top-left corner.** The inverse `Scale` uses a `CenterPoint` of zero rather than the compositor animation's origin, and the invert lands in the same commit as layout's new offset. Reason: the inverse scale must pivot on the corner the offset math uses, and a later commit would render one frame at the new position first.
- **Interruption retargets from a UI-side mirror.** Each move keeps a mirror Presto animation on scratch `(x, y, sx, sy)`; a second layout change measures First as the slot plus the mirror's current translation, and the mirror supplies `Then()`, `OnComplete` and `Progress`. Reason: the UI thread cannot read a server value, and server animations have no completion event.
- **`Position` is the default; `PositionAndSize` scales the content.** `Position` translates only, and `PositionAndSize` scales the control and everything it draws. Reason: counter-scaling children as Motion does means writing every child's visual, so the scaling is documented rather than hidden.
- **`PositionAndSizeCorrected` counter-scales direct children only.** Each direct visual child gets a render-thread expression of `1 / parent scale`, anchored by a static center proportional to its position in the parent's spare room, its `Scale` taken through the compositor registry and restored at the end; what the control draws itself still scales. Reason: deeper descendants ride their corrected child, drawn at final size from the first frame, and corner radius is not a composition property; `Animate.ContentSize` is the answer for rounded surfaces.
- **Headless green is not acceptance for layout motion.** A layout-motion implementation counts as working only once the sample has been watched on the Desktop head. Reason: the `Offset` version passed a pixel-accurate headless interpolation test and still snapped on Win32.
- **Shared-element sources are measured on screen.** A source is measured where it is drawn, scroll included, and the rect is moved into the target's layout frame at the target's first pass by adding the target's scroll offsets; later passes take scroll out. Reason: taking scroll out keeps scrolling from counting as a move, but a flight between a scrolled and an unscrolled view would land off by the difference in their offsets.
- **Keyed shared elements store a rect, return null when unprepared, and expire.** `PrepareSharedElement` stores only the on-screen rect under the key, replacing any earlier entry; the keyed `AnimateSharedElement` consumes it, returns null when there is none, and an entry expires three seconds after it was prepared. Reason: framework-driven navigation never has both controls at once, storing no control keeps nothing alive, null mirrors the Windows Community Toolkit's `GetAnimation(key)?.TryStart(...)`, and the expiry stops a navigation that never completed from flying a later, unrelated element from a stale place.
- **Layout-transition builders carry timing and eases only; per-axis eases make arcs.** Builders give `Duration`, `Delay` (the control holds its inverted place during it) and `Ease`; `X`, `Y` and `Scale` may carry only an ease, and anything else throws `NotSupportedException` naming it. One ease plays as one exact segment, differing X and Y eases are sampled into 100 segments, and the mirror carries the same eases. Reason: the layout decides where the control goes, so it owns the values, and the mirror must follow the arc so an interruption measures where the control is drawn.
- **Zero-length moves are not started.** A move whose compiled track has zero duration and zero delay lands in one frame without a composition animation. Reason: a zero-length composition animation can draw its first key for a frame, which broke `ReduceAll` making layout moves instant.
- **Duration overloads land at once under the flag.** The `(duration, ease)` overloads read `ReducedMotion` as each move starts and land in one frame, as `Recipes.Reflow`'s reduced form does. Reason: they built their motion directly and kept easing, forcing workarounds in the sample.
- **The flag has no change notification.** See CORE-REDUCED.
- **Only pure eases cross to the render thread.** See BRIDGE-THREAD.
- **One owner per composition property.** See BRIDGE-THREAD.
- **Arming on attach records, never eases.** See BRIDGE-DECLLAYOUT.
- **A content size host owns its size.** See BRIDGE-CONTENTSIZE.
- **Wall-clock reads take a `TimeProvider`.** See CORE-ENGINE.

## Non-goals

- Enter and leave transitions; lifting a flying control into an overlay; cross-fading dissimilar shared elements.
- Counter-scaling below direct children, or correcting what the control draws itself (use BRIDGE-CONTENTSIZE).

## Open items

- `Then()` before the first move resolves at once, so awaiting a fresh shared-element flight right after creating it returns before it flies (BRIDGE-DECLLAYOUT waits a layout pass for this reason).
- A source that is not on screen silently degrades to a one-shot `AnimateLayout` on the target (BRIDGE-LAYOUT-035) rather than returning null as the keyed form does.
- Untested: scope registration (005), the no-op pass (008), `once: false` (034), an off-screen source (035).
