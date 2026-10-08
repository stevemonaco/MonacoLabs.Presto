---
id: BRIDGE-PRESENCE-EXIT
title: Presence exits and ghosts
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge
sources:
  - Monaco.Presto.AvaloniaBridge/Presence.cs
  - Monaco.Presto.AvaloniaBridge/VisualSnapshot.cs
  - Monaco.Presto.AvaloniaBridge/Text/PieceGhosts.cs
  - Monaco.Presto.AvaloniaBridge/PrestoAvalonia.cs
types: []
tests:
  - PresenceTests
  - PresenceLifecycleTests
  - PresencePageTransitionTests
  - PresenceGhostScrollTests
  - PresenceTextExitTests
  - PresenceModeTests
  - SnapshotScalingTests
  - ContentSizePresenceTests
  - WhenDetachedTests
  - AnimationBuilderBridgeTests
  - ReorderContainerTests
  - ActivityFeedPageTests
  - BuilderRecipePagesTests
  - PresenceVisibilityTests
upstream: []
depends:
  - BRIDGE-PRESENCE
  - BRIDGE-HELPERS
  - BRIDGE-TRANSFORMS
  - BRIDGE-TEXTREVEAL
  - BRIDGE-CONTENTSIZE
  - BRIDGE-PAGES
  - CORE-ANIMATION
  - CORE-REDUCED
  - CORE-DIAGNOSTICS
---

# Presence exits and ghosts

## Purpose

The departure half of `Presence` (BRIDGE-PRESENCE): which departures play `Presence.Exit`, and the ghost they play on. A removed control cannot be kept, since a list may recycle it and a panel re-add it at once, so an exit plays on a snapshot of the element in the window's `OverlayLayer`, drawn and clipped where the element was. `PrestoAvalonia.AnimateExit` is the same ghost for code that removes a control itself. This spec owns no types; it specifies `Presence.ExitProperty`'s departure behavior and the `AnimateExit` members of `PrestoAvalonia` (owned by BRIDGE-HELPERS).

## Requirements

### Which departures exit

- **BRIDGE-PRESENCE-EXIT-001** — When an element with `Exit` detaches and its attachment point has no top level or is itself detached (the window closed, a whole view removed above it), `Presence` shall play nothing.
  - Tests: `PresenceTests.ClosingTheWindowLeavesNoGhost`
- **BRIDGE-PRESENCE-EXIT-002** — When any visual between the element and the root of the removed branch has other than one visual child, the departure shall play nothing, so an element removed with its view leaves no ghost.
  - Tests: `PresenceTests.RemovingTheWholeViewAnElementLivesInLeavesNoGhost`
- **BRIDGE-PRESENCE-EXIT-003** — When the nearest control at or above the attachment point is not loaded (a view shown again re-creating its item containers), the departure shall play nothing.
  - Tests: `PresenceLifecycleTests.AListInAViewShownAgainLeavesNoGhostsOfTheContainersItReCreates`, `PresenceLifecycleTests.AViewPutStraightBackIsInitialAgainWithoutGhosts`
- **BRIDGE-PRESENCE-EXIT-004** — When the attachment point is the content of a page host whose `PageTransition` is a `PrestoPageTransition`, the departure shall play nothing, so a page root leaves no ghost.
  - Tests: `PresencePageTransitionTests.APageRootsExitLeavesNoGhostUnderAPrestoTransition`, `PresencePageTransitionTests.ANavigationPageRootNeitherEntersNorLeavesAGhost`, `PresencePageTransitionTests.ATabPageNeitherEntersNorLeavesAGhost`, `PresencePageTransitionTests.ACarouselPageNeitherEntersNorLeavesAGhost`
- **BRIDGE-PRESENCE-EXIT-005** — When the attachment point is not effectively visible or any of its ancestors has opacity 0, the departure shall play nothing; a partial opacity still ghosts.
  - Tests: `PresencePageTransitionTests.AnElementLeavingAParentThatIsNotOnScreenLeavesNoGhost`, `PresencePageTransitionTests.AnElementLeavingAVisibleLoadedParentStillLeavesAGhost`, `PresencePageTransitionTests.APageRootsExitLeavesNoGhostAfterACrossFade`
