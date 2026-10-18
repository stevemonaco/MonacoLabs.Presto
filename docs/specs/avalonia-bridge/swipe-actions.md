---
id: BRIDGE-SWIPE
title: Swipe actions and swipe-to-dismiss
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto.Gestures
sources:
  - MonacoLabs.Presto.Avalonia/Gestures/Swipe.cs
  - MonacoLabs.Presto.Avalonia/Gestures/SwipeTracker.cs
  - MonacoLabs.Presto.Avalonia/Gestures/GestureOwners.cs
types:
  - Swipe
tests:
  - SwipeTests
upstream: []
depends:
  - CORE-ANIMATABLE
  - CORE-INERTIA
  - CORE-REDUCED
  - BRIDGE-TRANSFORMS
  - BRIDGE-DRAGGABLE
  - BRIDGE-TRANSFORMABLE
  - BRIDGE-PRESENCE
  - BRIDGE-DECLLAYOUT
  - BRIDGE-TOKENS
  - BRIDGE-GESTURES
---

# Swipe actions and swipe-to-dismiss

## Purpose

Swipe actions on a row, as SwiftUI's `swipeActions` and Flutter's `Dismissible`: a horizontal drag slides an element aside through its X shorthand (BRIDGE-TRANSFORMS) to reveal leading or trailing actions behind it, a partial reveal coasts open or closed through `Inertia` (CORE-INERTIA), and a swipe released past half the element's width commits through a command and can slide the element away, leaving the removal to `Presence` and `Animate.ItemLayout` (BRIDGE-PRESENCE, BRIDGE-DECLLAYOUT).

## Requirements

### Attaching

- **BRIDGE-SWIPE-001** (inherited) — When `Leading`, `Trailing` or `Dismiss` is first set to a non-default value on an element, the element shall become swipeable; `Command`, `LeadingCommand`, `TrailingCommand` or `CommandParameter` alone shall not.
  - Tests: `SwipeTests.APartialRevealSnapsOpen`
- **BRIDGE-SWIPE-002** (inherited) — When `Leading`, `Trailing` and `Dismiss` are all cleared, the element shall stop handling input, drop its action layers and revert its X animation.
  - Tests: `SwipeTests.ClearingEveryActionStopsTheSwipeDropsTheLayersAndRevertsX`
- **BRIDGE-SWIPE-003** (inherited) — When `Leading`, `Trailing` or `Dismiss` changes on a swipeable element, the element shall snap closed and drop its action layers, rebuilding them on the next reveal.
  - Tests: untested
- **BRIDGE-SWIPE-004** (inherited) — While in design mode, setting the properties shall do nothing.
  - Tests: `SwipeTests.SettingTheActionsInDesignModeDoesNothing`
- **BRIDGE-SWIPE-005** — When the element is already driven by a `Draggable` or `Transformable`, making it swipeable shall throw `InvalidOperationException`.
  - Tests: `SwipeTests.MakingAGestureDrivenElementSwipeableThrows`
- **BRIDGE-SWIPE-006** — A side's actions shall be its `Control`, or its `IDataTemplate` built with the element's `DataContext`; any other value shall give that side no actions.
  - Tests: `SwipeTests.APartialRevealSnapsOpen`, `SwipeTests.ADismissPlaysTheExitAndRemovesTheRowWhileItemLayoutClosesTheGap`

### Action layers

- **BRIDGE-SWIPE-007** — The actions shall be hosted lazily on the first grab or context request, each side in a clipping layer appended as a visual and logical child of the element itself, and re-appended if a re-template drops it.
  - Tests: `SwipeTests.TheActionsAreHostedInsideTheRowOnFirstUseAndAfterAReTemplate`
- **BRIDGE-SWIPE-008** — Each side's layer shall be arranged at the strip the element uncovers, outside the element's box (left of it for leading, right of it for trailing), as tall as the element and as wide as the offset, unrounded and in step with the offset on every drag move, so the actions hold still on screen while the element slides.
  - Tests: `SwipeTests.APartialRevealSnapsOpen`, `SwipeTests.ActionsStayPutWhileARowIsDraggedByFractionalOffsets`
- **BRIDGE-SWIPE-009** — Within its strip, a side's actions shall be laid out at least as wide as they measure, aligned to the element's edge they sit against, and clipped to the strip.
  - Tests: `SwipeTests.APartialRevealSnapsOpen`
