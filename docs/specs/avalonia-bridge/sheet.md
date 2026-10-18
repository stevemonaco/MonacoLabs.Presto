---
id: BRIDGE-SHEET
title: Sheet and drawer
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto.Controls
sources:
  - MonacoLabs.Presto.Avalonia/Controls/Sheet.cs
  - MonacoLabs.Presto.Avalonia/Controls/SheetDetents.cs
  - MonacoLabs.Presto.Avalonia/Controls/SheetPresentation.cs
  - MonacoLabs.Presto.Avalonia/Controls/Drawer.cs
  - MonacoLabs.Presto.Avalonia/Controls/Dismissal.cs
types:
  - Sheet
  - SheetDetents
  - SheetPresentation
  - Drawer
tests:
  - SheetTests
  - SheetPresentationTests
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
  - BRIDGE-GESTURES
---

# Sheet and drawer

## Purpose

`Sheet` is a control that slides its content out from an edge of its host over a scrim and rests on a list of detents. A drag moves it, and a release coasts through inertia to the detent nearest where the fling would carry it. While closed, a strip `EdgeSwipe` DIPs deep along its edge opens it on a swipe in; `Drawer` is the preset with a 20-DIP strip and the `Left` edge, and a style can turn either into the other. Both fill their host (a page's panel or the window's `OverlayLayer`); their drag is their own pointer handling, not a `Draggable`, so an inner `ScrollViewer` can keep a touch scroll. With `Presentation` set to `Anchored`, the same sheet opens its content in an Avalonia `Flyout` at its `Anchor` instead, so one sheet with one `IsOpen` and one `Content` can be a side drawer, a bottom sheet or a popover across an app's breakpoints, switched by a style.

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
  - Tests: `GesturesPageTests.TappingTheHandleStepsTheCurtainToHalfThenFullWithTheContentLinked`, `SheetTests.AnOpenSheetWhoseDetentsShrinkLandsOpenOnTheCoercedDetentAtOnce`
- **BRIDGE-SHEET-006** — When `Edge` changes, the sheet shall revert its animatable, rebuild it on the new axis and land on `DetentIndex`'s detent at once, keeping `IsOpen` and playing no entrances; a drag in progress shall be dropped.
  - Tests: `SheetTests.AnOpenSheetFlippedToTheBottomByAStyleStaysOpenOnItsNewEdgeWithoutReplayingEntrances`, `SheetTests.ChangingTheEdgeOrDetentsMidDragDropsTheDragAndALaterDragWorks`
- **BRIDGE-SHEET-007** — `Position` shall read the panel's extent out from its edge in DIPs, set from the panel's render in the same frame.
  - Tests: `SheetTests.TheScrimOpacityTracksThePosition`, `GesturesPageTests.PullingTheCurtainHalfwaySinksShrinksAndDimsTheContentWithIt`
- **BRIDGE-SHEET-039** — The sheet shall carry exactly one of the pseudo-classes `:left`, `:top`, `:right`, `:bottom`, matching `Edge`.
  - Tests: `SheetTests.TheEdgePseudoClassFollowsTheEdge`, `SheetTests.AStyleOnTheBottomEdgeReachesTheContent`

### Open state

- **BRIDGE-SHEET-008** — When `DetentIndex` changes, the sheet shall set `IsOpen` to `DetentIndex > 0` and, unless the change came from a release, coast to that detent.
  - Tests: `SheetTests.AnInnerScrollerHandsTheDragOffAtItsTopAndScrollsOtherwise`, `GesturesPageTests.TappingTheHandleStepsTheCurtainToHalfThenFullWithTheContentLinked`
- **BRIDGE-SHEET-009** — When `IsOpen` is set true on a sheet at detent 0, the sheet shall go to detent 1; when set false on an open sheet, to detent 0.
  - Tests: `SheetTests.OpeningShowsTheContentAndPlaysTheEntrancesInside`, `SheetTests.EscapeBackOrAScrimTapClosesIt`
- **BRIDGE-SHEET-010** — `DetentIndex` shall be coerced into the declared `Detents` (`[0, Count - 1]`, 0 when there are none), not the arranged stops, and coerced again from the value last set when `Detents` changes.
  - Tests: `SheetTests.TheDetentIndexIsCoercedIntoTheDeclaredDetents`