- **BRIDGE-PRESENCE-EXIT-006** — When the element or any visual up to the removed root has opacity 0 (such as content still held by a `Wait`), the departure shall play nothing.
  - Tests: `PresenceModeTests.AThirdContentWaitsForTheFirstExitAndTheHeldSecondLeavesNoGhost`
- **BRIDGE-PRESENCE-EXIT-007** — When the element is the content of an item container of a watched list, it shall exit only when its item was just removed or the list just reset; a row recycled or moved by the list shall play nothing.
  - Tests: `PresenceTests.AnItemRemovedFromItsCollectionLeavesAGhost`, `PresenceLifecycleTests.ClearingAListExitsEveryRow`, `PresenceLifecycleTests.ScrollingAVirtualizedListPlaysNothingAndLeavesNoGhosts`, `PresenceLifecycleTests.AVirtualizedListStillEntersAddedItemsAndExitsRemovedOnes`, `ReorderContainerTests.AMovePlaysNeitherTheEnterNorTheExitOfTheMovedRow`
- **BRIDGE-PRESENCE-EXIT-008** — When the element's base `IsVisible` is false at the detach, the departure shall play nothing.
  - Tests: `PresenceVisibilityTests.DetachingMidHoldReleasesTheHoldAndCancelsTheExit`
- **BRIDGE-PRESENCE-EXIT-009** — When an element exits, `Presence` shall snapshot it before reverting its arrival, so the ghost shows it as drawn mid-entrance and the element itself returns to its base values.
  - Tests: `PresenceLifecycleTests.ALeavingElementIsGhostedAsDrawnMidEntranceAndThenReverted`
- **BRIDGE-PRESENCE-EXIT-010** — When an element attaches again while its ghost is still exiting, `Presence` shall cancel the ghost's animation, removing the ghost, and classify the new arrival.
  - Tests: `PresenceLifecycleTests.AnElementAddedBackWhileItsGhostExitsDismissesTheGhostAndEnters`

### The ghost

- **BRIDGE-PRESENCE-EXIT-011** — The ghost shall be an `Image` of the element in a non-hit-testable host added to the `OverlayLayer` of the attachment point's top level, and the exit shall animate the ghost, never the element.
  - Tests: `PresenceTests.TheExitPlaysOnTheGhost`, `PresenceTests.ABlurExitBlursTheGhostAndLeavesTheElementUntouched`
- **BRIDGE-PRESENCE-EXIT-012** — The ghost shall stand where the element was drawn in its former parent: a removed panel child's slot, a removed item's row, replaced content's box beside the new content.
  - Tests: `PresenceTests.ARemovedPanelChildLeavesAGhostInItsSlotUntilItsExitEnds`, `PresenceTests.AnItemRemovedFromItsCollectionLeavesAGhost`, `PresenceTests.ReplacedContentLeavesAGhostBesideTheNewContent`, `ActivityFeedPageTests.DismissingARowRemovesItsEventAndCollapsesAGhost`
- **BRIDGE-PRESENCE-EXIT-013** — While the element's item container sits in a `VirtualizingPanel`, `Presence` shall record where the element is drawn after every layout pass and place its ghost there, since the panel moves the container before the content detaches.
  - Tests: `PresenceLifecycleTests.ARowRemovedFromAScrolledVirtualizedListIsGhostedWhereItWasDrawn`
- **BRIDGE-PRESENCE-EXIT-014** — The snapshot shall draw the element with the styles, dynamic resources and inherited values it had where it was drawn, lending the removed branch's logical root to the nearest logically attached ancestor with its `DataContext` pinned for the render.
  - Tests: `PresenceTests.AGhostKeepsTheStylesAndResourcesTheRemovedItemHad`, `PresenceLifecycleTests.AGhostOfARecycledRowKeepsWhatItInheritedFromItsContainer`
- **BRIDGE-PRESENCE-EXIT-015** — The transform the shorthands hold (BRIDGE-TRANSFORMS) shall move onto the ghost as a `MatrixTransform` about the element's `RenderTransformOrigin`, and any other render transform shall be drawn into a snapshot cropped to the element's untransformed box, so the ghost draws where the transform had moved the element.
  - Tests: `PresenceTests.AGhostIsDrawnWhereTheElementsRenderTransformHadMovedIt`
