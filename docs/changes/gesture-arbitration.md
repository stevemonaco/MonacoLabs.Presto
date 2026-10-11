# Gesture arbitration

## Why

Backlog [BRIDGE-REORDER]: Reorder, Swipe and a scroller on one row cooperate only because their rules happen to line up. Each Presto gesture runs its own press → slop → direction → capture state machine:

| Gesture | Starts after | Direction test | Holds |
|---|---|---|---|
| `Swipe` (BRIDGE-SWIPE-013/014) | 3 DIPs mouse, 10 touch | horizontal > vertical (touch: > 2×) | — |
| `Reorder` (BRIDGE-REORDER-007/041) | 3 DIPs, tunnels on the list | along > across | `TouchHold`, cancelled past 10 DIPs |
| `Sheet` (BRIDGE-SHEET-017) | 3 DIPs | along > across, plus an inner-scroller hand-off | — |
| `PullToRefresh` (BRIDGE-PULL-007) | 3 DIPs | down > horizontal, scroller at its top | — |
| `Draggable`, `Transformable` | press (capture at once) | — | — |
| Avalonia `ScrollGestureRecognizer` | 5 DIPs on an enabled axis, touch and pen only | none, each axis on its own | — |

Nothing ties these together, and the workarounds show it:

- **A gesture that loses is never told.** Swipe needed BRIDGE-SWIPE-043 because a press that Reorder took ended where the row could not see it. A `Reorder.TouchHold` timer only stops when the pointer moves past 10 DIPs. A touch that scrolls the list 5–10 DIPs and then rests probably still lifts the row at 400 ms and takes the capture back from the scroller (to be confirmed by task 1).
- **Who decides depends on handler order.** Reorder tunnels on the list, so it sees every move before Swipe does on the row. Today their direction tests leave a gap between 1:1 and 2:1 in which neither claims, but nothing guarantees that.
- **Each new gesture has to be fitted against all the others by hand.** One more gesture on a row (say a magnetic drag, or a long press that is not a reorder) needs its own direction and slop rules that dodge every other gesture.

### What the platforms do

Research done 2026-10-11 against official docs and framework source.

- **Flutter: gesture arena, per pointer.** Every recognizer hit on pointer-down joins. A recognizer wins by accepting, or by being the last one left after the others reject. When the pointer lifts with no winner, the deepest member wins. Horizontal and vertical drag recognizers each accept once their own axis passes the slop (`kTouchSlop` 18, mouse 1). That is how `Dismissible` and a `ListView` resolve. `ReorderableListView` on touch starts a drag after a `kLongPressTimeout` delay, cancelled by moving more than `kTouchSlop`; on desktop it drags at once.
- **iOS UIKit: recognizers race.** Every recognizer that is hit watches the touches; the first to leave Possible wins and cancels the others. `require(toFail:)` and the delegate's simultaneity and failure methods state the relationships between recognizers. Long press: `allowableMovement` 10 pt, 0.5 s. `UIScrollView` delays content touches and cancels them once it scrolls.
- **Android Views: the parent intercepts, the child vetoes.** `onInterceptTouchEvent` takes the stream once movement passes the slop (8 dp), and the child gets `ACTION_CANCEL`. A child keeps the stream with `requestDisallowInterceptTouchEvent`. `ItemTouchHelper` puts swipe and long-press drag on one helper, and starts a swipe only when the dominant axis is one the list cannot scroll.
- **Jetpack Compose: consumption.** Children react first in the Main pass. The first detector past its slop consumes the change, and the others see it consumed and cancel. Slop is 8 dp for touch and 0.125 dp for a mouse.
- **Web.** `touch-action` is declared up front, and the browser's takeover is final (`pointercancel`).
- **WinUI.** The `ScrollViewer` pans through DirectManipulation. A child takes over with `ManipulationMode` rails or by calling `CancelDirectManipulations`.
- **Avalonia 12.1.3: first capture wins.**
  - Gesture recognizers see each move deepest first, and the first to `Capture` wins. Capturing also prevents gesture recognition for the rest of the press.
  - The previous capture and its ancestors get `PointerCaptureLost`.
  - Since 12.x, `PointerCaptureChanging` lets an ancestor veto a capture.
  - There is no priority, failure dependency or simultaneity API.
  - `Gestures.Holding` is not a recognizer: it waits 300 ms and is cancelled past a fixed 4 DIPs.

