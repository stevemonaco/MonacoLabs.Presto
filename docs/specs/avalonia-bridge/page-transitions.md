---
id: BRIDGE-PAGES
title: Page transitions (PrestoPageTransition)
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/PrestoPageTransition.cs
  - MonacoLabs.Presto.Avalonia/PageStacking.cs
  - MonacoLabs.Presto.Avalonia/PageAxis.cs
types:
  - PrestoPageTransition
  - PageStacking
  - PageAxis
tests:
  - PageTransitionTests
  - PresencePageTransitionTests
  - WhenDetachedTests
  - DevToolsOriginTests
  - RecipeTests
  - ContentSizePresenceTests
upstream: []
depends:
  - BRIDGE-INIT
  - BRIDGE-THREAD
  - BRIDGE-TRANSFORMS
  - BRIDGE-PRESENCE
  - BRIDGE-RECIPES
  - BRIDGE-MARKUP
  - CORE-BUILDER
  - CORE-ANIMATION
  - CORE-REDUCED
  - CORE-DIAGNOSTICS
---

# Page transitions (PrestoPageTransition)

## Purpose

`PrestoPageTransition` is an Avalonia `IPageTransition` and `IProgressPageTransition` that plays an `Enter` builder on the incoming page host and an `Exit` builder on the outgoing one, so every Avalonia page host (`TransitioningContentControl`, `Carousel`, `NavigationPage`, `TabControl`/`TabbedPage`) uses Presto motion from XAML. It holds builders, not animations, so one instance serves many hosts; it stacks the two hosts, runs on the render thread when the builders allow (BRIDGE-THREAD), hands a canceled transition over to the next one, and seeks for swipe-driven progress. The page recipes and their markup extensions that return it are BRIDGE-RECIPES and BRIDGE-MARKUP; `PageAxis` parameterizes `Recipes.PageSharedAxis`. `Presence` treats a page under it as navigation (BRIDGE-PRESENCE).

## Requirements

### Start

- **BRIDGE-PAGES-001** (inherited) — When `Start` is called with a token already canceled, it shall return a completed task and touch neither host.
  - Tests: untested
- **BRIDGE-PAGES-002** — Going forward, `Start` shall play `Enter` on `to` and `Exit` on `from`.
  - Tests: `PageTransitionTests.ASwapPlaysEnterOnTheNewPresenterAndExitOnTheOld`
- **BRIDGE-PAGES-003** — Going back, `Start` shall play `BackEnter` on `to` and `BackExit` on `from`, and for each that is null the opposite forward builder reversed (`Exit` reversed on `to`, `Enter` reversed on `from`).
  - Tests: `PageTransitionTests.GoingBackPlaysTheForwardPairReversed`, `PageTransitionTests.GoingBackOnTheCompositorPlaysTheForwardPairReversed`, `PageTransitionTests.AnExplicitBackEnterWins`
- **BRIDGE-PAGES-004** — Every `Start` shall build fresh params from the builders (`ToParams()`), so `Recipes.Tokens`, reduced motion and function values are read per transition.
  - Tests: `RecipeTests.ReducedMotionPlaysEachPageRecipesShortCrossfade`
- **BRIDGE-PAGES-005** — When a builder in play is null, its host shall stay still and count as done at once, while still being stacked and claimed.
  - Tests: `PageTransitionTests.TheIncomingHostsFirstFrameIsAtTheEntrancesStartValues`
- **BRIDGE-PAGES-006** — Either side may be null: with `from` null only the incoming host plays, with `to` null only the outgoing host plays and is hidden at the end, and the task completes successfully.
  - Tests: `PageTransitionTests.EitherSideMayBeMissing`
- **BRIDGE-PAGES-007** — `Start` shall set `to.IsVisible = true` and apply the entrance's start values in the same call, so the incoming host's first rendered frame is at its start values, on either thread.
  - Tests: `PageTransitionTests.TheIncomingHostsFirstFrameIsAtTheEntrancesStartValues`, `PageTransitionTests.GoingBackPlaysTheForwardPairReversed`

