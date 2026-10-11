---
id: BRIDGE-REORDER
title: Drag-to-reorder lists
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/Reorder.cs
  - MonacoLabs.Presto.Avalonia/ReorderList.cs
  - MonacoLabs.Presto.Avalonia/ReorderExtension.cs
  - MonacoLabs.Presto.Avalonia/ReorderMovingEventArgs.cs
types:
  - Reorder
  - ReorderExtension
  - ReorderMovingEventArgs
tests:
  - ReorderTests
  - ReorderContainerTests
  - GestureArenaTests
  - RecipeTests
  - RecipeMarkupTests
  - BoardPageTests
  - BuilderPagesTests
upstream: []
depends:
  - BRIDGE-DECLLAYOUT
  - BRIDGE-TRANSFORMS
  - BRIDGE-PRESENCE
  - BRIDGE-DRAGGABLE
  - BRIDGE-GESTURES
  - BRIDGE-RECIPES
  - BRIDGE-MARKUP
  - CORE-ANIMATION
  - CORE-EASES
  - CORE-DIAGNOSTICS
---

# Drag-to-reorder lists

## Purpose

`Reorder.*` attached properties make the containers of an `ItemsControl` draggable along one axis: the dragged container lifts, follows the pointer, moves its item one index each time its center crosses a neighbor's midpoint, and springs into its slot on release. With `Animate.ItemLayout` on the same list (BRIDGE-DECLLAYOUT) the neighbors ease out of the way. `ReorderExtension` is the `{presto:Reorder}` markup form of `Recipes.ReorderLift` (BRIDGE-RECIPES).

## Requirements

### Enabling

- **BRIDGE-REORDER-001** — When `Reorder.Enabled` becomes true on an `ItemsControl`, the list shall start handling pointer press, move, release and key down in the tunnel phase, so a `ListBoxItem` that handles the press cannot swallow it.
  - Tests: `ReorderTests.DraggingAcrossSeveralRowsMovesOneStepPerRowInOrderDownAndUp`, `ReorderContainerTests.ADraggableOnAListBoxItemNeverGrabsBecauseTheItemHandlesThePress`
- **BRIDGE-REORDER-002** (inherited) — When `Reorder.Enabled` becomes false mid-drag, the list shall drop the drag without the settle spring: the lift is reverted, the `ZIndex` raise and the axis shorthand are removed at once, and later pointer moves move nothing.
  - Tests: `ReorderTests.DisablingMidDragRevertsTheDragAndStopsListening`
- **BRIDGE-REORDER-003** (inherited) — While in design mode, setting `Reorder.Enabled` shall do nothing.
  - Tests: untested
- **BRIDGE-REORDER-004** (inherited) — When the list is detached from the visual tree, an active drag shall be dropped without the settle spring.
  - Tests: untested

### Grabbing

- **BRIDGE-REORDER-005** (inherited) — A drag shall start only from a left-button press whose source lies inside a realized container that is a direct child of the list's `ItemsPanelRoot`, and only one pointer at a time.
  - Tests: `ReorderTests.DraggingAcrossSeveralRowsMovesOneStepPerRowInOrderDownAndUp`
- **BRIDGE-REORDER-006** — When a container has any descendant with `Reorder.Handle` set, the container shall be grabbed only by a press inside one of its handles.
  - Tests: `ReorderTests.AContainerWithAHandleIsGrabbedOnlyByIt`, `BoardPageTests.DraggingAPriorityByItsGripReordersTheListAndSettlesItInItsNewSlot`
- **BRIDGE-REORDER-007** — On a press that passes BRIDGE-REORDER-005 and -006, the list shall join the pointer's gesture arena (BRIDGE-GESTURES) with a same-axis gate along `Reorder.Axis` (3 DIPs, travel along greater than across, for a mouse and for touch or a pen) on the pressed container, and start the drag when it wins; when the gate drops out on a crosswise start, or the arena gives the press to another gesture, the list shall forget the press, leaving it to that gesture (a swipe, a scroll).
  - Tests: `ReorderTests.AHorizontalListReordersAlongX`, `ReorderTests.APressThatMovesAcrossTheAxisFirstIsLeftToThatGestureEvenAfterDriftingAlongIt`, `GestureArenaTests.AHorizontalMouseDragSwipesAndAVerticalOneReorders`
