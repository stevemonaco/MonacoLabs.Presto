---
id: BRIDGE-SHEET
title: Sheet and drawer
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto.Controls
sources:
  - MonacoLabs.Presto.Avalonia/Controls/Sheet.cs
  - MonacoLabs.Presto.Avalonia/Controls/SheetDetents.cs
  - MonacoLabs.Presto.Avalonia/Controls/Drawer.cs
  - MonacoLabs.Presto.Avalonia/Controls/Dismissal.cs
types:
  - Sheet
  - SheetDetents
  - Drawer
tests:
  - SheetTests
  - DrawerTests
  - GesturesPageTests
upstream: []
depends:
  - CORE-ANIMATABLE
  - CORE-INERTIA
  - CORE-MOTIONVALUE
  - CORE-REDUCED
  - BRIDGE-FOLLOW
  - BRIDGE-PRESENCE
  - BRIDGE-TOKENS
  - BRIDGE-RECIPES
---

# Sheet and drawer

## Purpose

`Sheet` is a control that slides its content out from an edge of its host over a scrim and rests on a list of detents. A drag moves it, and a release coasts through inertia to the detent nearest where the fling would carry it. `Drawer` is the side variant with an edge swipe to open. Both fill their host (a page's panel or the window's `OverlayLayer`); their drag is their own pointer handling, not a `Draggable`, so an inner `ScrollViewer` can keep a touch scroll.

## Requirements

### Detents and layout

- **BRIDGE-SHEET-001** — `SheetDetents.Parse` shall read comma- or space-separated `GridLength`s, turning a plain number up to 1 into a star length of that fraction and keeping larger numbers as DIPs, `n*` as stars and `Auto` as auto.
  - Tests: `SheetTests.ADragReleasedBetweenDetentsSettlesOnTheNearest`, `DrawerTests.AnEdgeSwipeOpensIt`
- **BRIDGE-SHEET-002** — The sheet shall resolve each detent to an extent along its axis: a star length as that fraction of the host's extent, a pixel length as DIPs, `Auto` as the content's desired extent, each clamped to `[0, host extent]`; no detents resolve as one detent at 0.
  - Tests: `SheetTests.OpeningShowsTheContentAndPlaysTheEntrancesInside`, `GesturesPageTests.TappingTheHandleStepsTheCurtainToHalfThenFullWithTheContentLinked`
- **BRIDGE-SHEET-003** (inherited) — The sheet shall measure its content even while it is hidden, and take the available size, or the content's size in an infinite dimension.
  - Tests: `SheetTests.ItFillsTheOverlayLayer`
- **BRIDGE-SHEET-004** — The sheet shall arrange its scrim over its whole bounds and its content panel just past its `Edge`, as deep as the last detent, so the panel's translate along the axis is its extent.
  - Tests: `SheetTests.ItFillsTheOverlayLayer`, `DrawerTests.AnEdgeSwipeOpensIt`
- **BRIDGE-SHEET-005** (inherited) — When the resolved detents change (host resized, `Detents`, `Edge` or content extent changed) and no drag is in progress, the sheet shall land on `DetentIndex`'s detent at once.
  - Tests: `GesturesPageTests.TappingTheHandleStepsTheCurtainToHalfThenFullWithTheContentLinked`
- **BRIDGE-SHEET-006** (inherited) — When `Edge` changes after the sheet has moved, the sheet shall revert its animatable and rebuild it on the new axis.
  - Tests: untested
- **BRIDGE-SHEET-007** — `Position` shall read the panel's extent out from its edge in DIPs, set from the panel's render in the same frame.
  - Tests: `SheetTests.TheScrimOpacityTracksThePosition`, `GesturesPageTests.PullingTheCurtainHalfwaySinksShrinksAndDimsTheContentWithIt`

### Open state

- **BRIDGE-SHEET-008** — When `DetentIndex` changes, the sheet shall set `IsOpen` to `DetentIndex > 0` and, unless the change came from a release, coast to that detent.
  - Tests: `SheetTests.AnInnerScrollerHandsTheDragOffAtItsTopAndScrollsOtherwise`, `GesturesPageTests.TappingTheHandleStepsTheCurtainToHalfThenFullWithTheContentLinked`
- **BRIDGE-SHEET-009** — When `IsOpen` is set true on a sheet at detent 0, the sheet shall go to detent 1; when set false on an open sheet, to detent 0.
  - Tests: `SheetTests.OpeningShowsTheContentAndPlaysTheEntrancesInside`, `SheetTests.EscapeBackOrAScrimTapClosesIt`
