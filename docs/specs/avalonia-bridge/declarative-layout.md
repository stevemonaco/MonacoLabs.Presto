---
id: BRIDGE-DECLLAYOUT
title: Declarative layout animation
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/Animate.cs
  - MonacoLabs.Presto.Avalonia/LayoutArm.cs
  - MonacoLabs.Presto.Avalonia/ItemChanges.cs
  - MonacoLabs.Presto.Avalonia/ItemTransfers.cs
  - MonacoLabs.Presto.Avalonia/Compositor/LayoutTransition.cs
types: []
tests:
  - AnimateLayoutAttachedTests
  - ItemLayoutTests
  - ItemTransferTests
  - ScrollAnchorTests
  - SwipeTests
  - PresenceVisibilityTests
  - ReorderTests
  - ReorderContainerTests
upstream: []
depends:
  - BRIDGE-LAYOUT
  - BRIDGE-ATTACH
  - BRIDGE-CONNECTED
  - BRIDGE-CONTENTSIZE
  - BRIDGE-PRESENCE
  - BRIDGE-REVEAL
  - BRIDGE-REORDER
  - BRIDGE-RECIPES
  - CORE-BUILDER
  - CORE-REDUCED
---

# Declarative layout animation

## Purpose

`Animate.Layout` keeps a layout transition (BRIDGE-LAYOUT) armed on a control for as long as it is attached, and `Animate.ItemLayout` does the same for every item container of a list, so layout animation needs no code-behind; `Animate.ItemGroup` lets an item removed from one such list and added to another fly between them. The attachment is the lifetime and the scope: arriving records rather than flies, recycled containers are rebased, scrolling is never a move, and one-shot flights (BRIDGE-CONNECTED) win their move. The `Animate` class is BRIDGE-ATTACH's; `{presto:Reflow}` and `Recipes.Reflow` are BRIDGE-MARKUP's and BRIDGE-RECIPES'. Also specifies `Animate.Layout`, `Animate.LayoutMode`, `Animate.ItemLayout` and `Animate.ItemGroup`.

## Requirements

### Animate.Layout

- **BRIDGE-DECLLAYOUT-001** — When `Animate.Layout` is set on an attached, arranged control, the control shall get a persistent layout transition measured where it is now, so the next layout change eases.
  - Tests: `AnimateLayoutAttachedTests.SettingItOnAnAttachedControlEasesAMarginChange`
- **BRIDGE-DECLLAYOUT-002** — When the control is attached but not yet arranged, its first layout pass shall only record where it is.
  - Tests: `AnimateLayoutAttachedTests.AControlAttachedWithStaleBoundsRecordsItsFirstPass`
- **BRIDGE-DECLLAYOUT-003** — When the control attaches to the visual tree, it shall be armed so that its first layout pass only records, and its arrival is never eased.
  - Tests: `AnimateLayoutAttachedTests.DetachingRevertsAndReattachingDoesNotEaseTheArrival`
- **BRIDGE-DECLLAYOUT-004** — When the control detaches, its transition shall be reverted, writing identity.
  - Tests: `AnimateLayoutAttachedTests.DetachingRevertsAndReattachingDoesNotEaseTheArrival`
- **BRIDGE-DECLLAYOUT-005** — Changing the builder shall revert the live transition, putting the control in its slot, and arm a new one that eases the next move with the new motion.
  - Tests: `AnimateLayoutAttachedTests.ChangingTheBuilderMidMoveRevertsAndTheNextMoveUsesTheNewOne`
- **BRIDGE-DECLLAYOUT-006** — Changing `Animate.LayoutMode` shall re-arm with the new mode.
  - Tests: `AnimateLayoutAttachedTests.SwitchingToPositionAndSizeMakesAResizeScale`
- **BRIDGE-DECLLAYOUT-007** — Setting `Animate.Layout` to null shall revert the transition and stop listening; setting the same builder back shall leave exactly one transition.
  - Tests: `AnimateLayoutAttachedTests.ClearingAndRestoringTheSameBuilderMidFlightLeavesOneTransitionThatDetachReverts`
