# Exits leave in batches

## Why

Removing several rows at once cannot be choreographed. Each departure snapshots its element and starts its own exit on the spot (`Presence.Ghost` animates one ghost: index 0 of 1), so a `Delay(Presto.Stagger(30))` on an `Exit` does nothing. A cleared list drops every row on the same frame. A `Wait` host holds an element that *attaches* while ghosts exit, but not one that is *shown*: an empty-state line bound to `Count` appears while the rows are still leaving.

Tempo works around this in code (BACKLOG, Tempo findings, BRIDGE-PRESENCE "A list removal cannot be choreographed"). The view model's `ConfirmCommand` awaits a `PlayClear` hook that `ShellView` sets. `ShellView.PlayClear` collects the live rows by class, scatters them with `TempoMotion.ClearScatter.Animate(rows)` and awaits it, then collapses the Stats bars the same way, and only then calls `MainViewModel.ClearAll`. Afterwards it has to revert the scatter by hand, or the recycled containers keep its end values and a restored row arrives at opacity 0. ARCHITECTURE §5 records the reason: an exit stagger on the rows "would play it with the removal, in the same pass as the empty line … so nothing could come between."

An exit has an advantage an arrival lacks: every element leaving in one change has gone by the end of the dispatcher turn. So a batch of exits can know its total and its order before anything is drawn. The order staggers, the `From = Last`/`Center` origins and the grids that arrivals cannot support (BRIDGE-PRESENCE-022) all work on exits.

## What

The departures that start ghosts under one panel in one dispatcher turn form an **exit batch**. Each ghost is snapshotted at its detach as today, so it stands where its element was drawn. Its exit starts when the batch closes, at the end of the turn and before the next layout and render. The members are ordered by where their ghosts stand: top to bottom, then in reading order. Each exit resolves function values and `Presto.Stagger` with its index in that order and the batch's ghosts as its targets, so the total is known. A single removal is a batch of one and behaves as today.

A ghost's `DataContext` is the item its element showed. That lets an exit's function values read the item, as Tempo's `HeatCellArrive` does on arrivals.

Under a `Wait` host, showing an element (`IsVisible` turning true) now waits like an attach. If an exit under the host is in flight, or starts later in the same turn, the shown element is held at opacity 0 until the last ghost ends, and only then is its entrance played. The same-turn case matters because `ObservableCollection` raises `Count` before `CollectionChanged`. An empty-state line bound to `Count` is shown before the last row detaches.

With this, Tempo's clear is `ClearAll()` and nothing else:
- the rows leave with `RowExit`, which gains `Delay(Presto.Stagger(30))`, top row first;
- the Stats bars, whose `Days` are replaced, leave on ghosts with `BarCollapse` as their `Presence.Exit`, while the new bars arrive at zero height;
- the empty line, under the tasks `Panel` now set to `Presence.Mode="Wait"`, fades in once the last row's ghost ends.

Unchanged:
- `PrestoAvalonia.AnimateExit`, held hides (`HideMode.Hold`) and `Ghost`-mode hides stay single exits that start at once.
- A split `TextBlock` still leaves as its own batch of piece ghosts (BRIDGE-PRESENCE-EXIT-024), as its arrival plays on its pieces.
- Under `ReduceAll`, exits still complete inside the departure (BRIDGE-PRESENCE-030, -044, -055; BRIDGE-PRESENCE-EXIT-020).
- Arrivals keep their arrival-order batches.

## Decisions