- **BRIDGE-SHEET-040** — When `Detents` changes while the sheet is open, it shall stay open on the coerced `DetentIndex` and land on its detent at once; a drag in progress shall be dropped.
  - Tests: `SheetTests.AnOpenSheetWhoseDetentsShrinkLandsOpenOnTheCoercedDetentAtOnce`, `SheetTests.ChangingTheEdgeOrDetentsMidDragDropsTheDragAndALaterDragWorks`
- **BRIDGE-SHEET-038** — When `IsOpen` is set true on a sheet with no detent past the first, the index shall stay 0 and `IsOpen` shall revert to false, leaving the scrim taking no hits.
  - Tests: `SheetTests.OpeningASheetWithASingleDetentLeavesItClosed`
- **BRIDGE-SHEET-011** — While the sheet rests at an extent of 0, its panel shall be hidden; when it heads for an extent above 0 or is grabbed, the panel shall show, and it shall hide again when an animation to an extent of 0 completes.
  - Tests: `SheetTests.OpeningShowsTheContentAndPlaysTheEntrancesInside`, `SheetTests.EscapeBackOrAScrimTapClosesIt`
- **BRIDGE-SHEET-012** — When the panel shows, the entrances inside the content shall replay through `Presence.EnterOnShow` on the panel.
  - Tests: `SheetTests.OpeningShowsTheContentAndPlaysTheEntrancesInside`
- **BRIDGE-SHEET-036** — `TimeConstant` shall set the time constant, in ms, of the inertia that settles the sheet on a detent after a release, an open or a close.
  - Tests: `SheetTests.AShorterTimeConstantSettlesSooner`

### Scrim and dismissal

- **BRIDGE-SHEET-013** — The scrim's brush shall be `Scrim`, and its opacity shall follow the extent's progress from the first detent to the last, clamped to `[0, 1]`, and 0 when there are fewer than two detents or the last is not beyond the first.
  - Tests: `SheetTests.TheScrimOpacityTracksThePosition`, `GesturesPageTests.PullingTheCurtainHalfwaySinksShrinksAndDimsTheContentWithIt`
- **BRIDGE-SHEET-014** — The scrim shall take hits only while `IsOpen` is true, and a tap on it shall set `IsOpen` false.
  - Tests: `SheetTests.EscapeBackOrAScrimTapClosesIt`
- **BRIDGE-SHEET-015** — While the sheet is in a visual tree, open, not presented anchored (BRIDGE-SHEET-046) and the most recently opened overlay on its top level, an Escape key reaching that top level unhandled, or the top level's `BackRequested`, shall close it and mark the event handled before the top level's own handlers run; while closed, those events shall pass.
  - Tests: `SheetTests.EscapeBackOrAScrimTapClosesIt`, `SheetTests.AnOpenSheetGetsBackAndEscapeBeforeAnEarlierTopLevelHandler`, `SheetTests.EscapeClosesTheMostRecentlyOpenedOverlayFirst`

### Drag

- **BRIDGE-SHEET-016** — The sheet shall consider a press only on its panel or a descendant of it, or on its edge strip, one pointer at a time, and for a mouse only with the left button.
  - Tests: `DrawerTests.ASwipeAwayFromTheEdgeDoesNothing`
- **BRIDGE-SHEET-017** — A press the sheet considers shall join the pointer's gesture arena (BRIDGE-GESTURES) with a same-axis gate along the sheet's axis, whose `Accepts` is the scroller hand-off of BRIDGE-SHEET-018; when the gate drops out, as on a first move at least as far across as along, the sheet shall forget the press.
  - Tests: `SheetTests.ACrosswiseFirstMoveDropsThePressAndTheSheetStays`
- **BRIDGE-SHEET-018** — When the press began inside a `ScrollViewer` within the panel that can still scroll the way the pointer moves, the gate's `Accepts` shall refuse the move, leaving the drag to the scroller, unless the move opens the sheet further while it is below its last detent.
  - Tests: `SheetTests.AnInnerScrollerHandsTheDragOffAtItsTopAndScrollsOtherwise`
- **BRIDGE-SHEET-019** (inherited) — When the sheet wins the arena, it shall pause its running motion, show its panel and pick up from its current extent, including one rubber-banded past a bound; the arena captures the pointer for it.
  - Tests: `SheetTests.ADragReleasedBetweenDetentsSettlesOnTheNearest`