- **BRIDGE-DECLLAYOUT-008** — A builder the layout transition cannot honor (BRIDGE-LAYOUT-040) shall throw `NotSupportedException` when it is set, whether or not the control is attached.
  - Tests: `AnimateLayoutAttachedTests.ABuilderALayoutTransitionCannotHonorThrowsWhenApplied`
- **BRIDGE-DECLLAYOUT-009** — The builder shall be snapshotted at each arm, so one value shared by a style setter gives each element its own transition.
  - Tests: `AnimateLayoutAttachedTests.AStyleSetterSharesOneReflowAndEachElementGetsItsOwnTransition`
- **BRIDGE-DECLLAYOUT-010** — Arming shall never register with an ambient scope.
  - Tests: `AnimateLayoutAttachedTests.ArmingNeverRegistersWithTheAmbientScope`
- **BRIDGE-DECLLAYOUT-011** — In design mode, setting `Animate.Layout`, `Animate.LayoutMode`, `Animate.ItemLayout` or `Animate.ItemGroup` shall do nothing.
  - Tests: `AnimateLayoutAttachedTests.DoesNothingInDesignMode`

### Animate.ItemLayout

- **BRIDGE-DECLLAYOUT-012** — Setting `Animate.ItemLayout` on a list shall arm every realized item container with the list's `Animate.LayoutMode`, so an insert eases the containers it moves while the new container does not fly.
  - Tests: `ItemLayoutTests.InsertingAnItemEasesTheContainersBelowAndTheNewOneDoesNotFly`
- **BRIDGE-DECLLAYOUT-013** — When the list prepares a container, it shall be armed as if attaching, so a recycled container lands at its new index in one frame.
  - Tests: `ItemLayoutTests.ARecycledContainerLandsAtItsNewIndexInOneFrame`
- **BRIDGE-DECLLAYOUT-014** — When the list clears a container, its transition shall be reverted and its arm released.
  - Tests: `ItemLayoutTests.AClearedContainerIsRevertedAndReleased`
- **BRIDGE-DECLLAYOUT-015** — Setting `Animate.ItemLayout` to null shall revert every realized container and stop listening to the list.
  - Tests: `ItemLayoutTests.ClearingItRevertsEveryArmedContainer`
- **BRIDGE-DECLLAYOUT-016** — An `Animate.ItemLayout` builder shall be validated when it is set, before any container is realized.
  - Tests: `ItemLayoutTests.ABuilderALayoutTransitionCannotHonorThrowsWhenSetOnAnEmptyList`
- **BRIDGE-DECLLAYOUT-017** — Changing `Animate.LayoutMode` on the list shall re-arm every realized container with the new mode.
  - Tests: `ItemLayoutTests.ChangingTheListsLayoutModeRearmsEveryContainer`

### Lists and scrolling

- **BRIDGE-DECLLAYOUT-018** — For a container armed through `ItemLayout`, or a control with `Animate.Layout` that is an item container, a layout pass in which the list's `ScrollViewer` offset changed with no collection change pending shall cancel any move and only record.
  - Tests: `ItemLayoutTests.ScrollingAVirtualizedListEasesNothing`, `ItemLayoutTests.ScrollingEasesNothingWhenTheContainersSetTheLayoutPropertyThemselves`
- **BRIDGE-DECLLAYOUT-019** — A layout pass in which the list's scroll anchor compensates for a change above the first visible row shall likewise ease no container.
  - Tests: `ScrollAnchorTests.WithItemLayoutNoContainerEasesOnTheCompensatingPass`
- **BRIDGE-DECLLAYOUT-020** — When a row is removed, the rows below shall ease into the gap while the removed row's exit plays.
  - Tests: `ItemLayoutTests.RemovingARowEasesTheRowsBelowWhileItsExitPlays`, `SwipeTests.ADismissPlaysTheExitAndRemovesTheRowWhileItemLayoutClosesTheGap`, `PresenceVisibilityTests.ItemLayoutClosesTheSlotOnceTheHeldExitEnds`

### Precedence

- **BRIDGE-DECLLAYOUT-021** — When a one-shot flight (a `Connected` flight or a once `AnimateSharedElement`) takes the control over, the flight shall win its move, and once its `Then` completes the property shall arm again, unless it was disarmed or re-armed meanwhile.
  - Tests: `AnimateLayoutAttachedTests.AConnectedFlightWinsItsMoveAndTheLayoutPropertyResumesAfterIt`