- **BRIDGE-PRESENCE-EXIT-016** — The snapshot shall be rendered at device resolution into a 96-DPI bitmap sized ÷ render scaling, so on a scaled display the ghost draws where and as large as the element did.
  - Tests: `SnapshotScalingTests.AnExitGhostDrawsWhereAndAsLargeAsTheElementDid`, `ContentSizePresenceTests.TheHostRemovedMidResizeGhostsAtItsDrawnSize`
- **BRIDGE-PRESENCE-EXIT-017** — The ghost shall be clipped to the intersection of the top level's bounds and the boxes of the clipping ancestors (`ClipToBounds`, or a `ContentSize` host) from the attachment point up, starting above the item container when the attachment point is one.
  - Tests: `PresenceTests.AGhostLeavingAScrolledListIsClippedToTheViewport`, `ContentSizePresenceTests.ASwapMidResizeRetargetsFromTheCurrentSizeAndClipsItsGhostThere`, `ContentSizePresenceTests.ASwapGhostsTheOldContentEntersTheNewAndResizesBetween`
- **BRIDGE-PRESENCE-EXIT-018** (inherited) — Each clipping box shall keep the size it had when the exit started, so a list or host shrinking as the element leaves does not cut off its ghost.
  - Tests: untested
- **BRIDGE-PRESENCE-EXIT-019** — When the attachment point and its clipping ancestors move during the exit (a scroll), `Presence` shall re-place the ghost and recompute its clip on each layout pass of the ghost's host, so the ghost follows its anchor and never draws outside the clip.
  - Tests: `PresenceGhostScrollTests.ScrollingMidExitMovesTheGhostWithItsAnchorAndReclipsIt`
- **BRIDGE-PRESENCE-EXIT-020** — When the exit completes or is canceled, `Presence` shall remove the ghost's host from the overlay and dispose every ghost bitmap exactly once; an exit that completes inside its creation (as under `ReduceAll`) shall do so in the same call.
  - Tests: `PresenceTests.ARemovedPanelChildLeavesAGhostInItsSlotUntilItsExitEnds`, `PresenceTests.CancelingTheExitRemovesTheGhost`, `PresenceTextExitTests.TheHostIsDismissedWhenTheLastGhostEnds`, `PresenceTextExitTests.ReduceAllLeavesNoGhost`
- **BRIDGE-PRESENCE-EXIT-021** — A ghost's exit shall use `WhenDetached.Continue`, so it runs to completion even when its window closes.
  - Tests: `WhenDetachedTests.AnExitGhostRunsToCompletionWhenItsWindowCloses`
- **BRIDGE-PRESENCE-EXIT-022** (inherited) — When the element has a zero width or height, or its top level has no overlay layer, no ghost shall be created and nothing shall play.
  - Tests: untested
- **BRIDGE-PRESENCE-EXIT-023** — The exit animation shall carry the diagnostics origin `"Presence"`.
  - Tests: untested

### Text ghosts

- **BRIDGE-PRESENCE-EXIT-024** — When the leaving element is a `TextBlock` whose `TextReveal.Pieces` is not `None` and the exit is not a reduced form, it shall leave as one ghost per piece, each standing in its piece's slot under one shared host, the ghosts being the exit's targets in reading order.
  - Tests: `PresenceTextExitTests.EachGhostStandsInItsPiecesSlot`, `PresenceTextExitTests.AnIndexStaggerStartsTheGhostsInReadingOrder`, `PresenceTextExitTests.TextExitRecipesAnimateEveryGhostToItsEndAndDismissTheHost`
- **BRIDGE-PRESENCE-EXIT-025** — Piece ghosts shall carry their line index, index in line and the block's line height, so `StaggerByLine` and `LineHeightOf` work on them.
  - Tests: `PresenceTextExitTests.StaggerByLineStartsEveryGhostOfALineAfterEveryGhostOfTheLineBefore`, `PresenceTextExitTests.LineHeightOfAGhostIsTheBlocksLineHeight`, `PresenceTextExitTests.CharCascadeOutStartsEachLinesGhostsAfterTheLineBefore`
- **BRIDGE-PRESENCE-EXIT-026** — With more than 256 pieces (`PieceGhosts.Max`) at the asked level, the split shall coarsen chars → words → lines, then fall back to one whole-element ghost.
  - Tests: `PresenceTextExitTests.OverTheCapGhostsCoarsenFromCharsToWordsToLinesToOneWholeGhost`
