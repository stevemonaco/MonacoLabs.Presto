---
id: BRIDGE-PRESENCE
title: Presence lifecycle (classification, arrivals, show and hide, modes)
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge
sources:
  - Monaco.Presto.AvaloniaBridge/Presence.cs
  - Monaco.Presto.AvaloniaBridge/PresenceMode.cs
  - Monaco.Presto.AvaloniaBridge/PresenceHideMode.cs
  - Monaco.Presto.AvaloniaBridge/PresenceMoment.cs
  - Monaco.Presto.AvaloniaBridge/ItemChanges.cs
  - Monaco.Presto.AvaloniaBridge/Targets/VisibilityHold.cs
types:
  - Presence
  - PresenceHideMode
  - PresenceMode
tests:
  - PresenceLifecycleTests
  - PresenceModeTests
  - PresenceVisibilityTests
  - PresenceEnterOnShowTests
  - PresencePageTransitionTests
  - PresenceTextExitTests
  - WhenDetachedTests
  - ReorderContainerTests
  - PopupSpikeTests
  - TextRevealTests
  - ReferencePagesTests
  - VisibilityPageTests
upstream: []
depends:
  - BRIDGE-PRESENCE-EXIT
  - BRIDGE-REVEAL
  - BRIDGE-INIT
  - BRIDGE-TARGETS
  - BRIDGE-HELPERS
  - BRIDGE-TEXTREVEAL
  - BRIDGE-PAGES
  - BRIDGE-DECLLAYOUT
  - CORE-BUILDER
  - CORE-ANIMATION
  - CORE-STAGGER
  - CORE-REDUCED
  - CORE-DIAGNOSTICS
---

# Presence lifecycle (classification, arrivals, show and hide, modes)

## Purpose

`Presence` is the bridge's lifecycle layer: attached `AnimationBuilder`s that play when an element appears with its view (`Initial`), arrives on its own into a loaded view (`Enter`), either (`Arrive`), is scrolled into view (`Reveal`, BRIDGE-REVEAL) or leaves on its own (`Exit`, played on a ghost, BRIDGE-PRESENCE-EXIT). One tracker per element classifies every arrival and departure by one rule, so the moments never disagree. This spec covers the classification, arrivals and their batches, the element's own `IsVisible` flips (`HideMode`, `EnterOnShow`), `Mode` (`Sync`/`Wait`) and the interplay with Presto page transitions. `Animate.OnAttached` (BRIDGE-ATTACH) is the unclassified trigger beside it.

## Requirements

### Tracking

- **BRIDGE-PRESENCE-001** — When any of `Initial`, `Enter`, `Arrive`, `Reveal` or `Exit` is set non-null or `EnterOnShow` is set true on a control, `Presence` shall start tracking the control's attaches, detaches and `IsVisible` changes.
  - Tests: `PresenceLifecycleTests.InitialPlaysOnlyWithTheViewAndEnterOnlyOnItsOwn`
- **BRIDGE-PRESENCE-002** — When every lifecycle builder is cleared and `EnterOnShow` is false, `Presence` shall stop tracking and revert the running arrival and any held hide.
  - Tests: `PresenceLifecycleTests.ClearingEveryLifecycleBuilderRevertsTheArrival`, `PresenceVisibilityTests.ClearingTheBuildersMidHoldReleasesTheHold`
- **BRIDGE-PRESENCE-003** (inherited) — Setting a builder on a control that is already attached shall play nothing until the control's next arrival or show.
  - Tests: untested
- **BRIDGE-PRESENCE-004** (inherited) — While `Design.IsDesignMode` is true, setting a lifecycle property shall start no tracking.
  - Tests: untested

### Classifying an arrival

- **BRIDGE-PRESENCE-005** — `Presence` shall classify an arrival once, when the control attaches to the visual tree, never at `Loaded`.
  - Tests: `PresenceLifecycleTests.EverythingShownWithTheWindowArrivesAsInitial`
