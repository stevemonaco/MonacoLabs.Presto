---
id: BRIDGE-PAGER
title: Pager carousel and indicator
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto.Controls
sources:
  - MonacoLabs.Presto.Avalonia/Controls/Pager.cs
  - MonacoLabs.Presto.Avalonia/Controls/PagerIndicator.cs
  - MonacoLabs.Presto.Avalonia/Controls/PagerIndicatorMode.cs
types:
  - Pager
  - PagerIndicator
  - PagerIndicatorMode
tests:
  - PagerTests
  - CarouselPageTests
upstream: []
depends:
  - BRIDGE-DRAGGABLE
  - BRIDGE-TARGETS
  - BRIDGE-TOKENS
  - CORE-MOTIONVALUE
  - CORE-INERTIA
  - CORE-TIMER
  - CORE-EASES
  - CORE-STAGGER
  - CORE-REDUCED
---

# Pager carousel and indicator

## Purpose

`Pager` is a carousel control: its slides sit side by side on a private strip, each as wide as the pager, and a horizontal drag throws the strip through inertia and snaps it to a slide. It pages by keyboard and `GoTo`, can loop and autoplay, and exposes continuous `MotionValue`s for parallax and progress rings. `PagerIndicator` follows a pager as dots or a progress bar and pages it when tapped. The drag is a `Draggable` (BRIDGE-DRAGGABLE) on the strip's `X` shorthand.

## Requirements

### Layout

- **BRIDGE-PAGER-001** — The pager shall lay each slide out at the pager's full width and height, slide `i` in column `i` of the strip.
  - Tests: `PagerTests.TheBoundsFollowAResize`
- **BRIDGE-PAGER-002** (inherited) — When measured with an infinite width or height, the pager shall take that dimension from the largest slide's desired size; otherwise it shall take the available size.
  - Tests: `PagerTests.AnInfiniteMeasureTakesTheLargestSlideAndAFiniteOneTheAvailableSize`
- **BRIDGE-PAGER-003** — When the pager is attached to a visual tree, or arranged at a width different from the last one, it shall refresh the drag bounds and place the strip on `SelectedIndex` at once, without animation.
  - Tests: `PagerTests.TheBoundsFollowAResize`
- **BRIDGE-PAGER-004** — While `Loop` is true, the pager shall arrange each slide at the copy of its column nearest the page shown, re-arranging on every move, so a drag past the last slide reveals the first.
  - Tests: `PagerTests.LoopingWrapsOnToTheFirstSlideAndBackToTheLast`
- **BRIDGE-PAGER-005** (inherited) — When `Loop` changes, the pager shall refresh the drag bounds and re-arrange the strip.
  - Tests: `PagerTests.TurningLoopOnAndOffAtRuntimeReArrangesTheStripAndItsBounds`

### Drag

- **BRIDGE-PAGER-006** — The pager shall move the strip through a `Draggable` on its `X` shorthand, with the pager itself as trigger, the Y axis disabled, snapping to multiples of the pager's width and releasing through `Inertia`.
  - Tests: `PagerTests.ADragReleasedPastHalfASlidePages`, `PagerTests.AFlingPages`, `CarouselPageTests.AHardFlickCoastsPastSeveralSlidesAndLandsOnOne`
- **BRIDGE-PAGER-007** — While `Loop` is false, the drag shall be bounded to the span from the first slide to the last; while it is true, it shall be unbounded horizontally.
  - Tests: `PagerTests.TheBoundsFollowAResize`, `PagerTests.LoopingWrapsOnToTheFirstSlideAndBackToTheLast`
- **BRIDGE-PAGER-008** (inherited) — When the strip is grabbed, the pager shall pause any running travel animation.
  - Tests: `PagerTests.GrabbingTheStripPausesTheTravel`
- **BRIDGE-PAGER-009** — When a drag settles, the pager shall set `SelectedIndex` to the rounded `Position`, wrapped when looping and clamped otherwise, without starting a new travel.
  - Tests: `PagerTests.ADragReleasedPastHalfASlidePages`, `PagerTests.AFlingPages`
