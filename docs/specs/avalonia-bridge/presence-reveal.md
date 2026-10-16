---
id: BRIDGE-REVEAL
title: Scroll reveals and scroll anchoring (Presence.Reveal, RevealOnce, ScrollAnchor)
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/Presence.cs
  - MonacoLabs.Presto.Avalonia/ItemChanges.cs
types: []
tests:
  - PresenceLifecycleTests
  - ScrollRevealTests
  - ScrollAnchorTests
  - ScrollStickToEndTests
  - ActivityFeedPageTests
  - DashboardPageTests
upstream: []
depends:
  - BRIDGE-PRESENCE
  - BRIDGE-SCROLLOBSERVER
  - BRIDGE-SCROLL
  - BRIDGE-DECLLAYOUT
  - CORE-STAGGER
  - CORE-UTILS
  - CORE-REDUCED
---

# Scroll reveals and scroll anchoring (Presence.Reveal, RevealOnce, ScrollAnchor)

## Purpose

`Presence.Reveal` plays when an element is scrolled into view, and never on an initial load, an enter or a recycle (BRIDGE-PRESENCE). The element picks its path by where it sits: a row of a virtualizing list reveals through the list's realization, held until a layout pass shows it; any other element under a `ScrollViewer` is held hidden and watched by a private `ScrollObserver` (BRIDGE-SCROLLOBSERVER). `RevealOnce` limits a reveal to the first showing, and `Presence.ScrollAnchor` keeps a virtualizing list's first visible row in place when items change above it, so a live list can take inserts while it is read. This spec owns no types; it specifies `Presence.Reveal`, `Presence.RevealOnce`, `Presence.RevealThreshold` and `Presence.ScrollAnchor`, declared on `Presence` (BRIDGE-PRESENCE).

## Requirements

### Virtualizing lists

- **BRIDGE-REVEAL-001** — `Reveal` shall play only when a list realizes a row for an item it already had; it shall play nothing on an initial load or on an added item.
  - Tests: `PresenceLifecycleTests.RevealAlonePlaysNothingOnFirstLoadOrOnAnAddedItem`, `ActivityFeedPageTests.ScrollingALittleRevealsTheNewlyRealizedRows`
- **BRIDGE-REVEAL-002** — A row realized for a reveal shall be held, unanimated and not counted as seen, until a layout pass ends with its container effectively visible and intersecting the viewport; that pass shall reveal it.
  - Tests: `PresenceLifecycleTests.ARowRealizedOutOfViewRevealsOnceWhenScrolledIntoView`, `ActivityFeedPageTests.EventsInsertedAboveRevealOnlyInThePassThatBringsThemIntoView`
- **BRIDGE-REVEAL-003** — The rows one layout pass shows shall reveal as one batch in realization order, keyed by their panel, so `Delay(Presto.Stagger(n))` ripples them, each starting from its from-values in that pass.
  - Tests: `PresenceLifecycleTests.RowsRevealedInOnePassStaggerDownFromTheRowsAlreadyShown`
- **BRIDGE-REVEAL-004** — With `RevealOnce` true (the default), a row whose item has been shown shall not reveal again; with it false, every realization shall reveal.
  - Tests: `PresenceLifecycleTests.ScrollingBackOverSeenRowsRevealsThemOnlyWithoutRevealOnce`, `ActivityFeedPageTests.ScrollingBackOverSeenRowsRevealsThemAgainOnlyEveryTime`
- **BRIDGE-REVEAL-005** — The seen memory shall be kept per list and keyed by item, and an item shall count as seen once its row was shown by any moment, initial load and enter included.
  - Tests: `PresenceLifecycleTests.TheRowsOfTheFirstLoadAndAnAddedItemCountAsSeen`
- **BRIDGE-REVEAL-006** — When an item leaves the list, the memory shall forget it, so an item removed and added back reveals again.
  - Tests: `PresenceLifecycleTests.AnItemRemovedAndAddedBackIsRevealedAgain`, `PresenceLifecycleTests.TheSeenMemoryHoldsNoItemAfterItLeavesTheList`
- **BRIDGE-REVEAL-007** — When the list resets, the memory shall be cleared.
  - Tests: `PresenceLifecycleTests.AResetForgetsTheSeenRows`
- **BRIDGE-REVEAL-008** — When the list's scroller has moved more than one viewport (on either axis) since the previous layout pass, the rows realized or shown in that pass shall be marked seen and not animated, so jumps and flings land at rest.
  - Tests: `PresenceLifecycleTests.AJumpOfMoreThanAViewportSnapsTheRowsItRealizes`, `PresenceLifecycleTests.AJumpOntoHeldRowsSnapsThem`, `ActivityFeedPageTests.AJumpFarDownTheFeedLandsAtRest`