- **BRIDGE-PRESENCE-006** — The arrival shall walk up the control's visual ancestors past those that are not loaded, are not a `Panel` and have exactly one visual child, and shall classify as `Enter` when the first ancestor that stops the walk is loaded (`Control.IsLoaded`).
  - Tests: `PresenceLifecycleTests.AChildAddedToALoadedPanelEnters`, `PresenceLifecycleTests.SwappedInContentEntersWhileTheContentOfItsViewIsInitial`
- **BRIDGE-PRESENCE-007** — When the walk stops at an ancestor that is not loaded (a `Panel` or one with other than one visual child) or reaches the root, the arrival shall classify as `Initial`.
  - Tests: `PresenceLifecycleTests.EverythingShownWithTheWindowArrivesAsInitial`, `PresenceLifecycleTests.ElementsAddedTogetherInOneSubtreeAreInitial`, `PresenceLifecycleTests.TheOnlyRowOfAListNavigatedInIsInitialEvenThroughSingleChildWrappers`, `PopupSpikeTests.PopupContentArrivesAsInitialAndOnReopenAsEnterUnlessItsTemplateWrapsItWithSiblings`
- **BRIDGE-PRESENCE-008** — When a view is shown again, the arrivals inside it shall classify as `Initial` again.
  - Tests: `PresenceLifecycleTests.AViewShownAgainIsInitialAgain`, `PresenceLifecycleTests.AViewPutStraightBackIsInitialAgainWithoutGhosts`