- **BRIDGE-SHEET-020** — While dragging, the sheet shall follow the pointer along its axis at once, rubber-banding with friction 0.35 past the first and last detents, and mark the moves handled and withheld from gesture recognizers.
  - Tests: `SheetTests.TheScrimOpacityTracksThePosition`, `SheetTests.AnInnerScrollerHandsTheDragOffAtItsTopAndScrollsOtherwise`
- **BRIDGE-SHEET-021** — When a drag is released or its capture is lost, the sheet shall project `extent + velocity · TimeConstant` (or keep the extent when it is outside the detents), clamp it to the detents, set `DetentIndex` to the nearest detent, and coast there through `Inertia` from the release velocity, with `TimeConstant` as its time constant.
  - Tests: `SheetTests.ADragReleasedBetweenDetentsSettlesOnTheNearest`, `SheetTests.AFlingSkipsToTheNextDetent`
- **BRIDGE-SHEET-022** — When a coast's projected target lies past the first or last detent, the sheet shall let it cross the bound and bounce back onto it.
  - Tests: `GesturesPageTests.AFlickUpCoastsTheSheetOpenPastItsStopAndBouncesBackOntoIt`
- **BRIDGE-SHEET-023** (inherited) — When a release follows an actual drag, the sheet shall mark the release handled; a press that never became a drag shall leave the release unhandled.
  - Tests: `SheetTests.AReleaseIsHandledOnlyAfterADrag`

### Edge swipe and drawer

- **BRIDGE-SHEET-024** — A `Drawer` shall default `Edge` to `Left`.
  - Tests: `DrawerTests.ADrawerDefaultsToTheLeftEdgeAndA20DipStrip`
- **BRIDGE-SHEET-025** — While a sheet's panel is hidden and it does not present anchored (BRIDGE-SHEET-041), a strip `EdgeSwipe` DIPs deep along its edge shall take presses, so a swipe in from that edge opens it.
  - Tests: `DrawerTests.AnEdgeSwipeOpensIt`, `DrawerTests.APlainSheetOpensOnAnEdgeSwipeOnlyWithAStrip`, `SheetPresentationTests.OpeningAnAnchoredSheetShowsItsContentInAFlyoutAtTheAnchorAndLeavesThePanelHidden`
- **BRIDGE-SHEET-026** — A swipe starting outside the edge strip of a closed `Drawer` shall not open it.
  - Tests: `DrawerTests.ASwipeAwayFromTheEdgeDoesNothing`
- **BRIDGE-SHEET-027** — `EdgeSwipe` shall default to 0 on a `Sheet` and 20 on a `Drawer`, so a closed, hidden plain sheet takes no drag.
  - Tests: `DrawerTests.ADrawerDefaultsToTheLeftEdgeAndA20DipStrip`, `DrawerTests.APlainSheetOpensOnAnEdgeSwipeOnlyWithAStrip`

### Anchored presentation

- **BRIDGE-SHEET-041** — While `Presentation` is `Anchored` and `Anchor` is effectively visible in a top level, opening the sheet shall set `DetentIndex` to 1, move `Content` into `Flyout` (or a default `Flyout` when it is null) and show it at `Anchor`, leaving the panel, scrim and edge strip hidden and the scrim taking no hits.
  - Tests: `SheetPresentationTests.OpeningAnAnchoredSheetShowsItsContentInAFlyoutAtTheAnchorAndLeavesThePanelHidden`
- **BRIDGE-SHEET-042** — When the anchored flyout closes on its own (light dismiss, Escape, `Hide()`), the sheet shall set `IsOpen` false, and move `Content` back to its panel on the flyout's `Closed`, never earlier, so a `Popover.Close` plays with the content in it.
  - Tests: `SheetPresentationTests.ClosingTheFlyoutClosesTheSheetAndReturnsTheContentToThePanel`, `SheetPresentationTests.APopoverCloseOnTheFlyoutPlaysWithTheContentBeforeItReturns`
- **BRIDGE-SHEET-043** — Setting `IsOpen` false on an anchored open sheet shall hide its flyout, so a `Popover.Close` on it plays; set true again before the flyout has closed, the sheet shall show the flyout again once it has.
  - Tests: `SheetPresentationTests.ClosingTheFlyoutClosesTheSheetAndReturnsTheContentToThePanel`, `SheetPresentationTests.APopoverCloseOnTheFlyoutPlaysWithTheContentBeforeItReturns`, `SheetPresentationTests.ReopenedWhileItsFlyoutClosesItShowsTheFlyoutAgainOnceClosed`