### Stacking

- **BRIDGE-PAGES-008** — With `IncomingAboveForward` (the default) `Start` shall write `ZIndex` 1 on the incoming host and 0 on the outgoing one going forward, and the reverse going back, at `Animation` priority for the transition's life.
  - Tests: `PageTransitionTests.IncomingAboveForwardStacksByDirection`
- **BRIDGE-PAGES-009** — `IncomingAbove` shall put the incoming host above in both directions and `OutgoingAbove` the outgoing host.
  - Tests: untested
- **BRIDGE-PAGES-010** — `Host` shall write no `ZIndex`, leaving the order to the host.
  - Tests: `PageTransitionTests.HostStackingWritesNothing`

### Thread

- **BRIDGE-PAGES-011** — With `Thread = PreferCompositor` (the default) `Start` shall play both hosts on the render thread when every builder in play has a composition counterpart for every setting and property, and both on the UI thread otherwise.
  - Tests: `PageTransitionTests.AutoRunsTransformAndOpacityOnTheCompositor`, `PageTransitionTests.AutoFallsBackToTheUIThreadForBlur`, `RecipeTests.EveryPropertyOfAPageRecipeHasACompositionCounterpart`
- **BRIDGE-PAGES-012** — With `Thread = UIThread` both hosts shall play on the UI thread.
  - Tests: `PageTransitionTests.ASwapPlaysEnterOnTheNewPresenterAndExitOnTheOld`
- **BRIDGE-PAGES-013** — With `Thread = Compositor` and a builder in play that the compositor cannot run, `Start` shall throw `NotSupportedException`.
  - Tests: `PageTransitionTests.ForcingTheCompositorWithBlurThrows`
- **BRIDGE-PAGES-014** — A compositor scale shall honor the host's `RenderTransformOrigin`.
  - Tests: `PageTransitionTests.ASharedAxisScaleOnTheCompositorHonorsTheHostsOrigin`

### Completion

- **BRIDGE-PAGES-015** — When both hosts' animations complete without cancellation, the transition shall set the outgoing host's `IsVisible` to false first, if it is still this transition's, and then revert both animations and the `ZIndex` writes, leaving nothing at `Animation` priority.
  - Tests: `PageTransitionTests.CompletionHidesTheOldPresenterBeforeRevertingAndLeavesNothingAnimated`
- **BRIDGE-PAGES-016** — The task `Start` returns shall complete normally, never faulted or canceled, whether the transition ran to its end or was canceled.
  - Tests: `PageTransitionTests.ACanceledHostIsTakenOverFromWhereItStands`, `PageTransitionTests.EitherSideMayBeMissing`
- **BRIDGE-PAGES-017** — UI-thread page animations shall use `WhenDetached.Continue`, so a transition finishes when its pages detach.
  - Tests: `WhenDetachedTests.APageTransitionFinishesWhenItsPagesDetach`
- **BRIDGE-PAGES-018** — After a `NavigationPage` push or pop, the presenters shall keep the host's own `ZIndex` and reset values, with nothing left animating.
  - Tests: `PageTransitionTests.ANavigationPagePushAndPopRunTheTransitionAndKeepItsOwnValues`
- **BRIDGE-PAGES-019** — A `Carousel` selection change shall run the transition end to end, leaving the old container hidden and both at rest.
  - Tests: `PageTransitionTests.ACarouselSelectionChangeRunsTheTransitionEndToEnd`

### Cancellation and take-over

- **BRIDGE-PAGES-020** — When the token is canceled, both hosts' animations shall stop where they stand (a compositor animation paused, a UI-thread one canceled) and the task shall complete without hiding or reverting.
  - Tests: `PageTransitionTests.ACanceledHostIsTakenOverFromWhereItStands`