- **BRIDGE-PAGER-010** — The pager shall set `Position` to `-stripX / width` from the strip's render, in the same frame the strip moves, and to 0 while the width is 0.
  - Tests: `PagerTests.GoToSpringsAndSettlesOnTheIndex`, `PagerTests.TheParallaxValueTracksTheOffset`
- **BRIDGE-PAGER-011** (inherited) — While `Loop` is true, `Position` shall count on past the last slide rather than wrap (four slides paged forward four times read 4).
  - Tests: `PagerTests.LoopingWrapsOnToTheFirstSlideAndBackToTheLast`

### Paging

- **BRIDGE-PAGER-012** — When `GoTo(index)` is called on a pager with slides, it shall set `SelectedIndex` to `index` wrapped (when looping) or clamped to the slides, spring the strip there with `Eases.Spring(bounce: .2, duration: 700)` and restart autoplay.
  - Tests: `PagerTests.GoToSpringsAndSettlesOnTheIndex`
- **BRIDGE-PAGER-013** (inherited) — When `GoTo` is called on a pager with no slides, it shall do nothing.
  - Tests: `PagerTests.GoToWithNoSlidesDoesNothing`
- **BRIDGE-PAGER-014** — When `SelectedIndex` is set on a pager with slides, the pager shall coerce it as BRIDGE-PAGER-012 and BRIDGE-PAGER-050 coerce `GoTo` (wrapped when looping, clamped otherwise), a two-way binding's source receiving the coerced value; when it is set other than by a settling drag or `GoTo`, the pager shall spring the strip to the coerced slide and restart autoplay.
  - Tests: `PagerTests.ASelectedIndexPastTheSlidesIsClampedAndTheStripRestsOnTheLast`, `PagerTests.ALoopingSelectedIndexWrapsAndIsReCoercedWhenLoopChanges`, `PagerTests.ATwoWayBindingSourceGetsTheCoercedIndex`
- **BRIDGE-PAGER-052** — When `Slides` or `Loop` changes, the pager shall coerce `SelectedIndex` again from the value last set, springing to the slide it now names when that changes.
  - Tests: `PagerTests.RemovingSlidesPastTheIndexReCoercesIt`, `PagerTests.ALoopingSelectedIndexWrapsAndIsReCoercedWhenLoopChanges`
- **BRIDGE-PAGER-053** — While the pager has no slides, `SelectedIndex` shall keep the value set, so an index set before the slides are added (an earlier XAML attribute) survives once they are.
  - Tests: `PagerTests.ASelectedIndexSetBeforeTheSlidesAreAddedSurvives`
- **BRIDGE-PAGER-015** — While looping, a travel shall take the slide's column nearest the page shown, so paging takes the shorter way round.
  - Tests: `PagerTests.LoopingWrapsOnToTheFirstSlideAndBackToTheLast`
- **BRIDGE-PAGER-016** (inherited) — While the pager is detached or 0 wide, a change of `SelectedIndex` shall not move the strip; the next attach or arrange shall place it.
  - Tests: `PagerTests.ASelectionWhileDetachedOrZeroWideWaitsForTheNextPlacement`
- **BRIDGE-PAGER-017** — When the pager receives an unhandled Left, Right, Home or End key, it shall `GoTo` the previous, next, first or last slide and mark the key handled; other keys shall pass.
  - Tests: `PagerTests.KeysPage`
- **BRIDGE-PAGER-018** — `OffsetOf(slide)` shall return a `MotionValue` mapped from `Position` that reads the slide's column (the nearest copy when looping) minus `Position`: negative to the left, positive to the right.
  - Tests: `PagerTests.TheParallaxValueTracksTheOffset`, `CarouselPageTests.DraggingTheStripMovesEachParallaxLayerAtItsOwnRate`
- **BRIDGE-PAGER-050** — While `Loop` is true, `GoTo` shall wrap the index by positive modulo of the slide count, so on four slides 7 selects 3, −2 selects 2, and Right on the last slide selects 0 while `Position` counts on to 4.
  - Tests: `PagerTests.LoopingWrapsOnToTheFirstSlideAndBackToTheLast`
- **BRIDGE-PAGER-051** (inherited) — When Left on the first slide or Right on the last slide of a non-looping pager clamps to the slide shown, the pager shall stay on it, still restart autoplay and still mark the key handled.
  - Tests: `PagerTests.KeysPage`

### Autoplay

