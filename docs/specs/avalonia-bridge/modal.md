---
id: BRIDGE-MODAL
title: Modal host
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto.Controls
sources:
  - MonacoLabs.Presto.Avalonia/Controls/Modal.cs
  - MonacoLabs.Presto.Avalonia/Controls/Dismissal.cs
types:
  - Modal
tests:
  - ModalTests
  - StorefrontPageTests
upstream: []
depends:
  - BRIDGE-PRESENCE
  - BRIDGE-RECIPES
  - BRIDGE-EFFECTS
  - BRIDGE-TOKENS
  - BRIDGE-HELPERS
  - CORE-REDUCED
---

# Modal host

## Purpose

`Modal` is a dialog control over a scrim that covers its host. `Open(element)` reveals its content in a circle grown from the element's center; Escape, back, a scrim tap or `Close()` closes it, shrinking back into that origin, and gives focus back to the opener. Show and hide go through `Presence`'s held hide (BRIDGE-PRESENCE), with the reveal and fades from `Recipes` (BRIDGE-RECIPES) and an optional backdrop blur (BRIDGE-EFFECTS).

## Requirements

### Layout

- **BRIDGE-MODAL-001** — The modal shall measure as its content's desired size, measuring the content even while closed, so opening and closing never change the host's layout.
  - Tests: `ModalTests.ItMeasuresAsItsContentWhetherOpenOrClosed`
- **BRIDGE-MODAL-002** — The modal shall arrange its scrim and its content's panel over its whole bounds, the content placing itself by its own alignment.
  - Tests: `ModalTests.OpeningShowsTheScrimBlursTheBackdropAndRevealsFromTheOriginsCenter`
- **BRIDGE-MODAL-003** — While closed, the scrim and the content's panel shall be invisible and take no hits.
  - Tests: `ModalTests.TheClosedModalNeitherHitTestsNorShows`

### Opening

- **BRIDGE-MODAL-004** — When `Open(from)` is called on a closed modal, it shall record the center of `from` in the modal's coordinates as the origin, through screen coordinates when `from` is under another visual root, and none when `from` is null or its root or the modal's has no top level, and set `IsOpen` true.
  - Tests: `ModalTests.OpeningShowsTheScrimBlursTheBackdropAndRevealsFromTheOriginsCenter`, `ModalTests.OpeningFromAnElementUnderAnotherVisualRootMapsThroughTheScreen`
- **BRIDGE-MODAL-005** — When `Open(point)` is called on a closed modal, it shall use `point` as the origin and set `IsOpen` true.
  - Tests: `ModalTests.OpeningFromAPointRevealsAroundThatPoint`
- **BRIDGE-MODAL-006** — When `Open` is called on an open modal, it shall only replace the origin with the one it was given (found as -004 does, none for a null `from` or one whose root has no top level), so the close shrinks into it.
  - Tests: `ModalTests.OpeningAgainWhileOpenTakesTheNewOriginForTheClose`
- **BRIDGE-MODAL-007** — When `IsOpen` becomes true, the modal shall make the scrim and panel visible and hit-testable, with the scrim entering through `Recipes.Fade()`.
  - Tests: `ModalTests.OpeningShowsTheScrimBlursTheBackdropAndRevealsFromTheOriginsCenter`
- **BRIDGE-MODAL-008** — When it opens with an origin, the content's panel shall enter through `Recipes.CircularReveal` centered on the origin (a `ClipCircle` growing over `Tokens.Long`).
  - Tests: `ModalTests.OpeningShowsTheScrimBlursTheBackdropAndRevealsFromTheOriginsCenter`, `StorefrontPageTests.ClickingACardOpensItsQuickViewOverABlurredGridAndCloseHidesIt`
- **BRIDGE-MODAL-028** — When an element with `Modal.IsOrigin` true raises `Click` (a `Button`) or `Tapped` (any other `InputElement`), the bridge shall record it as the pending origin until the dispatcher's next job.
  - Tests: `ModalTests.AnIsOpenSetByAnIsOriginButtonsCommandRevealsFromTheButton`, `ModalTests.AnIsOriginElementsTapIsTheOrigin`, `ModalTests.AnOriginLessOpenInALaterJobShowsAtOnce`
- **BRIDGE-MODAL-029** — A modal that opens without an origin (`Open()`, an `Open(from)` that found none, or `IsOpen` set true) while an origin is pending shall open from the pending element's center, found as -004 does.
  - Tests: `ModalTests.AnIsOpenSetByAnIsOriginButtonsCommandRevealsFromTheButton`, `ModalTests.AnIsOriginElementsTapIsTheOrigin`
