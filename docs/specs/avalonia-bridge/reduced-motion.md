---
id: BRIDGE-REDUCED
title: Reduced motion in the bridge
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge
sources:
  - Monaco.Presto.AvaloniaBridge/Platform/ReducedMotionProbe.cs
  - Monaco.Presto.AvaloniaBridge/PrestoAvalonia.cs
  - Monaco.Presto.AvaloniaBridge/Compositor/LayoutMotion.cs
  - Monaco.Presto.AvaloniaBridge/Events/ScrollMotion.cs
types: []
tests:
  - SystemReducedMotionTests
  - RecipeTests
  - LayoutTransitionTests
  - ContentSizeTests
  - AnimateLayoutAttachedTests
  - ScrollParallaxTests
  - ScrollToTests
  - CompositorAnimationTests
  - FollowerTests
  - PopoverFlyoutTests
  - PresenceTextExitTests
  - GalleryPageTests
upstream: []
depends:
  - CORE-REDUCED
  - CORE-SCOPE
  - BRIDGE-RECIPES
  - BRIDGE-LAYOUT
  - BRIDGE-CONTENTSIZE
  - BRIDGE-SCROLL
  - BRIDGE-THREAD
  - BRIDGE-MOTION
  - BRIDGE-FOLLOW
  - BRIDGE-PRESENCE
  - BRIDGE-POPOVER
  - BRIDGE-TEXTREVEAL
---

# Reduced motion in the bridge

## Purpose

The core owns the flags: `PrestoDefaults.ReducedMotion` picks each builder's `WhenReduced` form and `ReduceAll` collapses the rest (CORE-REDUCED). This spec adds what the bridge does with them: an opt-in that keeps `ReducedMotion` in step with the OS setting, and the rules every bridge trigger, helper and control follows to honor the flag, including the APIs that take no builder. Also specifies `PrestoAvalonia.FollowSystemReducedMotion`, `WatchSystemReducedMotion` and `RefreshSystemReducedMotion`.

## Requirements

### Following the OS setting

- **BRIDGE-REDUCED-001** — The bridge shall never set `ReducedMotion` or `ReduceAll` on its own; following the OS setting is opt-in through `FollowSystemReducedMotion`.
  - Tests: `GalleryPageTests.TheOsPreferenceIsShownWhicheverChoiceIsPicked`
- **BRIDGE-REDUCED-002** — `FollowSystemReducedMotion` shall read the setting at once and write it to `Defaults.ReducedMotion` of the engine that was `Engine.Current` when it was called.
  - Tests: `SystemReducedMotionTests.FollowingSetsTheFlagFromTheProbe`
- **BRIDGE-REDUCED-003** — When a reading differs from the flag, following shall write it and call `changed` with the new value; when it equals the flag, it shall do neither.
  - Tests: `SystemReducedMotionTests.ARefreshPicksUpAFlippedProbeAndReportsIt`
- **BRIDGE-REDUCED-004** — When the probe reads `null` (unknown), following shall leave the flag as it is.
  - Tests: `SystemReducedMotionTests.AnUnknownReadingLeavesTheFlagAlone`
- **BRIDGE-REDUCED-005** — Disposing the handle shall stop following and leave the flag at its last value.
  - Tests: `SystemReducedMotionTests.DisposingStopsFollowingAndLeavesTheFlag`
- **BRIDGE-REDUCED-006** — `WatchSystemReducedMotion` shall call `changed` with the reading at once and again whenever it differs from the last one reported, `null` included, without writing the flag, until disposed.
  - Tests: `SystemReducedMotionTests.WatchingReportsTheSettingAndEachChangeWithoutTouchingTheFlag`, `GalleryPageTests.TheOsPreferenceIsShownWhicheverChoiceIsPicked`
- **BRIDGE-REDUCED-007** — `RefreshSystemReducedMotion` shall read the setting again for every follower and watcher still active.
  - Tests: `SystemReducedMotionTests.ARefreshPicksUpAFlippedProbeAndReportsIt`