- **BRIDGE-PAGES-021** — When a later `Start` is given a host a canceled transition left, it shall take the host over from the values it stands at.
  - Tests: `PageTransitionTests.ACanceledHostIsTakenOverFromWhereItStands`, `PageTransitionTests.ASecondSwapMidTransitionTakesTheEnteringPresenterOverFromWhereItStands`
- **BRIDGE-PAGES-022** — When a compositor run takes over a UI-thread run, the compositor shall seed from where the host stands and the UI-thread values shall be reverted in the same commit, so the offset is never drawn twice.
  - Tests: `PageTransitionTests.ACompositorRunTakesOverAUIThreadRunWithoutDrawingItsOffsetTwice`, `PageTransitionTests.ACompositorTakeOverStartsWhereTheCanceledSlideStood`
- **BRIDGE-PAGES-023** — When a UI-thread run takes over a compositor run, the host shall first be set to the compositor mirror's transform and opacity values, and the compositor run then reverted.
  - Tests: `PageTransitionTests.AUIThreadRunTakesOverACompositorRunFromWhereItsMirrorStood`
- **BRIDGE-PAGES-024** — When no transition claims a canceled host by the next animation frame, its values shall be reverted.
  - Tests: `PageTransitionTests.AnUnclaimedHostRevertsAfterOneFrame`
- **BRIDGE-PAGES-033** — When `Start` is given a host that another run still drives, canceled or not, it shall stop that run without reverting it and start from the values the host stands at; the stopped run's values shall be reverted when the new run reverts.
  - Tests: `PageTransitionTests.ASecondSwapMidTransitionTakesTheEnteringPresenterOverFromWhereItStands`, `PageTransitionTests.ACanceledHostIsTakenOverFromWhereItStands`

### Progress (Update and Reset)

- **BRIDGE-PAGES-025** — `Update` shall create paused UI-thread animations on first use for a (from, to, forward) triple, whatever `Thread` says, and seek both to `progress × Duration`.
  - Tests: `PageTransitionTests.UpdateSeeksBothSidesOnTheUIThread`
- **BRIDGE-PAGES-026** — When `Update` is called with another triple, or after a host of its run was taken over or `Reset`, it shall revert the previous run before building a new one; a host a running `Start` holds is reverted rather than taken over.
  - Tests: untested
- **BRIDGE-PAGES-027** — When `visibleItems` is non-empty, `Update` shall do nothing.
  - Tests: `PageTransitionTests.UpdateWithVisibleItemsChangesNothing`
- **BRIDGE-PAGES-028** — `Reset(visual)` shall revert whatever this transition applied to that visual (animations and `ZIndex`).
  - Tests: `PageTransitionTests.ResetRevertsWhatUpdateApplied`
- **BRIDGE-PAGES-029** — `Update` shall ignore `pageLength`.
  - Tests: untested

### Diagnostics and Presence

- **BRIDGE-PAGES-030** — Every animation a transition creates shall carry the diagnostics origin `"PageTransition"`.
  - Tests: `DevToolsOriginTests.APageTransitionCarriesPageTransition`
- **BRIDGE-PAGES-031** — Content under a host whose `PageTransition` is a `PrestoPageTransition` shall classify its arrival as `Initial` and its departure as unload (BRIDGE-PRESENCE-009, BRIDGE-PRESENCE-EXIT-004).
  - Tests: `PresencePageTransitionTests.APageRootsEnterDoesNotPlayUnderAPrestoTransition`, `PresencePageTransitionTests.APageRootsExitLeavesNoGhostUnderAPrestoTransition`, `ContentSizePresenceTests.AHostInsideATransitioningContentControlResizesAcrossItsSwap`

### PageAxis