- **BRIDGE-PRESENCE-009** — When the walk reaches the content of a page host whose `PageTransition` is a `PrestoPageTransition` (a `TransitioningContentControl` or `NavigationPage` presenter, a `TabControl`'s content presenter, a `Carousel` item container), the arrival shall classify as `Initial`, so a page root's `Enter` does not play and its `Initial` does.
  - Tests: `PresencePageTransitionTests.APageRootsEnterDoesNotPlayUnderAPrestoTransition`, `PresencePageTransitionTests.APageRootsInitialPlaysUnderAPrestoTransition`, `PresencePageTransitionTests.ANavigationPageRootNeitherEntersNorLeavesAGhost`, `PresencePageTransitionTests.ATabPageNeitherEntersNorLeavesAGhost`, `PresencePageTransitionTests.ACarouselPageNeitherEntersNorLeavesAGhost`
- **BRIDGE-PRESENCE-010** — When the control is (in) the content of an item container and its list has been watched for one finished layout pass, an arrival the walk calls `Enter` shall stay `Enter` only when the control's item (`DataContext`) was just added; otherwise the list decides.
  - Tests: `PresenceLifecycleTests.ItemsAListLoadsWithAreInitialAndItemsAddedLaterEnter`, `PresenceLifecycleTests.AVirtualizedListStillEntersAddedItemsAndExitsRemovedOnes`
- **BRIDGE-PRESENCE-011** — When the list realizes a row for an item it already had (as it does while scrolling), the arrival shall classify as a realization, which plays no `Initial`, `Enter` or `Arrive` (only `Reveal`, BRIDGE-REVEAL).
  - Tests: `PresenceLifecycleTests.ScrollingAVirtualizedListPlaysNothingAndLeavesNoGhosts`
- **BRIDGE-PRESENCE-012** — When the list hosts a row for an item it has just moved, the arrival shall play nothing.
  - Tests: `ReorderContainerTests.AMovePlaysNeitherTheEnterNorTheExitOfTheMovedRow`
- **BRIDGE-PRESENCE-013** (inherited) — When the list has just reset, a row it realizes shall classify as `Initial`.
  - Tests: untested

### Playing an arrival

- **BRIDGE-PRESENCE-014** — An `Initial` arrival shall play `Initial ?? Arrive` and an `Enter` arrival `Enter ?? Arrive`; an element with only `Initial` plays nothing on an enter, and one with only `Enter` nothing on an initial load.
  - Tests: `PresenceLifecycleTests.InitialPlaysOnlyWithTheViewAndEnterOnlyOnItsOwn`, `PresenceLifecycleTests.ASetInitialOrEnterWinsOverArriveForItsOwnMoment`
- **BRIDGE-PRESENCE-015** — An arrival animation shall apply its start values in the turn the control attaches, before its first frame is drawn.
  - Tests: `PresenceLifecycleTests.InitialPlaysOnlyWithTheViewAndEnterOnlyOnItsOwn`
- **BRIDGE-PRESENCE-016** — When the control attaches again, `Presence` shall revert its previous arrival before classifying the new one.
  - Tests: `PresenceLifecycleTests.AnElementAddedBackWhileItsGhostExitsDismissesTheGhostAndEnters`
- **BRIDGE-PRESENCE-017** — When the control detaches, `Presence` shall revert its arrival (after any exit has snapshotted it), rather than leave it paused by the detach rule (arrivals use `WhenDetached.Continue`).
  - Tests: `WhenDetachedTests.APresenceEnterOnADetachingControlIsRevertedByPresenceNotHeldPaused`, `PresenceLifecycleTests.RecyclingOrDetachingARowRevertsItsRunningReveal`
- **BRIDGE-PRESENCE-018** — When the control is a `TextBlock` whose `TextReveal.Pieces` is not `None` and the builder is not a reduced form, the arrival shall play on the block's split pieces (BRIDGE-TEXTREVEAL).
  - Tests: `TextRevealTests.InitialWordsAnimateEveryWordAndNotTheBlockWhichWaitsHiddenForTheSplit`
- **BRIDGE-PRESENCE-019** — Every animation `Presence` creates shall carry the diagnostics origin `"Presence"` and initialize the bridge first.
  - Tests: untested

### Batches and order staggers

- **BRIDGE-PRESENCE-020** — Arrivals under the same nearest ancestor `Panel` (else the top level) in one layout pass shall form a batch, and each arrival's animation shall resolve function values and `Presto.Stagger` with its arrival index and the arrivals so far as its targets.
  - Tests: `PresenceLifecycleTests.ItemsAddedInOnePassStaggerByArrivalOrder`, `PresenceLifecycleTests.AListStaggersInWithTheViewItLoadsWith`
- **BRIDGE-PRESENCE-021** — A batch shall close once the layout pass that formed it has finished, so arrivals in a later pass start a new batch at index 0.
  - Tests: untested
- **BRIDGE-PRESENCE-022** — Because a batch's total is not known while it is still arriving, a stagger shall see the arrivals so far as the total, so staggers run in arrival order.
  - Tests: `PresenceLifecycleTests.ItemsAddedInOnePassStaggerByArrivalOrder`

### Mode (Sync and Wait)

- **BRIDGE-PRESENCE-023** — The mode host of an element shall be its nearest ancestor that sets `Mode`, walking from an item container to its list; with no such host, or under `Sync`, arrivals shall enter while exits play.
  - Tests: `PresenceModeTests.UnderSyncTheEnterPlaysWhileTheGhostExits`
- **BRIDGE-PRESENCE-024** — When an exit ghost of something leaving a host in `Wait` starts, the host shall wait until every exit ghost started under it has ended.
  - Tests: `PresenceModeTests.AWaitingListHoldsAnAddedRowUntilTheRemovedRowsGhostEnds`, `ReferencePagesTests.TheWaitingWizardHoldsTheNextStepUntilTheOldStepsGhostEnds`
- **BRIDGE-PRESENCE-025** — While a host waits, every `Initial` or `Enter` arrival under it shall be held at opacity 0 at `Animation` priority with its entrance not started.
  - Tests: `PresenceModeTests.UnderWaitTheNewContentEntersOnlyOnceTheExitEnds`
- **BRIDGE-PRESENCE-026** — While a content host (its exit anchor a `ContentPresenter` or `Decorator`) waits, its new content root shall be held at opacity 0 even when it has no lifecycle builder.
  - Tests: `PresenceModeTests.NewContentWithoutABuilderAppearsWhenTheExitEnds`
- **BRIDGE-PRESENCE-027** — When the last exit under a waiting host ends, `Presence` shall lift every hold first and then play the held entrances, laid out.
  - Tests: `PresenceModeTests.UnderWaitTheNewContentEntersOnlyOnceTheExitEnds`, `PresenceModeTests.AThirdContentWaitsForTheFirstExitAndTheHeldSecondLeavesNoGhost`
- **BRIDGE-PRESENCE-028** — When a held element detaches, it shall leave no ghost and play no entrance, and its hold shall be released after the detach.
  - Tests: `PresenceModeTests.AThirdContentWaitsForTheFirstExitAndTheHeldSecondLeavesNoGhost`, `ReferencePagesTests.DoubleClickingNextInTheWaitingWizardEndsOnTheThirdStepVisibleAndReleasesTheSkippedStep`
- **BRIDGE-PRESENCE-029** — When a waiting host's `Mode` is cleared or set to anything but `Wait`, `Presence` shall release the wait at once.
  - Tests: `PresenceModeTests.ClearingTheModeReleasesTheHold`
- **BRIDGE-PRESENCE-030** — When an exit completes inside its creation (an instant exit, as under `ReduceAll`), the wait it started shall end in the same call.
  - Tests: `PresenceModeTests.UnderReduceAllTheWaitEndsWithTheInstantExit`
- **BRIDGE-PRESENCE-031** — An `Exit` set by a style setter, which leaving the logical tree withdraws before the visual detach, shall still play on that detach and start the wait.
  - Tests: `PresenceModeTests.AnExitFromAStyleSetterGhostsTheSwappedContentAndStartsTheWait`
- **BRIDGE-PRESENCE-032** — A held hide (`HideMode = Hold`) and `PrestoAvalonia.AnimateExit` shall start no wait.
  - Tests: untested
- **BRIDGE-PRESENCE-055** — When an exit under a `Wait` host completes inside its creation (as under `ReduceAll`), the content that replaces it shall not be held: it shows at full opacity and no ghost is left.
  - Tests: `PresenceModeTests.UnderReduceAllTheWaitEndsWithTheInstantExit`

### IsVisible show and hide

- **BRIDGE-PRESENCE-033** — `Presence` shall treat an `IsVisible` change as a show or hide only when it is raised below `Animation` priority and the base value differs from the one last recorded; Presto's own `Animation`-priority visibility writes shall play nothing.
  - Tests: `PresenceVisibilityTests.AnimationPriorityVisibilityHoldsPlayNothing`
- **BRIDGE-PRESENCE-034** — When an attached element is shown, `Presence` shall play `Initial ?? Arrive` while its view is still loading and `Enter ?? Arrive` otherwise, after reverting its previous arrival.
  - Tests: `PresenceVisibilityTests.HiddenAtLoadAndShownLaterEnters`
- **BRIDGE-PRESENCE-035** — When an attached element is hidden and has an `Exit`, is loaded, has a non-zero own opacity and its parent chain is effectively visible with no ancestor at opacity 0, `Presence` shall play the exit as `HideMode` says; otherwise it shall hide at once with no exit.
  - Tests: `PresenceVisibilityTests.AnElementNotDrawnHidesAtOnceWithoutItsExit`, `PresenceVisibilityTests.AnElementWithoutBuildersHidesAtOnce`
- **BRIDGE-PRESENCE-036** — Under `HideMode.Hold` (the default), the hide shall hold `IsVisible` true at `Animation` priority while the exit plays on the live element in its layout slot, and when the exit ends shall release the hold and revert the exit, so the element hides at its base values.
  - Tests: `PresenceVisibilityTests.ABoundHidePlaysTheExitOnTheLiveElementInItsSlotAndThenHides`, `VisibilityPageTests.TheBoundToggleHidesTheCardOnlyOnceItsExitHasPlayed`
- **BRIDGE-PRESENCE-037** — When a held exit ends, layout shall close the slot, which `Animate.ItemLayout` or `Animate.ContentSize` can ease (BRIDGE-DECLLAYOUT).
  - Tests: `PresenceVisibilityTests.ItemLayoutClosesTheSlotOnceTheHeldExitEnds`
- **BRIDGE-PRESENCE-038** — While a held exit plays, `Presence` shall read the base `IsVisible` on every frame of the exit, since the hold hides base changes from notifications.
  - Tests: `PresenceVisibilityTests.ShownAgainMidExitPlaysBackFromWhereItStands`
- **BRIDGE-PRESENCE-039** — When an element is shown again mid held exit, `Presence` shall release the hold and play the exit back from where it stands (`Reverse`) instead of its entrance; hidden again mid-way, it shall play the exit forward.
  - Tests: `PresenceVisibilityTests.ShownAgainMidExitPlaysBackFromWhereItStands`, `PresenceTextExitTests.ShownAgainMidHeldHideThePieceExitPlaysBack`
- **BRIDGE-PRESENCE-040** — Under `HideMode.Ghost`, the hide shall let layout close the slot at once and play the exit on a ghost (BRIDGE-PRESENCE-EXIT) snapshotted under a momentary visibility hold, then revert the arrival.
  - Tests: `PresenceVisibilityTests.GhostModeClosesTheSlotAtOnceWhileAGhostExits`, `VisibilityPageTests.TheGhostCardLeavesTheLayoutAtOnce`, `PresenceTextExitTests.AGhostHideCollapsesAtOnceAndGhostsPerPieceInOneHost`
- **BRIDGE-PRESENCE-041** — When an element hidden under `Ghost` is shown again mid-exit, `Presence` shall dismiss the ghost and play the entrance.
  - Tests: `PresenceVisibilityTests.GhostModeShownAgainMidExitDismissesTheGhostAndEnters`
- **BRIDGE-PRESENCE-042** — When an element detaches mid held exit, `Presence` shall release the hold, cancel and revert the exit, and play no departure exit, since its base is hidden.
  - Tests: `PresenceVisibilityTests.DetachingMidHoldReleasesTheHoldAndCancelsTheExit`, `PresenceTextExitTests.DetachingMidHeldHideReleasesTheHoldAndRevertsTheSplit`
- **BRIDGE-PRESENCE-043** — When a split `TextBlock` is hidden under `Hold`, the exit shall play on its live pieces with no ghost and the block shall be whole again once hidden; its reduced form plays on the unsplit block.
  - Tests: `PresenceTextExitTests.AHeldHidePlaysOnTheLiveCharsAndHidesWholeOnceItEnds`, `PresenceTextExitTests.AReducedHeldHidePlaysOnTheUnsplitBlock`
- **BRIDGE-PRESENCE-044** — When an exit completes inside its creation, as under `ReduceAll`, the hide shall end in the same call, and a show shall land at once.
  - Tests: `PresenceVisibilityTests.UnderReduceAllAHideAndAShowAreInstant`, `PresenceTextExitTests.UnderReduceAllAHideIsInstantAndLeavesNoSplit`
- **BRIDGE-PRESENCE-056** (inherited) — When the element's base `IsVisible` changes while it is detached, `Presence` shall record the new value and play nothing.
  - Tests: untested

### EnterOnShow

- **BRIDGE-PRESENCE-045** — When a host with `EnterOnShow` true is shown, `Presence` shall replay `Enter ?? Arrive` on its descendants as one batch keyed by the host, in visual-tree order, in the layout pass that shows them.
  - Tests: `PresenceEnterOnShowTests.ShowingTheHostStaggersItsRevealedChildrenInOrder`
- **BRIDGE-PRESENCE-046** — When `EnterOnShow` is false (the default), showing a host shall replay nothing on its descendants.
  - Tests: `PresenceEnterOnShowTests.WithoutEnterOnShowNothingReplays`
- **BRIDGE-PRESENCE-047** — Set on a template part that collapses (an `Expander`'s `Border#ExpanderContent`), `EnterOnShow` shall replay the content's entrances on every expand, the first included.
  - Tests: `PresenceEnterOnShowTests.ExpandingAnExpanderReplaysItsContentsEntrances`, `PresenceEnterOnShowTests.TheFirstExpandOfANeverOpenedExpanderReplaysTheRowsThatAttachInThatPass`
- **BRIDGE-PRESENCE-048** — The replay shall skip a descendant whose own `IsVisible` is false, with its subtree, and a descendant under a nested `EnterOnShow` host shown in the same pass shall play once, in the outer batch.
  - Tests: `PresenceEnterOnShowTests.AHiddenChildIsSkippedAndOneUnderANestedHostShownWithItEntersOnce`
- **BRIDGE-PRESENCE-049** — The host's own entrance shall play apart from the batch, in the same frame.
  - Tests: untested
- **BRIDGE-PRESENCE-050** (inherited) — When the host is no longer effectively visible by the pass that would replay, or a descendant is mid held hide, that replay shall be skipped.
  - Tests: untested
- **BRIDGE-PRESENCE-051** — Hiding an `EnterOnShow` host shall play only the host's own `Exit`, nothing on its descendants.
  - Tests: `PresenceEnterOnShowTests.HidingTheHostPlaysOnlyItsOwnExit`
- **BRIDGE-PRESENCE-052** — Under `ReduceAll` the replayed descendants shall land at once.
  - Tests: `PresenceEnterOnShowTests.UnderReduceAllRevealedChildrenLandAtOnce`

### Diagnostics and samples

- **BRIDGE-PRESENCE-053** — The classification shall be observable end to end in the sample's Lifecycle page log.
  - Tests: `ReferencePagesTests.LifecycleLogsWhatPresenceClassified`

### Defaults

- **BRIDGE-PRESENCE-054** (inherited) — When no ancestor sets `Mode`, the element shall behave as under `Sync`; `Mode`'s default value is `Sync`.
  - Tests: untested

## Invariants

- Each tracked element has at most one running arrival and at most one held hide; a new arrival or show reverts the previous arrival first.
- An element's hold on `IsVisible` is owned through `VisibilityHold`, which lets several owners hold one visual; the last owner to change its value wins, and the base value shows once all release.
- Holds at opacity 0 (`Wait`) are at `Animation` priority and are always released, by the wait's end, a detach or a mode change.

## Edge cases

- A list is watched from the first lifecycle arrival in it with a `DataContext`; until the first layout pass after that ends, its rows are classified by the general walk (BRIDGE-PRESENCE-006/007).
- Inside a `TreeView`, a nested item resolves to the outer list, whose changes never see the section's collection, so a page added or removed in a section reads as a realization or recycle.
- Changing one builder while others stay set does not restart or revert the running arrival.
- A host whose nearest `Mode` setter is `Sync` shadows a `Wait` further up.
- An element hidden by its parent (not its own `IsVisible`) plays nothing; only `EnterOnShow` on the parent replays it.

## Threading and lifetime

- UI thread only. Batches, pending `EnterOnShow` hosts and pending scroll crossings are static, process-wide collections keyed by visual; each is cleared after the pass or frame that filled it.
- The tracker lives in a private attached property and holds the control's events; it is released when every builder is cleared, or on a detach that leaves no builder. Arrivals and held hides use `WhenDetached.Continue`; `Presence` reverts them itself.
- Per-list state (`ItemChanges`) is kept in a `ConditionalWeakTable`, so a collected list takes it along.

## Reduced motion

- Every builder is played through `ToParams()`, which resolves `WhenReduced` and `ReduceAll` (CORE-REDUCED). Under `ReduceAll` exits complete inside their creation: holds and waits end in the same call and ghosts are dismissed at once. A reduced form on split text plays on the unsplit block.

## Allocation

- Not on the tick path, except a held exit's per-frame base-visibility read (`OnUpdate`), which allocates nothing. Each arrival allocates its params, animation and a copy of its batch's targets.

## Deviations from anime.js

- No upstream counterpart. A growing batch staggers because `Presto.Stagger` rebuilds its cache when the target count changes, a deviation CORE-STAGGER owns.

## Decisions

- **Every arrival and departure is classified once, by one rule.** One internal classification sorts each into Initial, Enter, Exit or Unload (plus the internal Realize, Recycle and Move), and Unload (leaving with the view) plays nothing. Reason: the earlier pairing of an unfiltered `Animate.OnAttached` with a filtered `Presence.Exit` had different rules and undefined ordering, and could not tell a view's initial load from an item added later. Rejected: a `Presence.Unload` ghost of a whole view, since page transitions belong to the navigation host.
- **Classification happens at attach, never at `Loaded`.** An arrival into a loaded host (`Control.IsLoaded`) is an Enter, anything else Initial. Reason: `Loaded` runs after the first render, so an animation started there shows one frame at rest; `IsLoaded` is set after first layout and render and cleared by `Unloaded`, so a re-shown cached view counts as Initial.
- **One tracker per element; a detach snapshots the exit before reverting the arrival.** The tracker owns every `Presence` property; a re-attach while its own ghost exits dismisses the ghost, and every attach is classified afresh. Reason: with independent handlers the order depended on XAML attribute order, so a ghost of a row removed mid-entrance started from either look, and a recycled control must be left clean.
- **Arrivals stop at panels.** The walk passes non-panel, single-child, not-yet-loaded ancestors and stops at the first panel, multi-child or loaded one. Reason: an `ItemsControl` realizing its first item holds one child for a moment, so a "wraps only it" walk passed the panel, misclassifying a view's only row or splitting one list into several batches.
- **Batches are keyed by the nearest `Panel` and support order staggers only.** Each animation is created at its own attach with its ordinal, and the total is the arrivals so far. Reason: creating it at attach lets the first frame show from-values, and the total is unknown while the batch arrives; keying by the deciding ancestor split a list once its containers had loaded. Rejected: deferring a batch to the first engine tick so the total is known, since that tick runs before layout and realized rows would render a frame at rest.
- **The item-change window and a batch end after the next layout pass.** They close on `LayoutUpdated` plus a posted job, not at `DispatcherPriority.Loaded`. Reason: on Desktop layout runs on the next render tick, so `Loaded` ran before the pass that realized an added row, which then read as a scroll realization.
- **Lists decide for their items; realization and recycling play nothing.** An item container's content enters only if its item was just added and exits only if it was removed or the list reset; the owner list is found from the items panel. Reason: a virtualizing panel detaches content when it recycles a container, so scrolled rows exited and entered, and a recycled container has lost the logical parent `ItemsControlFromItemContainer` reads. Rejected: accepting that scrolled-in rows enter (as Framer does), a `Presence.IsInitialScope`, and treating a virtualizing panel's realizations as Initial (the panel does not say why it realized).
- **A `Move` is tracked as a move.** The list records moved items, the arrival classifies as Move and the departure as a recycle, so Enter, Exit and Reveal play nothing. Reason: a move used to play the moved row's exit on a ghost and its enter on the new row.
- **The first-appearance moment is named `Initial`.** Reason: it reads as first appearance (Framer Motion's term). Rejected: `Load`, since Avalonia's `Loaded` also fires for items added later; `Appear`.
- **`Arrive` is a real attached property and the fallback for both arrivals.** Initial plays `Initial ?? Arrive`, Enter plays `Enter ?? Arrive`; Realize, Recycle and Exit are unaffected. Reason: only a real property works from style setters and bindings, and setting Initial and Enter to one builder was duplication that pushed the sample back to `OnAttached`, which wrongly plays on recycling.
- **A Presto page transition counts as navigation.** Content under a host whose `PageTransition` is a `PrestoPageTransition` arrives as Initial and departs as Unload, so rows keep their Initial stagger under the transition. Reason: a page host nulls the old page's content while it is still visible at opacity 0, and its `Exit` ghosted and popped back into view.
- **`Presence.Mode` is `Sync` or `Wait`; there is no `popLayout`.** `Wait` holds incoming arrivals, and a content host's new root even without a builder, at opacity 0 until the last exit ghost ends; `Sync` is the default. Reason: it matches Motion's `AnimatePresence mode="wait"`, with hold-until-measured as precedent. Rejected: `popLayout`, since a ghost never holds layout, so it would equal `Sync`.
- **An element's own `IsVisible` flip is a lifecycle moment, played on the live element.** A show plays its arrival and a hide its `Exit`. Reason: most apps and Fluent templates show and hide through `IsVisible`, and a fade-out otherwise needed a `Motion` state plus code.
- **`HideMode` defaults to `Hold`.** The exit plays on the live element under a `VisibilityHold` taken in the `IsVisible` notification, keeping its slot; `Ghost` (layout collapses at once, the snapshot taken under a momentary hold) is opt-in and the one exception to "an element leaving invisible is never ghosted". Reason: the spike found the live path workable, and a snapshot of an invisible element draws nothing. Rejected: `Ghost` as the default.
- **Base flips are told from Presto's writes by base value plus priority.** The tracker records `GetBaseValue(IsVisible)`, skips changes at `Animation` priority, acts only when the base differs, and re-reads the base each frame while holding its own exit. Reason: neither test works alone (a hold's release carries the uncovered value's priority, and a hold pushed inside a template-priority flip still reads the old base), and nothing reports a base change under a hold.
- **Ancestor reveal is opt-in `EnterOnShow`.** A shown host replays its effectively visible descendants' `Enter ?? Arrive` as one batch in visual-tree order, laid out; a nested host shown in the same pass joins the outer batch, and hiding plays only the host's `Exit`. Reason: expanding a deep subtree should not replay everything by default, one batch in tree order keeps order staggers working, and joining avoids a double replay.
- **Shown again mid-exit plays the exit back.** Under `Hold` the held exit reverses from where it stands (hidden again, it plays forward); under `Ghost` the ghost is dismissed and `Enter` plays; a held hide starts no `Wait`. Reason: an entrance with a `from` would snap and a from-less one would land on the mid-exit value, while reversing lands on the exit's start.
- **No `AutoAlpha` shorthand; `VisibilityHold` stays.** A GSAP-style `AutoAlpha` was built and removed, and the internal owner-keyed hold backs the held hide. Reason: `IsVisible = false` takes the element out of layout, unlike CSS `visibility: hidden`, so a fade ending hidden jumps; `HideMode.Hold` covers the need. Rejected: `AutoAlpha`.
- **Lifecycle animations run on the UI thread.** A compositor option for arrivals (`AnimateOnCompositor`, as `Connected` anchors use) is deferred, and the XAML entry points have no thread mode yet. Reason: not recorded.
- **Features that manage their own lifetimes pass `WhenDetached.Continue`.** See BRIDGE-HELPERS.
- **A stagger rebuilds when the target count changes.** See CORE-STAGGER.
- **Exits play on a ghost.** See BRIDGE-PRESENCE-EXIT.
- **Hiding split text follows `HideMode`.** See BRIDGE-TEXTREVEAL.

## Non-goals

- A Motion-style `popLayout` mode: a ghost never holds layout, so it would equal `Sync` (`PresenceMode` remarks).
- Reveal mirroring by scroll direction.
- A thread choice for lifecycle animations: they run on the UI thread (see Decisions).

## Open items

- The recorded `Presence.Mode` decision cites `PresenceModeTests.WithoutWaitTheEnterPlaysWhileTheGhostExits`; the test is named `UnderSyncTheEnterPlaysWhileTheGhostExits`.
- `TreeView` nested items resolve to the outer list (Edge cases): a known limitation, unchanged here.
- Changing or clearing one lifecycle builder while others stay set leaves the running arrival playing; only clearing all reverts it. Untested.
- BRIDGE-PRESENCE-003, -004, -013, -019, -021, -032, -049, -050, -054 and -056 have no test.