- **BRIDGE-SHEET-044** — When `Presentation` or `Anchor` changes while the sheet is open, the sheet shall stay open: to anchored, its panel shall hide at once, `DetentIndex` become 1 and its flyout open; to the edge, its flyout shall close and, once it has closed, the panel land open on `DetentIndex`'s detent at once.
  - Tests: `SheetPresentationTests.AnOpenSheetMovesBetweenItsEdgeAndItsFlyoutWhenThePresentationFlips`
- **BRIDGE-SHEET-045** — While `Presentation` is `Anchored` and `Anchor` is null or not effectively visible, the sheet shall open at its edge; when the anchor of an open anchored sheet stops being effectively visible, its flyout shall close and the sheet land open at its edge.
  - Tests: `SheetPresentationTests.WithoutAVisibleAnchorItOpensAtItsEdge`, `SheetPresentationTests.AStyleHidingTheAnchorWhileOpenMovesTheSheetToItsEdge`
- **BRIDGE-SHEET-046** — While its content is in its flyout, the sheet shall not take Escape or back through `Dismissal`: as the newest open overlay it lets both pass, closing no earlier overlay, and its flyout's own dismissal closes it.
  - Tests: `SheetPresentationTests.EscapeOnAnAnchoredSheetIsLeftToItsFlyout`

### Reduced motion

- **BRIDGE-SHEET-028** — While `ReducedMotion` is set, read at each release or `DetentIndex` change, the sheet shall land on its detent at once instead of coasting.
  - Tests: `SheetTests.UnderReducedMotionItLandsAtOnceAndTheScrimFades`, `DrawerTests.UnderReducedMotionAnEdgeSwipeLandsOpenAtOnce`
- **BRIDGE-SHEET-029** — While `ReducedMotion` is set, the scrim's opacity shall fade to its new value over `Tokens.Short`, linearly, rather than jump.
  - Tests: `SheetTests.UnderReducedMotionItLandsAtOnceAndTheScrimFades`

### Defaults

- **BRIDGE-SHEET-030** — When `Detents` is not set, the sheet shall rest on `"0,Auto"`: closed, and open at its content's own extent.
  - Tests: `SheetTests.WithoutDetentsItOpensToItsContentsOwnExtent`
- **BRIDGE-SHEET-031** — When `Edge` is not set on a `Sheet`, it shall slide out from the bottom edge.
  - Tests: `SheetTests.ADragReleasedBetweenDetentsSettlesOnTheNearest`
- **BRIDGE-SHEET-032** (inherited) — When `DetentIndex` and `IsOpen` are not set, the sheet shall rest closed on its first detent.
  - Tests: `SheetTests.ItDefaultsClosedOnTheFirstDetentWithTwoWayStateAClippedBoundsAndATranslucentBlackScrim`
- **BRIDGE-SHEET-033** — `DetentIndex` and `IsOpen` shall bind two-way by default.
  - Tests: `SheetTests.ItDefaultsClosedOnTheFirstDetentWithTwoWayStateAClippedBoundsAndATranslucentBlackScrim`
- **BRIDGE-SHEET-034** — When `Scrim` is not set, the scrim shall be black at 32 % alpha (`#52000000`).
  - Tests: `SheetTests.ItDefaultsClosedOnTheFirstDetentWithTwoWayStateAClippedBoundsAndATranslucentBlackScrim`
- **BRIDGE-SHEET-035** — When `ClipToBounds` is not set, the sheet shall clip to its bounds.
  - Tests: `SheetTests.ItDefaultsClosedOnTheFirstDetentWithTwoWayStateAClippedBoundsAndATranslucentBlackScrim`
- **BRIDGE-SHEET-037** (inherited) — When `TimeConstant` is not set, it shall be 350.
  - Tests: `SheetTests.TheDefaultTimeConstantIs350`
- **BRIDGE-SHEET-047** — When `Presentation` is not set, it shall be `Edge`.
  - Tests: `SheetPresentationTests.ThePresentationDefaultsToTheEdge`

## Invariants