- **BRIDGE-DECLLAYOUT-022** — When the property arms while a one-shot flight on the control is still in flight, it shall wait for that flight instead of replacing it.
  - Tests: `AnimateLayoutAttachedTests.SettingItWhileAFlightIsInTheAirWaitsForTheFlight`
- **BRIDGE-DECLLAYOUT-023** — When code arms a persistent `AnimateLayout` on the control, the code transition shall win, and the property shall not arm again until the next attach or value change.
  - Tests: `AnimateLayoutAttachedTests.APersistentCodeTransitionWinsUntilTheValueChanges`
- **BRIDGE-DECLLAYOUT-024** — While `Reorder` drags a container, its `ItemLayout` arm shall stay disarmed whatever attaches or changes, and on release it shall arm again as if attaching, while the neighbors keep easing.
  - Tests: `ReorderContainerTests.ADraggablesTranslationAddsToTheFlipThatIsEasingTheSameContainer`, `ReorderTests.NeighborsEaseWithItemLayoutAndSnapWithout`
- **BRIDGE-DECLLAYOUT-028** — When an item container would be armed both by its own `Animate.Layout` and by its list's `Animate.ItemLayout`, the second to arm shall throw `InvalidOperationException`.
  - Tests: `ItemLayoutTests.ArmingAContainerThroughBothItsOwnLayoutAndItsListsItemLayoutThrows`

### Transfers

