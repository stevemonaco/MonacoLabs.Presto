---
id: BRIDGE-REDUCED
title: Reduced motion in the bridge
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/Platform/ReducedMotionProbe.cs
  - MonacoLabs.Presto.Avalonia/PrestoAvalonia.cs
  - MonacoLabs.Presto.Avalonia/Motion.cs
  - MonacoLabs.Presto.Avalonia/ReducedMotionFollowers.cs
  - MonacoLabs.Presto.Avalonia/Compositor/LayoutMotion.cs
  - MonacoLabs.Presto.Avalonia/Events/ScrollMotion.cs
types: []
tests:
  - SystemReducedMotionTests
  - MotionPreferenceTests
  - AnimateAttachedTests
  - MotionStateTests
  - BridgeAllocationTests
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
  - PullToRefreshTests
  - DistortTests
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

The core owns the flags: `PrestoDefaults.ReducedMotion` picks each builder's `WhenReduced` form and `ReduceAll` collapses the rest (CORE-REDUCED). This spec adds what the bridge does with them: an opt-in that keeps `ReducedMotion` in step with the OS setting, a preference that switches between following it and fixing the flag, and the rules every bridge trigger, helper and control follows to honor the flag, including the APIs that take no builder. Also specifies `PrestoAvalonia.FollowSystemReducedMotion`, `WatchSystemReducedMotion`, `RefreshSystemReducedMotion`, `PrestoAvalonia.MotionPreference`, `PrestoAvalonia.SystemReducedMotionProbe` and the `Motion.Preference` attached property.

## Requirements

### Following the OS setting

- **BRIDGE-REDUCED-001** — The bridge shall never set `ReducedMotion` or `ReduceAll` on its own; following the OS setting is opt-in through `FollowSystemReducedMotion` or `MotionPreference`.
  - Tests: `GalleryPageTests.TheOsPreferenceIsShownWhicheverChoiceIsPicked`
