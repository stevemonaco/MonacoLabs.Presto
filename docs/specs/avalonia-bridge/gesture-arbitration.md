---
id: BRIDGE-GESTURES
title: Gesture arbitration
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto.Gestures
sources:
  - MonacoLabs.Presto.Avalonia/Gestures/GestureArena.cs
  - MonacoLabs.Presto.Avalonia/Gestures/GestureGate.cs
  - MonacoLabs.Presto.Avalonia/Gestures/IGestureMember.cs
types: []
tests:
  - GestureArenaTests
upstream: []
depends:
  - BRIDGE-DRAGGABLE
---

# Gesture arbitration

## Purpose

An internal gesture arena, one per pressed pointer, modelled on Flutter's and running on Avalonia's pointer capture. Each Presto gesture whose press rules pass joins the pointer's arena with a gate (an axis, a slop and dominance ratio for a mouse and for touch or a pen, an optional `Accepts(travel)` check, an optional touch or pen hold), and the arena decides which one gets the press and tells every other one it lost. It also holds the drag thresholds `Draggable` (BRIDGE-DRAGGABLE), Swipe, Reorder, Sheet and PullToRefresh share.

## Requirements

### Opening and deciding

- **BRIDGE-GESTURES-001** — When a gesture joins on a press, the pointer's arena shall open if it is not open, and stay open until a member wins, every member has dropped out, the pointer is released, or another element captures it.
  - Tests: `GestureArenaTests.AClosedArenaHearsNoLaterMoveOrRelease`, `GestureArenaTests.AMoveInADirectionTheGateDoesNotTakeDropsItsMemberForTheRestOfThePress`
- **BRIDGE-GESTURES-002** — While open, on each pointer move, the arena shall evaluate every pending gate against the travel since the press, before any member's or scroller's handler sees the move.
  - Tests: `GestureArenaTests.AScrollRecognizerCaptureTellsThePendingMemberItLost`, `GestureArenaTests.WhenTwoGatesPassOnOneMoveTheDeeperElementWinsThoughItJoinedLast`
- **BRIDGE-GESTURES-003** — When a gate's travel passes its slop in a direction it does not take, or its `Accepts` returns false, its member shall drop out and be told it lost.
  - Tests: `GestureArenaTests.AMoveInADirectionTheGateDoesNotTakeDropsItsMemberForTheRestOfThePress`, `GestureArenaTests.AGateWhoseAcceptsRefusesTheTravelDropsItsMember`, `GestureArenaTests.AGateClaimsPastItsSlopAlongItsAxisByItsDominance`, `SheetTests.ACrosswiseFirstMoveDropsThePressAndTheSheetStays`, `PullToRefreshTests.APullIsLeftToAListScrolledAwayFromItsTop`
- **BRIDGE-GESTURES-004** — When a gate's travel passes its slop along its axis by its dominance ratio and its `Accepts` allows it, its member shall win. When several win on one move, the member on the deepest element shall win.
  - Tests: `GestureArenaTests.AGateClaimsPastItsSlopAlongItsAxisByItsDominance`, `GestureArenaTests.WhenTwoGatesPassOnOneMoveTheDeeperElementWinsThoughItJoinedLast`
- **BRIDGE-GESTURES-005** — When a member wins, the arena shall tell it first, then tell every other pending member it lost, capture the pointer for the winner's capture element, and, when the win comes on a move, prevent gesture recognition for that move. A winner keeps preventing recognition on each move it drags, which also covers a hold that won with no move to prevent it on.
  - Tests: `GestureArenaTests.TheWinnerIsToldBeforeTheLosersAndThePointerEndsCapturedToItsCaptureElement`

### Holding