- **BRIDGE-REVEAL-009** — When a held row detaches, is recycled or its item is removed before it is shown, the list shall drop it from the held rows without marking it seen.
  - Tests: `PresenceLifecycleTests.AHeldRowThatLeavesIsForgotten`
- **BRIDGE-REVEAL-010** — When a revealing row is recycled or detaches, `Presence` shall revert its running reveal.
  - Tests: `PresenceLifecycleTests.RecyclingOrDetachingARowRevertsItsRunningReveal`
- **BRIDGE-REVEAL-011** (inherited) — When `Reveal` is cleared mid-reveal on an element with no other lifecycle builder, the reveal shall be reverted.
  - Tests: `PresenceLifecycleTests.ClearingRevealMidRevealReverts`
- **BRIDGE-REVEAL-012** — When a row is recycled while its list is anchoring (BRIDGE-REVEAL-024) before its reveal completes, the memory shall forget its item, so it reveals again when shown.
  - Tests: `PresenceLifecycleTests.RowsInsertedAboveAScrolledListRevealInTheWheelNotchThatBringsThemIntoView`
- **BRIDGE-REVEAL-013** — A layout pass whose anchor correction scrolled shall reveal nothing; the pass that lays the corrected rows out shall reveal them as a scroll would.
  - Tests: `ScrollAnchorTests.ARowTheCorrectionBringsIntoViewRevealsAsAScrollWould`
- **BRIDGE-REVEAL-014** — A container's template built for no item (no `DataContext`), as during recycling, shall not be held or revealed.
  - Tests: untested
- **BRIDGE-REVEAL-015** — A row of a virtualizing list shall not get a scroll observer, even inside a `ScrollViewer`.
  - Tests: `ScrollRevealTests.AVirtualizingListStillRevealsThroughItsRows`

### Elements outside virtualizing lists

- **BRIDGE-REVEAL-016** — When an element with `Reveal` attaches under a `ScrollViewer` with no `VirtualizingPanel` between them, `Presence` shall hold it at opacity 0 at `Animation` priority until it crosses its reveal threshold, then reveal it.
  - Tests: `ScrollRevealTests.AnElementInAPlainScrollViewerRevealsWhenScrolledIntoView`
- **BRIDGE-REVEAL-017** — The threshold shall be `RevealThreshold` parsed as a `ScrollThreshold` (BRIDGE-SCROLLOBSERVER) used as the observer's `Enter`; unset, the element's top reaching the viewport's bottom.
  - Tests: `ScrollRevealTests.ItWaitsForItsThreshold`
- **BRIDGE-REVEAL-018** — An element already past its threshold at attach shall cross on its first frame.
  - Tests: `ScrollRevealTests.ElementsInViewOnAttachRevealAsOneBatchInReadingOrder`
- **BRIDGE-REVEAL-019** — Elements that cross in the same frame shall reveal on the next engine tick as one batch per window, ordered by window position (top, then left), not attach order.
  - Tests: `ScrollRevealTests.ElementsInViewOnAttachRevealAsOneBatchInReadingOrder`
- **BRIDGE-REVEAL-020** — Each frame's crossings shall form their own batch, so the next crossing's stagger starts again at index 0.
  - Tests: untested
- **BRIDGE-REVEAL-021** — With `RevealOnce` true, the observer shall be reverted after the reveal, and the element shall stay revealed however it is scrolled.
  - Tests: `ScrollRevealTests.ItRevealsOnceAndStopsWatching`
- **BRIDGE-REVEAL-022** — With `RevealOnce` false, leaving the view shall revert the reveal and hold the element hidden again, and the next crossing shall reveal it again.
  - Tests: `ScrollRevealTests.WithoutRevealOnceItHidesOnLeavingAndRevealsAgain`
- **BRIDGE-REVEAL-023** — Outside any `ScrollViewer` and outside a list's realization, `Reveal` shall do nothing and hold nothing; on detach the observer and the hold shall be released and the reveal reverted.
  - Tests: untested

### ScrollAnchor

- **BRIDGE-REVEAL-024** — When `ScrollAnchor` is true and a non-reset change (add, remove, replace, move) starts before the first visible row, or is an add at that row's index, while the list is scrolled from 0 and not stuck to its end, the list shall correct its offset so that row stays where it is drawn.
  - Tests: `ScrollAnchorTests.AChangeAboveKeepsTheRowBeingReadInPlace`, `ScrollStickToEndTests.WithTheScrollAnchorAnInsertAtTheTopWhileScrolledUpKeepsTheRowStillAndMovesTheOffsetOnce`