**The common pattern.** Every candidate gesture watches the pointer from the press. Each claims once its own gate passes:

- **slop:** distance past a threshold;
- **direction:** the dominant axis and the directions it accepts;
- **time:** a hold deadline.

The first claim wins, and every other candidate is told it lost. Platforms differ mainly in how parents and children negotiate (Android intercepts, UIKit declares relationships, Flutter and Compose let the first claim win), and in slop, which is always far smaller for a mouse than for touch.

## What

An internal **gesture arena** in `MonacoLabs.Presto.Avalonia/Gestures/`, one per pressed pointer, modelled on Flutter's arena and running on Avalonia's capture. Swipe, Reorder, Sheet and PullToRefresh become members instead of each running its own start logic.

**Joining.** At the press, each gesture whose press rules pass (left button, outside the action layers, on a handle, inside the edge strip…) joins the pointer's arena with a **gate**:

- an axis, or both;
- a slop and a dominance ratio for mouse and for touch/pen;
- an optional `Accepts(travel)` predicate, for Sheet's scroller hand-off and the pull's "scroller at its top";
- an optional touch/pen hold, with its own slop.

The member's element sets its depth.

**Deciding.** While the arena is open, it handles `PointerMoved` in the tunnel phase on the `TopLevel`, ahead of every member and every scroller. On each move it evaluates every pending gate against the same travel since the press:

- A gate whose slop passes in a direction it does not take rejects, and its member drops out.
- A gate that passes claims. When several pass on the same move, the deepest element wins, as with Avalonia's recognizers and Flutter's sweep.
- The winner is told first, then the losers, then the arena captures the pointer for the winner's element and prevents gesture recognition, so no scroller starts.

**Holding.** The arena owns one timer, for the shortest pending hold. When it fires, that member wins as if it had claimed. The timer stops as soon as the arena resolves any other way.

**Outside captures.** A capture the arena did not make settles the press: an Avalonia `ScrollGestureRecognizer`, a `Draggable` or `Transformable` (which capture on press), or a `Button`. The arena hears it through `PointerCaptureChanging` on the `TopLevel`, tells every pending member it lost, and closes.

**Release.** A release with no winner ends every pending member as a tap: Swipe closes an open row, and Reorder forgets the press. A release or capture loss after a win goes to the winner's own handlers, as today.

**What does not change:**

- Every gate keeps today's numbers, so each gesture starts on the same moves it does now. The one change is that a pen swipes with touch's slop (see Decisions).
- `Draggable` and `Transformable` stay outside the arena.
- `GestureOwners` still stops two drivers from claiming one target.
- BRIDGE-SWIPE-044 (a touch long press is not a context request) stays as it is.
- No public type or property is added.

## Decisions

- **An arena per pointer, with first-claim-wins gates, built on Avalonia's capture.** Reason: it is the shared model across Flutter, Compose and UIKit's default race, and Avalonia already delivers "first capture wins, the rest get `PointerCaptureLost`". What is missing is a single point that decides on each move and tells every candidate when it loses. Rejected:
  - Android-style parent interception. Presto's gestures are mostly children of the scroller, and Avalonia's scroller is not Presto's to change.
  - UIKit-style failure requirements. They delay the winner, and nothing here needs one gesture to wait for another to fail.
  - Rewriting each gesture as an Avalonia `GestureRecognizer`. It moves six gestures' plumbing without adding a hold timer or a "you lost" signal for members that never captured. It also cannot express Reorder's list-level handling over its containers.
