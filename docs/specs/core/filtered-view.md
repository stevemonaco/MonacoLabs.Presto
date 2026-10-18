---
id: CORE-FILTERVIEW
title: Filtered view
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/FilteredView.cs
  - MonacoLabs.Presto/IMovableItems.cs
types:
  - FilteredView
tests:
  - FilteredViewTests
upstream: []
depends: []
---

# Filtered view

## Purpose

`FilteredView<T>` is a read-only live view over an `ObservableCollection<T>` that shows the items its `Filter` accepts, in source order, and raises one change event per row, so list motion that reads per-row changes (entrances, exits, layout easing) sees rows come and go one at a time instead of a reset. `Move` writes through to the source, and an internal non-generic move interface lets the bridge's reorder drive the view without reflection. It implements a read-only non-generic `IList` so UI frameworks that read an items source through `IList` can bind it.

## Requirements

### Contents

- **CORE-FILTERVIEW-001** — The view shall contain exactly the source items the filter accepts, in source order; a null filter shall accept every item.
  - Tests: `FilteredViewTests.ShowsTheAcceptedItemsInSourceOrder`

### Source changes

- **CORE-FILTERVIEW-002** — When the source adds, removes, replaces or moves an item, the view shall raise at most one change for that item, `Add`, `Remove`, `Replace` or `Move`, at its view index; a rejected item shall raise nothing.
  - Tests: `FilteredViewTests.SourceAddRaisesOneAddAtTheViewIndex`, `FilteredViewTests.SourceRemoveRaisesOneRemoveAtTheViewIndex`, `FilteredViewTests.SourceMovePastAVisibleItemRaisesOneMove`
- **CORE-FILTERVIEW-003** — When the source replaces an item, the view shall raise `Replace` when both items are accepted, `Remove` when only the old one was, `Add` when only the new one is, and nothing when neither is.
  - Tests: `FilteredViewTests.SourceReplaceRaisesOnlyWhatTheViewSees`
- **CORE-FILTERVIEW-004** — When the source moves an item that is rejected, or whose view index does not change, the view shall raise nothing.
  - Tests: `FilteredViewTests.SourceMovePastHiddenItemsOrOfAHiddenItemRaisesNothing`
- **CORE-FILTERVIEW-005** — When the source resets, the view shall re-evaluate every item and raise one `Reset`.
  - Tests: `FilteredViewTests.SourceResetRaisesOneReset`

### Re-evaluation

- **CORE-FILTERVIEW-006** — When `Filter` is set or `Refresh()` is called, the view shall remove each row no longer accepted, last to first, then insert each newly accepted item at its view index in source order, one event per row, and shall never raise `Reset` or move a row.
  - Tests: `FilteredViewTests.FilterChangeRemovesLastToFirstThenInsertsInSourceOrder`, `FilteredViewTests.RefreshReEvaluatesStateOutsideTheItems`
- **CORE-FILTERVIEW-007** — `Refresh(item)` shall re-evaluate that item alone.
  - Tests: `FilteredViewTests.RefreshItemReEvaluatesThatItemAlone`
- **CORE-FILTERVIEW-008** — When an item in the source raises `PropertyChanged` for a name in `refreshOn`, or with a null or empty name, the view shall refresh that item; other names shall do nothing.
  - Tests: `FilteredViewTests.RefreshOnReactsOnlyToTheListedNames`
- **CORE-FILTERVIEW-009** — The view shall listen to an item's `PropertyChanged` only while `refreshOn` is non-empty and the item is in the source.
  - Tests: `FilteredViewTests.ItemsAreWatchedOnlyWhileInTheSourceAndOnlyWithRefreshOn`

### Moving

- **CORE-FILTERVIEW-010** — `Move(oldIndex, newIndex)` shall move the item in the source to just before the item that follows it in the view after the move, or just after its predecessor when `newIndex` is last, so that the view raises exactly one `Move(oldIndex, newIndex)`.
  - Tests: `FilteredViewTests.MoveWritesTheSourceNextToTheVisibleNeighbor`