- **BRIDGE-REVEAL-025** — The first visible row shall be the lowest-index realized, visible container whose top-left corner lies inside the viewport.
  - Tests: `ScrollAnchorTests.AChangeAboveKeepsTheRowBeingReadInPlace`
- **BRIDGE-REVEAL-026** — A change inside or below the first visible row, a removal of that row, a reset, or any change while scrolled to 0 shall play out as it would without the anchor, so at the top new items stay in view.
  - Tests: `ScrollAnchorTests.ChangesItDoesNotAnchorPlayOutAsWithoutIt`
- **BRIDGE-REVEAL-027** — While `Scroll.StickToEnd` holds the list at its end, the anchor shall leave the change to it (BRIDGE-SCROLL).
  - Tests: `ScrollStickToEndTests.WithTheScrollAnchorAnInsertAtTheTopWhileAtTheEndIsLeftToStickToEnd`, `ScrollStickToEndTests.WithTheScrollAnchorAppendsAtTheEndFollowWithoutAnAnchorAndMoveForwardOnly`
- **BRIDGE-REVEAL-028** — When the panel's re-estimate has recycled the anchor row, the list shall bring it into view before measuring, and shall correct at most twice per change.
  - Tests: `ScrollAnchorTests.AChangeAboveKeepsTheRowBeingReadInPlace`
- **BRIDGE-REVEAL-029** — When a layout pass ends at an offset other than the one the change or its last correction left, the anchor shall end and keep the reader's scroll.
  - Tests: `ScrollAnchorTests.AWheelNotchBeforeTheAnchorClearsIsNotUndone`, `ScrollAnchorTests.AScrollToTheTopBeforeTheAnchorClearsStaysThereWithTheAddedRowShown`
- **BRIDGE-REVEAL-030** — Otherwise the anchor shall end once the layout pass that realizes the change has finished.
  - Tests: untested
- **BRIDGE-REVEAL-031** — Rows already revealed shall not reveal again because of a correction, and an inserted row that lands out of view shall play no `Enter`.
  - Tests: `ScrollAnchorTests.RowsAlreadyRevealedDoNotRevealAgain`, `ScrollAnchorTests.AnInsertThatLandsOutOfViewEntersNothing`
- **BRIDGE-REVEAL-032** — With `Animate.ItemLayout` on the list, no container shall ease on the compensating pass (BRIDGE-DECLLAYOUT).
  - Tests: `ScrollAnchorTests.WithItemLayoutNoContainerEasesOnTheCompensatingPass`
- **BRIDGE-REVEAL-033** — With `ScrollAnchor` false (the default), the list shall keep Avalonia's own behavior, under which an insert above can move or recycle the row being read.
  - Tests: `ScrollAnchorTests.WithoutTheAnchorAnInsertAboveMovesTheRowBeingRead`

### Reduced motion

- **BRIDGE-REVEAL-034** — Under reduced motion a reveal shall play its builder's reduced form.
  - Tests: `ScrollRevealTests.UnderReducedMotionItRevealsWithTheReducedForm`, `DashboardPageTests.UnderReducedMotionTheRevealAndEachPushLandAtOnce`

### Defaults

- **BRIDGE-REVEAL-035** (inherited) — When `Reveal` is not set, the element shall reveal nothing and no reveal shall hold it hidden.
  - Tests: untested

## Invariants

- A row is never both held and revealing; it leaves the held rows when revealed, snapped, recycled or detached.
- The seen memory holds only items still in the list.
- An element outside a virtualizing list with `Reveal` is either hidden by Presence's opacity hold, revealing, or revealed; never visible at rest before its first crossing.

## Edge cases

- `RevealOnce` with `Reveal` unset does nothing.
- A plain `ItemsControl` without virtualization inside a `ScrollViewer` takes the observer path, row by row.
- Setting `ScrollAnchor` starts watching the list's item changes even when no row has a lifecycle builder.
- The anchor survives one extra re-estimate after its first correction; a third shift is not corrected.

## Threading and lifetime

- UI thread only. Per-list state lives in a `ConditionalWeakTable` keyed by the list. Scroll crossings are collected in a static list and played through `Utils.Sync` on the next engine tick (CORE-UTILS).

## Reduced motion

- The reduced form plays (BRIDGE-REVEAL-034); under `ReduceAll` a reveal lands at once. Jumps snap regardless of motion settings.

## Allocation

- Not on the tick path. Each layout pass with held rows measures them; each crossing frame allocates arrays to sort the batch.

## Provenance

- No upstream counterpart. The observer path reuses the `ScrollObserver` port (BRIDGE-SCROLLOBSERVER), which follows anime.js `onScroll`.

## Decisions

