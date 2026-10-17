---
id: BRIDGE-POPOVER
title: Popover open and close motion
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/Popover.cs
  - MonacoLabs.Presto.Avalonia/PopoverRun.cs
  - MonacoLabs.Presto.Avalonia/Targets/AnimatedValue.cs
types:
  - Popover
tests:
  - PopoverFlyoutTests
  - PopoverPopupTests
  - PopoverRecipeTests
  - PopupSpikeTests
  - RecipeMarkupTests
  - PopupsPageTests
upstream: []
depends:
  - BRIDGE-INIT
  - BRIDGE-TARGETS
  - BRIDGE-PRESENCE-EXIT
  - BRIDGE-RECIPES
  - BRIDGE-MARKUP
  - CORE-BUILDER
  - CORE-ANIMATION
  - CORE-REDUCED
  - CORE-DIAGNOSTICS
---

# Popover open and close motion

## Purpose

`Popover` gives popups Presto motion: an attached `Open` builder plays on the popup's presenter as it opens, and a `Close` builder plays on it before it closes. It works on flyouts (`PopupFlyoutBase`), `ContextMenu`, `Popup` and `ToolTip`, each closing by the mechanism its Avalonia type allows: flyouts and context menus by canceling the first `Closing` and replaying the close, a `Popup` by an `IsOpen` hold, and a tooltip (or a popup closed past its hold) at once with `Close` played on a ghost (BRIDGE-PRESENCE-EXIT). The placement-aware `Recipes.PopoverIn`/`PopoverOut` and `{presto:Popover}` are BRIDGE-RECIPES and BRIDGE-MARKUP.

## Requirements

### Owners and presenters

- **BRIDGE-POPOVER-001** — `Popover` shall play on the presenter of its owner: a flyout's `Popup.Child` (`FlyoutPresenter`, `MenuFlyoutPresenter`), the `ContextMenu` itself, a `Popup`'s child, the `ToolTip` itself.
  - Tests: `PopoverFlyoutTests.OpenRendersItsStartValuesOnTheFirstFrame`, `PopoverPopupTests.PopupOpenRendersItsStartValuesOnTheFirstFrame`, `PopoverPopupTests.AToolTipOpensFromItsStartValuesAndClosesAtOnceWhileCloseFadesItsGhost`
- **BRIDGE-POPOVER-002** — Each builder shall be read from the owner first and the presenter second, so a style reaches a flyout through its presenter (narrowed by a class from `FlyoutPresenterClasses`) and a value on the flyout wins.
  - Tests: `PopoverFlyoutTests.AStyleNarrowedByAPresenterClassSetsTheBuildersAndAValueOnTheFlyoutWins`, `PopupSpikeTests.FlyoutBaseIsNotStyleableSoStylesReachFlyoutsThroughTheirPresenters`
- **BRIDGE-POPOVER-003** — A `Popup` shall play when the `Popup` itself carries `Open` or `Close` (set directly or through a `/template/` style such as `ComboBox /template/ Popup#PART_Popup`) or, by the presenter fallback (BRIDGE-POPOVER-002), its child does; the `Popup` hosting a flyout's or context menu's presenter (a `FlyoutPresenter`, `MenuFlyoutPresenter` or `ContextMenu` child) shall leave the presenter's builders to that owner.
  - Tests: `PopoverPopupTests.TheHoldKeepsThePopupDrawnUntilCloseEndsThenClosesIt`, `PopoverPopupTests.BuildersOnAPlainPopupsChildOpenAndHoldItAsOnThePopup`
- **BRIDGE-POPOVER-004** — When a popup opens and neither builder is found on its owner or presenter, `Popover` shall not handle it: it opens and closes as Avalonia does and its presenter's transitions are left alone.
  - Tests: `PopoverFlyoutTests.OnlyAnOwnerWithPopoverIsHandledAndHasItsPresentersOpacityTransitionLifted`, `PopoverPopupTests.WithoutPopoverAPopupClosesAtOnce`
- **BRIDGE-POPOVER-005** (inherited) — A `Popup` whose child is not in the visual tree when `IsOpen` turns true shall get no run.
  - Tests: untested
- **BRIDGE-POPOVER-006** (inherited) — While `Design.IsDesignMode` is true, no run shall start.
  - Tests: untested

### Open

- **BRIDGE-POPOVER-007** — When a handled popup opens, `Open` shall play on the presenter with its start values applied in the same turn, so the presenter never draws at rest first.
  - Tests: `PopoverFlyoutTests.OpenRendersItsStartValuesOnTheFirstFrame`, `PopoverPopupTests.PopupOpenRendersItsStartValuesOnTheFirstFrame`, `PopupsPageTests.AFlyoutOpensFromItsStartValuesAndStaysUntilItsCloseHasPlayed`