- **BRIDGE-GESTURES-006** — While a touch or pen member's hold is pending, the arena shall run one timer for the shortest hold. When it fires with that member still pending and within its hold slop, the member shall win. When the arena resolves any other way first, the timer shall stop.
  - Tests: `GestureArenaTests.ATouchHeldStillPastAMembersHoldWinsForIt`, `GestureArenaTests.AHoldStopsWhenAnotherMemberWins`, `GestureArenaTests.ACaptureElsewhereTellsEveryPendingMemberItLostAndStopsTheHold`, `GestureArenaTests.AScrollRecognizerCaptureTellsThePendingMemberItLost`, `GestureArenaTests.ATouchThatScrollsWithinTheHoldSlopAndRestsDoesNotLift`, `ReorderTests.ATouchHoldIsCancelledWhenASwipeOnTheRowWinsFirst`, `ReorderTests.AHoldLetGoInPlaceRunsTheHoldCommandWhileAHoldThatDragsReordersInstead`
- **BRIDGE-GESTURES-007** — When the travel since the press passes a member's hold slop, that member's hold shall stop, and the member shall stay pending unless its gate drops it.
  - Tests: `GestureArenaTests.AMovePastTheHoldSlopStopsTheHoldAndLeavesTheMemberPending`

### Ending

- **BRIDGE-GESTURES-008** — When the pointer is captured by anything but the arena while it is open, every pending member shall be told it lost and the arena shall close.
  - Tests: `GestureArenaTests.ACaptureElsewhereTellsEveryPendingMemberItLostAndStopsTheHold`, `GestureArenaTests.AScrollRecognizerCaptureTellsThePendingMemberItLost`, `SwipeTests.APressAnotherGestureCapturesIsLostAndTheNextSwipeOpens`
- **BRIDGE-GESTURES-009** — When the pointer is released while the arena is open, every pending member shall be told the press ended without a claim.
  - Tests: `GestureArenaTests.AReleaseBeforeAnyClaimEndsEveryMemberAsATap`, `GestureArenaTests.AMovePastTheHoldSlopStopsTheHoldAndLeavesTheMemberPending`
- **BRIDGE-GESTURES-010** — When the arena closes, it shall remove its handlers, so a later move or release of the pointer reaches no member through it; a release or capture loss after a win is left to the winner's own handlers.
  - Tests: `GestureArenaTests.AClosedArenaHearsNoLaterMoveOrRelease`, `GestureArenaTests.TheWinnerIsToldBeforeTheLosersAndThePointerEndsCapturedToItsCaptureElement`

### Gates

- **BRIDGE-GESTURES-011** — The mouse slop shall be 3 DIPs with a 1:1 dominance. The crosswise touch and pen slop shall be 10 DIPs with a 2:1 dominance. A same-axis touch or pen gesture shall use 3 DIPs with a 1:1 dominance.
  - Tests: `GestureArenaTests.AGateClaimsPastItsSlopAlongItsAxisByItsDominance`, `SwipeTests.APenSwipesWithTouchsSlop`
- **BRIDGE-GESTURES-012** — A gate's travel shall be within its slop while neither axis has moved more than the slop, and a hold's slop shall be 10 DIPs of straight-line travel.
  - Tests: `GestureArenaTests.AGateClaimsPastItsSlopAlongItsAxisByItsDominance`, `GestureArenaTests.AMovePastTheHoldSlopStopsTheHoldAndLeavesTheMemberPending`

## Invariants

- A pointer has at most one open arena, and an open arena has its move, release and capture-loss handlers attached; a closed one has none.
- A member is told exactly one of won, lost or ended without a claim per press.

## Edge cases

- When every member drops out, the arena closes without telling anyone else.
- A member that loses its hold stays pending on its gate, and ends as a tap if the press is released before its gate decides.

## Threading and lifetime

- UI thread only, like every pointer handler. The hold timer is a Presto timer on the current engine with the diagnostics origin `"Gestures"`. An arena lives from the first join on a press until it closes; nothing outlives the press.

## Reduced motion

- Not affected: the arena decides input and animates nothing.

## Allocation

- Each move evaluates the pending gates in a loop over the members, without LINQ or allocation; a press allocates the arena and one entry per member.

## Deviations from anime.js

- No upstream counterpart.

## Decisions