- **BRIDGE-PAGES-032** — `Recipes.PageSharedAxis(PageAxis.X)` shall bring the incoming page from the right (`X` from +distance) and send the outgoing one left; `Y` shall do the same from below; `Z` shall grow the incoming page from `Tokens.Scale` and the outgoing one to `2 − Tokens.Scale`, ignoring the distance.
  - Tests: `RecipeTests.EveryPropertyOfAPageRecipeHasACompositionCounterpart`, `PageTransitionTests.ASharedAxisScaleOnTheCompositorHonorsTheHostsOrigin`

## Invariants

- Each host is driven by at most one run at a time; a new run on a host stops the previous one first.
- After a completed transition neither host has an animation, a compositor track or a `ZIndex` value at `Animation` priority.
- The transition object holds no per-host state except the one progress run; per-host runs live in a weak table keyed by the host.

## Edge cases

- A `Carousel` lays out and swipes horizontally whatever the builders do: `Carousel.GetTransitionAxis` recognizes only Avalonia's `PageSlide`.
- A swipe that completes leaves the landed page at its seeked end values at `Animation` priority until the next transition or recycle takes it over or resets it (`VirtualizingCarouselPanel` does not reset that page).
- `IsVisible` writes (`to` shown, `from` hidden) are local values, as Avalonia's built-in transitions write them.
- A seek ignores `Delay` staging.

## Threading and lifetime

- `Start`, `Update` and `Reset` run on the UI thread; the compositor path runs on the render thread through BRIDGE-THREAD. Completion continuations post back to the UI context without a thread-pool hop.
- Host runs are kept in a `ConditionalWeakTable`, so a collected host takes its run along.

## Reduced motion

- Builders resolve their `WhenReduced` forms and `ReduceAll` per `Start` (CORE-REDUCED); every page recipe supplies a `Short` crossfade (BRIDGE-RECIPES).

## Allocation

- Not on the tick path. Each `Start` allocates two params and their animations.

## Deviations from anime.js

- No upstream counterpart.

## Decisions