- **BRIDGE-MODAL-009** — When it opens without an origin and none is pending (`Open()` or `IsOpen` set directly), the content shall show at once, with no entrance of its own.
  - Tests: `ModalTests.AnOriginLessOpenInALaterJobShowsAtOnce`
- **BRIDGE-MODAL-010** — Each time the panel shows, the entrances inside the content shall replay through `Presence.EnterOnShow` on the panel.
  - Tests: `ModalTests.EachShowReplaysTheEntrancesInsideTheContent`
- **BRIDGE-MODAL-011** — When it opens, the modal shall record the top level's focused element and move focus to the content's panel.
  - Tests: `ModalTests.FocusMovesIntoItAndReturnsToTheOpener`
- **BRIDGE-MODAL-012** — When it opens with `Backdrop` set, the backdrop shall animate its `Blur` to 6 over `Tokens.Medium` with `Tokens.Ease`.
  - Tests: `ModalTests.OpeningShowsTheScrimBlursTheBackdropAndRevealsFromTheOriginsCenter`

### Closing

- **BRIDGE-MODAL-013** — When `IsOpen` becomes false, the modal shall set the scrim and panel invisible and not hit-testable at once, so they take no input while their exits play under the held hide.
  - Tests: `ModalTests.TheClosedModalNeitherHitTestsNorShows`
- **BRIDGE-MODAL-014** — When it closes, the scrim shall exit through `Recipes.FadeOut()`.
  - Tests: `ModalTests.TheClosedModalNeitherHitTestsNorShows`
- **BRIDGE-MODAL-015** — When it closes after opening from an origin, the panel shall exit by shrinking its `ClipCircle` to 0 around that origin over `Tokens.Medium` with `Tokens.Ease`; after opening without one, by `Recipes.FadeOut()`. The origin is then cleared.
  - Tests: `ModalTests.ReopeningMidCloseGrowsBackFromWhereTheCircleStands`
- **BRIDGE-MODAL-016** — When it closes with `Backdrop` set, the backdrop shall animate its `Blur` back to 0 over `Tokens.Medium`.
  - Tests: `ModalTests.EscapeOrBackClosesIt`
- **BRIDGE-MODAL-027** — When `Backdrop` is replaced while the modal is open, the old backdrop shall animate its `Blur` back to 0 and the new one to 6, both over `Tokens.Medium` with `Tokens.Ease`.
  - Tests: `ModalTests.ABackdropReplacedWhileOpenUnblursAndTheNewOneBlurs`
- **BRIDGE-MODAL-017** — When it closes, the modal shall return focus to the element recorded at opening, then raise `Closed`, however it was closed.
  - Tests: `ModalTests.FocusMovesIntoItAndReturnsToTheOpener`, `ModalTests.AnOutsideClickClosesItAndAnInsideClickDoesNot`
- **BRIDGE-MODAL-018** — When the modal reopens before its exits end, the scrim and content shall play back from where they stand rather than restart.
  - Tests: `ModalTests.ReopeningMidCloseGrowsBackFromWhereTheCircleStands`
- **BRIDGE-MODAL-019** — A tap on the scrim shall close the modal; a tap on the content shall not.
  - Tests: `ModalTests.AnOutsideClickClosesItAndAnInsideClickDoesNot`
- **BRIDGE-MODAL-020** — While the modal is in a visual tree, open and the most recently opened overlay on its top level, an Escape key reaching that top level unhandled, or the top level's `BackRequested`, shall close it and mark the event handled before the top level's own handlers run.
  - Tests: `ModalTests.EscapeOrBackClosesIt`, `SheetTests.EscapeClosesTheMostRecentlyOpenedOverlayFirst`
- **BRIDGE-MODAL-021** — When `Scrim` is null, the scrim shall draw nothing and leave a tap there to the host.
  - Tests: `ModalTests.ANullScrimDrawsNothingAndLeavesATapToTheHost`

### Reduced motion

- **BRIDGE-MODAL-022** — While `ReducedMotion` is set, read at each open and close, the content shall show and hide at once, with no reveal, shrink or fade.
  - Tests: `ModalTests.UnderReducedMotionTheContentShowsAndHidesAtOnceAndTheScrimFades`
- **BRIDGE-MODAL-023** — While `ReducedMotion` is set, the scrim shall still fade in and out through the fade recipes' reduced forms, and the backdrop blur shall still animate.
  - Tests: `ModalTests.UnderReducedMotionTheContentShowsAndHidesAtOnceAndTheScrimFades`

### Defaults

- **BRIDGE-MODAL-024** (inherited) — When `IsOpen` is not set, the modal shall be closed.
  - Tests: `ModalTests.ByDefaultItIsClosedWithA32PercentBlackScrim`