- **BRIDGE-POPOVER-008** — While a handled popup is open, its presenter's `Opacity` transitions shall be lifted (`Transitions` set at `Animation` priority to the list without them), and the lift shall be disposed when the popup closes.
  - Tests: `PopoverFlyoutTests.OnlyAnOwnerWithPopoverIsHandledAndHasItsPresentersOpacityTransitionLifted`, `PopoverPopupTests.AToolTipOpensFromItsStartValuesAndClosesAtOnceWhileCloseFadesItsGhost`
- **BRIDGE-POPOVER-009** (inherited) — Popover animations shall use `WhenDetached.Continue` and carry the diagnostics origin `"Popover"`.
  - Tests: untested

### Flyouts and context menus

- **BRIDGE-POPOVER-010** — When a flyout or context menu with `Close` raises its first `Closing` (light dismiss, Escape, `Hide()`, `ContextMenu.Close()`), `Popover` shall cancel it, play `Close` on the live presenter, and when `Close` ends close the popup through `Popup.Close()`, letting that `Closing` through.
  - Tests: `PopoverFlyoutTests.ClosePlaysOnTheLivePresenterAndThenCloses`, `PopupsPageTests.AFlyoutOpensFromItsStartValuesAndStaysUntilItsCloseHasPlayed`
- **BRIDGE-POPOVER-011** — When a close is asked for again while `Close` plays, `Popover` shall cancel it again and let `Close` keep playing.
  - Tests: `PopoverFlyoutTests.ClosePlaysOnTheLivePresenterAndThenCloses`
- **BRIDGE-POPOVER-012** (inherited) — A `Closing` another handler has already canceled shall be left alone.
  - Tests: untested
- **BRIDGE-POPOVER-013** — When the placement target is no longer under a top level, the popup shall close at once whatever `Closing` says, and the presenter's values shall revert.
  - Tests: `PopoverFlyoutTests.AnAnchorDetachingClosesAtOnceAndRevertsTheClose`, `PopupSpikeTests.AnOwnerDetachingClosesWhateverClosingSays`
- **BRIDGE-POPOVER-014** — When a context menu is requested again (`Opening`) while `Close` plays, `Popover` shall play `Close` back from where it stands (`Reverse`) and write `Popup.IsOpen` true again under the canceled close, so a later close plays again.
  - Tests: `PopoverFlyoutTests.AContextMenuRequestedAgainMidClosePlaysBackFromWhereItStands`
- **BRIDGE-POPOVER-015** — When `ShowAt` is called on a flyout's current target while `Close` plays, `Close` shall finish and the flyout close; on another target, the run shall end at once and `Open` replay from its start values.
  - Tests: `PopoverFlyoutTests.AShowAtMidCloseOnTheSameTargetLetsCloseFinishAndOnAnotherReplaysOpen`
- **BRIDGE-POPOVER-016** (inherited) — When a popup's run ends (it closed), `Popover` shall revert the presenter's `Open` and `Close` animations, leaving it at its base values.
  - Tests: `PopoverFlyoutTests.ClosePlaysOnTheLivePresenterAndThenCloses`

### Popup

- **BRIDGE-POPOVER-017** — When a `Popup` with `Close` opens, `Popover` shall hold `IsOpen` true at `Animation` priority until its close ends.
  - Tests: `PopoverPopupTests.TheHoldKeepsThePopupDrawnUntilCloseEndsThenClosesIt`
- **BRIDGE-POPOVER-018** — While held, the base `IsOpen` shall be polled every 30 ms by a dispatcher timer, not an engine timer, so an open popup lets the engine idle.
  - Tests: `PopoverPopupTests.AnOpenHeldPopupLetsTheEngineIdleAndAWriteUnderTheHoldStillStartsClose`