- **BRIDGE-SWIPE-010** (inherited) — Only the side being revealed shall be visible; at offset 0 both shall be hidden unless a context request is opening one.
  - Tests: `SwipeTests.AShortDragSnapsClosed`
- **BRIDGE-SWIPE-011** — A side's open width shall be its actions' measured width.
  - Tests: `SwipeTests.APartialRevealSnapsOpen`

### Dragging

- **BRIDGE-SWIPE-012** (inherited) — A press on the element, other than inside its action layers and while no press is armed on it, shall arm a drag by joining the pointer's gesture arena (BRIDGE-GESTURES); a mouse press shall arm it only with the left button.
  - Tests: `SwipeTests.APartialRevealSnapsOpen`
- ~~**BRIDGE-SWIPE-043**~~ — Removed with the gesture arena: a press another gesture takes is now reported lost (BRIDGE-SWIPE-045), so no armed press can outlive its pointer.
- **BRIDGE-SWIPE-013** — The element shall join the pointer's arena with a horizontal crosswise gate (BRIDGE-GESTURES-011, a pen counting as a touch); when the gate drops out, the press shall be left to the ancestor scroller.
  - Tests: `SwipeTests.AVerticalDragScrollsTheParentAndNeverOpens`, `SwipeTests.ATouchWithinItsSlopOrNotClearlyHorizontalNeverOpens`, `SwipeTests.APenSwipesWithTouchsSlop`
- **BRIDGE-SWIPE-014** — When the element wins the arena, it shall pause its running X animation and from then on mark each move handled; the capture and the gesture prevention are the arena's (BRIDGE-GESTURES-005).
  - Tests: `SwipeTests.APartialRevealSnapsOpen`
- **BRIDGE-SWIPE-015** — While dragging, X shall follow the pointer's horizontal travel from where the grab picked the element up, including a grab of an element still coasting.
  - Tests: `SwipeTests.APartialRevealSnapsOpen`
- **BRIDGE-SWIPE-016** — While dragging, X past its range shall rubber-band with friction 0.35; the range on each side shall be its actions' width, or the whole element's width when that side commits (BRIDGE-SWIPE-024); a side with no actions shall be limited to 0, unless the element has no actions at all and a side commits, when that side ranges over its width.
  - Tests: `SwipeTests.ASideWithNoCommandStopsAtItsActionsWidth`, `SwipeTests.ADragRubberBandsPastItsRange`
- **BRIDGE-SWIPE-017** — When a grab starts, the element shall become its list's open element, closing any other open element in the same list (its nearest `ItemsControl` ancestor, or its visual parent outside one).
  - Tests: `SwipeTests.ASecondRowOpeningClosesTheFirst`

### Releasing

- **BRIDGE-SWIPE-018** — When a drag is released short of committing, the element shall coast through an `Inertia` with a 120 ms time constant to its side's open width when the projected rest (`x + velocity · TimeConstant`) reaches half that width, and to 0 otherwise, staying on the side it was dragged to.
  - Tests: `SwipeTests.APartialRevealSnapsOpen`, `SwipeTests.AShortDragSnapsClosed`
, `SwipeTests.AShortReleaseIsBackAtRestWithinFourHundredMilliseconds`
- **BRIDGE-SWIPE-019** — When a short drag is released with a fling whose projected rest passes half the open width, the side shall open.
  - Tests: `SwipeTests.AFlingOpens`
- **BRIDGE-SWIPE-020** (inherited) — When a drag is released beyond its side's open width without committing, the element shall land on the open width.
  - Tests: `SwipeTests.AReleasePastTheOpenWidthLandsOnIt`
- **BRIDGE-SWIPE-021** — When a press ends without a claim (a tap) while the element is open, the element shall close.
  - Tests: `SwipeTests.EscapeOrATapClosesTheOpenRow`
- **BRIDGE-SWIPE-041** — While `Reveal` is false, a drag released short of committing shall coast to 0 whatever its position or velocity, while a commit and a context request behave as when it is true.
  - Tests: `SwipeTests.WithoutRevealAPartialDragOrAFlingSpringsClosedAndAFullSwipeStillCommits`
- **BRIDGE-SWIPE-022** (inherited) — When the pointer's capture is lost mid-drag, the element shall settle as on a release.
  - Tests: `SwipeTests.LosingTheCaptureMidDragSettlesAsAReleaseDoes`
