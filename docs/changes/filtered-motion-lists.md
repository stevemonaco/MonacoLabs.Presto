# Filtered lists and moves between lists keep their motion

## Why

`Presence`, `Animate.ItemLayout` and `Reorder` all read a list's motion from its `ItemsSource`'s per-row change events (BRIDGE-PRESENCE-010, BRIDGE-PRESENCE-EXIT-007, BRIDGE-DECLLAYOUT-020, BRIDGE-REORDER-021). Two common list patterns break that, and Tempo hit both (BACKLOG, Tempo findings, BRIDGE-DECLLAYOUT):

- **Filtering.** A list filtered by a chip row needs a second collection whose rows come and go one at a time. A filter that resets the collection ghosts every row and replays every entrance. Tempo hand-keeps `MainViewModel.VisibleTasks`, about 50 lines: `SyncVisible` diffs it against `Tasks` on every source change, tag change and filter change. A `Reorder` drag moves the copy, so `OnVisibleTasksChanged` replays each move into `Tasks`. Avalonia has no general filtered view (`DataGridCollectionView` lives in the DataGrid package and raises `Reset` on refresh), and DynamicData would be a new dependency for one feature.
- **Moving between lists.** A task marked done leaves the open list and joins the Done list. To each list that is a removal and an add, so the row plays its exit ghost in one place and its entrance in the other instead of moving. `Connected.Key` cannot carry the move: its claim window is three seconds, so a row filtered out and back flies instead of entering, and a container that `Reorder` rebuilds would fly against its own drag. Tempo lives with the exit and re-entry.

Both patterns show up in any task, inbox or kanban app, and both rest on one fact: the list should see *the same item* where today it sees a removal and an add.

## What

**`FilteredView<T>`** (core, namespace `MonacoLabs.Presto`) is a read-only live view over an `ObservableCollection<T>`. It shows the items its `Filter` accepts, in source order, and raises one change event per row.

```csharp
VisibleTasks = new FilteredView<TaskItemViewModel>(Tasks, t => TagFilter is not { } tag || t.Tag == tag,
    refreshOn: [nameof(TaskItemViewModel.Tag)]);
partial void OnTagFilterChanged(TaskTag? value) => VisibleTasks.Refresh();
```

The view translates each source change:
- An accepted item that is added, removed or moved in the source gives one `Add`, `Remove` or `Move` at its view index.
- A rejected item gives nothing.
- A source `Reset` gives one `Reset`.

Setting `Filter`, `Refresh()`, `Refresh(item)`, and an item raising `PropertyChanged` for a name in `refreshOn` all re-evaluate. The rows that are no longer accepted are removed first, last to first, then the newly accepted rows are inserted in source order. A row that stays accepted never moves, and a re-evaluation never resets.

`Move(oldIndex, newIndex)` writes through: it moves the item in the source to just before its new visible successor (just after its predecessor when it ends up last), and the view raises one `Move(oldIndex, newIndex)`. `Reorder` drives the view the way it drives an `ObservableCollection<T>`, so a drag in a filtered list reorders the source.