- **BRIDGE-PAGER-019** — While attached with `AutoplayInterval` above 0, the pager shall run a looping timer of that duration that advances one slide per iteration, from the last slide to the first even when `Loop` is false.
  - Tests: `PagerTests.AutoplayAdvancesAndHolds`, `PagerTests.AutoplayRewindsFromTheLastSlideWithoutLooping`, `CarouselPageTests.AutoplayAdvancesEveryFourSeconds`
- **BRIDGE-PAGER-020** — When `AutoplayInterval` changes while attached, the pager shall cancel the running timer, reset `AutoplayProgress` to 0, and start a new timer if the interval is above 0.
  - Tests: `PagerTests.AutoplayAdvancesAndHolds`
- **BRIDGE-PAGER-021** — While autoplay runs, `AutoplayProgress` shall read the timer's iteration progress, 0 to 1.
  - Tests: `PagerTests.AutoplayAdvancesAndHolds`
- **BRIDGE-PAGER-022** — When the user pages by `GoTo`, a key, an indicator tap, a set `SelectedIndex` or a drag release, the pager shall restart the autoplay interval and reset `AutoplayProgress` to 0.
  - Tests: `PagerTests.PagingByHandRestartsTheAutoplayInterval`
- **BRIDGE-PAGER-023** — While the pointer is over the pager, while the strip is dragged, or while focus inside it arrived by Tab or directional navigation, autoplay shall pause; it shall resume when none of these holds.
  - Tests: `PagerTests.AutoplayAdvancesAndHolds`, `CarouselPageTests.AutoplayHoldsWhileThePointerIsOverTheStrip`
- **BRIDGE-PAGER-024** — Focus that arrives by pointer or programmatically shall not hold autoplay.
  - Tests: `PagerTests.FocusByPointerOrCodeDoesNotHoldAutoplay`

### Indicator

- **BRIDGE-PAGER-025** — While attached, the indicator shall follow `Pager`: its `SelectedIndex`, its `Slides` collection and its `Position`; when detached it shall stop following, and when `Pager` changes while in a top level it shall switch.
  - Tests: `PagerTests.TheDotsWidenTheSelectedOne`
- **BRIDGE-PAGER-026** — In `Dots` mode the indicator shall show one 8x8 dot per slide, 8 DIPs apart, the selected one 24 wide in `Fill` and the rest 8 wide in `Track`, and measure as their total width by 8 high.
  - Tests: `PagerTests.TheDotsWidenTheSelectedOne`
- **BRIDGE-PAGER-027** — When the followed pager's `SelectedIndex` changes in `Dots` mode, the indicator shall animate each dot's width and brush to its new state over `Tokens.Medium` with `Tokens.Ease`, staggered by `Tokens.StaggerStep / 2` from the selected dot.
  - Tests: `PagerTests.TheDotsWidenTheSelectedOne`, `CarouselPageTests.TheNextArrowSettlesTheStripOneSlideOnAndMarksItsDot`
- **BRIDGE-PAGER-028** — In `Progress` mode the indicator shall show a full-width track in `Track` and a bar in `Fill` whose width is `min(1, (page + 1) / count)` of the indicator's, `page` being `Position` wrapped when the pager loops and clamped otherwise, re-arranged as `Position` moves.
  - Tests: `PagerTests.TheProgressBarFillsWithThePosition`
- **BRIDGE-PAGER-029** (inherited) — In `Progress` mode the indicator shall measure 4 DIPs high and as wide as available, or 24 per slide when the available width is infinite.
  - Tests: `PagerTests.AProgressBarIsFourHighAndAsWideAsOfferedOrTwentyFourPerSlide`
- **BRIDGE-PAGER-030** (inherited) — When `Mode`, `Fill`, `Track` or the pager's slide collection changes, the indicator shall rebuild its dots or bar in their resting state.
  - Tests: `PagerTests.ChangingTheLookOrTheSlidesRebuildsTheIndicatorAtRest`
- **BRIDGE-PAGER-031** (inherited) — The indicator shall take hits over its whole width in a band at least 24 DIPs high centered on it, its dots taking none, so the hand cursor and the tap are the indicator's.
  - Tests: `PagerTests.ThePointerOverADotIsOverTheIndicatorSoItGetsTheHandCursor`, `PagerTests.ATapJustAboveADotStillSelectsIt`