- **Decisions are made in one tunnel handler on the `TopLevel`.** Reason: the order of decisions no longer depends on which element's handler runs first (Reorder tunnelling on the list, Swipe bubbling on the row), and the arena decides before the scroller's recognizer, which runs during the bubble. Rejected: each member deciding in its own handler and racing to capture, which is today's model.
- **Ties go to the deepest element.** Reason: it matches Avalonia's recognizer order and Flutter's sweep, and the innermost gesture is the most specific one. Rejected: join order, which is handler order again.
- **Same-axis gestures keep the 3-DIP threshold on touch.** Avalonia's `ScrollGestureRecognizer` starts at 5 DIPs. A touch gesture on the scroller's own axis must claim before that or never win, so the following keep 3 DIPs:
  - Reorder through a handle or after its hold;
  - a Sheet;
  - a pull.

  Only crosswise gestures (Swipe) use the 10-DIP touch slop and the 2:1 ratio, as Android's `ItemTouchHelper` swipes only on an axis the list cannot scroll. Rejected: one touch slop for every gesture (Android's 8 dp, Flutter's 18), which hands every same-axis drag to the scroller. Also rejected: vetoing the scroller's capture through `PointerCaptureChanging` while a member is pending, which makes scrolling lag the finger, and whose effect on a vetoed recognizer is unverified.