- **BRIDGE-REDUCED-008** — Once any follower or watcher exists, the bridge shall refresh whenever a window becomes active, the app is activated (`IActivatableLifetime.Activated`) and, in the browser, when the media query reports a change.
  - Tests: manual — toggle the OS reduce-motion setting while the Desktop sample (Gallery's motion picker on System) is in the background, then activate its window.
- **BRIDGE-REDUCED-009** — The built-in probe shall read the OS setting on Windows (client-area animation off means reduced), macOS (`NSWorkspace.accessibilityDisplayShouldReduceMotion`), iOS (`UIAccessibilityIsReduceMotionEnabled`) and the browser (`prefers-reduced-motion: reduce`), and return `null` on every other platform, Android and Linux included.
  - Tests: `SystemReducedMotionTests.ThePlatformProbeReadsWithoutThrowing`
- **BRIDGE-REDUCED-010** (inherited) — When a native library or entry point is missing or a JS call fails, the probe shall return `null` instead of throwing.
  - Tests: `SystemReducedMotionTests.ThePlatformProbeReadsWithoutThrowing`
- **BRIDGE-REDUCED-011** — When `probe` is given, it shall replace the built-in probe for that follower or watcher.
  - Tests: `SystemReducedMotionTests.FollowingSetsTheFlagFromTheProbe`

### When the flag is read

- **BRIDGE-REDUCED-012** — The bridge shall raise no event when the flag changes; each trigger shall read it when it fires.
  - Tests: `RecipeTests.TogglingReducedMotionTakesEffectAtTheNextTransition`
- **BRIDGE-REDUCED-013** — A builder handed to a bridge trigger (`Animate`, `Presence`, `Animate.OnAttached`, `Motion` states, page transitions, popovers) shall take its reduced form when it becomes params at the moment its animation starts.
  - Tests: `RecipeTests.ReducedMotionMakesAPresenceEnterRecipePlayItsFade`
- **BRIDGE-REDUCED-014** — A `Motion` state whose values were read under the other setting shall re-read them at its next transition; a state already held keeps its values until it is left.
  - Tests: `RecipeTests.TogglingReducedMotionTakesEffectAtTheNextTransition`
- **BRIDGE-REDUCED-015** — A layout or content-size arm built from a builder shall re-arm at the next layout pass after the flag flips; the move whose pass detects the flip still runs on the old arm.
  - Tests: `AnimateLayoutAttachedTests.FlippingReducedMotionRearmsAtTheNextLayoutPass`

### APIs without a builder

- **BRIDGE-REDUCED-016** — The `(duration, ease)` overloads of `AnimateLayout`, `AnimateContentSize` and both `AnimateSharedElement` forms shall read the flag as each move starts and, while it is set, land in one frame as `Recipes.Reflow`'s reduced form does; with the flag clear they ease as given.
  - Tests: `LayoutTransitionTests.ADurationLayoutMoveLandsInOneFrameOnlyUnderReducedMotion`, `ContentSizeTests.ADurationResizeLandsInOneFrameOnlyUnderReducedMotion`, `LayoutTransitionTests.ADurationSharedElementMoveLandsInOneFrameOnlyUnderReducedMotion`, `LayoutTransitionTests.ADurationKeyedSharedElementLandsInOneFrameOnlyUnderReducedMotion`
- **BRIDGE-REDUCED-017** — A persistent duration layout transition created with the flag clear shall land in one frame for moves that start after the flag is set.
  - Tests: `LayoutTransitionTests.APersistentDurationLayoutTransitionLandsOnceReducedMotionIsSet`
- **BRIDGE-REDUCED-018** — The builder overloads of the layout, content-size and shared-element APIs shall follow the builder's reduced form, and under `ReduceAll` land at once.
  - Tests: `AnimateLayoutAttachedTests.AReducedMoveIsInstant`, `ContentSizeTests.AReducedResizeIsInstant`, `LayoutTransitionTests.AReducedOrZeroDurationMoveLandsInOneFrame`
- **BRIDGE-REDUCED-019** — While the flag is set, `Scroll.Speed` shall treat every element as speed 1, read at each offset change, so a flipped flag shows at the next scroll.
  - Tests: `ScrollParallaxTests.UnderReducedMotionTheElementKeepsItsLayoutPositionAndResumesAtTheScrollAfterItClears`
- **BRIDGE-REDUCED-020** — While the flag is set, an animated scroll (`ScrollTo`, `ScrollIntoView`, `ScrollToIndex`, `StickToEnd`) shall jump with zero duration and delay, unless its params are already a `WhenReduced` form, which plays as given.
  - Tests: `ScrollToTests.ReducedMotionJumpsOnTheFirstFrameAndTheEngineIdles`, `ScrollToTests.ReducedMotionPlaysTheBuildersReducedForm`, `ScrollIntoViewTests.ReducedMotionJumpsOnTheFirstFrameAndTheEngineIdles`, `ScrollToIndexTests.ReducedMotionLandsAlignedAndTheEngineIdles`, `ScrollStickToEndTests.ReducedMotionJumpsToTheNewEndAndTheEngineIdles`
- **BRIDGE-REDUCED-021** — `AvaloniaAnimationParams.WhenReduced` shall behave as the core dictionary `WhenReduced`, typed for the bridge indexer.
  - Tests: `AnimationBuilderBridgeTests.ATypedWhenReducedReplacesTheProperties`

### Compositor and value-driven motion

- **BRIDGE-REDUCED-022** — A compositor animation shall play its params' reduced form while the flag is set, and `ReduceAll` shall land it on its end values at once unless it is already a reduced alternative.
  - Tests: `CompositorAnimationTests.DictionaryParamsOnTheCompositorPlayTheirReducedForm`, `CompositorAnimationTests.ReduceAllLandsACompositorAnimationOnItsEndValuesAtOnce`, `CompositorAnimationTests.ReduceAllLeavesAReducedAlternativeOnTheCompositorAlone`
- **BRIDGE-REDUCED-023** — While the flag is set, a follower (`Follow`, `{presto:Follow}`) shall snap to each new value unless `FollowParams.WhenReduced` gives it a timing.
  - Tests: `FollowerTests.ReducedMotionSnapsUnlessWhenReducedGivesATiming`, `FollowTests.ReducedMotionSnaps`, `FollowBindingTests.ReducedMotionSnaps`

### Presence, text and popups

- **BRIDGE-REDUCED-024** — When a split text block's arrival or exit plays a reduced form, it shall play on the whole, unsplit block (one whole-element ghost for an exit).
  - Tests: `TextRevealTests.ReducedTextRevealsFadeTheBlockUnsplit`, `PresenceTextExitTests.ReducedTextExitRecipesFadeOneWholeGhostOverShort`, `PresenceTextExitTests.AReducedHeldHidePlaysOnTheUnsplitBlock`
- **BRIDGE-REDUCED-025** — When an exit or popup close completes on its first frame, as every one does under `ReduceAll`, the bridge shall finish it inside the call and leave no ghost or wait.
  - Tests: `PresenceTextExitTests.ReduceAllLeavesNoGhost`, `PresenceModeTests.UnderReduceAllTheWaitEndsWithTheInstantExit`, `PopoverFlyoutTests.UnderReduceAllCloseIsInstantAndOpenPlaysOnlyTheReducedFade`
- **BRIDGE-REDUCED-026** — While the flag is set as a popup closes, it shall close at once with no ghost; opening plays the builder's reduced form.
  - Tests: `PopoverFlyoutTests.UnderReducedMotionCloseIsInstant`

### Controls, gestures and recipes

- **BRIDGE-REDUCED-027** — Every recipe shall carry a reduced form as BRIDGE-RECIPES and BRIDGE-RECIPES-MEDIA state per family.
  - Tests: `RecipeTests.EachReducedFormOnlyFades`, `RecipeTests.EachReducedFormDropsTravelAndScaleAndOnlyLoadingLoops`
- **BRIDGE-REDUCED-028** — A bridge control or gesture that moves on its own shall, while the flag is set, land its travel at once and keep only color, opacity or a fade, as its own spec states (e.g. `Tilt` stays flat, `Magnetic` stays at rest, a `Sheet` lands with its scrim fading, a `Pager` lands each page).
  - Tests: `TiltTests.ReducedMotionLeavesTheElementFlat`, `MagneticTests.ReducedMotionStaysAtRest`, `SheetTests.UnderReducedMotionItLandsAtOnceAndTheScrimFades`, `PagerTests.UnderReducedMotionPagingLandsAtOnceAndAutoplayStillPages`, `TransformableTests.UnderReducedMotionTheReleaseLandsOnItsClampedTargetAtOnce`
- **BRIDGE-REDUCED-029** — Every sample page shall run under the reduced setting without error and revert when detached.
  - Tests: `GalleryPageTests.RunsForASecondAndRevertsWhenDetached`

## Invariants

- The flag is per engine (`Engine.Defaults`); everything here reads `Engine.Current.Defaults` at its trigger, except a follower, which keeps the engine current at its creation.
- A reduced form is never itself collapsed by `ReduceAll`.

## Edge cases

- Several followers may run at once; each writes the flag of its own engine, and the last reading wins.
- Following on a platform the probe cannot read never touches the flag; the app keeps whatever it set.
- A follower created before the app's lifetime exists misses app activation, since the hook is installed with the first follower or watcher only.

## Threading and lifetime

- UI thread. The window-activation, app-activation and browser hooks are installed once per process with the first follower or watcher and never removed; disposing a handle only removes its reader from the list.

## Reduced motion

- This spec is the bridge's reduced-motion contract; the reduced form of each feature is in that feature's spec.

## Allocation

- Not on the tick path; each read of the flag is a field read. The probe allocates nothing on Windows, iOS and macOS beyond the interop call.

## Deviations from anime.js

- No upstream counterpart: anime.js leaves `prefers-reduced-motion` to the app.

## Decisions

- **Following the OS setting is opt-in, with Presto's own probes.** `FollowSystemReducedMotion` is the only way the bridge writes `ReducedMotion`; it writes only on a differing reading, a `null` reading leaves the flag alone, disposing leaves the flag as it is, and `WatchSystemReducedMotion` reports readings without writing. Built-in probes cover Windows, macOS, iOS and the browser without a platform TFM; elsewhere the app passes its own `probe`. Reason: Avalonia 12.1.2 exposes no reduced-motion setting, and Android's animator duration scale needs Java interop, which the plain `net10.0` bridge lacks. Rejected: an Android probe; earlier, Presto shipped no OS probes at all (CORE-REDUCED).
- **The flag has no change notification.** Each consumer reads `ReducedMotion` when its own trigger fires (an `Offset` notification, a move start, a call); `Scroll.Speed` treats every element as speed 1 while it is set, and the `(duration, ease)` layout, content-size and shared-element overloads read it as each move starts and land in one frame. Reason: `PrestoDefaults` is a record copied with `with` into every scope and engine, so an event would be compared by record equality, carried into each copy, and subscribers would not know which copy to watch; `FollowSystemReducedMotion`'s `changed` covers apps that must react at once. Rejected: an event on `PrestoDefaults`.
- **Every feature states its reduced form.** Every trigger, recipe and control added from the reduced-motion fixes on states its reduced-motion form in its own spec, and its tests run under both settings. Reason: the post-Phase-54 review found four places where `ReducedMotion` was ignored and samples worked around it; stating the reduced form up front stops new holes.
- **Reduced motion is an app-set flag with a per-builder alternative.** See CORE-REDUCED.
- **Reduced motion is read at arm time.** See BRIDGE-DECLLAYOUT.
- **The tracker re-reads a state when the reduced flag has flipped.** See BRIDGE-MOTION.
- **Reduced popups close at once.** See BRIDGE-POPOVER.
- **Every recipe carries its own reduced form.** See BRIDGE-RECIPES.

## Non-goals

- Following the OS by default, or resetting the flag when following stops (see Following the OS setting is opt-in, with Presto's own probes).
- A change event on `PrestoDefaults` (see The flag has no change notification); apps that must react at once use `FollowSystemReducedMotion`'s `changed`.
- Android and Linux probes: Avalonia exposes no setting and no platform TFM is targeted.

## Open items

- The activation and browser-change refreshes (BRIDGE-REDUCED-008) are untested; only the Gallery page exercises them by hand.
- A follower captures `Engine.Current` at creation, so following from a scope or secondary engine writes that engine's flag, not the main one; this is undocumented outside the XML summary.
- The app-activation hook is skipped when no `Application.Current` exists at the first call and is never retried.