- **BRIDGE-PAGER-032** — When tapped in `Dots` mode, the indicator shall `GoTo` the dot whose span, extended by half the gap on either side, holds the tap (the last dot beyond them), and mark the tap handled.
  - Tests: `PagerTests.TappingTheIndicatorSlidesToThatSlide`, `CarouselPageTests.ClickingADotSlidesToIt`
- **BRIDGE-PAGER-033** (inherited) — When tapped in `Progress` mode, the indicator shall `GoTo` slide `min(count - 1, floor(x / width · count))` and mark the tap handled.
  - Tests: `PagerTests.TappingTheIndicatorSlidesToThatSlide`
- **BRIDGE-PAGER-034** (inherited) — When tapped with no pager or a pager with no slides, the indicator shall do nothing.
  - Tests: `PagerTests.TappingAnIndicatorWithNoPagerOrNoSlidesDoesNothing`

### Reduced motion

- **BRIDGE-PAGER-035** — While `ReducedMotion` is set, read at each travel, a `GoTo` or `SelectedIndex` change shall set the strip on its slide at once.
  - Tests: `PagerTests.UnderReducedMotionPagingLandsAtOnceAndAutoplayStillPages`
- **BRIDGE-PAGER-036** — While `ReducedMotion` is set, a release shall land the strip at once on the slide its fling projects to (`x + v · cos(angle) · TimeConstant`, snapped to the width and clamped unless looping) and settle `SelectedIndex` there.
  - Tests: `PagerTests.UnderReducedMotionPagingLandsAtOnceAndAutoplayStillPages`
- **BRIDGE-PAGER-037** — While `ReducedMotion` is set, autoplay shall still page, landing at once, and `AutoplayProgress` shall stay at 0.
  - Tests: `PagerTests.UnderReducedMotionPagingLandsAtOnceAndAutoplayStillPages`, `CarouselPageTests.UnderReducedMotionAutoplayAndTheNextArrowJumpASlideOn`
- **BRIDGE-PAGER-038** — While `ReducedMotion` is set at a selection change, the dots shall change with no delay and no duration.
  - Tests: `PagerTests.UnderReducedMotionPagingLandsAtOnceAndAutoplayStillPages`

### Lifetime

- **BRIDGE-PAGER-039** (inherited) — When the pager is detached, it shall pause its travel, cancel autoplay and revert its draggable.
  - Tests: `PagerTests.DetachingPausesTheTravelCancelsAutoplayAndRevertsTheDraggable`

### Defaults

- **BRIDGE-PAGER-040** (inherited) — When `SelectedIndex` is not set, the pager shall rest on slide 0.
  - Tests: `PagerTests.TheDotsWidenTheSelectedOne`
- **BRIDGE-PAGER-041** — When a binding to `SelectedIndex` names no mode, the pager shall bind it two-way.
  - Tests: `PagerTests.ABindingWithNoModeWritesPagingBackToTheSource`, `PagerTests.ATwoWayBindingSourceGetsTheCoercedIndex`
- **BRIDGE-PAGER-042** (inherited) — When `Loop` is not set, the pager shall not loop.
  - Tests: `PagerTests.TheBoundsFollowAResize`
- **BRIDGE-PAGER-043** (inherited) — When `AutoplayInterval` is not set, it shall be 0 and the pager shall not autoplay.
  - Tests: `PagerTests.APagerDefaultsToFocusableClippedAndNotAutoplaying`
- **BRIDGE-PAGER-044** — When `Focusable` and `ClipToBounds` are not set, the pager shall be focusable and clip to its bounds.
  - Tests: `PagerTests.APagerDefaultsToFocusableClippedAndNotAutoplaying`
- **BRIDGE-PAGER-045** (inherited) — When the indicator's `Pager` is not set, the indicator shall follow no pager.
  - Tests: `PagerTests.AnIndicatorDefaultsToWhiteDotsWithAHandCursorAndNoPager`
- **BRIDGE-PAGER-046** (inherited) — When `Mode` is not set, the indicator shall show dots.
  - Tests: `PagerTests.AnIndicatorDefaultsToWhiteDotsWithAHandCursorAndNoPager`