The view implements `IReadOnlyList<T>`, a read-only `IList` (for Avalonia's `ItemsSourceView`), `INotifyCollectionChanged` and `INotifyPropertyChanged` (`Count`, `Item[]`), and `IDisposable`, which unsubscribes it from the source and the items.

**`Animate.ItemGroup`** (bridge) is a string on a list that has `Animate.ItemLayout`. Suppose an item is removed from one list of a group and, before the layout pass after the removal finishes, added to a list of the same group under the same top level. That is a **transfer**:
- The arriving container flies from where the leaving container was last drawn into its new slot. The flight is a one-shot shared-element transition with the arriving list's `ItemLayout` builder and `LayoutMode`, and the arm resumes once it lands (BRIDGE-DECLLAYOUT-021).
- The leaving row plays no `Presence.Exit`. If its ghost has already started, it is dismissed in the same pass, before it is drawn.
- The arriving row plays no `Presence` arrival, as a moved row plays none (BRIDGE-PRESENCE-012).

A remove-then-insert of one item in the same grouped list is a transfer too, so a source that sorts by removing and inserting moves its rows.

What does not change:
- Lists without `ItemGroup` behave as today.
- An `ObservableCollection.Move` is still a move, not a transfer.
- Neighbors still ease through `ItemLayout` in both lists.
- `Connected` is untouched.
- `Follow` still snaps its first value (BRIDGE-FOLLOW-008), so a value that changed on the leaving row arrives already changed on the new one.

## Decisions

- **The filtered view is in core.**
  - Reason: it needs nothing from Avalonia, and a view-model project that references only the core can use it.
  - Rejected: the bridge, which would make a view model reference Avalonia to filter a list. DynamicData, a dependency with range changes that lists would have to split.
- **Per-row events and no reset from a re-evaluation.**
  - Reason: a `Reset` ghosts every drawn row and makes every arrival `Initial` (BRIDGE-PRESENCE-013), which is the motion a filter must not have. Removals go before insertions so `ItemLayout` sees one gap-closing pass and one opening pass in that order, as Tempo's `SyncVisible` does today.
  - Rejected: a minimal edit script that also moves rows, since a filter never changes order.
- **The source must be an `ObservableCollection<T>`.**
  - Reason: `Move` must write through as a move, and that is the type `Reorder` already moves (BRIDGE-REORDER-021).
  - Rejected: any `IList<T>` plus `INotifyCollectionChanged`, whose only write-through for a move is a remove and an insert, so the row would exit and enter.
- **No sorting.**
  - Reason: Tempo and the other cases in the backlog only filter. A sort key that changes would make the view move rows, and Reorder would then fight the sort.
  - Rejected for now: a `Comparison<T>`.
- **Re-evaluation is explicit, plus opt-in property names.** `Refresh()` covers state outside the items (the chip), and `refreshOn` covers state on them (a task's tag).
  - Reason: watching every `PropertyChanged` of every item would re-run the filter on each keystroke in a title.
  - Rejected: re-evaluating on any property change; requiring `Refresh(item)` from the view model, which is the `Track` handler Tempo writes today.
- **The view holds no weak references.** It subscribes strongly and is `IDisposable`.
  - Reason: a view usually lives exactly as long as the view model that owns the source.
  - Rejected: weak event subscriptions, which add a type and make a dropped view's events fire on until a GC collects it.
- **`Reorder` recognizes the view directly.** It recognizes `FilteredView<T>` through an internal non-generic move interface, which the core exposes to the bridge (`InternalsVisibleTo` exists).
  - Reason: no reflection, and no public interface that only Reorder would read.
  - Rejected: a public `IMovableList`.
- **Transfers match items by identity, inside an opt-in group.** The item object is already the same in both lists.
  - Reason: a group bounds which lists may claim a row. An app that wants a move between lists to exit and enter (an archive, say) just leaves `ItemGroup` unset.
  - Rejected: `Connected.Key` per row, which needs a string key and a global registry, claims for three seconds, so a row filtered out and back would fly, and would fly containers that `Reorder` rebuilds. Also rejected: making every pair of `ItemLayout` lists transfer, which turns today's exit-and-enter into flights without asking.
- **A transfer needs `ItemLayout` on both lists.**
  - Reason: the arm already records each container's slot every pass, which is where the leaver was drawn, and the arriving list's `ItemLayout` builder and `LayoutMode` are the natural motion for the flight. A transfer is the cross-list form of an `ItemLayout` move.
  - Rejected: separate transfer motion and mode properties, which no case needs.
- **The removal must come first, within one layout pass.** The arrival is classified at attach (BRIDGE-PRESENCE-005), and a non-virtualizing panel attaches the new container during `Insert`. If the removal came after, the entrance would already have started.
  - Accepted: an add before its removal is an exit and an enter. This is documented.
- **The transfer is confirmed at the arriving container's first layout pass.** If the arriving container is not effectively visible then (Tempo's Done list collapsed in its `Expander`), the leaver's ghost plays as usual.
  - Reason: a row must never vanish without motion.
- **The flight is clipped by its ancestors.** A row flying from one scroller into another shows only inside the destination's viewport, sliding in from its edge.
  - Accepted: lifting a flight above clipping ancestors is already a Connected non-goal and stays one here.

## Spec changes

**CORE-FILTERVIEW** (new: `docs/specs/core/filtered-view.md`, type `FilteredView`, depends none)
- The view shall contain exactly the source items the filter accepts, in source order; a null filter accepts every item.
- When the source adds, removes, replaces or moves an item, the view shall raise at most one change for that item: `Add`, `Remove`, `Replace` or `Move`, at its view index. A `Replace` raises `Remove` and `Add` when only one of the two items is accepted, and nothing when neither is. A `Move` raises nothing when the item is rejected or its view index does not change.
- When the source resets, the view shall re-evaluate every item and raise one `Reset`.
- When `Filter` is set or `Refresh()` is called, the view shall remove each row no longer accepted, last to first, and then insert each newly accepted item at its view index in source order, one event per row, and shall never raise `Reset` or move a row.
- `Refresh(item)` shall re-evaluate that item alone. When an item in the source raises `PropertyChanged` for a name in `refreshOn`, or with a null or empty name, the view shall refresh that item.
- `Move(oldIndex, newIndex)` shall move the item in the source to just before the item at view index `newIndex` (after the move), or just after its predecessor when `newIndex` is last, so that the view raises exactly one `Move(oldIndex, newIndex)`. Indices out of range shall throw `ArgumentOutOfRangeException`.
- Every change shall raise `PropertyChanged` for `Count` (when it changed) and `Item[]` after the collection event.
- The `IList` members that write (`Add`, `Insert`, `Remove`, `RemoveAt`, `Clear`, the indexer setter) shall throw `NotSupportedException`, and `IsReadOnly` shall be true.
- `Dispose` shall unsubscribe from the source and every item. Calls after it shall throw `ObjectDisposedException`.
- Decisions: the first seven decisions above.

**BRIDGE-REORDER** (`reorder.md`)
- Change 021: "When no command is set and `ItemsSource` is (or derives from) an `ObservableCollection<T>`, or is a `FilteredView<T>`, each uncanceled step shall call its `Move(old, new)`; any other items source refuses the step."
- Change 011: the warning names both accepted source types.
- Edge cases: a step in a filtered list moves the source item next to its visible neighbor, so hidden items between them stay put.

**BRIDGE-DECLLAYOUT** (`declarative-layout.md`; Purpose adds `Animate.ItemGroup`)
- Add, under a new "Transfers" heading: "When an item is removed from a list whose `Animate.ItemGroup` is set and, before the layout pass after the removal finishes, added to a list with the same group under the same top level, both with `Animate.ItemLayout`, the arriving container shall fly from where the leaving container was last drawn (its arm's recorded slot moved by its live transition) into its slot, as a one-shot shared-element transition with the arriving list's `ItemLayout` builder and `LayoutMode`."
- Add: "A transfer's flight shall win its move, and the container's arm shall resume as BRIDGE-DECLLAYOUT-021 says."
- Add: "An item added before its removal, a removal and an add under different top levels or groups, a list without `ItemLayout`, or an `ObservableCollection.Move` shall not transfer."
- Add: "When the arriving container is not effectively visible after its first layout pass, there shall be no transfer: the leaving row exits and the arriving row arrives as without a group."
- Add: "Removing and re-adding an item in the same grouped list within one layout pass shall be a transfer."
- In design mode, setting `Animate.ItemGroup` shall do nothing (extend 011).
- Non-goals: reword the slot-memory item to "a container rebuilt by `ObservableCollection.Move` flies nowhere"; transfers between lists without a shared group.
- Decisions: the last five decisions above.

**BRIDGE-PRESENCE** (`presence.md`)
- Add: "When the list hosts a row for an item it has just received through a transfer (BRIDGE-DECLLAYOUT), the arrival shall play nothing."
- Decisions: "Lists decide for their items" gains the transfer as a third kind of non-arrival.

**BRIDGE-PRESENCE-EXIT** (`presence-exit.md`)
- Change 007: "…it shall exit only when its item was just removed or the list just reset; a row recycled, moved or transferred by the list shall play nothing."
- Add: "When a transfer is confirmed after the leaving row's ghost was created, `Presence` shall dismiss the ghost in the same layout pass, before it is drawn."

**BRIDGE-ATTACH**: none (`Animate` stays its type; the property is specified in BRIDGE-DECLLAYOUT, as `Layout` and `ItemLayout` are).

## Tasks

1. **`FilteredView<T>`.** Add `MonacoLabs.Presto/FilteredView.cs`, the internal move interface, and the CORE-FILTERVIEW spec.
   - Tests (core, `FilteredViewTests`):
     - Each source action maps to the expected single event and index.
     - A filter change removes before it inserts and never resets or moves.
     - `refreshOn` reacts only to the listed names and unsubscribes removed items.
     - `Move` writes the source next to the visible neighbor, in both directions and to either end, with hidden items between.
     - `Count` and `Item[]` notifications are raised.
     - The writing `IList` members throw.
     - `Dispose` detaches from the source and the items.
2. **Reorder over a view.** Recognize the view in `ReorderList.MoverOf` and update the warning. Update BRIDGE-REORDER.
   - Tests (bridge, `ReorderTests`):
     - Dragging in a `ListBox` over a `FilteredView` with hidden items moves the source, and the dragged row stays under the pointer.
     - Escape walks it back.
     - Alt+arrow moves through the view.
3. **Transfers.**
   - Add `Animate.ItemGroup`.
   - Track the per-pass removals of each grouped list in `ItemChanges`, keeping each removed container's last drawn rect.
   - Classify a matching arrival as transferred in `Presence`, dismiss a confirmed leaver's ghost, and start the flight from `LayoutArm`.
   - Update BRIDGE-DECLLAYOUT, BRIDGE-PRESENCE and BRIDGE-PRESENCE-EXIT.
   - Tests (bridge, headless, new `ItemTransferTests`):
     - A row moved between two grouped lists flies from its old slot, plays neither exit nor entrance, and lands with its arm re-armed.
     - The same move with the destination collapsed ghosts the leaver.
     - An add before its removal exits and enters.
     - Ungrouped lists and different groups exit and enter.
     - A remove-and-reinsert in one list flies.
     - A `Reorder` move in a grouped list is untouched (the existing `ReorderContainerTests` stay green).
     - Reduced motion lands the flight at once.
4. **Tempo.**
   - `VisibleTasks` becomes a `FilteredView` over `Tasks` with `refreshOn: [Tag]`.
   - Delete `SyncVisible`, `Shows` and `OnVisibleTasksChanged`, and the `SyncVisible` calls in the constructor, `Track` and `OnTagFilterChanged` (which becomes `Refresh()`).
   - Drop the manual `SelectedTask` clearing if the `ListBox` clears the selection of a removed row.
   - Set `presto:Animate.ItemGroup="tasks"` on `TaskList` and the Done `ItemsControl`.
   - Existing `MainViewModelTests` on `VisibleTasks` (filtering, per-row changes, reorder through the filter) must pass unchanged.
5. **Gallery.** Add a filter chip row and a second list to the Lifecycle page's `ItemLayout` demo, with a transfer between the lists.
6. **Visual check** on Desktop, wide and at `--size 360x780`, in both motion modes:
   - Tempo: switching tag chips adds and removes rows with their own motion while the rest ease; dragging a filtered row reorders and survives switching back to All; checking a task draws the strike, then the row flies into Done with no exit ghost and no entrance; with Done collapsed, the row exits as before; unchecking a done task flies it back to the end of the list.
   - Gallery Lifecycle page: the same transfer between its two lists.

## Open questions

- Should Tempo keep `DoneMoveDelay` (strike, then fly) or drop it and let the strike draw on the arriving row through `Follow.AnimateInitial`? Settle it at the visual check. The proposal assumes the delay stays.
- How `SpecTests` names a generic type in a spec's `types` (`FilteredView` or ``FilteredView`1``). Check it when the spec is written.