- **BRIDGE-SHEET-010** (inherited) — A `DetentIndex` beyond the detents shall resolve to the nearest end for its target, without being coerced.
  - Tests: untested
- **BRIDGE-SHEET-011** — While the sheet rests at an extent of 0, its panel shall be hidden; when it heads for an extent above 0 or is grabbed, the panel shall show, and it shall hide again when an animation to an extent of 0 completes.
  - Tests: `SheetTests.OpeningShowsTheContentAndPlaysTheEntrancesInside`, `SheetTests.EscapeBackOrAScrimTapClosesIt`
- **BRIDGE-SHEET-012** — When the panel shows, the entrances inside the content shall replay through `Presence.EnterOnShow` on the panel.
  - Tests: `SheetTests.OpeningShowsTheContentAndPlaysTheEntrancesInside`

### Scrim and dismissal

- **BRIDGE-SHEET-013** — The scrim's brush shall be `Scrim`, and its opacity shall follow the extent's progress from the first detent to the last, clamped to `[0, 1]`, and 0 when there are fewer than two detents or the last is not beyond the first.
  - Tests: `SheetTests.TheScrimOpacityTracksThePosition`, `GesturesPageTests.PullingTheCurtainHalfwaySinksShrinksAndDimsTheContentWithIt`
- **BRIDGE-SHEET-014** — The scrim shall take hits only while `IsOpen` is true, and a tap on it shall set `IsOpen` false.
  - Tests: `SheetTests.EscapeBackOrAScrimTapClosesIt`
- **BRIDGE-SHEET-015** — While the sheet is in a visual tree and open, an Escape key reaching its top level unhandled, or the top level's `BackRequested`, shall close it and mark the event handled; while closed, those events shall pass.
  - Tests: `SheetTests.EscapeBackOrAScrimTapClosesIt`

### Drag

- **BRIDGE-SHEET-016** — The sheet shall consider a press only on its panel or a descendant of it, or on its edge strip, one pointer at a time, and for a mouse only with the left button.
  - Tests: `DrawerTests.ASwipeAwayFromTheEdgeDoesNothing`
- **BRIDGE-SHEET-017** — Until the pointer moves more than 3 DIPs along or across the axis, the sheet shall not take the drag; when that first move is at least as far across as along, it shall drop the press.
  - Tests: untested
- **BRIDGE-SHEET-018** — When the press began inside a `ScrollViewer` within the panel that can still scroll the way the pointer moves, the sheet shall leave the drag to it, unless the move opens the sheet further while it is below its last detent.
  - Tests: `SheetTests.AnInnerScrollerHandsTheDragOffAtItsTopAndScrollsOtherwise`
- **BRIDGE-SHEET-019** (inherited) — When the sheet takes a drag, it shall capture the pointer, pause its running motion, show its panel and pick up from its current extent, including one rubber-banded past a bound.
  - Tests: `SheetTests.ADragReleasedBetweenDetentsSettlesOnTheNearest`
- **BRIDGE-SHEET-020** — While dragging, the sheet shall follow the pointer along its axis at once, rubber-banding with friction 0.35 past the first and last detents, and mark the moves handled and withheld from gesture recognizers.
  - Tests: `SheetTests.TheScrimOpacityTracksThePosition`, `SheetTests.AnInnerScrollerHandsTheDragOffAtItsTopAndScrollsOtherwise`
- **BRIDGE-SHEET-021** — When a drag is released or its capture is lost, the sheet shall project `extent + velocity · TimeConstant` (or keep the extent when it is outside the detents), clamp it to the detents, set `DetentIndex` to the nearest detent, and coast there through `Inertia` from the release velocity.
  - Tests: `SheetTests.ADragReleasedBetweenDetentsSettlesOnTheNearest`, `SheetTests.AFlingSkipsToTheNextDetent`
- **BRIDGE-SHEET-022** — When a coast's projected target lies past the first or last detent, the sheet shall let it cross the bound and bounce back onto it.
  - Tests: `GesturesPageTests.AFlickUpCoastsTheSheetOpenPastItsStopAndBouncesBackOntoIt`
- **BRIDGE-SHEET-023** (inherited) — When a release follows an actual drag, the sheet shall mark the release handled; a press that never became a drag shall leave the release unhandled.
  - Tests: untested

### Drawer

- **BRIDGE-SHEET-024** — A `Drawer` shall default `Edge` to `Left`.
  - Tests: untested
- **BRIDGE-SHEET-025** — While a `Drawer`'s panel is hidden, a 20-DIP strip along its edge shall take presses, so a swipe in from that edge opens it.
  - Tests: `DrawerTests.AnEdgeSwipeOpensIt`