- **One adapter type holds builders.** `PrestoPageTransition` is a single sealed, settable, XAML-constructible class implementing `IPageTransition` and `IProgressPageTransition`; it holds builders rather than animations and builds fresh params on every `Start`, so one instance serves many hosts. Reason: per-transition `ToParams()` reads `Recipes.Tokens`, reduced motion and function values per transition, and Avalonia's built-ins are fixed shapes with no Presto eases, recipes, reduced motion or render-thread playback; the name follows `PrestoEasing`, the bridge's other adapter of an Avalonia interface.
- **It animates the hosts it is given.** `from` and `to` (presenters or containers) are animated directly and their content is never reached into; size-dependent distances are function values reading the common visual parent's `Bounds`, as `PageSlide` does. Reason: a `NavigationPage`'s incoming page has not been measured when `Start` runs.
- **Entrances state start values, exits end values; staging is a delay.** Page `Enter`/`Exit` follow the entrance/exit recipe convention, so `Recipes.FadeUp`/`FadeOut` work unchanged, and a staged pair (Material fade-through) delays `Enter`. Reason: existing recipes are reused. Rejected: a sequential (exit-then-enter) mode.
- **Going back reverses the forward pair by default.** Without `BackEnter`/`BackExit`, the incoming host plays `Exit` reversed and the outgoing host `Enter` reversed, eases included; a pair that must keep its ease shape sets the back builders. Reason: that is exactly the mirrored motion a push/pop or a slide needs.
- **Stacking is part of the transition.** `Stacking` writes both hosts' `ZIndex` at `Animation` priority for the transition's life and reverts it after; `Host` leaves the order alone. Reason: `TransitioningContentControl`'s alternating presenters then stack like `NavigationPage`'s, and `NavigationPage`'s own local `ZIndex` shows again afterwards.
- **Render thread when it can.** `Thread` defaults to `PreferCompositor`: both sides play on the compositor when every builder in play can, on the UI thread otherwise, and forcing the compositor throws; progress-driven `Update` always seeks UI-thread animations. Earlier a `UseCompositor` (`bool?`) property; replaced when the thread became an entry-point argument across the bridge. Reason: a page transition is the moment the UI thread is busiest (template instantiation, the new page's first layout), and a compositor animation seeks by recompiling, too costly per pointer move.
- **Show first, hide before revert, never fault.** `to` is shown and given its start values in the same call before anything plays; on completion `from` is hidden first, then both animations and the `ZIndex` writes are reverted, and the task never faults or cancels. Reason: the incoming host's first frame is never at rest, and hiding before reverting avoids a visible snap and leaves nothing at `Animation` priority where a host's local reset (`NavigationPage`) could not reach it.
- **Cancellation hands over; it does not revert.** Canceled animations stop where they stand, each host remembers its driving run, a later `Start` given that host takes it over from its current values, and a host nobody claims by the next frame is reverted. Reason: `TransitioningContentControl`'s mid-entrance presenter becomes the next `from` and must exit from where it is, and reverting unclaimed hosts means switching to another transition, or none, leaves nothing behind.
- **A take-over across threads hands the state over.** UI thread to compositor reverts the old UI-thread run right after the compositor seeds from it, in the same commit; compositor to UI thread copies the paused mirror's transform and opacity out first, sets them as the new run's own values, reverts the compositor run, then starts. Reason: otherwise the page was drawn offset twice, or the UI run started from rest under a still-applied composition `Translation`; values are copied before the revert because it winds the mirror back, and the set belongs to the new run so properties it does not animate are not left behind.
- **Progress seeks one paused run per transition.** `Update` creates the pair paused on first use for a (from, to, forward) triple and seeks it to `progress × Duration`; a new triple or a taken-over or reset host reverts the previous run first, a host a running `Start` holds is reverted rather than taken over, and seeks ignore `Delay` and `pageLength`. Reason: a seek measures from base values, and staging collapses while dragging.
- **Peeking carousels are not animated.** `Update` with a non-empty `visibleItems` does nothing, as `PageSlide` does. Reason: matches Avalonia's `PageSlide`. Rejected for now: an offset-to-builder mapping like `CrossFade`'s offset-to-opacity.
- **A `Carousel`'s layout axis stays horizontal.** Reason: `Carousel.GetTransitionAxis` recognizes only Avalonia's `PageSlide`. Rejected: having the page recipes return a `CompositePageTransition` with a zero-duration `PageSlide` purely to steer the axis.
- **`Connected` flights compose with the host's motion.** A claimed element flies on its composition `Translation` inside the incoming host, so a moving host adds to the flight's path while it still lands right; `PageFade` and `PageFadeThrough` are the documented pairing for connected navigation. Reason: counter-moving keyed elements needs a shared clock or start frame, and was left open.
- **Reduced motion needs nothing new.** Page transitions rely on the builders' `WhenReduced` forms and `ReduceAll`, and every page recipe supplies a short crossfade. Reason: the existing reduced-motion model already covers builders.
- **`PageBloom` is a page recipe, not a transition type.** `Recipes.PageBloom()` returns a `PrestoPageTransition` revealing on `ClipCircle` from the last press. Reason: it inherits the page recipes' take-over and revert, and a transition is handed only the two hosts. Rejected: a transition type of its own; a colored disk over the old page.
- **The thread is an argument on bridge entry points.** See BRIDGE-THREAD.
- **The pivot follows `RenderTransformOrigin`.** See BRIDGE-THREAD.
- **Features that manage their own lifetimes pass `WhenDetached.Continue`.** See BRIDGE-HELPERS.
- **A Presto page transition counts as navigation.** See BRIDGE-PRESENCE.
- **Page recipe shapes.** See BRIDGE-RECIPES.

## Non-goals

- A sequential (exit-then-enter) mode: staging is a `Delay` on `Enter`.
- Offset-to-builder mapping for carousels showing several pages.
- Counter-moving `Connected` flights against the page motion: it needs a shared clock or start frame.

## Open items

- BRIDGE-PAGES-001, -009, -026 and -029 have no test.