- **Exit batches close at the end of the dispatcher turn, not after the next layout pass.** Reason: a collection change detaches its containers synchronously, within the job that changed the collection (`Clear`, a run of `RemoveAt`, an `ItemsSource` swap). A job posted at `DispatcherPriority.Send` from the first departure therefore runs after every departure of the change, and before layout and render. Rejected: closing on `ItemChanges.AfterLayout` as arrivals do, which runs after a render, so every ghost would draw a frame at rest and the stagger would start a frame late.
- **Ghosts stand still until their batch starts.** A ghost added to the overlay at its detach shows the element exactly as it was drawn until the batch's job starts its exit. Reason: no frame is drawn in between. If one were drawn, the ghost would look the same as the element did. Accepted: an exit with explicit `from` values would show its element's look for that frame.
- **Batches are keyed by the nearest `Panel` at or above the attachment point, else its top level.** Reason: this mirrors the arrival key, so a list's rows form one batch and two lists cleared together (Tempo's open and Done lists) form two, each with its own index. Rejected: one batch per turn, which would stagger the Done list on from the open list's last row.
- **Members are ordered by their ghosts' positions, not by departure order.** The order is top to bottom, then left to right, or right to left when the attachment point's `FlowDirection` is `RightToLeft`. Ties keep departure order. Reason: removal order is whatever the view model did (Tempo's `SyncVisible` removes from the end), while where a ghost stands is what the reader sees. Rejected: the items' former indices, which a `Reset` does not report and a non-list panel does not have.
- **A ghost carries its element's `DataContext`.** Reason: function values on an exit receive the ghost (an `Image`), so without it a per-item exit could not read its item. The ghost has no bindings, so pinning the context has no other effect.
- **One `Exit` per element, no batch-only exit.** A delete and a clear use the same builder, and a lone delete is index 0 of 1. Reason: a stagger already does nothing for one element, and function values see the total, so a builder can tell a clear from a delete if it must. Rejected: a `Presence.BatchExit` used when more than one element leaves, since it would double every exit rule (pieces, reduced forms, `Wait`) for a look a builder can already express. Tempo's scatter is dropped for the clear; its rows clear with the staggered `RowExit`.
- **No ordering between batches.** Two batches that close in one turn start together. Reason: `Wait` already orders the step that matters (the empty state after the list empties), and an order between unrelated lists would need a shared host and an order key that nothing else uses. Rejected: a `Presence.ExitOrder`, and an awaitable `Presence.WhenExited(host)` for code. Tempo's bars follow the rows by a stagger `Start` offset on `BarCollapse`, not by waiting for them.
- **Under `Wait`, a show is held like an attach, decided at the end of the turn.** Reason: an empty-state line is the usual element that appears when a list empties, and it is shown through `IsVisible`, not attached. Because `Count` changes before `CollectionChanged`, the show can come before the last removal in the same turn. So a show under a `Wait` host with no exit in flight plays its entrance from the batch job, held if an exit under the host started by then. BRIDGE-PRESENCE-015 still holds: nothing is drawn before that job.
- **`ReduceAll` exits do not batch.** Reason: they complete inside their creation and land everything at once, so there is no stagger to honor, and deferring them would break the same-call guarantees of BRIDGE-PRESENCE-030/-044/-055 and BRIDGE-PRESENCE-EXIT-020.
- **An element attaching again before its batch starts leaves the batch.** Its ghost is dismissed with no exit played, the `Wait` it started ends, and the other members keep their indices. Reason: a remove-then-insert in one turn (Tempo's `SyncVisible` re-places a task this way) is not a departure, as BRIDGE-PRESENCE-EXIT-010 already says for a ghost that has started.

## Spec changes

**BRIDGE-PRESENCE-EXIT** (`presence-exit.md`)
- Add, new section "Batches": "Departures that start a ghost under the same nearest ancestor `Panel` of their attachment point (else its top level) in one dispatcher turn shall form an exit batch."
- Add: "An exit batch shall start its members' exits in one job posted at `DispatcherPriority.Send` by its first departure, so they start before the next layout pass and render."
- Add: "Each member's exit shall resolve function values and `Presto.Stagger` with its index in the batch ordered by ghost position (top to bottom, then in the attachment point's `FlowDirection`, ties in departure order) and the batch's ghosts as its targets."
- Add: "Until its batch starts, a ghost shall stand in the overlay as its element was drawn."
- Add: "When an element attaches again before its batch starts, `Presence` shall dismiss its ghost with no exit, end the wait it started, and leave the other members' order unchanged."
- Add: "A ghost's `DataContext` shall be the leaving element's `DataContext`."
- Add: "Under `ReduceAll`, a departure's exit shall start at the departure, outside any batch."
- Add: "A split `TextBlock`'s piece ghosts shall be their own batch (BRIDGE-PRESENCE-EXIT-024), and `AnimateExit` and hides shall join no batch."
- Change BRIDGE-PRESENCE-EXIT-010: "When an element attaches again while its ghost is still exiting, or before its batch has started, `Presence` shall cancel the ghost's animation or dismiss the waiting ghost, removing it, and classify the new arrival."
- Decisions: the batch decisions above (closing, standing still, key, order, `DataContext`, one `Exit`, no ordering between batches, `ReduceAll`, re-attach).

**BRIDGE-PRESENCE** (`presence.md`)
- Change BRIDGE-PRESENCE-025: "While a host waits, every `Initial` or `Enter` arrival and every show under it shall be held at opacity 0 at `Animation` priority with its entrance not started."
- Add, under Mode: "When an element under a host in `Wait` with no exit in flight is shown, its entrance shall play from the end of the dispatcher turn (the exit batch job), held when an exit under the host started by then."
- BRIDGE-PRESENCE-032 keeps its wording and gains its first test (task 2).
- Decisions: reword "Batches are keyed by the nearest `Panel` and support order staggers only" to "Arrival batches … support order staggers only", pointing to BRIDGE-PRESENCE-EXIT for exit batches, which know their total. Add the "a show is held like an attach" decision.

**BRIDGE-HELPERS**: `AnimateExit`'s docs say it starts at once and joins no batch (no requirement change).

**ARCHITECTURE §5** (Tempo): replace "The clear is a sequence the view plays before the view model clears" with the new clear: a staggered `RowExit`, the bars' `BarCollapse` exit and the empty line under `Wait`. Record that an added or filtered-in row now waits for a removed row's ghost (at most `RowExit`'s 220 ms plus its stagger).

## Tasks

1. **Exit batches.** In `Presence.cs`, split `Ghost` into the snapshot and overlay placement (at the departure) and the animation start (at the batch job). Keep a static `Dictionary<Visual, List<…>>` of open exit batches beside `s_batches`, filled by `PlayGhost` and drained by one `Send`-priority job. Sort each batch by ghost position, and create each member's animation as `Arrive` does: `new Animation(ghosts, parameters, null, 0, index, allTargets).Init()`. Set the ghost `Image`'s `DataContext`. `ReduceAll` keeps the direct path. A re-attach before the job (`HandleAttached`) dismisses the waiting ghost and calls its `Waiting.Ended`.
   - Tests (bridge, new `PresenceExitBatchTests`):
     - clearing a five-row `ItemsControl` whose rows' `Exit` has `Delay(Presto.Stagger(50))` starts the ghosts 0, 50, …, 200 ms apart, top row first, even when the view model removes them bottom-up;
     - `From = Last` delays the top ghost most;
     - a `Grid` stagger over a cleared `UniformGrid` delays by cell distance;
     - two lists cleared in one job stagger independently;
     - a single removal still plays at index 0 of 1;
     - a remove-then-insert of one item in one job leaves no ghost and the other ghosts keep their indices;
     - a function value reads the ghost's `DataContext` as the removed item;
     - a ghost is in the overlay before the job runs, and its exit has started after `Dispatcher.UIThread.RunJobs()` with no layout pass in between;
     - under `ReduceAll` a clear leaves no ghost in the same call.
   - Existing exit tests that read a ghost's exit synchronously after a removal run the dispatcher's jobs first. Each is checked to keep its assertion, not loosened.
   - Update BRIDGE-PRESENCE-EXIT.
2. **Shows wait under `Wait`.** In `Tracker.Show`, under a `Wait` host: hold at once if a wait is in flight. Otherwise queue the entrance on the turn's batch job (posting one if none is open), which holds it if the host is waiting by then and plays it if not.
   - Tests (bridge, `PresenceModeTests`):
     - an empty-state line bound to `!Items.Count` under a `Wait` host with the list fades in only once the last row's ghost ends, whether `Count` changes before or after the last row detaches;
     - a show under a `Wait` host with nothing exiting plays in the same turn, before a frame;
     - a show while a wait is running is held and released with the arrivals;
     - a held hide (`HideMode.Hold`) still starts no wait (BRIDGE-PRESENCE-032, now tested).
   - Update BRIDGE-PRESENCE.
3. **Gallery.** On the Lifecycle page, add a "Clear rows" button beside Add/Remove row. Give the rows' `Exit` a stagger, and put an empty-state line under a `Presence.Mode="Wait"` host shared with the list. Extend `ReferencePagesTests` for it.
4. **Tempo.**
   - Delete `ShellView.PlayClear` and `Classed`, `MainViewModel.PlayClear` and its branch in `Confirm` (which now calls `ClearAll()`), the `PlayClear` wiring in `ShellView.OnDataContextChanged`, and `TempoMotion.ClearScatter`.
   - Add `Delay(Presto.Stagger(30))` to `RowExit`.
   - Set `Presence.Exit="{x:Static m:TempoMotion.BarCollapse}"` on the bars, and give `BarCollapse` a stagger `Start` so the bars follow the rows.
   - Set `Presence.Mode="Wait"` on the tasks `Panel` in `TasksView.axaml`.
   - Update the clear row of `samples/Tempo/Tempo/README.md` and ARCHITECTURE §5.
   - `Tempo.Tests` that awaited `PlayClear` assert on the view model only.
5. **Visual checks** on Desktop, DevTools screenshots mid-exit:
   - Gallery Lifecycle "Clear rows": the rows leave top first in turn, and the empty line fades in after the last.
   - Tempo, wide with Stats showing: Clear all data → the rows leave top first, the bars collapse after them, and the empty line arrives last, with no flash back of a row.
   - Tempo: Restore after a clear brings the rows in whole.
   - Tempo: a single Del, an Undo, a tag-chip filter swap and adding a task during a delete all read as one change after another.
   - Tempo: a Stats refresh (a completed session) collapses the old bars as the new ones grow, without a doubled bar.
   - All of the above again under Reduced motion.

## Open questions

None. Two related Tempo findings stay in the backlog:
- BRIDGE-POPOVER (popover content cannot leave in a reversed stagger). Its content leaves with the whole popup, which is never a departure (BRIDGE-PRESENCE-EXIT-001/002). It needs `Close` to play the content's exits on the live elements before closing, which is a popover change. That change could use this proposal's ordering for its batch.
- BRIDGE-PRESENCE (arrival batches cannot stagger a grid from its last cell). Arrivals still cannot know their total before their first frame, a limit this change does not lift.