- **BRIDGE-SWIPE-045** — When the element loses the arena (its gate drops out, or another gesture captures the pointer), it shall forget the press without capturing, handling or closing anything, so its next press arms a drag again.
  - Tests: `SwipeTests.APressAnotherGestureCapturesIsLostAndTheNextSwipeOpens`, `SwipeTests.AVerticalDragScrollsTheParentAndNeverOpens`
- **BRIDGE-SWIPE-023** (inherited) — The release shall mark the pointer-released event handled only when a drag took place.
  - Tests: `SwipeTests.TheReleaseIsHandledOnlyAfterADrag`

### Committing

- **BRIDGE-SWIPE-024** — When a drag is released with `|x|` past half the element's width on a side that commits (its command can execute, or `Dismiss` is set), the swipe shall commit by position alone, regardless of velocity.
  - Tests: `SwipeTests.AFullSwipeCommitsAndRunsTheCommand`, `SwipeTests.EachSideRunsItsOwnCommandWithTheParameter`
- **BRIDGE-SWIPE-046** — When the command a side would run has `CanExecute` false at a release past half the width and `Dismiss` is false, the release shall settle as one short of committing.
  - Tests: `SwipeTests.ASideWhoseCommandCannotExecuteSpringsBackInsteadOfCommitting`
- **BRIDGE-SWIPE-025** — A commit shall execute the side's own command (`LeadingCommand` for the leading side, `TrailingCommand` for the trailing) with `CommandParameter`, or, when that side has none, `Command` with `Edge.Right` for the leading side and `Edge.Left` for the trailing; only when its `CanExecute` of that parameter is true.
  - Tests: `SwipeTests.AFullSwipeCommitsAndRunsTheCommand`, `SwipeTests.EachSideRunsItsOwnCommandWithTheParameter`, `SwipeTests.ASideWithoutItsOwnCommandFallsBackToCommandWithTheEdge`, `SwipeTests.UnderReducedMotionAReleaseLandsAndADismissCommitsAtOnce`
- **BRIDGE-SWIPE-026** — When `Dismiss` is false, a commit shall run the command first and then coast the element closed.
  - Tests: `SwipeTests.AFullSwipeCommitsAndRunsTheCommand`
- **BRIDGE-SWIPE-027** — When `Dismiss` is true, a commit shall slide the element out to its full width over `Tokens.Short` on `Tokens.Ease`, then run the command.
  - Tests: `SwipeTests.ADismissPlaysTheExitAndRemovesTheRowWhileItemLayoutClosesTheGap`
- **BRIDGE-SWIPE-028** — When a dismissed element is still attached and visible after its command runs, it shall be hidden through `IsVisible = false`, which plays its `Presence` exit per its hide mode.
  - Tests: `SwipeTests.UnderReducedMotionAReleaseLandsAndADismissCommitsAtOnce`
- **BRIDGE-SWIPE-029** — When the command removes the element's item, its `Presence.Exit` shall play as a ghost and `Animate.ItemLayout` shall close the gap.
  - Tests: `SwipeTests.ADismissPlaysTheExitAndRemovesTheRowWhileItemLayoutClosesTheGap`

### Closing

- **BRIDGE-SWIPE-030** — While an element is open, a tunneling pointer press anywhere in its window outside it shall close it.
  - Tests: `SwipeTests.EscapeOrATapClosesTheOpenRow`
- **BRIDGE-SWIPE-031** — While an element is open, Escape on its window shall close it and be handled, refocusing the element when focus was inside it.
  - Tests: `SwipeTests.EscapeOrATapClosesTheOpenRow`
- **BRIDGE-SWIPE-032** — When an element closes, it shall coast to 0 and release its open-element slot and its window handlers.
  - Tests: `SwipeTests.ASecondRowOpeningClosesTheFirst`

### Context request

- **BRIDGE-SWIPE-033** — When an element with actions and no `ContextMenu` or `ContextFlyout` receives an unhandled `ContextRequested`, it shall handle it and move closed → trailing (or leading when it has no trailing actions), trailing → leading (or closed when it has no leading actions), and leading → closed.
  - Tests: `SwipeTests.AContextRequestOpensTheTrailingThenTheLeadingActionsAndTheyRun`
- **BRIDGE-SWIPE-034** — When a context request without a pointer position opens a side, the first focusable element in that side's actions shall take focus.
  - Tests: `SwipeTests.AContextRequestOpensTheTrailingThenTheLeadingActionsAndTheyRun`