- **BRIDGE-REORDER-041** — While `Reorder.TouchHold` is above 0, a touch or pen press shall join with that many milliseconds as its gate's hold (10-DIP hold slop) and claim by nothing else, then, when the hold wins, lift its container and drag from there without a further threshold. The hold shall stop when anything else wins the press first; one that moves more than 10 DIPs along either axis first, or is released first, shall be forgotten, and a mouse press shall drag as BRIDGE-REORDER-007 says.
  - Tests: `ReorderTests.WithATouchHoldATouchThatMovesFirstIsLeftAloneAndOneHeldStillLiftsWhileAMouseDragsAtOnce`, `ReorderTests.ATouchHoldSurvivesAFingersJitterWithinItsSlop`, `ReorderTests.ATouchHoldIsCancelledWhenASwipeOnTheRowWinsFirst`
- **BRIDGE-REORDER-044** — When a scroller captures a touch before its hold is up, the list shall not lift the container.
  - Tests: `GestureArenaTests.ATouchThatScrollsWithinTheHoldSlopAndRestsDoesNotLift`
- **BRIDGE-REORDER-043** — When a hold has lifted a container and is released with its item still at its start index and within the touch slop of where it lifted, the list shall settle it as usual and then run `Reorder.HoldCommand` with the item, when `CanExecute` allows; a hold that moved the item, or a mouse drag, shall not run it.
  - Tests: `ReorderTests.AHoldLetGoInPlaceRunsTheHoldCommandWhileAHoldThatDragsReordersInstead`
- **BRIDGE-REORDER-008** — When the drag starts, the list shall capture the pointer on the list itself (not the container), raise the container's `ZIndex` to `int.MaxValue` at `Animation` priority, focus the container, and suspend the container's item `LayoutArm`.
  - Tests: `ReorderTests.ReleaseSpringsTheItemIntoItsSlotAndLeavesNothingBehind`, `ReorderContainerTests.AMoveTakesThePointerCaptureFromTheMovedContainerButNotFromTheList`
- **BRIDGE-REORDER-009** — When the drag starts, the list shall play `Reorder.Lift` on the container, or `Recipes.ReorderLift()` when `Lift` is null; the builder is read at each grab.
  - Tests: `ReorderTests.ALiftSetOnTheListPlaysInPlaceOfTheDefaultAndIsRevertedOnRelease`, `ReorderTests.ReleaseSpringsTheItemIntoItsSlotAndLeavesNothingBehind`
- **BRIDGE-REORDER-010** (inherited) — When a drag starts while the previous release is still settling, the previous drag shall be finished at once (settle canceled, lift reverted, transform cleared) before the new one begins.
  - Tests: untested
- **BRIDGE-REORDER-011** — When a drag starts with no `Reorder.Command` and an `ItemsSource` that is not an `ObservableCollection<T>`, the list shall log an Avalonia warning (area `Control`) naming the list type, and the drag shall still lift, follow and spring back.
  - Tests: untested

### Following the pointer

- **BRIDGE-REORDER-012** — While dragging, the list shall write the `"Y"` shorthand (`"X"` on a horizontal axis) of the dragged container through `RenderTransformComposer` as `pointer offset since grab − (slot now − slot at grab)`, so the item stays under the pointer across every move.
  - Tests: `ReorderTests.TheDraggedItemIsDrawnUnderThePointerAcrossEveryMove`, `ReorderTests.AHorizontalListReordersAlongX`
- **BRIDGE-REORDER-013** — The list shall track the dragged item, not its container: after each move and each layout pass it re-finds the container with `ContainerFromItem`, reverts the old container's lift, shorthand, `ZIndex` and arm suspension, and moves them onto the new container with the lift seeked to its end.
  - Tests: `ReorderTests.TheDraggedItemIsDrawnUnderThePointerAcrossEveryMove`, `ReorderContainerTests.AMoveClearsAndPreparesTheMovedItemsContainerAndOnlyAVirtualizingListKeepsItsInstance`