- **BRIDGE-PAGER-047** (inherited) — When `Fill` is not set, the indicator shall fill with opaque white.
  - Tests: `PagerTests.AnIndicatorDefaultsToWhiteDotsWithAHandCursorAndNoPager`
- **BRIDGE-PAGER-048** (inherited) — When `Track` is not set, the indicator shall draw its track in white at 40 % opacity (`#66FFFFFF`).
  - Tests: `PagerTests.AnIndicatorDefaultsToWhiteDotsWithAHandCursorAndNoPager`
- **BRIDGE-PAGER-049** (inherited) — When `Cursor` is not set, the indicator shall show the hand cursor.
  - Tests: `PagerTests.AnIndicatorDefaultsToWhiteDotsWithAHandCursorAndNoPager`

## Invariants

- After a drag settles, `SelectedIndex` is in `[0, Slides.Count - 1]`, and `Position` equals it modulo the slide count.
- Each slide is exactly the pager's size; the strip is the only visual and logical child.
- `AutoplayProgress` is in `[0, 1]` and is 0 while autoplay is off or reduced.

## Edge cases

- No slides: `GoTo`, a settle and an autoplay advance do nothing; the indicator draws nothing in `Dots` mode and an empty bar in `Progress` mode.
- A 0-wide pager does not travel, and the snap passes X through unchanged.
- Left at the first slide of a non-looping pager calls `GoTo(-1)`, which clamps to 0, still restarts autoplay and still handles the key.
- A drag ending while a travel animation is paused leaves that animation paused; the next travel replaces it.
- Escape mid-swipe springs the strip back to the slide it was grabbed on, through the draggable's default (BRIDGE-DRAGGABLE-066).

## Threading and lifetime

- UI thread only. The draggable and autoplay timer exist only while the pager is attached; `Position`, `AutoplayProgress` and `OffsetOf` values live as long as the pager and survive re-attach.
- The indicator subscribes to its pager only while attached, so a detached indicator does not keep listening.

## Reduced motion

- Read at each travel, release, selection change and autoplay update: the strip lands at once, the dots change at once, autoplay pages and its progress stays 0 (BRIDGE-PAGER-035–038). Parallax layers move only as the strip does.

## Allocation

- A travel allocates one `Animation`; a selection change in `Dots` mode allocates one staggered animation over the dots. A looping drag re-arranges the strip each frame without allocating. Not otherwise on the tick path.

## Provenance

- No upstream counterpart.

## Decisions

- **`Pager` is its own control, not an attached form on `Carousel`.** Reason: Avalonia 12's `Carousel` pages through a virtualizing panel with its own swipe and page transition, realizes only the slides it shows and exposes no continuous offset, so a drag with inertia, a parallax value per slide and a looping strip would replace the whole control; the name avoids a clash with `Avalonia.Controls.Carousel`. Rejected: an attached form on Avalonia's `Carousel`.
- **The slides sit on a private strip moved by a `Draggable` the pager triggers.** The pager is untemplated, like `Sheet`, and the strip moves through its X shorthand. Reason: the slides give the look, and triggering from the pager keeps a press on a button inside a slide the button's.
- **A looping pager places each slide at its nearest column copy.** The strip is re-arranged on every move. Reason: not recorded. Rejected: cloning slides.
- **`Position` is set from the strip's render.** Reason: `OffsetOf` then moves parallax layers in the same frame as the strip. Rejected: following `XValue.Changed`.
- **`SelectedIndex` is coerced on the property.** It wraps or clamps like `GoTo`, is re-coerced when `Slides` or `Loop` change, and passes through while there are no slides. Reason: bindings, the indicator dots and anything watching the property read it, so it must read in range. Rejected: normalizing only at travel, which leaves the index and dots wrong while the view is right.
- **Autoplay holds for keyboard focus within, not any focus.** It also holds while the pointer is over the pager and while it is dragged, and restarts when the user pages. Reason: a click focuses the pager, so pausing on any focus would stop autoplay for good after the first drag.

## Non-goals

- No virtualization: every slide is realized and laid out.
- No templating or themes; the slides give the look.
- Not built on Avalonia's `Carousel` and has no page transition of its own.
- No vertical paging.

## Open items

None.