- **BRIDGE-SWIPE-035** — An element with its own `ContextMenu` or `ContextFlyout`, or with no actions, shall leave the context request alone.
  - Tests: `SwipeTests.AContextRequestIsLeftAloneWithItsOwnMenuOrNoActions`
- **BRIDGE-SWIPE-044** — A context request that arrives while a touch or pen press is armed on the element, or within 500 ms after one ends, shall be left alone, unhandled.
  - Tests: `SwipeTests.ALongPressContextRequestDuringOrJustAfterATouchLeavesTheRowClosed`

### Resetting

- **BRIDGE-SWIPE-036** — When the element is attached to a visual tree, its `DataContext` changes, or it is shown again (`IsVisible` true at any priority but `Animation`), it shall snap closed at once.
  - Tests: `SwipeTests.AttachingRecyclingOrReShowingSnapsClosedAtOnce`
- **BRIDGE-SWIPE-037** (inherited) — When the element's size changes, its action layers shall be re-placed.
  - Tests: `SwipeTests.AResizedRowRePlacesItsActions`

### Reduced motion

- **BRIDGE-SWIPE-038** — While `Presto.Defaults.ReducedMotion` is true at a release, a close or a context request, the element shall land on its snap at once.
  - Tests: `SwipeTests.UnderReducedMotionAReleaseLandsAndADismissCommitsAtOnce`
- **BRIDGE-SWIPE-039** — While `Presto.Defaults.ReducedMotion` is true at a commit with `Dismiss`, the element shall move out at once and its command shall run in the same call.
  - Tests: `SwipeTests.UnderReducedMotionAReleaseLandsAndADismissCommitsAtOnce`

### Defaults

- **BRIDGE-SWIPE-040** (inherited) — When `Dismiss` is not set, it shall be false, so a commit runs the command and coasts the element closed.
  - Tests: `SwipeTests.AFullSwipeCommitsAndRunsTheCommand`
- **BRIDGE-SWIPE-042** — When `Reveal` is not set, it shall be true, so a release can rest open on its actions.
  - Tests: `SwipeTests.APartialRevealSnapsOpen`
- **BRIDGE-SWIPE-047** — When `LeadingCommand`, `TrailingCommand` or `CommandParameter` is not set, it shall be null, so a side without its own command falls back to `Command`.
  - Tests: `SwipeTests.ASideWithoutItsOwnCommandFallsBackToCommandWithTheEdge`

## Invariants

- At most one open element per list scope.
- The element moves only through its X shorthand; the actions never shift the element's layout or its parent's children.
- While closed, no window-level handlers are attached.

## Edge cases

- A press inside the actions never arms a drag, so their buttons click normally.
- A second pointer while one is armed or dragging is ignored.
- The element must not clip to its bounds, or the strip outside it is clipped away.
- A `Control` given as `Leading`/`Trailing` can only be hosted by one element; share actions through an `IDataTemplate`.
- A side commits only with a command it can execute (its own, or `Command`) or with `Dismiss`; otherwise its drag stops at its actions' width plus rubber band.
- The commands and their `CanExecute` are read on each drag move and release, not when the element becomes swipeable, so a command set or enabled mid-drag widens that side's range (BRIDGE-SWIPE-016) from the next move, and clearing or disabling it narrows it.
- With `Dismiss` set, a side whose command cannot execute still slides the element out and hides it, running nothing.

## Threading and lifetime

- UI thread only. The tracker lives in a private attached property and holds the element's handlers; open elements are kept in a `ConditionalWeakTable` keyed by list scope, so a collected list releases its entry. Window handlers exist only while open.

## Reduced motion

- Read at each release, close and commit: the element lands on its snap at once and a dismiss commits at once. The drag itself is unchanged.

## Allocation

- Per pointer move: one `Animatable.Set` and a velocity sample; no LINQ. Hosting and context requests allocate and are off the tick path.

## Provenance

- No upstream counterpart.

## Decisions