- **BRIDGE-REORDER-014** (inherited) — When the dragged item no longer has a container, the list shall drop the drag without the settle spring.
  - Tests: untested
- **BRIDGE-REORDER-015** — While dragging, the list shall refocus the dragged container after every move, so a focusable container stays realized in a virtualizing panel however far the list scrolls.
  - Tests: `ReorderTests.AVirtualizedDragFarPastTheViewportKeepsTheSameContainerAndLandsCleanly`, `ReorderContainerTests.AFocusedContainerStaysRealizedWhileItsListScrollsItFarAway`, `ReorderContainerTests.AMoveTakesFocusFromTheMovedContainer`
- **BRIDGE-REORDER-016** — While dragging, the dragged container's `LayoutArm` shall stay suspended so its translation is not composed with a FLIP, while the neighbors' arms keep easing.
  - Tests: `ReorderTests.NeighborsEaseWithItemLayoutAndSnapWithout`, `ReorderContainerTests.ADraggablesTranslationAddsToTheFlipThatIsEasingTheSameContainer`

### Moving

- **BRIDGE-REORDER-017** — When the dragged container's center along the axis crosses the midpoint of the next or previous realized neighbor, the list shall move the item one step toward it; several crossings in one pointer move become that many one-step moves, in order.
  - Tests: `ReorderTests.DraggingAcrossSeveralRowsMovesOneStepPerRowInOrderDownAndUp`
- **BRIDGE-REORDER-018** — Each step shall first raise `Reorder.Moving` on the list with `Item`, `OldIndex` and `NewIndex = OldIndex ± 1`, and a handler that sets `Cancel` shall stop the step and leave the collection untouched.
  - Tests: `ReorderTests.CancelingMovingLeavesTheCollectionUntouched`
- **BRIDGE-REORDER-019** — When `Reorder.Command` is set, each uncanceled step shall call `CanExecute` and then `Execute` with the `ReorderMovingEventArgs` as parameter, and the list shall move nothing itself; `CanExecute` false refuses the step.
  - Tests: `ReorderTests.ACommandReceivesEachStepAndOwnsTheMove`
- **BRIDGE-REORDER-020** (inherited) — When the command does not change the item's index, the step shall end the current run of crossings, and the drag shall continue.
  - Tests: `ReorderTests.ACommandReceivesEachStepAndOwnsTheMove`
- **BRIDGE-REORDER-021** — When no command is set and `ItemsSource` is (or derives from) an `ObservableCollection<T>`, each uncanceled step shall call its `Move(old, new)` through a delegate cached per runtime type; any other items source refuses the step.
  - Tests: `ReorderTests.DraggingAcrossSeveralRowsMovesOneStepPerRowInOrderDownAndUp`
- **BRIDGE-REORDER-022** (inherited) — When the list is a `SelectingItemsControl` whose `SelectedItem` is the moved item, the list shall restore `SelectedItem` after the move.
  - Tests: `ReorderTests.AMovedItemStaysSelected`
- **BRIDGE-REORDER-023** — After each move the list shall run a synchronous layout pass, so the next crossing reads the new slots and the translation rebase lands before the frame renders.
  - Tests: `ReorderTests.TheDraggedItemIsDrawnUnderThePointerAcrossEveryMove`
- **BRIDGE-REORDER-024** — A move shall play neither the moved row's `Presence` enter nor its exit (the move is tracked as `Moved`, see BRIDGE-PRESENCE).
  - Tests: `ReorderContainerTests.AMovePlaysNeitherTheEnterNorTheExitOfTheMovedRow`
- **BRIDGE-REORDER-025** — When `Animate.ItemLayout` is set on the list, the neighbors shall ease to their new slots; without it they snap.
  - Tests: `ReorderTests.NeighborsEaseWithItemLayoutAndSnapWithout`, `ReorderContainerTests.AMoveEasesTheNeighborsAndLandsTheMovedContainerInOneFrame`

### Edge auto-scroll