- **BRIDGE-SHEET-026** — A swipe starting outside the edge strip of a closed `Drawer` shall not open it.
  - Tests: `DrawerTests.ASwipeAwayFromTheEdgeDoesNothing`
- **BRIDGE-SHEET-027** (inherited) — A plain `Sheet` shall have a 0-wide edge strip, so a closed, hidden sheet takes no drag.
  - Tests: untested

### Reduced motion

- **BRIDGE-SHEET-028** — While `ReducedMotion` is set, read at each release or `DetentIndex` change, the sheet shall land on its detent at once instead of coasting.
  - Tests: `SheetTests.UnderReducedMotionItLandsAtOnceAndTheScrimFades`, `DrawerTests.UnderReducedMotionAnEdgeSwipeLandsOpenAtOnce`
- **BRIDGE-SHEET-029** — While `ReducedMotion` is set, the scrim's opacity shall fade to its new value over `Tokens.Short`, linearly, rather than jump.
  - Tests: `SheetTests.UnderReducedMotionItLandsAtOnceAndTheScrimFades`

### Defaults

- **BRIDGE-SHEET-030** — When `Detents` is not set, the sheet shall rest on `"0,Auto"`: closed, and open at its content's own extent.
  - Tests: untested
- **BRIDGE-SHEET-031** — When `Edge` is not set on a `Sheet`, it shall slide out from the bottom edge.
  - Tests: `SheetTests.ADragReleasedBetweenDetentsSettlesOnTheNearest`
- **BRIDGE-SHEET-032** (inherited) — When `DetentIndex` and `IsOpen` are not set, the sheet shall rest closed on its first detent.
  - Tests: untested
- **BRIDGE-SHEET-033** — `DetentIndex` and `IsOpen` shall bind two-way by default.
  - Tests: untested
- **BRIDGE-SHEET-034** — When `Scrim` is not set, the scrim shall be black at 32 % alpha (`#52000000`).
  - Tests: untested
- **BRIDGE-SHEET-035** — When `ClipToBounds` is not set, the sheet shall clip to its bounds.
  - Tests: untested

## Invariants

- `IsOpen == (DetentIndex > 0)` after every property change.
- The edge strip is visible exactly while the panel is hidden.
- After a release, `DetentIndex` names the detent the sheet is heading for.

## Edge cases

- A first detent above 0 (a peek): the sheet closes to it, stays shown, and its scrim is 0 there.
- A single detent: `IsOpen = true` sets `DetentIndex` 1, which targets the only detent, so the sheet reads open while resting closed.
- A drag released with no motion outside the detents lands on the nearer end.
- `Scrim = null`: the scrim draws nothing and a tap there reaches the host.
- Capture lost mid-drag (another control captures) settles as a release.

## Threading and lifetime

- UI thread only. The animatable on the panel's axis shorthand is created on first move and reverted on an `Edge` change. Escape and back listeners live on the top level while the sheet is in a visual tree, and move with it.

## Reduced motion

- Read at each release or `DetentIndex` change: land at once; the scrim fades over `Tokens.Short` (BRIDGE-SHEET-028–029). A drag still follows the pointer.

## Allocation

- Pointer moves set the animatable without allocating; a release allocates one fling. Arrange allocates the resolved detent array.

## Deviations from anime.js

- No upstream counterpart.

## Decisions

- **Sheet is an untemplated control with its own drag.** `Sheet`, with a sealed `Drawer` subclass, is a control that builds its scrim, panel and edge strip as visual children and takes its look from its `Content`; its drag is its own pointer handling, built like `Swipe`'s. Reason: the scrim and edge strip are siblings an attached property on the panel could only add by reaching into its parent's children; the bridge ships no themes; `Draggable`'s press captures the pointer, so it could never hand a touch scroll to an inner `ScrollViewer`. Rejected: an attached form; a drag built on `Draggable`.

## Non-goals

- Not templated and ships no theme; the content gives the look (see Sheet is an untemplated control with its own drag).
- No `Presence` builder for the sheet's own slide; the glide is the motion.
- No modal focus trap or focus return (see BRIDGE-MODAL for that).

## Open items

- `DetentIndex` is never coerced into the detent range, so `IsOpen` can read true while the sheet rests at its only, closed detent.
- `Drawer` docs say Left or Right, but any `Edge` works, including Top and Bottom with an edge strip.
- Untested: the 3-DIP threshold and cross-axis drop, the handled release, the `Edge` change rebuild, the `Drawer` default edge, and `Scrim = null` letting taps through.