- **BRIDGE-REDUCED-002** — `FollowSystemReducedMotion` shall read the setting at once and write it to the own `Defaults.ReducedMotion` (never a scope's copy) of the engine that was `Engine.Current` when it was called.
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
- **BRIDGE-REDUCED-008** — The bridge shall refresh whenever the app is activated (`IActivatableLifetime.Activated`), including when the lifetime is found only after the first follower or watcher (BRIDGE-INIT-027).
  - Tests: `SystemReducedMotionTests.AnAppActivationAfterALateLifetimeLookupReadsTheSettingAgain`
- **BRIDGE-REDUCED-030** — Once any follower or watcher exists, the bridge shall also refresh whenever a window becomes active and, in the browser, when the media query reports a change.
  - Tests: manual — toggle the OS reduce-motion setting while the Desktop sample (Gallery's motion picker on System) is in the background, then activate its window.
- **BRIDGE-REDUCED-009** — The built-in probe shall read the OS setting on Windows (client-area animation off means reduced), macOS (`NSWorkspace.accessibilityDisplayShouldReduceMotion`), iOS (`UIAccessibilityIsReduceMotionEnabled`) and the browser (`prefers-reduced-motion: reduce`), and return `null` on every other platform, Android and Linux included.
  - Tests: `SystemReducedMotionTests.ThePlatformProbeReadsWithoutThrowing`
- **BRIDGE-REDUCED-010** (inherited) — When a native library or entry point is missing or a JS call fails, the probe shall return `null` instead of throwing.
  - Tests: `SystemReducedMotionTests.ThePlatformProbeReadsWithoutThrowing`
- **BRIDGE-REDUCED-011** — When `probe` is given, it shall replace the built-in probe for that follower or watcher.
  - Tests: `SystemReducedMotionTests.FollowingSetsTheFlagFromTheProbe`

### Motion preference

- **BRIDGE-REDUCED-031** — `PrestoAvalonia.MotionPreference` shall default to `null`, and while it is `null` the bridge shall not write either flag.
  - Tests: `MotionPreferenceTests.DesignModeIgnoresTheAttachedProperty`
- **BRIDGE-REDUCED-032** — When the preference is set to `Full` or `Reduced`, the bridge shall stop the follower it owns and write `false` or `true` to `Presto.Engine.Defaults.ReducedMotion`.
  - Tests: `MotionPreferenceTests.FullAndReducedWriteTheFlagAndStopFollowing`
- **BRIDGE-REDUCED-033** — When the preference is set to `System`, the bridge shall follow the OS setting on `Presto.Engine` as `FollowSystemReducedMotion` does, reading through `SystemReducedMotionProbe` when it is set.
  - Tests: `MotionPreferenceTests.SystemFollowsTheProbeUntilTheChoiceChanges`, `MotionPreferenceTests.ThePreferenceGovernsTheMainEngineInsideAScope`
- **BRIDGE-REDUCED-034** — When the preference is set to `null`, the bridge shall stop following and leave the flag as it is.
  - Tests: `MotionPreferenceTests.NullStopsFollowingAndLeavesTheFlag`
- **BRIDGE-REDUCED-035** — Setting the preference to its current value shall do nothing.
  - Tests: `MotionPreferenceTests.SettingTheSameChoiceAgainKeepsTheFollower`
- **BRIDGE-REDUCED-036** — The preference shall never write `ReduceAll`.
  - Tests: `MotionPreferenceTests.ThePreferenceNeverWritesReduceAll`
- **BRIDGE-REDUCED-037** — When `SystemReducedMotionProbe` changes while the preference is `System`, the bridge shall read the setting again through it.
  - Tests: `MotionPreferenceTests.ChangingTheProbeUnderSystemReadsAgain`
- **BRIDGE-REDUCED-038** — When `Motion.Preference` changes to a non-null value on any element, the bridge shall set `PrestoAvalonia.MotionPreference` to that value.
  - Tests: `MotionPreferenceTests.ABindingToSystemFromXamlStartsFollowing`, `MotionPreferenceTests.ClearingOrDetachingTheAttachedPropertyLeavesThePreference`
- **BRIDGE-REDUCED-039** — When `Motion.Preference` changes to `null`, or its element detaches, the bridge shall leave the preference as it is.
  - Tests: `MotionPreferenceTests.ClearingOrDetachingTheAttachedPropertyLeavesThePreference`
- **BRIDGE-REDUCED-040** — `Motion.Preference` shall default to `null`, so a binding's first value, `System` included, applies.
  - Tests: `MotionPreferenceTests.ABindingToSystemFromXamlStartsFollowing`
- **BRIDGE-REDUCED-041** — While `Design.IsDesignMode` is true, `Motion.Preference` shall do nothing.
  - Tests: `MotionPreferenceTests.DesignModeIgnoresTheAttachedProperty`

### When the flag is read

- **BRIDGE-REDUCED-012** — When the flag changes, the bridge shall act as Following a change states; every other trigger shall read the flag when it fires.
  - Tests: `RecipeTests.TogglingReducedMotionTakesEffectAtTheNextTransition`, `AnimateAttachedTests.AFiniteOnAttachedFinishesInItsFormWhenTheFlagFlips`
- **BRIDGE-REDUCED-013** — A builder handed to a bridge trigger (`Animate`, `Presence`, `Animate.OnAttached`, `Motion` states, page transitions, popovers) shall take its reduced form when it becomes params at the moment its animation starts.
  - Tests: `RecipeTests.ReducedMotionMakesAPresenceEnterRecipePlayItsFade`
- **BRIDGE-REDUCED-014** — A `Motion` state whose values were read under the other setting shall re-read them at its next transition; a held state keeps its values until it is left, unless Following a change replays it.
  - Tests: `RecipeTests.TogglingReducedMotionTakesEffectAtTheNextTransition`, `MotionStateTests.AHeldFiniteStateKeepsItsValuesUntilLeft`, `MotionStateTests.AReplayLeavesAHeldFiniteStateOnTheSameElementInItsForm`
- **BRIDGE-REDUCED-015** — A layout or content-size arm built from a builder shall re-arm at the next layout pass after the flag flips; the move whose pass detects the flip still runs on the old arm.
  - Tests: `AnimateLayoutAttachedTests.FlippingReducedMotionRearmsAtTheNextLayoutPass`

### Following a change

- **BRIDGE-REDUCED-042** — When an engine raises `ReducedMotionChanged`, each bridge trigger or state with a live animation on that engine shall replay when the form its builder resolves to now differs from the form playing and either form loops forever.
  - Tests: `AnimateAttachedTests.ALoopingOnAttachedReplaysItsReducedFormWhenTheFlagIsSet`, `AnimateAttachedTests.ASettledReducedFormLoopsAgainWhenTheFlagClears`, `AnimateAttachedTests.ARunningLoopingTapAnimationFollowsTheFlag`, `MotionStateTests.AHeldLoopingStateReplaysInItsNewFormWhenTheFlagFlips`
- **BRIDGE-REDUCED-043** — A form shall differ when the builder has a `WhenReduced` form and `ReducedMotion` changed, or when `ReduceAll` changed and the playing form is not a reduced alternative.
  - Tests: `AnimateAttachedTests.ABuilderWithoutAReducedFormDoesNotRestartOnAReducedMotionFlip`, `AnimateAttachedTests.AReduceAllFlipReplaysALoopCollapsed`
- **BRIDGE-REDUCED-044** — A live animation whose forms are both finite shall finish in the form it started with.
  - Tests: `AnimateAttachedTests.AFiniteOnAttachedFinishesInItsFormWhenTheFlagFlips`, `MotionStateTests.AHeldFiniteStateKeepsItsValuesUntilLeft`
- **BRIDGE-REDUCED-045** — A trigger or state shall be registered for the signal only while its element is attached and its animation is live.
  - Tests: `AnimateAttachedTests.ADetachedTriggerIsNotRegistered`, `BridgeAllocationTests.ASignalWithNothingLiveAllocatesNothing`
- **BRIDGE-REDUCED-046** — `Presto.Animate`, `PrestoAvalonia.Animate`, `AnimateOnCompositor` and their extension forms, on either thread, shall keep the form they were built with.
  - Tests: `CompositorAnimationTests.ACompositorLoopKeepsItsFormWhenTheFlagFlips`

### APIs without a builder

- **BRIDGE-REDUCED-016** — The `(duration, ease)` overloads of `AnimateLayout`, `AnimateContentSize` and both `AnimateSharedElement` forms shall read the flag as each move starts and, while it is set, land in one frame as `Recipes.Reflow`'s reduced form does; with the flag clear they ease as given.
  - Tests: `LayoutTransitionTests.ADurationLayoutMoveLandsInOneFrameOnlyUnderReducedMotion`, `ContentSizeTests.ADurationResizeLandsInOneFrameOnlyUnderReducedMotion`, `LayoutTransitionTests.ADurationSharedElementMoveLandsInOneFrameOnlyUnderReducedMotion`, `LayoutTransitionTests.ADurationKeyedSharedElementLandsInOneFrameOnlyUnderReducedMotion`
- **BRIDGE-REDUCED-017** — A persistent duration layout transition created with the flag clear shall land in one frame for moves that start after the flag is set.
  - Tests: `LayoutTransitionTests.APersistentDurationLayoutTransitionLandsOnceReducedMotionIsSet`
- **BRIDGE-REDUCED-018** — The builder overloads of the layout, content-size and shared-element APIs shall follow the builder's reduced form, and under `ReduceAll` land at once.
  - Tests: `AnimateLayoutAttachedTests.AReducedMoveIsInstant`, `ContentSizeTests.AReducedResizeIsInstant`, `LayoutTransitionTests.AReducedOrZeroDurationMoveLandsInOneFrame`
- **BRIDGE-REDUCED-019** — While the flag is set, `Scroll.Speed` shall treat every element as speed 1, read at each offset change, so a flipped flag shows at the next scroll.
  - Tests: `ScrollParallaxTests.UnderReducedMotionTheElementKeepsItsLayoutPositionAndResumesAtTheScrollAfterItClears`
- **BRIDGE-REDUCED-020** — While the flag is set, an animated scroll (`ScrollTo`, `ScrollIntoView`, `ScrollToIndex`, `StickToEnd`) shall jump with zero duration and delay, unless its params are already a `WhenReduced` form, which plays as given; a `ScrollObserver` snap shall jump to its target after its settle delay, under `ReduceAll` too (BRIDGE-SCROLLOBSERVER-059).
  - Tests: `ScrollToTests.ReducedMotionJumpsOnTheFirstFrameAndTheEngineIdles`, `ScrollToTests.ReducedMotionPlaysTheBuildersReducedForm`, `ScrollIntoViewTests.ReducedMotionJumpsOnTheFirstFrameAndTheEngineIdles`, `ScrollToIndexTests.ReducedMotionLandsAlignedAndTheEngineIdles`, `ScrollStickToEndTests.ReducedMotionJumpsToTheNewEndAndTheEngineIdles`, `ScrollSnapTests.AReducedSnapJumpsToItsStepOnceSettledAndLetsTheEngineIdle`
- **BRIDGE-REDUCED-021** — `AvaloniaAnimationParams.WhenReduced` shall behave as the core dictionary `WhenReduced`, typed for the bridge indexer.
  - Tests: `AnimationBuilderBridgeTests.ATypedWhenReducedReplacesTheProperties`

### Compositor and value-driven motion

- **BRIDGE-REDUCED-022** — A compositor animation shall play its params' reduced form while the flag is set, and `ReduceAll` shall land it on its end values at once unless it is already a reduced alternative.
  - Tests: `CompositorAnimationTests.DictionaryParamsOnTheCompositorPlayTheirReducedForm`, `CompositorAnimationTests.ReduceAllLandsACompositorAnimationOnItsEndValuesAtOnce`, `CompositorAnimationTests.ReduceAllLeavesAReducedAlternativeOnTheCompositorAlone`
- **BRIDGE-REDUCED-023** — While the flag is set, a follower (`Follow`, `{presto:Follow}`) shall snap to each new value unless `FollowParams.WhenReduced` (`Follow.WhenReduced` for the `Follow.*` shorthands) gives it a timing.
  - Tests: `FollowerTests.ReducedMotionSnapsUnlessWhenReducedGivesATiming`, `FollowTests.ReducedMotionSnaps`, `FollowTests.WhenReducedGivesTheTimingUnderReducedMotionFromTheNextValue`, `FollowBindingTests.ReducedMotionSnaps`

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
- **BRIDGE-REDUCED-028** — A bridge control or gesture that moves on its own shall, while the flag is set, land its travel at once and keep only color, opacity or a fade, as its own spec states (e.g. `Tilt` stays flat, `Magnetic` stays at rest, a `Sheet` lands with its scrim fading, a `Pager` lands each page, a `Marquee` rests at its start).
  - Tests: `TiltTests.ReducedMotionLeavesTheElementFlat`, `MagneticTests.ReducedMotionStaysAtRest`, `SheetTests.UnderReducedMotionItLandsAtOnceAndTheScrimFades`, `PagerTests.UnderReducedMotionPagingLandsAtOnceAndAutoplayStillPages`, `TransformableTests.UnderReducedMotionTheReleaseLandsOnItsClampedTargetAtOnce`, `MarqueeTests.UnderReducedMotionTheMarqueeRestsAtZeroWithNoCopies`
- **BRIDGE-REDUCED-029** — Every sample page shall run under the reduced setting without error and revert when detached.
  - Tests: `GalleryPageTests.RunsForASecondAndRevertsWhenDetached`

## Invariants

- The flag is per engine (`Engine.Defaults`); everything here reads `Engine.Current.Defaults` at its trigger, except a follower, which keeps the engine current at its creation.
- A reduced form is never itself collapsed by `ReduceAll`.

## Edge cases

- Several followers may run at once; each writes the flag of its own engine, and the last reading wins.
- Following on a platform the probe cannot read never touches the flag; the app keeps whatever it set.
- A follower created before the app's lifetime exists starts hearing app activation once a later bridge entry point finds the lifetime (BRIDGE-INIT-027).

## Threading and lifetime

- UI thread. The window-activation and browser hooks are installed once per process with the first follower or watcher, the app-activation hook once the lifetime is found (BRIDGE-INIT-027), and none is removed; disposing a handle only removes its reader from the list.
- An engine gets one `ReducedMotionChanged` subscription when the first trigger or state registers on it, kept for the engine's life; followers replay on the signalling engine, made current for the call.

## Reduced motion

- This spec is the bridge's reduced-motion contract; the reduced form of each feature is in that feature's spec.

## Allocation

- Not on the tick path; each read of the flag is a field read. A signal with nothing live allocates nothing; a replay allocates its params and animation. The probe allocates nothing on Windows, iOS and macOS beyond the interop call.

## Provenance

- No upstream counterpart: anime.js leaves `prefers-reduced-motion` to the app.

## Decisions

- **Following the OS setting is opt-in, with Presto's own probes.** `FollowSystemReducedMotion` is the only way the bridge writes `ReducedMotion`; it writes only on a differing reading, a `null` reading leaves the flag alone, disposing leaves the flag as it is, and `WatchSystemReducedMotion` reports readings without writing. Built-in probes cover Windows, macOS, iOS and the browser without a platform TFM; elsewhere the app passes its own `probe`. Reason: Avalonia 12.1.2 exposes no reduced-motion setting, and Android's animator duration scale needs Java interop, which the plain `net10.0` bridge lacks. Rejected: an Android probe; earlier, Presto shipped no OS probes at all (CORE-REDUCED).
- **The signal is an event on `Engine`, not on `PrestoDefaults`.** See CORE-REDUCED, with **The signal is raised synchronously, on the writer's thread, after the write.** and **Only a change of value raises, and only on the engine's own instance.**
- **The bridge property is nullable, and `null` means "not managed".** Reason: BRIDGE-REDUCED-001 holds, so the bridge writes nothing until the app opts in. The XAML property must also apply a binding's first value even when that value is `System`: Avalonia raises no change for a value equal to the property's default, so a non-nullable `System` default would silently never start following. Rejected: a non-nullable property defaulting to `System` (follows the OS uninvited, and has the default-value trap) or `Full` (writes `false` over an app's own flag).
- **The preference governs `Presto.Engine`, not `Engine.Current`.** It writes the main engine's own defaults instance, even while `Scope.Execute` has swapped a scope's copy in. Reason: it is an app-wide setting, and XAML can set it at load while another engine or scope is current. Engines created later already copy `Presto.Defaults` at creation. Rejected: following `Engine.Current` at set time, as `FollowSystemReducedMotion` does, which is right for that lower-level call but would make the XAML spelling depend on load order.
- **The XAML spelling is an attached property, `Motion.Preference`, that writes through to the static.** Setting a non-null value on any element sets `PrestoAvalonia.MotionPreference`. A null value or a detach leaves it as it is, and design mode does nothing. Reason: Avalonia XAML has no binding *to* a static. `x:Static` reads once, and a `{Binding Source={x:Static …}}` makes the static a source, while here the view model is the source and the preference the target. A binding target must be an `AvaloniaProperty` on an `AvaloniaObject`, so an attached property is the only way to bind a view model's setting into the bridge. Any element may host it, because the `DataContext` holding the setting can sit on a window (desktop template) or on a view inside the `TopLevel` (single-view template). Rejected: restricting the host to `Application` or `TopLevel` (the single-view `DataContext` is not there, and `Application.DataContext` is rarely set); a scoped, per-subtree preference (the flags are per engine, not per subtree); a markup extension (one-time, cannot follow a binding); a `MotionSettings` element in resources (it would need its own `DataContext` plumbing to bind). It lives on `Motion`, the class XAML authors already import; rejected: a `PrestoAvalonia.MotionPreference` attached property, whose name would clash with the static.
- **Write-through, not ownership.** The attached property never resets the preference when its element detaches or its binding clears. Reason: the preference is app-wide state. A second window closing, or a view recycled during navigation, must not change motion for the rest of the app. Rejected: reverting on detach (closing a settings window would undo the setting).
- **The preference never touches `ReduceAll`.** Reason: the OS setting asks for reduced motion, which the authored `WhenReduced` forms deliver. `ReduceAll` removes motion entirely, which is a separate, blunter app decision. Rejected: a fourth value mapping to `ReduceAll`.
- **The System follower's probe is a static setting.** `PrestoAvalonia.SystemReducedMotionProbe` (`null` for the built-in probe) is read by the preference's follower, which reads again when it changes. Reason: `FollowSystemReducedMotion` takes a probe per call, but the preference has no call site to pass one. Android and Linux apps need to supply theirs, and headless tests need a fixed reading instead of the build machine's OS setting.
- **Persistent motion follows a flag change; finite motion finishes in its form.** On the signal, a bridge-owned trigger or state replays only when its form changes and the playing form or the new one loops forever. Reason: a finite animation ends within its duration, and the next trigger reads the new flag, while restarting it mid-flight would replay entrances across the screen at the very moment the user asked for less motion. An infinite loop, by contrast, would show the wrong form for as long as it stays on screen. That is the reduced-motion failure the flag exists to prevent, and in the other direction a reduced form that settled would never loop again (Tempo's preview). Checking "either form loops" covers both directions. Checking that the form changed means a builder without a `WhenReduced` form does not restart on a `ReducedMotion` flip. Rejected: replaying every live trigger (mass replays of entrances); replaying only running loops (a settled reduced form would never return to its loop).
- **Caller-owned handles keep their form, on both threads.** `Presto.Animate`, `PrestoAvalonia.Animate`, `AnimateOnCompositor`, timelines and timers keep the form they were built with (CORE-REDUCED-003), and their owner subscribes to the signal. Reason: the caller holds the handle, its callbacks, `Then` and its timing, and a restart under it would break `await`, seek state and identity. Treating both threads alike keeps `PreferCompositor`'s fallback invisible, as "Unset settings follow the engine defaults" (BRIDGE-THREAD) requires. Rejected: re-resolving only compositor loops (the behavior would then depend on which thread the fallback picked); re-forming core animations in place (contradicts the CORE-REDUCED invariant and the ported timing model).
- **The bridge holds one subscription per engine and registers live triggers only.** A trigger or tracker enters the engine's list while it has a live animation on an attached element, and leaves it on detach, revert or `null`. Reason: the engine outlives every view, so a per-trigger delegate on it would root detached controls. Registering only live triggers keeps the reaction cost proportional to what is on screen.
- **Compositor scenes keep their snapshot.** Reason: a scene engine ticks off the UI thread (BRIDGE-SCENES), so propagating the main engine's signal into it would cross threads in a single-threaded core. A scene re-reads the flags when it attaches again.
- **Caller-owned compositor loops do not re-resolve on their own.** An `AnimateOnCompositor` with an infinite `Loop` keeps its form, and its owner reacts to `ReducedMotionChanged` (Tempo's backdrop drift keeps a short handler). Reason: the same params on the UI thread do not re-resolve, and a restart under a caller's handle breaks its `Then` and seek state. Revisit if a second app grows the same handler.
- **Every feature states its reduced form.** Every trigger, recipe and control added from the reduced-motion fixes on states its reduced-motion form in its own spec, and its tests run under both settings. Reason: the post-Phase-54 review found four places where `ReducedMotion` was ignored and samples worked around it; stating the reduced form up front stops new holes.
- **Reduced motion is an app-set flag with a per-builder alternative.** See CORE-REDUCED.
- **Reduced motion is read at arm time.** See BRIDGE-DECLLAYOUT.
- **The tracker re-reads a state when the reduced flag has flipped.** See BRIDGE-MOTION.
- **Reduced popups close at once.** See BRIDGE-POPOVER.
- **Every recipe carries its own reduced form.** See BRIDGE-RECIPES.

## Non-goals

- Following the OS by default, or resetting the flag when following stops (see Following the OS setting is opt-in, with Presto's own probes).
- A change event on `PrestoDefaults` itself (see CORE-REDUCED); apps that must react at once subscribe to `Engine.ReducedMotionChanged`.
- Re-forming caller-owned animations (see Caller-owned handles keep their form, on both threads).
- Propagating the signal into compositor scenes (see Compositor scenes keep their snapshot).
- Android and Linux probes: Avalonia exposes no setting and no platform TFM is targeted.

## Open items

- The window-activation and browser-change refreshes (BRIDGE-REDUCED-030) are untested; only the Gallery page exercises them by hand.