- `IsOpen == (DetentIndex > 0)` after every property change, in both presentations.
- The edge strip is visible exactly while the panel is hidden and the sheet presents at its edge (read when the panel shows or hides, or `Presentation` or `Anchor` changes).
- The content is in the panel or, while presented anchored and until the flyout has closed, in the flyout; never in both.
- After a release, `DetentIndex` names the detent the sheet is heading for.

## Edge cases

- A first detent above 0 (a peek): the sheet closes to it, stays shown, and its scrim is 0 there.
- An `Auto` detent over empty content resolves to 0, yet its index is above 0, so the sheet reads open.
- A drag released with no motion outside the detents lands on the nearer end.
- `Scrim = null`: the scrim draws nothing and a tap there reaches the host.
- Capture lost mid-drag (another control captures) settles as a release.
- An anchored sheet with a single detent stays closed, as at an edge (BRIDGE-SHEET-038; `SheetPresentationTests.AnAnchoredSheetWithASingleDetentStaysClosed`).
- A style that hides `Anchor` while the anchored flyout is open: Avalonia leaves a flyout open at a hidden target, so the sheet closes it and lands open at its edge (BRIDGE-SHEET-045).
- A sheet flipped back to anchored while its flyout is still closing waits for `Closed`, then shows it again.

## Threading and lifetime

- UI thread only. The animatable on the panel's axis shorthand is created on first move or arrange, and reverted on an `Edge` change; the next arrange rebuilds it at the detent, leaving a shown panel shown. An open sheet is tracked on its top level while in a visual tree, and moves with it. While presented anchored, the sheet subscribes to its flyout's `Closed` and its anchor's internal `IsEffectivelyVisibleChanged` (through `[UnsafeAccessor]`), and drops both once the flyout has closed.

## Reduced motion

- Read at each release or `DetentIndex` change: land at once; the scrim fades over `Tokens.Short` (BRIDGE-SHEET-028–029). A drag still follows the pointer.

## Allocation

- Pointer moves set the animatable without allocating; a release allocates one fling. Arrange allocates the resolved detent array.

## Provenance

- No upstream counterpart.

## Decisions