- **CORE-FILTERVIEW-011** — When `Move` is given an index outside the view, it shall throw `ArgumentOutOfRangeException`.
  - Tests: `FilteredViewTests.MoveOutOfRangeThrows`
- **CORE-FILTERVIEW-012** (inherited) — When `Move` is given equal indices, it shall do nothing.
  - Tests: `FilteredViewTests.MoveToTheSameIndexDoesNothing`

### Notifications and list surface

- **CORE-FILTERVIEW-013** — After each collection event, the view shall raise `PropertyChanged` for `Count` when the count changed, then for `Item[]`.
  - Tests: `FilteredViewTests.CountAndIndexerChangesAreRaisedAfterTheCollectionEvent`
- **CORE-FILTERVIEW-014** — The `IList` members that write (`Add`, `Insert`, `Remove`, `RemoveAt`, `Clear`, the indexer setter) shall throw `NotSupportedException`, and `IsReadOnly` and `IsFixedSize` shall be true.
  - Tests: `FilteredViewTests.WritingListMembersThrow`

### Lifetime

- **CORE-FILTERVIEW-015** — `Dispose` shall unsubscribe from the source and every item, and calls after it shall throw `ObjectDisposedException`.
  - Tests: `FilteredViewTests.DisposeDetachesFromTheSourceAndItemsAndLaterCallsThrow`

## Invariants

- The view's rows are always the accepted source items in source order.
- A re-evaluation never changes the relative order of rows that stay.

## Edge cases

- An item that appears twice in the source is shown, refreshed and unsubscribed once per occurrence.
- A source subclass that raises multi-item `Add`, `Remove` or `Replace` events is translated one item at a time.
- A filter is evaluated once per item per `Refresh()`; a filter whose answer changes between calls without a refresh leaves the view stale until the next re-evaluation.

## Threading and lifetime

- Single-threaded, like the source it views: events are raised synchronously on the thread that changed the source.
- Subscriptions are strong; the source and every watched item keep the view alive until it is disposed (see Decisions).

## Reduced motion

- Not affected: the view raises collection events, not motion.

## Allocation

- Not on the tick path. Each change allocates its event arguments; the bookkeeping is two lists, with no LINQ in the per-event paths.

## Provenance

- No upstream counterpart: anime.js has no collections.

## Decisions

- **The filtered view is in core.** Reason: it needs nothing from a UI framework, and a view-model project that references only the core can use it. Rejected: the bridge, which would make a view model reference the UI framework to filter a list; DynamicData, a dependency with range changes that lists would have to split.
- **Per-row events and no reset from a re-evaluation.** Removals go before insertions. Reason: a `Reset` ghosts every drawn row and makes every arrival an initial one, which is the motion a filter must not have; removals first give list layout one gap-closing pass and then one opening pass. Rejected: a minimal edit script that also moves rows, since a filter never changes order.
- **The source must be an `ObservableCollection<T>`.** Reason: `Move` must write through as a move, and that is the type the bridge's reorder already moves. Rejected: any `IList<T>` plus `INotifyCollectionChanged`, whose only write-through for a move is a remove and an insert, so the row would exit and enter.
- **No sorting.** Reason: the known cases only filter; a sort key that changes would make the view move rows, and a reorder would then fight the sort. Rejected for now: a `Comparison<T>`.
- **Re-evaluation is explicit, plus opt-in property names.** `Refresh()` covers state outside the items, and `refreshOn` covers state on them. Reason: watching every `PropertyChanged` of every item would re-run the filter on each keystroke in a title. Rejected: re-evaluating on any property change; requiring `Refresh(item)` from the view model.
- **The view holds no weak references.** It subscribes strongly and is `IDisposable`. Reason: a view usually lives exactly as long as the view model that owns the source. Rejected: weak event subscriptions, which add a type and make a dropped view's events fire on until a GC collects it.
- **The bridge's reorder recognizes the view directly.** The view implements an internal non-generic move interface, exposed to the bridge through `InternalsVisibleTo`. Reason: no reflection, and no public interface that only the reorder would read. Rejected: a public `IMovableList`.

## Non-goals

- Sorting or grouping.
- Range (multi-item) events: every change is one row.
- Weak subscriptions.

## Open items

- None.