- **BRIDGE-REORDER-026** — While dragging inside a `ScrollViewer` ancestor of the items panel, the list shall run a timer that every frame scrolls along the axis by `Draggable`'s edge-scroll formula (speed 1.5, edge band 20 DIPs), clamped to the extent, and re-runs follow and swap from the pointer's last position, so the item keeps moving in order under a still pointer.
  - Tests: `ReorderTests.HoldingThePointerInTheBottomEdgeBandScrollsTheListAndKeepsMovingTheItemDownInOrder`, `ReorderTests.AVirtualizedDragFarPastTheViewportKeepsTheSameContainerAndLandsCleanly`
- **BRIDGE-REORDER-027** — The auto-scroll timer and the release animations shall be created under the diagnostics origin `"Reorder"`.
  - Tests: untested

### Release

- **BRIDGE-REORDER-028** — When the pointer is released after a drag, the list shall mark the release handled, release its capture, reverse the lift, and animate the axis shorthand to 0 with `Eases.Spring()`.
  - Tests: `ReorderTests.ReleaseSpringsTheItemIntoItsSlotAndLeavesNothingBehind`
- **BRIDGE-REORDER-029** — When the settle spring completes, the list shall revert the lift, remove the `ZIndex` raise and the shorthand, and resume the container's `LayoutArm` as if attaching, leaving no effect, z-order or transform behind.
  - Tests: `ReorderTests.ReleaseSpringsTheItemIntoItsSlotAndLeavesNothingBehind`, `ReorderTests.ALiftSetOnTheListPlaysInPlaceOfTheDefaultAndIsRevertedOnRelease`
- **BRIDGE-REORDER-030** (inherited) — When the pointer is released before the list won the press, the list shall forget the press without handling the release or moving anything.
  - Tests: `ReorderTests.AContainerWithAHandleIsGrabbedOnlyByIt`
- **BRIDGE-REORDER-031** (inherited) — When the list loses the pointer capture during a drag, the list shall release as on pointer up (with the settle spring).
  - Tests: untested

### Keyboard

- **BRIDGE-REORDER-032** — While dragging, Escape pressed anywhere in the top level shall be handled and walk the item back toward its start index one step at a time through the same `Moving`/`Command`/`Move` path, then release with the settle spring; a canceled or refused step stops it where it is.
  - Tests: `ReorderTests.EscapeMidDragMovesTheItemBackStepByStepAndLandsItInItsOriginalSlot`
- **BRIDGE-REORDER-033** (inherited) — While dragging, keys other than Escape shall be left unhandled and move nothing.
  - Tests: untested
- **BRIDGE-REORDER-034** — While not dragging, Alt+Down/Alt+Up (Alt+Right/Alt+Left on a horizontal axis), with Alt as the only modifier, on a focused element inside a container shall be handled and move that container's item one step through the same path, without lift or settle, and focus the item's container afterwards.
  - Tests: `ReorderTests.AltArrowsMoveTheFocusedItemOneStepKeepItFocusedAndStopAtTheEnds`
- **BRIDGE-REORDER-035** (inherited) — When an Alt+arrow step would leave the range `0..ItemCount−1`, the key shall be handled and nothing raised or moved.
  - Tests: `ReorderTests.AltArrowsMoveTheFocusedItemOneStepKeepItFocusedAndStopAtTheEnds`
- **BRIDGE-REORDER-036** (inherited) — Alt+arrow moves shall ignore `Reorder.Handle`: any element inside a container qualifies.
  - Tests: untested

### Lift recipe and markup

- **BRIDGE-REORDER-037** — `ReorderExtension` shall build `Recipes.ReorderLift(null, Intensity, Duration, Delay, ease)`, with `Intensity` 1 by default and `Ease` parsed by the ease parser; `Out` has no effect since the recipe has no exit form.
  - Tests: `RecipeMarkupTests.ReorderParsesItsIntensityAndDuration`, `BuilderPagesTests.ReorderPageListsLiftWithTheirRecipeAndReorder`
- **BRIDGE-REORDER-038** — The default lift shall grow to scale `1 + 0.03 × intensity` over `ShadowBlur 16×intensity`, `ShadowY 6×intensity`, `ShadowOpacity 0.3`, with token timing read at call time, and shall not animate `X` or `Y`.
  - Tests: `RecipeTests.ReorderLiftGrowsOverARaisedShadowWithTheTokenTimingReadAtCallTime`