- **The newest open overlay wins Escape and back.** `Dismissal` handles both through class handlers on `TopLevel`, which run before any instance handler there, and closes only the most recently opened `Sheet` or `Modal` still open on that top level. Reason: instance handlers run in subscription order, so an app handler subscribed before the overlay attached (the gallery shell's back navigation) ran first and had to resubscribe on every page load, and of two stacked overlays the first attached closed even when it lay underneath. Escape still reaches focused controls inside the overlay first, as it bubbles; an app can preempt Escape by tunneling. Rejected: fixing only the stacking order; documenting the subscription order.
- **Sheet is an untemplated control with its own drag.** `Sheet`, with a sealed `Drawer` subclass, is a control that builds its scrim, panel and edge strip as visual children and takes its look from its `Content`; its drag is its own pointer handling, built like `Swipe`'s and started through the gesture arena (BRIDGE-GESTURES). Reason: the scrim and edge strip are siblings an attached property on the panel could only add by reaching into its parent's children; the bridge ships no themes; `Draggable`'s press captures the pointer, so it could never hand a touch scroll to an inner `ScrollViewer`. Rejected: an attached form; a drag built on `Draggable`.
- **`IsOpen` means "index above 0".** `DetentIndex` is coerced into the declared detents, and the `IsOpen` handler re-syncs after writing the index, so opening a single-detent sheet reads closed again. Reason: it stays independent of layout and cannot flip after an arrange. Rejected: "resolved extent above 0", which would change as the host or content resizes.
- **`EdgeSwipe` becomes a public styled property; `Drawer` stays.** Reason: the edge strip was the one behavior bound to the type, and it must follow the edge (a bottom sheet with a right-edge strip makes no sense). `Drawer` keeps working as the preset with `Left` and 20. Rejected: making `Drawer` obsolete (breaks nothing to keep, and its default edge reads well in XAML).
- **Edge pseudo-classes on the sheet.** Reason: a side panel and a bottom sheet need different chrome (border side, corner radius, grab handle) on the same content, and a pseudo-class is how Avalonia content follows its host's state. Rejected: a templated sheet with per-edge parts (the sheet is deliberately untemplated, see Sheet is an untemplated control with its own drag).
- **An edge or detent change while open keeps the sheet open and lands at once.** Reason: a breakpoint flip is a layout change, not a user gesture, so moving the open panel to its new edge without a glide matches what the rest of the layout does on resize; it reuses BRIDGE-SHEET-005's land-at-once. Rejected: closing on a flip (it loses the user's place); gliding to the new edge (the panel would slide across the content while the window is being dragged).
- **Extend `Sheet` rather than add a new adaptive control.** Reason: `Edge` and `Detents` are already styled properties, so most of the feature is making the rest styleable and keeping the open state across a change; one control keeps one `IsOpen`, one `Content`, and the one Escape/back registration (`Dismissal`). Rejected: an `AdaptiveSurface` that hosts a `Drawer`, a `Sheet` and a flyout and moves content between them (three open states to reconcile); a `Breakpoints` collection of edge/detent sets on the sheet (duplicates styles).
- **No breakpoint API; the switch is a style.** Reason: Avalonia has container queries and classes, and an app's breakpoint drives more than its surfaces (Tempo's bottom bar, Tasks page and Stats layout all key off `.compact`). Rejected: a `Sheet.CompactWidth` threshold (a second, competing breakpoint).
- **Anchored presentation reuses Avalonia's `Flyout`.** Reason: placement, light dismiss, popup windows and `Popover` motion already work there; the sheet only owns the content's move and the open state, which is the part apps get wrong (moving content out of a popup that is still playing its close). Rejected: arranging the sheet's own panel beside the anchor (reimplements popup placement and leaves the window's bounds); making `FlyoutBase.IsOpen` bindable in `Popover` (still two contents).
- **The content returns to the panel only after the flyout has closed.** Reason: `Popover.Close` plays on the live presenter (BRIDGE-POPOVER-010), so moving the content earlier empties the flyout mid-close; for the same reason a reopen or a flip while it closes waits for `Closed`, as Avalonia's `ShowAt` returns early on a flyout still open. Rejected: moving it on `Closing` (blank close).
- **Anchored falls back to the edge when `Anchor` is null or not effectively visible, also while open.** Reason: a style may hide the anchor at the same breakpoint (Tempo hides the gear in compact), and opening a flyout on a hidden element fails or places it at the origin. Avalonia closes a flyout when its target leaves the tree (BRIDGE-POPOVER-013) but not when a style hides it, so the sheet watches the anchor's effective visibility while presented anchored and moves to its edge, open, as on a presentation flip. Rejected: throwing or staying closed; closing the sheet when the anchor hides (the proposal assumed the flyout closes on its own, and closing would lose the user's place at the breakpoint).
- **Anchored, the sheet does not take Escape or back through `Dismissal`.** `Dismissal.Attach` takes a predicate, and while the newest open overlay's predicate is false both events pass. Reason: the flyout already closes on Escape and light dismiss, and a second handler would close the sheet ahead of a flyout opened above it; passing, rather than skipping to an earlier overlay, keeps Escape from closing what lies under the flyout. Rejected: handling Escape in both (double close); dropping the sheet from the open list (Escape would close an earlier overlay under the flyout).
- **An anchored sheet needs two detents, like an edge sheet.** A single-detent sheet stays closed when anchored too (BRIDGE-SHEET-038); the default `"0,Auto"` already has two. Reason: it keeps BRIDGE-SHEET-010's coercion whole for every presentation. Rejected: ignoring the detent count when anchored and setting `DetentIndex` to 1 past the coercion, which breaks BRIDGE-SHEET-010 for one state.
- **`Sheet.Flyout` is a `Flyout`, not any `PopupFlyoutBase`.** Reason: the sheet moves its content into the flyout's `Content`, which only `Flyout` has. Rejected: `PopupFlyoutBase`, whose subtypes build their presenter differently.
- **`TimeConstant` on `Sheet`, not a duration.** The settle's speed is set by a `TimeConstant` styled property. Reason: the settle is inertia, so its knob is the inertia's time constant, which keeps the fling physics: a shorter one settles sooner while a fast fling still projects from its release velocity. Rejected: a `Duration`; a shared `Inertia` property, which would expose the internal `ModifyTarget` and bounds.

## Non-goals

- Not templated and ships no theme; the content gives the look (see Sheet is an untemplated control with its own drag).
- No `Presence` builder for the sheet's own slide; the glide is the motion.
- No modal focus trap or focus return (see BRIDGE-MODAL for that).

## Open items

None.