- **A pen is touch for slop and dominance.** Reason: Avalonia's scroller pans for a pen as it does for touch, and Reorder's hold already treats them alike (its Decisions). Today Swipe gives a pen the mouse's 3 DIPs. Rejected: keeping Swipe's mouse treatment of a pen.
- **The arena owns the hold timer.** Reason: a hold must stop the moment anything else wins, including a scroller that captured within the hold's slop. Only the arena hears about every way a press is settled. Rejected: Avalonia's `Gestures.Holding`, which has a fixed 4-DIP tolerance, is not a recognizer, and arrives as an event other handlers also act on.
- **The arena stays internal.** Reason: its members are Presto's own gestures, and no sample needs a custom gesture yet. Its gate shape should survive the four gestures that are ported before it is published. Rejected: a public `GestureArena` or attached gate properties now.
- **`Draggable` and `Transformable` stay out.** Reason: both capture on the press by design (anime.js's draggable grabs on press), so the arena sees them as an outside capture and every other member loses, which is today's behavior. Letting a Draggable claim after its threshold, so a scroller can still win a touch on it, is a behavior change for BRIDGE-DRAGGABLE and is left to the backlog.

## Spec changes

**New spec** `docs/specs/avalonia-bridge/gesture-arbitration.md`, id **BRIDGE-GESTURES**:
- types: none (all internal);
- sources: `Gestures/GestureArena.cs` and `Gestures/GestureGate.cs`;
- tests: `GestureArenaTests`;
- depends: BRIDGE-DRAGGABLE;
- listed under Gestures in `index.md`.

The Decisions above move into it. Draft requirements:

- When a gesture joins on a press, the pointer's arena shall open if it is not open, and stay open until a member wins, every member has dropped out, the pointer is released, or another element captures it.
- While open, on each pointer move, the arena shall evaluate every pending gate against the travel since the press, before any member's or scroller's handler sees the move.
- When a gate's travel passes its slop in a direction it does not take, or its `Accepts` returns false, its member shall drop out and be told it lost.
- When a gate's travel passes its slop along its axis by its dominance ratio and its `Accepts` allows it, its member shall win. When several win on one move, the member on the deepest element shall win.
- When a member wins, the arena shall tell it first, then tell every other pending member it lost, capture the pointer for the winner's element, and prevent gesture recognition for the rest of the press.
- While a touch or pen member's hold is pending, the arena shall run one timer for the shortest hold. When it fires with that member still pending and within its hold slop, the member shall win. When the arena resolves any other way first, the timer shall stop.
- When the pointer is captured by anything but the arena while it is open, every pending member shall be told it lost and the arena shall close.
- When the pointer is released while the arena is open, every pending member shall be told the press ended without a claim.
- The mouse slop shall be 3 DIPs with a 1:1 dominance. The crosswise touch and pen slop shall be 10 DIPs with a 2:1 dominance. A same-axis touch or pen gesture shall use 3 DIPs with a 1:1 dominance.

**BRIDGE-SWIPE:**
- Changed:
  - 013: "joins the pointer's arena with a horizontal crosswise gate; when the gate drops out, the press is left to the ancestor scroller."
  - 014: "When the element wins the arena, it shall pause its running X animation and from then on mark each move handled."
  - 012: arming is joining.
- Struck: 043, because a lost press is now reported, so no armed press can outlive its pointer.
- The axis-lock and touch-slop Decisions point to BRIDGE-GESTURES for the numbers. The touch-slop Decision now covers a pen.

**BRIDGE-REORDER:**
- Changed:
  - 007: joins with a same-axis gate; a crosswise drop-out forgets the press.
  - 041: the hold is the gate's hold, so it is cancelled when anything else wins, including a scroller within the slop.
- Added: "When a scroller captures a touch before its hold is up, the list shall not lift the container."
- The "crosswise start gives the press away" Decision points to BRIDGE-GESTURES.

**BRIDGE-SHEET:**
- Changed: 017, joins with a same-axis gate whose `Accepts` is the scroller hand-off.

**BRIDGE-PULL:**
- Changed: 007, joins with a vertical gate whose `Accepts` is "downward, scroller at its top".

## Tasks

1. **Pin today's cooperation and settle the open question.**
   - Add tests on a Tempo-shaped list: a `ScrollViewer` around a reorderable `ListBox` whose rows have `Swipe` and `Reorder.TouchHold`. Cover:
     - a touch scroll;
     - a touch swipe;
     - a touch hold that lifts;
     - a mouse reorder and a mouse swipe;
     - a touch that scrolls 6 DIPs and rests past the hold time. Expected: no lift. Record whether it lifts today.
   - Also test whether `PointerCaptureChanging` reaches the `TopLevel` when `ScrollGestureRecognizer` captures.
   - Tests: `GestureArenaTests` (cooperation cases), `SwipeTests`, `ReorderTests`.
2. **Gate and arena.**
   - Add `GestureGate` and `GestureArena`, and move `Draggable.DefaultDragThreshold` and `TouchSlop` to them.
   - Unit-test with stub members:
     - drop-out on direction;
     - deepest wins a tie;
     - a hold wins, and its timer stops when another member wins or on an outside capture;
     - every member is told it lost on an outside capture;
     - a release with no winner ends members as taps;
     - the arena closes and leaves no handlers.
   - Write the BRIDGE-GESTURES spec with the requirements above.
3. **Port Swipe.** Remove the BRIDGE-SWIPE-043 replacement path, and give a pen touch's slop. Update BRIDGE-SWIPE. Tests: the existing `SwipeTests`, plus a pen test.
4. **Port Reorder.** The hold goes through the arena, and the hold command still runs on a release in place. Update BRIDGE-REORDER. Tests: the existing `ReorderTests`, plus task 1's scroll-then-rest case, expecting no lift.
5. **Port Sheet and PullToRefresh.** Update BRIDGE-SHEET and BRIDGE-PULL. Tests: the existing `SheetTests` and `PullToRefreshTests`, plus a `GesturesPageTests` run.
6. **Run Tempo's headless tests and the full suite.**
7. **Visual check on the Desktop sample** (Gestures page, Sheet, the swipe rows) and in Tempo with a mouse. Touch and pen can't be driven through DevTools, so add them to the [SAMPLE] by-hand Tempo checks in the backlog.
8. **Close the backlog items.**
   - Remove [BRIDGE-REORDER] "Reorder, Swipe and a scroller on one row only cooperate…".
   - Add a [BRIDGE-DRAGGABLE] item: let a Draggable join the arena and claim after its threshold, so a touch on a Draggable inside a `ScrollViewer` can still scroll.

## Open questions

- Does `PointerCaptureChanging` reach a `TopLevel` handler when a gesture recognizer captures (`CaptureGestureRecognizer`), and not only when an element captures? Task 1 settles it. If it does not, the arena listens for `PointerCaptureLost` on the element the press implicitly captured, which the research confirms is raised to it and its ancestors.