- **BRIDGE-PRESENCE-EXIT-027** — A masked block (`TextReveal.Mask`) shall clip each piece ghost to its piece's box.
  - Tests: `PresenceTextExitTests.AMaskedLineGhostIsClippedToItsLineBox`
- **BRIDGE-PRESENCE-EXIT-028** — A block still split by an in-flight arrival shall be ghosted from its live pieces as drawn; otherwise it shall be split for the snapshot and the split reverted before the call returns.
  - Tests: `PresenceTextExitTests.LeavingMidRevealGhostsTheLivePiecesAsDrawn`
- **BRIDGE-PRESENCE-EXIT-029** — A reduced exit on split text shall play on one whole-element ghost.
  - Tests: `PresenceTextExitTests.ReducedTextExitRecipesFadeOneWholeGhostOverShort`

### AnimateExit

- **BRIDGE-PRESENCE-EXIT-030** — `AnimateExit` shall play the exit on a ghost of the control drawn where the control is now, clipped as a departure is, so the caller can remove the control in the same frame.
  - Tests: `PresenceTests.AnimateExitGhostsAnAttachedControlThatIsThenRemoved`, `BuilderRecipePagesTests.ClosingAToastRemovesItAtOnceAndPlaysItsExitOnAGhost`
- **BRIDGE-PRESENCE-EXIT-031** — `AnimateExit` shall return `null` and play nothing when the control has no visual parent or top level, or no ghost can be drawn (BRIDGE-PRESENCE-EXIT-022).
  - Tests: `PresenceTests.AnimateExitGhostsAnAttachedControlThatIsThenRemoved`
- **BRIDGE-PRESENCE-EXIT-032** — Canceling the animation `AnimateExit` returns shall remove the ghost.
  - Tests: `PresenceTests.CancelingTheExitRemovesTheGhost`
- **BRIDGE-PRESENCE-EXIT-033** — The `Action<AnimationBuilder>` overload shall behave as the builder overload with `Presto.Build(exit)`.
  - Tests: `AnimationBuilderBridgeTests.ALambdaExitPlaysOnAGhostAsItsBuilderDoes`
- **BRIDGE-PRESENCE-EXIT-034** (inherited) — `AnimateExit` shall apply none of the departure rules (visibility, opacity, page hosts, lists) and start no `Wait`.
  - Tests: untested

### Defaults

- **BRIDGE-PRESENCE-EXIT-035** (inherited) — When `Exit` is not set, and no style setter withdrew one in the same departure (BRIDGE-PRESENCE-031), a departure shall create no ghost and play nothing.
  - Tests: untested

## Invariants

- A ghost exists only while its exit animation runs; its host is in the overlay exactly from the exit's start to its completion or cancellation.
- The leaving element is never animated by its exit and is left at its base values.

## Edge cases

- Clearing a list (`Reset`) ghosts every row that was drawn.
- A departure with no `Exit` still reverts the arrival.
- Content that was animating inside the element holds still in the ghost: a snapshot is a bitmap.

## Threading and lifetime

- UI thread only. The ghost host's `LayoutUpdated` handler lives as long as the host and leaves with it.
- Exits run with `WhenDetached.Continue` on the current engine; the bitmaps are disposed on dismissal.

## Reduced motion

- The exit's `ToParams()` resolves `WhenReduced` and `ReduceAll`. A reduced exit on split text uses one whole ghost. Under `ReduceAll` the exit completes at once and no ghost is left.

## Allocation

- Not on the tick path. Each exit allocates its bitmap(s), images, canvases and params; the per-layout-pass re-placement allocates nothing beyond the clip intersection.

## Deviations from anime.js

- No upstream counterpart.

## Decisions