- **BRIDGE-POPOVER-019** — When the base `IsOpen` turns false (a local write or a binding, such as the `ComboBox`'s `IsDropDownOpen`), `Close` shall play on the child and its end shall release the hold, closing the popup.
  - Tests: `PopoverPopupTests.TheHoldKeepsThePopupDrawnUntilCloseEndsThenClosesIt`, `PopoverPopupTests.ABoundIsOpenFollowsEveryCloseAndReopensFromTheViewModel`
- **BRIDGE-POPOVER-020** — When the base `IsOpen` turns true again while `Close` plays, `Close` shall play back from where it stands and the popup stay open.
  - Tests: `PopoverPopupTests.ReopenedMidClosePlaysBackFromWhereItStands`
- **BRIDGE-POPOVER-021** — A `ClearValue(IsOpen)` under the hold shall be lost (the base still reads true), so a held popup is closed by writing false.
  - Tests: `PopupSpikeTests.AClearValueUnderAStandingHoldIsLostSoReleasingLeavesThePopupOpen`
- **BRIDGE-POPOVER-022** — When light dismiss closes a held popup past `IsOpen`, it shall close at once and `Close` shall play on a ghost of the child at the position recorded while open; the base `IsOpen` shall then read false, so a two-way binding follows.
  - Tests: `PopoverPopupTests.LightDismissClosesAtOnceAndPlaysCloseOnAGhost`, `PopoverPopupTests.ABoundIsOpenFollowsEveryCloseAndReopensFromTheViewModel`
- **BRIDGE-POPOVER-023** — When a held popup detaches mid-close, the hold shall be released and the popup close at once, with no ghost.
  - Tests: `PopoverPopupTests.APopupDetachingMidCloseReleasesTheHoldAtOnce`

### ToolTip

- **BRIDGE-POPOVER-024** — When a `ToolTip` with `Close` closes, it shall close at once, and `Close` shall play on a ghost snapshotted with `Opacity` held at 1, placed at the window position recorded through the screen while it was open, in its anchor window's overlay.
  - Tests: `PopoverPopupTests.AToolTipOpensFromItsStartValuesAndClosesAtOnceWhileCloseFadesItsGhost`
- **BRIDGE-POPOVER-025** — When the popup has left the logical tree (its anchor left), the window has no overlay layer, or no position was recorded, a ghosting close shall close with no ghost.
  - Tests: `PopoverPopupTests.AToolTipWhoseAnchorLeavesClosesWithoutAGhost`

### Reduced motion

- **BRIDGE-POPOVER-026** — When `PrestoDefaults.ReducedMotion` is true as the popup closes, it shall close at once with no `Close` and no ghost.
  - Tests: `PopoverFlyoutTests.UnderReducedMotionCloseIsInstant`
- **BRIDGE-POPOVER-027** — Under reduced motion `Open` shall play the builder's reduced form.
  - Tests: `PopoverFlyoutTests.OpenRendersItsStartValuesOnTheFirstFrame`, `PopoverPopupTests.PopupOpenRendersItsStartValuesOnTheFirstFrame`
- **BRIDGE-POPOVER-028** — When `Close` completes on its first frame, as every one does under `ReduceAll`, the popup shall close at once.
  - Tests: `PopoverFlyoutTests.UnderReduceAllCloseIsInstantAndOpenPlaysOnlyTheReducedFade`, `PopoverPopupTests.UnderReduceAllCloseIsInstantAndOpenPlaysOnlyTheReducedFade`

### Recipes

- **BRIDGE-POPOVER-029** — `Recipes.PopoverIn`/`PopoverOut` set as `Open`/`Close` shall slide the presenter from and back into the edge facing its anchor (BRIDGE-RECIPES), and `{presto:Popover}` shall provide the entrance on `Open` and the exit on `Close` (BRIDGE-MARKUP).
  - Tests: `PopoverRecipeTests.ThePresenterSlidesFromAndBackIntoTheEdgeFacingItsAnchor`, `RecipeMarkupTests.PopoverIsAnEntranceOnOpenAndAnExitOnClose`

### Defaults

- **BRIDGE-POPOVER-030** (inherited) — When `Open` is not set on a handled popup's owner or presenter, the popup shall open with no Popover motion.
  - Tests: untested
- **BRIDGE-POPOVER-031** (inherited) — When `Close` is not set on a handled popup's owner or presenter, the popup shall close at once, as Avalonia closes it.
  - Tests: `PopoverFlyoutTests.OnlyAnOwnerWithPopoverIsHandledAndHasItsPresentersOpacityTransitionLifted`

## Invariants

- An owner has at most one run, from its open to its close; a run never outlives the popup's open.
- After a run ends, the presenter has no Popover animation, no lifted `Transitions` value and the popup no `IsOpen` hold.

## Edge cases

- A flyout cannot be reopened mid-close on the same target: Avalonia's `ShowAt` returns early while open and `Hide()` raises a `Closing`, so both read as a repeated close.
- Only the Fluent `ToolTip` animates its own open with an `Opacity` transition; its fade comes back once the tooltip has closed (`PopupSpikeTests.OnlyTheFluentToolTipAnimatesItsOwnOpenWithAnOpacityTransition`).
- The polling timer is internal and replaceable for tests (`PopoverRun.StartPolling`).

## Threading and lifetime

- UI thread only. The run lives in a private attached property on the owner and is released when the popup closes; it unsubscribes every event it took. The dispatcher poll stops with the run.

## Reduced motion

- Read as the popup closes (BRIDGE-POPOVER-026); `Open` resolves the builder's reduced form; recipes reduce to a `Short` fade.

## Allocation

- Not on the tick path. Each open allocates its run, params and animation; the 30 ms poll reads one base value.

## Provenance

- No upstream counterpart.

## Decisions

- **Attached `Open`/`Close` builders, styled through presenters.** A static `Popover` attaches the builders to flyouts, `ContextMenu`, `Popup` and `ToolTip`; motion plays on the presenter with start values applied in the opening turn, a value on the owner wins over a presenter style, and only the owner plays them. Reason: `FlyoutBase` is not a `StyledElement`, so styles reach flyouts through their presenters; presenters are reused across opens while hosts are not; and `Presence` on popup content fires at a template-dependent moment and never on the presenter.
- **Flyouts and context menus close by cancel-and-replay.** The first `Closing` is canceled, `Close` plays on the live presenter, then `Popup.Close()` lets the second through; a detached placement target closes at once. Reason: after Escape or a canceled `ContextMenu.Close()`, `IsOpen` is already false, so `Hide()` or `Close()` would write nothing.
- **A `Popup` is held open by an `IsOpen` hold polled on a dispatcher timer.** The hold is pushed at `Animation` priority as the popup opens and the base value read every 30 ms, so the popup is closed by writing false. Reason: a hold taken in `IsOpen`'s change notification comes too late (class handlers have already closed the popup), writes under a hold raise nothing, so the base must be polled, and a dispatcher timer lets the engine idle. Rejected: an engine timer for the poll.
- **Popups that cannot be held close at once and play `Close` on a ghost.** A `ToolTip` and a light-dismissed held `Popup` are snapshotted (with `Opacity` held at 1) at their recorded position in the anchor window's overlay. Reason: `ToolTipClosing` cannot cancel, `ToolTipService` clears `IsOpen`, and light dismiss calls the popup's close directly, past any hold; earlier, light dismiss closed with no motion, replaced so it closes like a tooltip.
- **Only a `ContextMenu` plays `Close` back when reopened mid-close.** On a flyout, `ShowAt` on the same target or `Hide()` mid-close counts as a repeated close, and `ShowAt` on another target ends the run and replays `Open`. Reason: `ShowAt` returns early while the flyout is open, and only `ContextMenu` raises `Opening` again.
- **A plain `Popup`'s child is its presenter, builders included.** Builders set only on the child start a run, held open as on the `Popup`; a `FlyoutPresenter`, `MenuFlyoutPresenter` or `ContextMenu` child belongs to its flyout or menu, which plays them. Reason: the presenter fallback promised it, while a run started only from the `Popup`, so builders on a plain popup's child did nothing; the presenter types tell another owner's popup apart without tracking which popups flyouts and menus create.
- **Presenter `Opacity` transitions are lifted while open.** Reason: the Fluent `ToolTip`'s own opacity transition would fight the builder.
- **Reduced popups close at once.** Read at close, with no `Close` and no ghost; `Open` plays the builder's reduced form, and a `Close` that completes on its first frame closes at once. Reason: not recorded beyond the reduced-motion rule (BRIDGE-REDUCED).
- **Shown again mid-exit plays the exit back.** See BRIDGE-PRESENCE.
- **`PopoverIn` predicts the facing edge from the placement.** See BRIDGE-RECIPES.
- **A bindable flyout `IsOpen` is not Presto's job.** Opening and closing a flyout from a view model is an MVVM gap in Avalonia (`FlyoutBase.IsOpen` is read-only and `ShowAttachedFlyout` is a method), not a motion one, and Presto ships no MVVM helpers. Reason: Popover's motion already plays for any opener, since `ShowAt` or `ShowAttachedFlyout` plays `Open` and any `Hide()` plays `Close` (BRIDGE-POPOVER-010), so an app or community `IsOpen` behavior gets the motion unchanged; for a state held while open, `Button.Flyout` sets `:flyout-open`, which `Motion` already takes as a condition. Rejected: a `Popover.IsOpen` attached property, which would own anchor and placement policy that Avalonia leaves to the app.

## Non-goals

- Reopening a flyout mid-close with `Close` played back.
- Holding a `ToolTip` open: Avalonia's tooltip service cannot be held, so it ghosts.
- Predicting `Pointer` placements in the recipes (BRIDGE-RECIPES).
- A bindable `IsOpen` for flyouts (see A bindable flyout `IsOpen` is not Presto's job).

## Open items

- BRIDGE-POPOVER-005, -006, -009 and -012 have no test.