- **BRIDGE-MODAL-025** — When a binding to `IsOpen` sets no mode, it shall bind two-way.
  - Tests: `ModalTests.ABindingToIsOpenWithNoModeWritesBackToItsSource`
- **BRIDGE-MODAL-026** — When `Scrim` is not set, the scrim shall be a 32 % black solid brush (`#52000000`).
  - Tests: `ModalTests.ByDefaultItIsClosedWithA32PercentBlackScrim`

## Invariants

- The scrim and the panel share visibility and hit-testability, both equal to `IsOpen`.
- `Closed` is raised exactly once per transition of `IsOpen` to false.

## Edge cases

- `IsOpen = true` from a binding opens without an origin, unless an `IsOrigin` element's `Click` or `Tapped` in the same dispatcher job left one pending (-029), as when its command sets the bound value.
- With no focused element at opening, closing returns focus nowhere.
- The modal itself is not focusable; the content's panel is.

## Threading and lifetime

- UI thread only. An open modal is tracked on its top level while in a visual tree. The recorded opener is held only while open. The pending origin is one static reference, cleared by a dispatcher post at the next job.

## Reduced motion

- Read at each open and close: the content shows and hides at once; the scrim still fades and the backdrop still blurs (BRIDGE-MODAL-022–023).

## Allocation

- Each open and close builds its `Presence` builders and one backdrop animation. Not on the tick path. Each `IsOrigin` activation posts one dispatcher job to clear the pending origin.

## Provenance

- No upstream counterpart.

## Decisions

- **The newest open overlay wins Escape and back.** See BRIDGE-SHEET.
- **Modal is a control with `Open` methods.** `Modal` exposes `Open(element)`, `Open(point)` and `Close()` over a two-way `IsOpen`. Reason: every page hosts it over a particular view under its own chrome, which a static builder would have to locate and draw above, and the scrim is a sibling of the content, which an attached property could add only by reaching into a parent's children. Rejected: a static `Modal.Open(content, from:)` building an overlay in the window's `OverlayLayer`; an attached `IsOpen` on an overlay control.
- **The origin is an argument to `Open`, or the activating element, held for one dispatcher job.** The reveal origin is passed to each `Open`, not held in a property; an opening without one takes the element with `Modal.IsOrigin` whose `Button.Click` or `Tapped` was raised in the same dispatcher job. Reason: it belongs to one opening. In MVVM the button runs a command and the view model opens the modal, so nothing that knows the button calls `Open`; `Click` is raised before the command executes, so the origin is pending before a bound `IsOpen` flips. Holding it for one job means a later origin-less open, such as a keyboard shortcut, does not grow from a stale button. Rejected: the focused element as origin (a flyout's button has focus in the popup's top level, not the modal's); an `Origin` property on `Modal` (an element binding would not cross a `UserControl` boundary such as Tempo's `SettingsFlyout`).
- **`Open(from)` under another visual root maps through screen coordinates.** `from`'s center goes through `PointToScreen` and the modal's `PointToClient` when `TranslatePoint` finds no common root. Reason: a button in a flyout's popup sits under another root, and screen coordinates are correct for popups on every desktop head (it was Tempo's workaround). A root with no top level still gives no origin.
- **A reopened modal follows its latest input.** `Open` while open replaces the origin the close shrinks into, and a `Backdrop` replaced while open unblurs as the new one blurs. Reason: the latest call or setting is what the caller means now; keeping the first origin shrank the content into an element the caller had moved on from, and a replaced backdrop stayed blurred for good. Rejected: ignoring `Open` while open.
- **The motion is fixed by `Recipes.Tokens`.** The reveal, shrink and fades use the token durations and ease, with no builders exposed. Reason: all three pages that hosted a modal wanted the same circle or none.
- **It measures as its content even while closed.** Reason: opening then never changes an auto-sized host's layout.
- **Closing goes through `Presence`'s held hide.** The modal sets the scrim's and content's `Presence.Enter`/`Exit` at each open and close, then flips their `IsVisible`. Reason: neither takes input once closing starts, and reopening mid-close plays the exits back from where they stand.

## Non-goals

- No static `Modal.Open(content)` that builds an overlay, and no attached form (see Decisions, "Modal is a control with `Open` methods"); `Modal.IsOrigin` marks an opener, not an overlay.
- The open and close motion are fixed by `Recipes.Tokens`, not exposed as builders (see Decisions, "The motion is fixed by `Recipes.Tokens`").
- No focus trap: Tab can still leave the content.

## Open items

None.