### Defaults

- **BRIDGE-REORDER-039** (inherited) — When `Reorder.Enabled` is not set, the list shall not handle reorder drags or keys.
  - Tests: untested
- **BRIDGE-REORDER-040** — When `Reorder.Axis` is not set, the list shall reorder vertically along `Y`.
  - Tests: `ReorderTests.DraggingAcrossSeveralRowsMovesOneStepPerRowInOrderDownAndUp`
- **BRIDGE-REORDER-042** — When `Reorder.TouchHold` is not set, it shall be 0, and a touch or pen shall drag as a mouse does.
  - Tests: untested

## Invariants

- At most one drag per list; `NewIndex − OldIndex` is always ±1.
- The drag state belongs to the dragged item; exactly one container (the item's current one) carries the lift, the raised `ZIndex`, the shorthand and a suspended arm.
- After a drop, settle completion, disable or detach, no container keeps the `ZIndex` raise, the lift's values or a non-identity drag translation.
- Reorder never removes and reinserts an item; it uses `Move` or delegates to the command.

## Edge cases

- A canceled `Moving` (or a refused/no-op command) is raised again on every later pointer move and every auto-scroll frame while the center stays past the neighbor's midpoint.
- A non-focusable container can be recycled in a virtualized drag far past the viewport; the drag then drops without settling (BRIDGE-REORDER-014).
- An `IList` that is not an `ObservableCollection<T>` without a command: every step is refused, the drag lifts and springs back.
- Only `SelectedItem` is restored after a move; other entries of a multi-selection are not.
- Crossings are only detected against realized neighbors.

## Threading and lifetime

- UI thread only. The per-list drag state lives in a private attached property and is released when `Enabled` changes. The auto-scroll timer runs on `Presto.Engine` from grab to drop. The mover cache is static, keyed by items-source type, and never cleared.

## Reduced motion

- The default lift is read at each grab, so with `ReducedMotion` set it raises the shadow only (no scale). Pinned by `RecipeTests.ReducedReorderLiftOnlyRaisesTheShadow`.
- The drag translation and the release spring are not reduced: the item follows the pointer, and the settle spring plays as usual unless `ReduceAll` applies (CORE-ANIMATION).

## Allocation

- Not on the tick path in the engine sense. Each auto-scroll frame does a `TranslatePoint`, possibly a scroll and layout pass, and a shorthand write; each step allocates one `ReorderMovingEventArgs`. The mover delegate is created once per items-source type by reflection.

## Deviations from anime.js

- No upstream counterpart.
- Reorder owns the pointer instead of driving a `Draggable` per container, sharing only the arena's gate and `Draggable`'s edge-scroll formula, because a `Move` takes the capture a `Draggable` depends on (see Reorder owns the pointer).

## Decisions

- **Reorder moves one step at a time, through `Command` or `ObservableCollection.Move`.** Each midpoint crossing is a one-step move (`NewIndex − OldIndex` = ±1): it raises the cancellable, bubbling `Reorder.Moving`, then runs `Reorder.Command` when set (the view model owns the change), else calls `Move(old, new)` on an `ObservableCollection<T>` `ItemsSource` through a delegate cached per type; any other `IList` needs the command, and without one the drag only lifts and springs back with a logged warning. Reason: Presence and scroll anchoring would read a remove-and-insert as a removal and an insert; routing the event lets XAML attach it. Rejected: `RemoveAt` plus `Insert`.
- **Reorder owns the pointer.** It handles `PointerPressed` on the list in the tunnel phase, joins the gesture arena, which captures the list when it wins, and tracks the dragged item, re-finding its container with `ContainerFromItem` after every move; it shares only the arena's gate and `Draggable`'s edge-scroll formula. Reason: a `Draggable` releases when its capture is lost, and a `Move` takes capture from the moved container (or replaces the container outright in an `ItemsControl`); tunnel handling stops a `ListBoxItem` swallowing the press. Rejected: a `Draggable` per container, as first sketched.
- **A touch hold is opt-in, and for touch and pen.** `Reorder.TouchHold` makes a touch or pen press hold still before it lifts, the mobile convention, so a whole-row drag target leaves vertical touch scrolling and horizontal swipe actions to the list; a touch that moves first is dropped rather than handled. Reason: without a handle, a 3 DIP threshold along the axis took every vertical touch scroll as a drag (Tempo's task rows); a mouse has no scroll gesture to protect, so it keeps dragging at once, while a pen pans scrollers as touch does and, at first limited to touch, never reached `HoldCommand` on a drawing tablet. Rejected: a hold for every pointer, which slows mouse reordering for nothing, and a handle, which on a phone sits beside the checkbox and invites misses.
- **A crosswise start gives the press away.** The same-axis gate decides on the first travel past its slop, and a crosswise start drops Reorder out of the arena; the numbers and the arbitration are BRIDGE-GESTURES's. Reason: with movement across the axis ignored, a swipe on a whole-row reorder target (Tempo's task rows) turned into a reorder at the first few DIPs of vertical drift and lost its pointer.
- **A held touch claims only by its hold.** With `TouchHold` set, a touch or pen gate never claims by moving and drops out once either axis passes the 10-DIP touch slop, so a touch that moves first is left to a scroll or a swipe. Reason: a whole-row target must not drag before its hold, and the arena's hold stops the moment a scroller or a swipe wins, which Reorder's own timer could not hear. Rejected: keeping the 3-DIP same-axis claim beside the hold, which drags on the first vertical scroll.
- **A hold let go in place runs `HoldCommand`.** One long press both picks an item up and, released where it lifted, opens it, the touch pattern where lifting is the feedback and the drag decides. Reason: Tempo's touch rows had no long-press action besides reorder, and a separate long-press gesture would race the lift. Rejected: raising a routed event, which XAML cannot bind to a view-model command.
- **The dragged container is excluded from FLIP.** Its item `LayoutArm` is suspended from grab until the release spring settles, and its translation is rebased to `pointer delta − (slot now − slot at grab)` in the same layout pass as each move, while the neighbors keep easing. Reason: the drag's shorthand adds to the FLIP's `Translation` rather than replacing it, so both would move the container; the rebase keeps it from jumping.
- **Focus keeps the dragged container realized.** Reorder focuses the container on grab and after every move; a non-focusable container may still be recycled in a long virtualized drag, as the docs on `Enabled` say. Reason: capture on the list does not keep a container realized but focus does, and a `Move` takes focus away.
- **Release, Escape and keyboard share the step path.** Release springs the shorthand to 0 with `Eases.Spring()`, reverses the lift and resumes the `LayoutArm` as if attaching; Escape walks the item back one step at a time, and Alt+arrows move one step, both through `Moving`/`Command`/`Move`. The default lift has no translation. Reason: a view model sees every step; resuming as attaching records the slot rather than flying there; the drag owns `X`/`Y`.
- **A `Move` is tracked as a move.** See BRIDGE-PRESENCE.

## Non-goals

- Cross-list or grouped drags (Kanban); the Board sample implements its own.
- Exposing threshold, edge-scroll speed/band or the settle ease as properties.
- Two-dimensional reordering in wrap or grid panels.
- Multi-pointer or touch-multi drags.

## Open items

- The plan's test list cites `BuilderPagesTests.XamlPageReorderListsLiftWithTheirRecipeAndReorder`; the test is now `BuilderPagesTests.ReorderPageListsLiftWithTheirRecipeAndReorder` on the Builder › Reorder page.
- The design recorded a "trace warning" for a non-movable items source; the code logs an Avalonia `Logger` warning once per grab (the decision is recorded as the code has it), untested.
- A canceled or refused step re-raises `Moving` every pointer move and every auto-scroll frame while held past the midpoint; untested and possibly surprising for handlers.
- Untested: design-mode no-op, detach mid-drag, capture loss mid-drag, container loss mid-drag, re-grab during the settle, non-Escape keys during a drag, Alt+arrow ignoring handles, and the `"Reorder"` diagnostics origin.
- `ReorderList.cs` `Raise` has `_raised =container.SetValue(...)` (missing space); cosmetic.