- **Exits play on a ghost.** On removal the element is snapshotted at the window's render scaling into a non-hit-testable `Image` in the window's `OverlayLayer` at its drawn slot, clipped to its former ancestors' clip boxes; the exit plays on the ghost, the real control is untouched, and the ghost leaves the overlay when its animation completes or is canceled or reverted, so a scope revert clears it. `Presence.Exit` (attached) and `PrestoAvalonia.AnimateExit` (imperative, `null` when not on screen) are the two surfaces. Reason: Avalonia cannot keep a removed control mounted as Framer's `AnimatePresence` does, since an item container can be recycled and a panel child re-added the moment it leaves, while at `DetachedFromVisualTree` the control still renders into a bitmap and `AttachmentPoint` is the still-attached parent; clipping keeps a row leaving a scrolled list inside the list, and render scaling keeps it crisp. Rejected: enter and leave inside layout transitions, which need a presenter hook, not a layout hook.
- **A ghost only when the element leaves on its own.** An element is ghosted only when `AttachmentPoint` is still attached and every visual between it and the removed subtree's root has exactly one visual child. Reason: it separates an item removed from a list (its container wraps only it) from a page navigated away, which takes many elements with it and should leave no ghost of each; a closing window has a null `AttachmentPoint`.
- **An exit needs a loaded parent.** A departure exits only out of a loaded parent, the mirror of the arrival rule. Reason: a view shown again re-creates its item containers before it has loaded, which ghosted every row.
- **The snapshot borrows the removed branch's styles.** The branch's logical root is lent to the nearest logically attached ancestor with its `DataContext` pinned for the render, then released; the transform the shorthands hold is suspended by emptying the composer's group and moved onto the ghost as a `MatrixTransform`, while other render transforms stay baked in. The snapshot is a still frame. Reason: Avalonia strips a removed branch's styles and dynamic resources before any detach event fires, and a compositor snapshot is impossible once the composition visual is gone; emptying the group changes no property, so the composer does not re-assert; `RenderTargetBitmap` bakes in the render transform and crops to the untransformed box.
- **Ghosts of virtualized rows use the last drawn slot.** Content of a container in a `VirtualizingPanel` records where it is drawn after each layout pass and its ghost uses that slot, clipped only by what is outside the item container. Reason: a virtualizing panel moves a removed item's container before the content detaches, and the recycled container clipped the ghost to itself.
- **Ghosts are re-placed on every layout pass.** A ghost's host is re-placed and its clip recomputed on each layout pass while it lives. Reason: placing it once at the exit's start left ghosts shifted and outside their card after a scroll mid-exit. Rejected: hosting ghosts in a layer inside the nearest clipping ancestor (reason not recorded).
- **Exit builders animate only what an `Image` has.** An exit animates transform shorthands, `Opacity`, effects and other `Image` properties; a property only the original control has (such as `Background`) throws as on any other target, as the docs on `Exit` say. Reason: a consequence of the ghost being an `Image`.
- **A ghost's clip is not inflated for blur.** Effect shorthands apply to the ghost directly. Reason: the clip box is the set of ancestor clips that would have cut the live element's blur too.
- **A leaving block exits as one ghost per piece.** See BRIDGE-TEXTREVEAL.
- **One overlay host per text exit.** See BRIDGE-TEXTREVEAL.
- **Hiding split text follows `HideMode`.** See BRIDGE-TEXTREVEAL.
- **Every arrival and departure is classified once, by one rule.** See BRIDGE-PRESENCE.
- **One tracker per element; a detach snapshots the exit before reverting the arrival.** See BRIDGE-PRESENCE.
- **Lists decide for their items; realization and recycling play nothing.** See BRIDGE-PRESENCE.
- **A `Move` is tracked as a move.** See BRIDGE-PRESENCE.
- **A Presto page transition counts as navigation.** See BRIDGE-PRESENCE.
- **`Presence.Mode` is `Sync` or `Wait`; there is no `popLayout`.** See BRIDGE-PRESENCE.
- **A content size host owns its size.** See BRIDGE-CONTENTSIZE.
- **Features that manage their own lifetimes pass `WhenDetached.Continue`.** See BRIDGE-HELPERS.

## Non-goals

- Keeping or animating the real control after removal.
- Animating content inside the ghost; only the ghost's transform shorthands, `Opacity` and effects animate.

## Open items

- `AnimateExit` ghosts a control even when it or an ancestor is at opacity 0 or hidden, unlike a departure (BRIDGE-PRESENCE-EXIT-034). Untested.
- BRIDGE-PRESENCE-EXIT-018 (clip boxes keep their exit-start size), -022 and -023 have no test.