- **Inside a virtualizing list, `Reveal` plays on realization only.** A row reveals when the list realizes it for an item it already had, never on an initial load, an enter or a recycle. Reason: Presence's realization played nothing, leaving no way to reveal rows as they scroll in, while attachment also plays on first load and adds, gets no ordinal and replays on every recycle.
- **`Reveal` works in plain `ScrollViewer`s.** Under a scroller with no virtualizing panel, `Reveal` holds the element at opacity 0 and watches it with a private `ScrollObserver` whose `Enter` is `RevealThreshold`, and crossings reveal on the next tick as one batch per window in reading order. Earlier `Reveal` had no effect outside an item container; replaced to keep one name for one idea. Reason: one idea keeps one name, and a threshold inside the viewport would show the element before its entrance. Rejected: a separate `Scroll.Reveal`.
- **Reveal once per item by default, remembered by item.** `RevealOnce` (default true) reveals a row only the first time its item is realized; the memory is per list, keyed by item, counts any showing moment (initial load, enter) as seen, forgets removed or replaced items, keeps moved ones and clears on reset. Reason: containers are recycled, so the memory must key on the item.
- **The seen memory lives on the item-change watcher, and `Arrive` is not a fallback for realization.** The seen set shares the watcher's one `CollectionChanged` subscription and starts with the list's first arrival; rows without a `DataContext` are neither revealed nor remembered; a reveal is opted into only through `Reveal`. Reason: first-load rows must count as seen, recycled containers first attach a template built for no item that must take no ordinal, and `Arrive` stays "every classified arrival".
- **The same builder plays from either edge.** A reveal plays the same builder scrolling down or up, so in the default once mode scrolling back reveals nothing. Reason: not recorded beyond scoping the first version; mirroring by direction is left open.
- **Jumps of more than one viewport snap, with no setting.** Rows realized in a pass whose scroller moved more than one viewport on either axis are marked seen and not animated, even with `RevealOnce` false. Reason: a fling or scrollbar jump should land at rest instead of fading in a screen of rows, and measurement showed one viewport separates wheel notches and slow drags from jumps and fast drags.
- **Reveal batches keep realization order.** Rows realized in one pass share a batch whose ordinals follow realization order. Reason: realization starts next to the rows already shown and ends at the edge scrolled in from, so the ripple follows the scroll. Rejected: ordering from the leading edge, which would mean re-creating the batch's animations after the pass and running the ripple against the scroll.
- **A revealed row counts as seen only once a pass shows it in the viewport.** A realized row is held, not marked seen, until a layout pass ends with it intersecting the viewport; that pass reveals the held rows as one batch, a pass whose anchor correction scrolled is skipped, a jump marks held rows seen, a detach drops them, and a recycle while anchoring forgets a row whose reveal had not finished. Reason: rows realized in an insert pass that the anchor correction scrolled away were marked seen before they were drawn and lost their reveal, and an insert in the same frame as a wheel notch reverted a row's reveal.
- **Presto ships its own `ScrollAnchor`.** A bool `Presence.ScrollAnchor` keeps the first visible item in place when a change lands above it (`ScrollIntoView`, then one exact correction, at most two per change); an insert at the anchor's index counts as above, while removing the anchor row, a reset and offset 0 do not anchor. The anchor item is the container's `DataContext`, so a list whose items are their own containers does not anchor, and `ItemLayout` eases nothing on the insert and compensating passes. Reason: a live list that cannot take inserts while it is read is a common need, and Avalonia's anchoring is defeated when an insert above the realized range re-estimates and recycles the anchor, with no `CacheLength` that fixes it; it sits on `Presence` beside the list rules it owns, and the anchor is recorded before the panel's own handling, while containers still hold old indices, hence `DataContext`. Offset 0 keeps new items showing at the top. Rejected: waiting for Avalonia; `Animate.ScrollAnchor` or a separate class, since it is not an animation.
- **A scroll anchor ends when the reader scrolls.** The list keeps the offset the anchor expects (at the change, then after each correction), and a pass ending at any other offset drops the anchor instead of correcting; otherwise the anchor waits for a background-priority clear. Reason: while the anchor lived every pass corrected, so a scroll in that window was undone, and on desktop continuous wheel or drag input kept it alive; the background wait stays because the panel's shift can show one pass after the insert.
- **`StickToEnd` arms by position and never acts on the same change as `ScrollAnchor`.** See BRIDGE-SCROLL.

## Non-goals

- Mirroring a reveal by scroll direction (see Decisions: the same builder plays from either edge).
- A separate `Scroll.Reveal` API (see Decisions: `Reveal` works in plain `ScrollViewer`s).
- Anchoring lists that are not scrolled or are stuck to their end.

## Open items

- The manual wheel, touchpad and thumb-drag check of the Activity feed on Desktop is still pending.
- BRIDGE-REVEAL-014, -020, -023 and -030 have no test.