- **Rows coast on a 120 ms time constant.** Reason: `Inertia`'s 350 ms default took about 1.5 s for a short release to settle, which read as sluggish; 120 ms lands in about half a second, and flings still open because the projection uses the same constant.
- **A long press is not a context request for the actions.** A touch or pen hold raises a context request (Avalonia's or Windows' press-and-hold, during the hold or as it ends), and opening the actions there cycled a held row through delete and complete at random; on touch the actions are swiped to, and a hold may belong to `Reorder.TouchHold`. The request is left unhandled for anything else on the route; right-click and the menu key still open the actions. Rejected: a grace keyed to the engine clock, which stops when nothing animates.
- **`Reveal` turns off resting open, not the actions.** With it false the actions still show under the drag and a full swipe still commits, but no release pins them. Reason: on touch a small sideways drift snapped Tempo's rows open on their check and delete, and the user had to start another gesture to close them; a context request stays the way to open them on purpose. Rejected: removing the actions, which loses the feedback of what a full swipe will do.
- **A touch or pen needs a wider slop and a clearly horizontal start** (BRIDGE-GESTURES' crosswise gate). Reason: a finger drifts a few DIPs and rarely moves dead straight, so on the 3-DIP majority rule an intended scroll or reorder hold in Tempo's task list often grabbed the row and a small flick snapped it open. Rejected: a wider slop for every pointer, which makes mouse swipes feel sticky.
- **Action layers live inside the swiped element.** Each side's actions sit in a clipping layer appended to the element's own visual and logical children, hosted lazily on the first reveal, re-appended if a re-template drops it, and arranging itself at the strip the element uncovers outside its box; a side opens as wide as its actions measure. Reason: the parent's children belong to a panel or a generator by index; in the element, the layer's draw order never matters and the element moves only through its X shorthand, so it must not clip to its bounds. Rejected: appending the layers to the parent.
- **The strip is placed without layout rounding, and on each drag move.** Reason: the element slides by a fractional render transform, so a rounded strip shifted against it by up to a pixel, and placing it only from the offset's `Changed` (raised on the next engine frame) left it a move behind; both read as the actions shaking. Rejected: rounding the offset itself, which steps the drag.
- **A full swipe commits by position alone.** A release past half the element's width commits, never one judged by its projected fling. Reason: not recorded.
- **The axis lock drops a vertical-first press.** The lock is the arena's crosswise gate (BRIDGE-GESTURES for the numbers); a press whose gate drops out is forgotten without capturing or handling it. Reason: a touch scroll then reaches the ancestor `ScrollViewer`. Rejected: closing an open row on any arena loss, which a vertical drop-out never did.
- **One command per side, plus a shared parameter.** `LeadingCommand` and `TrailingCommand` get `CommandParameter`. Reason: a row's two sides usually do different things to the row's item, and the parameter lets a list-level command (delete) take the item without an event back from the item's view model. Rejected: passing a `SwipeCommit(edge, item)` record to `Command` (the view model then depends on a Presto type, and one command still has to switch on the edge); passing the `DataContext` implicitly (Avalonia's `Button` passes null without a `CommandParameter`, and an implicit parameter is surprising).
- **`Command` stays as the fallback for a side with no command of its own, and keeps receiving the `Edge`.** Reason: "dismiss either way" is one command (`Toast`, the Gestures page), and the edge is what it needs. `CommandParameter` goes only to the side commands, so `Command`'s parameter never changes meaning. Rejected: dropping `Command` before the preview, which turns every either-way dismissal into two identical setters.
- **A side commits only when its command can execute or `Dismiss` is set.** `CanExecute` is read at each drag move and release, as the command itself already is. Reason: per-side commands make "this side does nothing" common (a done task can only be deleted), and such a side should stop at its actions instead of dragging the full width to a commit that runs nothing.
- **Swipe claims `GestureOwners`.** A `Draggable` or `Transformable` on the same element throws. Reason: one live gesture per control (see BRIDGE-DRAGGABLE).
- **One element is open per list.** A grab or open closes the list's other open element. Reason: not recorded.
- **The accessible path is the context request.** `ContextRequested` (the menu key, Shift+F10, a right-click or a touch hold) cycles trailing, leading, closed. Reason: the actions must be reachable without swiping. Rejected: keyboard-only swiping.

## Non-goals

- Card throws (the Swipe deck sample stays page code).
- Vertical swipes, multiple simultaneously open rows, or a threshold other than half the width.
- Keyboard-only swiping; the context request is the accessible path.

## Open items

- BRIDGE-SWIPE-003 disagrees with the code when a side is a `Control`: changing another side (say `Trailing` on an element whose `Leading` is a `Button`) unhosts both layers but leaves each `Control` the child of its dropped layer, so the next reveal throws `InvalidOperationException` ("already has a visual parent") hosting it in a new layer. Sides given as `IDataTemplate` rebuild. Untested until fixed.