- **An arena per pointer, with first-claim-wins gates, built on Avalonia's capture.** It is the shared model across Flutter, Compose and UIKit's default race, and Avalonia already delivers "first capture wins, the rest get `PointerCaptureLost`". What was missing is a single point that decides on each move and tells every candidate when it loses. Rejected: Android-style parent interception, since Presto's gestures are mostly children of the scroller, and Avalonia's scroller is not Presto's to change; UIKit-style failure requirements, which delay the winner when nothing here needs one gesture to wait for another to fail; rewriting each gesture as an Avalonia `GestureRecognizer`, which moves six gestures' plumbing without adding a hold timer or a "you lost" signal for members that never captured, and cannot express Reorder's list-level handling over its containers.
- **Decisions are made in one tunnel handler on the `TopLevel`.** Reason: the order of decisions no longer depends on which element's handler runs first (Reorder tunnelling on the list, Swipe bubbling on the row), and the arena decides before the scroller's recognizer, which runs during the bubble. Rejected: each member deciding in its own handler and racing to capture, which was the model before the arena.
- **Ties go to the deepest element.** Reason: it matches Avalonia's recognizer order and Flutter's sweep, and the innermost gesture is the most specific one. Rejected: join order, which is handler order again.
- **Same-axis gestures keep the 3-DIP threshold on touch.** Avalonia's `ScrollGestureRecognizer` starts at 5 DIPs, so a touch gesture on the scroller's own axis must claim before that or never win: Reorder through a handle or after its hold, a Sheet and a pull keep 3 DIPs. Only crosswise gestures (Swipe) use the 10-DIP touch slop and the 2:1 ratio, as Android's `ItemTouchHelper` swipes only on an axis the list cannot scroll. Rejected: one touch slop for every gesture (Android's 8 dp, Flutter's 18), which hands every same-axis drag to the scroller; vetoing the scroller's capture through `PointerCaptureChanging` while a member is pending, which makes scrolling lag the finger, and which Avalonia keeps internal.
- **A pen is touch for slop and dominance.** Reason: Avalonia's scroller pans for a pen as it does for touch, and Reorder's hold already treats them alike. Rejected: keeping Swipe's mouse treatment of a pen.
- **The arena owns the hold timer.** Reason: a hold must stop the moment anything else wins, including a scroller that captured within the hold's slop, and only the arena hears about every way a press is settled. Rejected: Avalonia's `Gestures.Holding`, which has a fixed 4-DIP tolerance, is not a recognizer, and arrives as an event other handlers also act on.
- **An outside capture is heard through `PointerCaptureLost` on the pressed element.** `PointerCaptureChanging`, its event field and its args are internal in Avalonia 12.1.3. `PointerCaptureLost` is raised on the element that held the capture and each ancestor up to the one it shares with the new capturer, so the arena listens on the element the pointer is implicitly captured to, and closes before capturing for a winner so its own capture is not taken for an outside one. Rejected: listening on the `TopLevel`, which a capture moved to another element in the window never reaches.
- **The arena stays internal.** Reason: its members are Presto's own gestures, and no sample needs a custom gesture yet. Its gate shape should survive the four gestures that are ported before it is published. Rejected: a public `GestureArena` or attached gate properties now.
- **`Draggable` and `Transformable` stay out.** Reason: both capture on the press by design (anime.js's draggable grabs on press), so the arena sees them as an outside capture and every other member loses, which is the behavior from before the arena. Letting a Draggable claim after its threshold, so a scroller can still win a touch on it, is a behavior change for BRIDGE-DRAGGABLE and is left to the backlog.

## Non-goals

- Simultaneous gestures: one member wins a press, and the rest lose.
- Arbitrating gestures outside Presto, other than treating their captures as settling the press.

## Open items

- The headless `Touch` helper raises events on elements only, so a recognizer that has captured hears no further moves; real touch and pen presses on Tempo's task list and the Gestures page's sheets and pull are still to be checked by hand (backlog [SAMPLE]).