- **BRIDGE-DECLLAYOUT-029** — When an item is removed from a list whose `Animate.ItemGroup` is set and, before the layout pass after the removal finishes, added to a list with the same group under the same top level, both with `Animate.ItemLayout`, the arriving container shall fly from where the leaving container was last drawn (its arm's recorded slot moved by its live transition) into its slot, as a one-shot shared-element transition with the arriving list's `ItemLayout` builder and `LayoutMode`.
  - Tests: `ItemTransferTests.ARowMovedToAnotherListFliesWithoutExitOrEntranceAndItsArmResumesAfterLanding`
- **BRIDGE-DECLLAYOUT-030** — A transfer's flight shall win its move, and the container's arm shall resume as BRIDGE-DECLLAYOUT-021 says.
  - Tests: `ItemTransferTests.ARowMovedToAnotherListFliesWithoutExitOrEntranceAndItsArmResumesAfterLanding`
- **BRIDGE-DECLLAYOUT-031** — An item added before its removal, a removal and an add under different top levels or groups, a list without `ItemLayout`, or an `ObservableCollection.Move` shall not transfer.
  - Tests: `ItemTransferTests.AnAddBeforeItsRemovalDoesNotFlyAndExitsAndEnters`, `ItemTransferTests.ListsWithoutASharedGroupDoNotFlyAndExitAndEnter`
- **BRIDGE-DECLLAYOUT-032** — When the arriving container is not effectively visible after its first layout pass, there shall be no transfer: the leaving row exits and the arriving row arrives as without a group.
  - Tests: `ItemTransferTests.ACollapsedDestinationDeclinesTheTransfer`
- **BRIDGE-DECLLAYOUT-033** — Removing and re-adding an item in the same grouped list within one layout pass shall be a transfer.
  - Tests: `ItemTransferTests.RemovingAndReinsertingAnItemInOneGroupedListFliesWithoutExitOrEntrance`

### Reduced motion

- **BRIDGE-DECLLAYOUT-025** — The builder's reduced form shall be read at each arm, so under `ReducedMotion` a `{presto:Reflow}` move lands at once.
  - Tests: `AnimateLayoutAttachedTests.AReducedMoveIsInstant`, `ItemTransferTests.UnderReducedMotionTheFlightLandsAtOnce`
- **BRIDGE-DECLLAYOUT-026** — When `ReducedMotion` changes, an armed control shall re-arm at its next layout pass.
  - Tests: `AnimateLayoutAttachedTests.FlippingReducedMotionRearmsAtTheNextLayoutPass`

### Defaults

- **BRIDGE-DECLLAYOUT-027** — When `Animate.LayoutMode` is not set, the control (or list) shall arm with `LayoutAnimateMode.Position`, so a resize lands in one frame.
  - Tests: `AnimateLayoutAttachedTests.SwitchingToPositionAndSizeMakesAResizeScale`

## Invariants

- While attached with a non-null builder and not suspended, a control has exactly one arm and at most one transition from it.
- A control's arrival (attach, realization, recycling) is never a layout move; its entrance belongs to `Presence`.

## Edge cases

- An item moved by `ObservableCollection.Move` that the list rebuilds a container for records in its new container and does not ease.
- A host with `Animate.ContentSize` eases only its position here, and controls beside a resizing host land in one frame (BRIDGE-CONTENTSIZE).
- Layout and a `Presence` entrance on the same element compete for `Translation`/`Scale`; the later owner wins and the other freezes. The documented pattern is layout on the container, `Presence` on the content.

## Threading and lifetime

- UI thread only. Each arm subscribes to its control's attach, detach and `LayoutUpdated`; releasing the property unsubscribes. A list's arms subscribe to `ContainerPrepared` and `ContainerClearing`.
- The re-arm after a flight runs when the flight's `Then()` resolves, which waits for its first layout pass (BRIDGE-LAYOUT-016).
- A grouped list's removals are kept, with the leaving container's drawn rect captured before the panel removes it, until the removing list's next layout pass has finished; a list's arms also subscribe to its `Items` changes.

## Reduced motion

- BRIDGE-DECLLAYOUT-025 and -026: arms read the flag at arm time and on each layout pass.

## Allocation

- Not on the tick path. A layout pass with no move and no flag change allocates nothing; each arm allocates a transition.

## Provenance

- No upstream counterpart: this is Framer Motion's `layout` prop over BRIDGE-LAYOUT's FLIP.

## Decisions

- **The property lives on `Animate`, named `Layout`.** The declarative form of `AnimateLayout` is `Animate.Layout`, on the class that already holds attachment-scoped triggers. Reason: it reads as the method it wraps and needs no new class. Rejected: a `Layout` class (shadows the `Avalonia.Layout` namespace inside any `namespace Avalonia.*` block); `Motion` (means state motion); `Presence` (means arrival and departure, and layout animation is neither).
- **The value is a builder validated as layout motion, where it is set; the mode is a separate property.** `Animate.Layout` and `Animate.ItemLayout` take an `AnimationBuilder?` read as layout motion, so only timing, ease and per-axis eases are allowed and anything else throws `NotSupportedException`, even while detached or before any container is realized; `Animate.LayoutMode` (default `Position`) is its own property. Reason: consistent with every other lifecycle property and fails at the point of the mistake; a separate mode lets a style set it alone and a `ToggleButton` bind it.
- **`{presto:Reflow}` is the shorthand, and its reduced form is instant.** `{presto:Reflow}` wraps `Recipes.Reflow(duration, delay, ease)` with the medium duration and standard ease tokens, has no exit form, and reduces to a zero-duration, zero-delay move. Reason: it makes `Animate.Layout="{presto:Reflow}"` the whole ceremony, and a reflow is a spatial change that reduced motion should land rather than shorten.
- **The attachment is the lifetime and the scope.** Attaching arms, detaching reverts to identity, changing the builder or mode reverts and re-arms, null stops listening, design mode does nothing, and arming never registers with an ambient scope. Reason: follows `Animate.OnAttached`'s trigger model.
- **Arming on attach records, never eases.** The first laid-out pass after arming on attach only records, whatever the bounds said at attach. Reason: the code API's measure-at-construction suits "arm, then change layout in the same pass" but not a container arriving with stale bounds; a control appearing is never a layout move, its entrance belongs to `Presence`.
- **Lists arm through `Animate.ItemLayout`, and recycled containers land rather than fly.** `ItemLayout` arms every item container with the list's mode; clearing a container releases its arm and preparing it arms again as if attaching, and index changes are ignored. Reason: a virtualizing panel reuses containers without detaching them, so an armed container would otherwise measure its old slot as First; an insert or removal above a row is exactly the move that should ease; it replaces sample `ContainerPrepared` handlers and `ItemContainerTheme` workarounds. Earlier a `Rebase()` on clearing was planned; the release-and-re-arm form replaced it.
- **Scrolling a list never eases.** A container armed through `ItemLayout`, or `Animate.Layout` on an item container, cancels to identity and records on a pass where its `ScrollViewer` offset changed with no collection change pending. Reason: offsets are already taken out, but a virtualizing panel also repositions realized rows as estimated sizes are corrected during a scroll.
- **A one-shot flight or code transition wins its move.** A keyed `Connected` or shared-element flight wins and the arm re-arms record-first after the flight's `Then()` if still attached; a persistent code `AnimateLayout` wins until the next attach or value change, with no re-arm. Reason: preserves the one-live-transition-per-control rule.
- **One arm per container.** `Animate.Layout` on an item container whose list sets `Animate.ItemLayout` throws `InvalidOperationException` on whichever arms second. Reason: two arms on one control replace each other's transition (BRIDGE-LAYOUT-004), so the one armed last silently owned the moves and the other never re-armed; one owner at a time, as a `CompositorScene` has one canvas.
- **Layout goes on the container, `Presence` on the content.** When layout and a transform entrance or exit share an element, the later owner takes the compositor `Translation`/`Scale` track and the other freezes, with a Debug warning; opacity-only or blur entrances may share. Reason: entrance and exit recipes run on the same tracks a layout transition inverts onto. Rejected: detecting the conflict and throwing, because sequential use (an enter that finishes before the first reflow) is legitimate.
- **Style setters can share one builder.** All three properties are plain attached properties, and one shared builder is safe because it is snapshotted per arm. Reason: not recorded beyond the snapshot guarantee.
- **Reduced motion is read at arm time.** `WhenReduced` applies when the builder is read at arm; an arm re-arms at its next layout pass when the flag differs from the value it armed with, and reduced motion makes a layout move instant. Reason: arms read the flag at the next layout pass, since a layout move is finite and Following a change in BRIDGE-REDUCED does not replay it.
- **Layout-transition builders carry timing and eases only; per-axis eases make arcs.** See BRIDGE-LAYOUT.
- **The dragged container is excluded from FLIP.** See BRIDGE-REORDER.
- **Transfers match items by identity, inside an opt-in group.** The item object is already the same in both lists. Reason: a group bounds which lists may claim a row, and an app that wants a move between lists to exit and enter (an archive, say) leaves `ItemGroup` unset. Rejected: `Connected.Key` per row, which needs a string key and a global registry and claims for three seconds, so a row filtered out and back would fly, and would fly containers that `Reorder` rebuilds; making every pair of `ItemLayout` lists transfer, which turns today's exit-and-enter into flights without asking.
- **A transfer needs `ItemLayout` on both lists.** Reason: the arm already knows where each container is drawn, which is where the leaver flies from, and the arriving list's `ItemLayout` builder and `LayoutMode` are the natural motion for the flight; a transfer is the cross-list form of an `ItemLayout` move. Rejected: separate transfer motion and mode properties, which no case needs.
- **The removal must come first, within one layout pass.** The arrival is classified at attach (BRIDGE-PRESENCE-005), and a non-virtualizing panel attaches the new container during `Insert`, so if the removal came after, the entrance would already have started. Accepted: an add before its removal is an exit and an enter.
- **The transfer is confirmed at the arriving container's first layout pass.** If the arriving container is not effectively visible then (a list collapsed in an `Expander`), there is no transfer. Reason: a row must never vanish without motion.
- **The flight is clipped by its ancestors.** A row flying from one scroller into another shows only inside the destination's viewport, sliding in from its edge. Accepted: lifting a flight above clipping ancestors is already a Connected non-goal and stays one here.

## Non-goals

- Per-item slot memory: a container rebuilt by `ObservableCollection.Move` flies nowhere.
- Transfers between lists without a shared group.
- `ItemLayout` for hosts without `ContainerPrepared`/`ContainerClearing` events (`ItemsRepeater`-style).
- A separate `Layout` class (it would shadow `Avalonia.Layout`).

## Open items

- Untested: in 031, a list without `ItemLayout`, different top levels and an `ObservableCollection.Move`.
