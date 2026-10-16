# Reduced-motion preference and change signal

## Why

Two Tempo findings in the backlog (BRIDGE-REDUCED), both about an app whose motion setting changes while it runs:

- **The System / Full / Reduced switch is hand-written in every app.** Following the OS is `PrestoAvalonia.FollowSystemReducedMotion()`, and overriding it is a direct write to `Presto.Defaults.ReducedMotion`, so a setting with three choices has to dispose the follower handle and then follow again or write the flag. Tempo's `App.ApplyMotionSetting` repeats the Gallery's `MainViewModel.Motion` setter line for line, and neither can be written in XAML: the flag is a property of a record and the follower is a method call.
- **Nothing signals a change of the flag.** An animation resolves its `WhenReduced` form once, when it starts (CORE-REDUCED-003), and nothing tells the code that started it that the flag has moved. Tempo works around this twice. `ShellView` restarts the compositor backdrop drift on the view model's `Motion` change, so under System an OS change while the app runs never reaches it. `SettingsFlyout` replays the `Animate.OnAttached` motion preview by clearing and resetting `OnAttached`, which only works because `App.ApplyMotionSetting` subscribed first and has already written the flag. Every infinite loop that the bridge starts from a trigger (`Animate.OnAttached`, a looping `Motion` state) has the same hole: once started, it keeps the form it started with for as long as it stays on screen.

The two findings share one fix. A signal on the engine lets the bridge, and apps, react to a change whatever made it. A single preference API that owns the follower gives the switch one spelling, in code and in XAML.

## What

**Core: a change signal on the engine.** `Engine.ReducedMotionChanged` (`event Action<Engine>?`) is raised synchronously, right after the write, whenever `ReducedMotion` or `ReduceAll` of the engine's own `PrestoDefaults` changes value. The write can come from app code, a follower or the preference below. Copies of the defaults never raise it: a `with` copy, the copy a new engine starts from, and a scope's configured defaults. `PrestoDefaults` stays a plain record, and its equality and `with` copies behave as before. Animations that already exist still keep their form (CORE-REDUCED-003 stands); the core only reports the change.

**Core: a preference enum.** `MotionPreference { System, Full, Reduced }` names where an app's flag comes from. It lives in the core so that a view model can hold the setting without an Avalonia reference. The core never acts on it.

**Bridge: one preference API.** `PrestoAvalonia.MotionPreference` (`MotionPreference?`, default `null`) owns the switch for the main engine (`Presto.Engine`):

```csharp
PrestoAvalonia.MotionPreference = MotionPreference.System;   // follow the OS
PrestoAvalonia.MotionPreference = MotionPreference.Reduced;  // stop following, set the flag
PrestoAvalonia.MotionPreference = null;                      // stop managing; the flag stays as it is
```

`Full` and `Reduced` stop the bridge's follower and write the flag. `System` follows the OS as `FollowSystemReducedMotion` does, through `PrestoAvalonia.SystemReducedMotionProbe` when the app sets one (for Android or Linux, and for tests). `null` stops following. In XAML, the attached property `Motion.Preference` writes through to it from any element, so a view model's setting binds where its `DataContext` is:

```xml
<Window presto:Motion.Preference="{Binding Motion}" …>
```

The attached property is write-only: a non-null value sets the app-wide preference, while clearing it or detaching the element leaves the preference as it is. `FollowSystemReducedMotion`, `WatchSystemReducedMotion` and `RefreshSystemReducedMotion` stay; the preference is built on them.

**Bridge: persistent motion follows a change.** When the engine signals, a bridge trigger whose animation is live re-resolves its builder. It replays when the form it would pick now differs from the one playing and either form loops forever:

- `Animate.OnAttached`, whether running or holding its end values, reverts and plays the new form from the start, as a builder change does (BRIDGE-ATTACH-007). Tempo's preview therefore stops bouncing and fades once when the flag is set, and bounces again when it clears.
- A running `OnTapped` or `OnChanged` animation does the same. One that has completed has already been reverted, so nothing replays.
- A `Motion` state that is held or playing plays its builder's new form for the properties it holds, from where they stand, as a return to a looping lower state does (BRIDGE-MOTION-013).
- The bridge controls with continuous motion of their own follow the same rule: `PullToRefresh`'s spinning ring stops or starts mid-refresh, and `Distort` redraws.

Finite motion finishes in the form it started with, and the next trigger reads the new flag, as today. Animations the caller holds a handle to keep their form on either thread: `Presto.Animate`, `PrestoAvalonia.Animate`, `AnimateOnCompositor`, timelines and timers. Their owner reacts to the signal.

**Samples.** Tempo deletes `App.ApplyMotionSetting`, its own `MotionPreference` enum and `SettingsFlyout.ReplayPreview`. `MainWindow` binds `presto:Motion.Preference`, and `ShellView` re-drifts on `Presto.Engine.ReducedMotionChanged` instead of the view model's `Motion`, which also covers an OS change under System. The Gallery deletes its enum and the follower logic in its `MainViewModel.Motion` setter, and `MainView` binds the attached property.

What does not change: the bridge still never writes the flag unless asked (BRIDGE-REDUCED-001), since the preference defaults to `null`. `ReduceAll` is never written by the preference. Layout arms, `Scroll.Speed`, followers and controls that already read the flag at each pass or frame behave as before. Pages that read the flag once when they load (the Gallery's `Loop = !Presto.Defaults.ReducedMotion` timelines) still need a reload, because their animations belong to the caller. Compositor scenes keep the snapshot of the flags they took at attach (BRIDGE-SCENES-012).

## Decisions

- **The signal is an event on `Engine`, not on `PrestoDefaults`.** The engine raises `ReducedMotionChanged` when a flag of its own defaults instance changes value. It finds its instance through a reference-keyed link, such as a weak table, that neither `with` copies nor record equality sees. Reason: the earlier objection to a change event ("The flag has no change notification", CORE-REDUCED and BRIDGE-REDUCED) was about an event *on the record*. Such an event would be compared by record equality and carried into every `with` copy, and a subscriber would not know which copy to watch. Subscribers already know the engine (`Presto.Engine`, or `Engine.Current` when a trigger plays), and the engine has exactly one instance that is its own. Rejected: an event or `INotifyPropertyChanged` on `PrestoDefaults` (the reasons above, and INPC would also report every other default); polling the flag each frame (an idle engine has no frames, and it costs the tick path); a static process-wide event (it cannot say which engine changed, and scene engines run off the UI thread).
- **The signal is raised synchronously, on the writer's thread, after the write.** Reason: the core is single-threaded and the flag is written on its engine's thread, so a handler can read the new value and replay in the same call. A test can then assert straight after setting the flag. The ordering accident in Tempo, where `SettingsFlyout` saw the new flag only because `App` had subscribed first, cannot happen when the source of the change raises the signal itself. Rejected: deferring the signal to the next frame (a change while the engine is idle would wait for an unrelated frame).
- **Only a change of value raises, and only on the engine's own instance.** Writing the current value raises nothing. A scope with configured defaults, a `with` copy, or a write that reaches a scope's copy while that scope executes raises nothing either. Reason: a scope's defaults are in force only during `Execute`, and nothing persistent resolves against them afterwards, while bridge triggers read `Engine.Current.Defaults` when they fire. Rejected: signalling scope copies, which would need a subscription per scope for no consumer.
- **`MotionPreference` is a core enum with `System` first.** Reason: a view model, which the samples and the BRIDGE-ATTACH builder decision keep free of Avalonia references, holds and persists the setting. `System` is the default value because following the OS is the right default for an app setting. Tempo already uses this order, so its persisted settings keep their meaning. Rejected: a bridge enum (it would pull Avalonia into view models); `Full` first (the Gallery's order, which it keeps as an explicit initial value instead).
- **The bridge property is nullable, and `null` means "not managed".** Reason: BRIDGE-REDUCED-001 holds, so the bridge writes nothing until the app opts in. The XAML property must also apply a binding's first value even when that value is `System`: Avalonia raises no change for a value equal to the property's default, so a non-nullable `System` default would silently never start following. Rejected: a non-nullable property defaulting to `System` (follows the OS uninvited, and has the default-value trap) or `Full` (writes `false` over an app's own flag).
- **The preference governs `Presto.Engine`, not `Engine.Current`.** Reason: it is an app-wide setting, and XAML can set it at load while another engine or scope is current. Engines created later already copy `Presto.Defaults` at creation. Rejected: following `Engine.Current` at set time, as `FollowSystemReducedMotion` does, which is right for that lower-level call but would make the XAML spelling depend on load order.
- **The XAML spelling is an attached property, `Motion.Preference`, that writes through to the static.** Setting a non-null value on any element sets `PrestoAvalonia.MotionPreference`. A null value or a detach leaves it as it is, and design mode does nothing. Reason: Avalonia XAML has no binding *to* a static. `x:Static` reads once, and a `{Binding Source={x:Static …}}` makes the static a source, while here the view model is the source and the preference the target. A binding target must be an `AvaloniaProperty` on an `AvaloniaObject`, so an attached property is the only way to bind a view model's setting into the bridge. Any element may host it, because the `DataContext` holding the setting can sit on a window (desktop template) or on a view inside the `TopLevel` (single-view template, the Gallery's `MainView`). `Motion` is the class XAML authors already import for motion. Rejected: restricting the host to `Application` or `TopLevel` (the single-view `DataContext` is not there, and `Application.DataContext` is rarely set); a scoped, per-subtree preference (the flags are per engine, not per subtree); a markup extension (one-time, cannot follow a binding); a `MotionSettings` element in resources (it would need its own `DataContext` plumbing to bind).
- **Write-through, not ownership.** The attached property never resets the preference when its element detaches or its binding clears. Reason: the preference is app-wide state. A second window closing, or a view recycled during navigation, must not change motion for the rest of the app. Rejected: reverting on detach (closing a settings window would undo the setting).
- **The preference never touches `ReduceAll`.** Reason: the OS setting asks for reduced motion, which the authored `WhenReduced` forms deliver. `ReduceAll` removes motion entirely, which is a separate, blunter app decision. Rejected: a fourth value mapping to `ReduceAll`.
- **The System follower's probe is a static setting.** `PrestoAvalonia.SystemReducedMotionProbe` (`Func<bool?>?`, default `null` for the built-in probe) is read by the preference's follower. Reason: `FollowSystemReducedMotion` takes a probe per call, but the preference has no call site to pass one. Android and Linux apps need to supply theirs, and headless tests need a fixed reading instead of the build machine's OS setting.
- **Persistent motion follows a flag change; finite motion finishes in its form.** On the signal, a bridge-owned trigger or state replays only when its form changes and the playing form or the new one loops forever. Reason: a finite animation ends within its duration, and the next trigger reads the new flag, while restarting it mid-flight would replay entrances across the screen at the very moment the user asked for less motion. An infinite loop, by contrast, would show the wrong form for as long as it stays on screen. That is the reduced-motion failure the flag exists to prevent, and in the other direction a reduced form that settled would never loop again (Tempo's preview). Checking "either form loops" covers both directions. Checking that the form changed means a builder without a `WhenReduced` form does not restart on a `ReducedMotion` flip. Rejected: replaying every live trigger (mass replays of entrances); replaying only running loops (a settled reduced form would never return to its loop).
- **Caller-owned handles keep their form, on both threads.** `Presto.Animate`, `PrestoAvalonia.Animate`, `AnimateOnCompositor`, timelines and timers keep the form they were built with (CORE-REDUCED-003), and their owner subscribes to the signal. Reason: the caller holds the handle, its callbacks, `Then` and its timing, and a restart under it would break `await`, seek state and identity. Treating both threads alike keeps `PreferCompositor`'s fallback invisible, as "Unset settings follow the engine defaults" (BRIDGE-THREAD) requires. Rejected: re-resolving only compositor loops (the behavior would then depend on which thread the fallback picked); re-forming core animations in place (contradicts the CORE-REDUCED invariant and the ported timing model).
- **The bridge holds one subscription per engine and registers live triggers only.** A trigger or tracker enters the engine's list while it has a live animation on an attached element, and leaves it on detach, revert or `null`. Reason: the engine outlives every view, so a per-trigger delegate on it would root detached controls. Registering only live triggers keeps the reaction cost proportional to what is on screen.
- **Compositor scenes keep their snapshot.** Reason: a scene engine ticks off the UI thread (BRIDGE-SCENES), so propagating the main engine's signal into it would cross threads in a single-threaded core. A scene re-reads the flags when it attaches again.
- **Caller-owned compositor loops do not re-resolve on their own.** An `AnimateOnCompositor` with an infinite `Loop` keeps its form; Tempo's `ShellView` keeps a short handler on `ReducedMotionChanged`. Reason: the same params on the UI thread do not re-resolve, and a restart under a caller's handle breaks its `Then` and seek state. Revisit if a second app grows the same handler.
- **The XAML spelling lives on `Motion`, as `Motion.Preference`, though it is app-wide.** Its doc comment states the write-through. Rejected: `PrestoAvalonia.MotionPreference` as an attached property, which would share its name with the static property.

## Spec changes

**CORE-REDUCED** (`docs/specs/core/reduced-motion.md`): add `MotionPreference` to `types` and `MonacoLabs.Presto/MotionPreference.cs`, `MonacoLabs.Presto/Engine.cs` to `sources`. In Purpose, name `Engine.ReducedMotionChanged` among the members it specifies. New subsection **Change signal**:

- When `ReducedMotion` or `ReduceAll` of an engine's own defaults changes value, the engine shall raise `ReducedMotionChanged` once, with itself, synchronously after the write.
- Writing a flag its current value shall raise nothing.
- Writing a flag of a copy of the defaults (`with`, the copy a new engine starts from, a scope's configured defaults) shall raise nothing on the engine it was copied from; a new engine's starting copy is that engine's own instance.
- A `PrestoDefaults` copy shall compare equal to its source until a value differs, whether or not the source belongs to an engine.
- Raising the signal shall not change any existing animation (CORE-REDUCED-003).

New subsection **Preference**:

- `MotionPreference` shall have the values `System`, `Full` and `Reduced`, in that order, `System` being the default; the core shall not act on it.

Change CORE-REDUCED-003 to cite its new test. Threading and lifetime: replace "there is no change notification" with the signal and its thread. Decisions: strike **The flag has no change notification** and add the first three decisions above, plus **`MotionPreference` is a core enum with `System` first**. Non-goals: keep "re-forming running animations when a flag changes" and add "a signal for scope copies". Open items: drop "flag changes not touching live animations".

**CORE-SCOPE** (`docs/specs/core/scope-and-defaults.md`): in Threading and lifetime, change "`PrestoDefaults` has no change notification" to "a scope's defaults raise no change signal; only an engine's own instance does (CORE-REDUCED)".

**CORE-ENGINE** (`docs/specs/core/engine.md`): in Reduced motion, add "The engine reports a change of its flags through `ReducedMotionChanged` (CORE-REDUCED)."

**BRIDGE-REDUCED** (`docs/specs/avalonia-bridge/reduced-motion.md`): add `Motion.cs` to `sources` and `AnimateAttachedTests`, `MotionStateTests`, `PullToRefreshTests`, `DistortTests`, `MotionPreferenceTests` to `tests`. In Purpose, name `PrestoAvalonia.MotionPreference`, `PrestoAvalonia.SystemReducedMotionProbe` and `Motion.Preference`. New subsection **Motion preference**:

- `PrestoAvalonia.MotionPreference` shall default to `null`, and while it is `null` the bridge shall not write either flag.
- When the preference is set to `Full` or `Reduced`, the bridge shall stop the follower it owns and write `false` or `true` to `Presto.Engine.Defaults.ReducedMotion`.
- When the preference is set to `System`, the bridge shall follow the OS setting on `Presto.Engine` as `FollowSystemReducedMotion` does, reading through `SystemReducedMotionProbe` when it is set.
- When the preference is set to `null`, the bridge shall stop following and leave the flag as it is.
- Setting the preference to its current value shall do nothing.
- The preference shall never write `ReduceAll`.
- When `SystemReducedMotionProbe` changes while the preference is `System`, the bridge shall read the setting again through it.
- When `Motion.Preference` changes to a non-null value on any element, the bridge shall set `PrestoAvalonia.MotionPreference` to that value.
- When `Motion.Preference` changes to `null`, or its element detaches, the bridge shall leave the preference as it is.
- `Motion.Preference` shall default to `null`, so a binding's first value, `System` included, applies.
- While `Design.IsDesignMode` is true, `Motion.Preference` shall do nothing.

New subsection **Following a change**:

- When an engine raises `ReducedMotionChanged`, each bridge trigger or state with a live animation on that engine shall replay when the form its builder resolves to now differs from the form playing and either form loops forever.
- A form shall differ when the builder has a `WhenReduced` form and `ReducedMotion` changed, or when `ReduceAll` changed and the playing form is not a reduced alternative.
- A live animation whose forms are both finite shall finish in the form it started with.
- A trigger or state shall be registered for the signal only while its element is attached and its animation is live.
- `Presto.Animate`, `PrestoAvalonia.Animate`, `AnimateOnCompositor` and their extension forms, on either thread, shall keep the form they were built with.

Change **BRIDGE-REDUCED-012** to: "When the flag changes, the bridge shall act as Following a change states; every other trigger shall read the flag when it fires." Change **BRIDGE-REDUCED-014** to: "A `Motion` state whose values were read under the other setting shall re-read them at its next transition; a held state keeps its values until it is left, unless Following a change replays it." Decisions: strike **The flag has no change notification** (replaced by a pointer to CORE-REDUCED's signal decisions) and add every bridge decision above. Non-goals: change "A change event on `PrestoDefaults`" to "A change event on `PrestoDefaults` itself (see CORE-REDUCED)"; add "Re-forming caller-owned animations" and "Propagating the signal into compositor scenes".

**BRIDGE-ATTACH** (`docs/specs/avalonia-bridge/xaml-attach.md`):

- (OnAttached) When the flags change while the visual is attached, `OnAttached` shall revert its animation, whether running or holding its end values, and play the current builder from its start, when Following a change (BRIDGE-REDUCED) asks for a replay.
- (Shared trigger rules) When the flags change while an `OnTapped` or `OnChanged` animation is running, `Animate` shall revert it and play the builder from its start, when Following a change asks for a replay.

Change the Reduced motion section to name the replay. Add the decision pointer "**Persistent motion follows a flag change; finite motion finishes in its form.** See BRIDGE-REDUCED."

**BRIDGE-MOTION** (`docs/specs/avalonia-bridge/motion-states.md`): change **BRIDGE-MOTION-056** to: "A state shall play its builder's `WhenReduced` form while `PrestoDefaults.ReducedMotion` is set, re-reading a state whose values were read under the other setting at its next transition, and, when the flags change while the state is held or playing and Following a change (BRIDGE-REDUCED) asks for a replay, playing the new form for the properties it holds from where they stand." Give the decision **The tracker re-reads a state when the reduced flag has flipped** its reason ("a held finite state rests, so its next transition is soon enough; a looping one is replayed, see BRIDGE-REDUCED") in place of "not recorded".

**BRIDGE-THREAD** (`docs/specs/avalonia-bridge/animation-thread.md`), under Reduced motion:

- A compositor animation shall keep the form it was compiled with when the flags change.

**BRIDGE-PULL** (`docs/specs/avalonia-bridge/pull-to-refresh.md`), under Reduced motion:

- When `ReducedMotion` becomes set during a refresh, the ring shall stop spinning at once; when it clears during a refresh, the ring shall start spinning.

**BRIDGE-DISTORT** (`docs/specs/avalonia-bridge/distort.md`): add "When the flags change, the control shall redraw." Strike the edge case "Toggling `ReducedMotion` … does not redraw by itself".

**BRIDGE-SCENES** (`docs/specs/avalonia-bridge/compositor-scenes.md`): in Reduced motion, add the reason: the scene engine ticks off the UI thread, so the main engine's signal does not reach it (see Compositor scenes keep their snapshot, BRIDGE-REDUCED).

## Tasks

1. Core: `Engine.ReducedMotionChanged`, the reference-keyed link from an engine to its own defaults (the main engine's and each `new Engine`'s starting copy), the raising setters for `ReducedMotion` and `ReduceAll`, and `MotionPreference`. Tests (`ReducedMotionTests`): `ChangingEitherFlagRaisesTheSignalOnceWithTheEngine`, `WritingTheCurrentValueRaisesNothing`, `AWithCopyAndAScopesDefaultsRaiseNothing`, `ANewEnginesCopyRaisesOnTheNewEngineOnly`, `ACopyOfAnEnginesDefaultsStillEqualsIt`, `TheSignalLeavesARunningAnimationInItsForm` (pins CORE-REDUCED-003, untested today).
2. Bridge preference: `PrestoAvalonia.MotionPreference`, `SystemReducedMotionProbe`, and the `Motion.Preference` attached property. Tests (`MotionPreferenceTests`, new, headless, resetting the preference to `null` after each test): `FullAndReducedWriteTheFlagAndStopFollowing`, `SystemFollowsTheProbeUntilTheChoiceChanges`, `NullStopsFollowingAndLeavesTheFlag`, `SettingTheSameChoiceAgainKeepsTheFollower`, `ThePreferenceNeverWritesReduceAll`, `ChangingTheProbeUnderSystemReadsAgain`, `ABindingToSystemFromXamlStartsFollowing` (the default-value trap), `ClearingOrDetachingTheAttachedPropertyLeavesThePreference`, `DesignModeIgnoresTheAttachedProperty`, `ThePreferenceGovernsTheMainEngineInsideAScope`. Add `SystemReducedMotionTests.AFollowedChangeRaisesTheEngineSignal`.
3. Bridge reaction: the per-engine subscription and the live-trigger registry, `Animate` (`OnAttached`, running `OnTapped`/`OnChanged`) and the `Motion` tracker replaying under the persistence rule. Tests: `AnimateAttachedTests.ALoopingOnAttachedReplaysItsReducedFormWhenTheFlagIsSet`, `…ASettledReducedFormLoopsAgainWhenTheFlagClears`, `…AFiniteOnAttachedFinishesInItsFormWhenTheFlagFlips`, `…ABuilderWithoutAReducedFormDoesNotRestartOnAReducedMotionFlip`, `…AReduceAllFlipReplaysALoopCollapsed`, `…ADetachedTriggerIsNotRegistered`, `…ARunningLoopingTapAnimationFollowsTheFlag`; `MotionStateTests.AHeldLoopingStateReplaysInItsNewFormWhenTheFlagFlips`, `…AHeldFiniteStateKeepsItsValuesUntilLeft`; `CompositorAnimationTests.ACompositorLoopKeepsItsFormWhenTheFlagFlips`; `BridgeAllocationTests.ASignalWithNothingLiveAllocatesNothing`.
4. Bridge controls: `PullToRefresh` stops or starts its spin, and `Distort` invalidates, on the signal. Tests: `PullToRefreshTests.SettingReducedMotionMidRefreshStopsTheSpin`, `…ClearingItMidRefreshStartsTheSpin`; `DistortTests.AFlagChangeRedrawsAtOnce`.
5. Update the specs listed above (requirements with `Tests:` lines, Decisions, types, sources, Non-goals, Open items), and let `SpecTests` and `SpecCoverageTests` pass.
6. Tempo: replace `Model/MotionPreference.cs` with the core enum (same order, so `TempoStore` data keeps its meaning); delete `App.ApplyMotionSetting` and `s_following`; bind `presto:Motion.Preference="{Binding Motion}"` on `MainWindow`; delete `SettingsFlyout.ReplayPreview` and its subscription; in `ShellView`, replace `RedriftOnMotionChange` with a handler on `Presto.Engine.ReducedMotionChanged`, subscribed while loaded. `TempoTestBase.ShowAsync(reduced)` sets `vm.Motion` (Full or Reduced) instead of writing the flag, and the test app sets a fixed `SystemReducedMotionProbe`. Tests: `SettingsTests.TheMotionPreviewFadesOnceTheChoiceIsReduced` without the `ApplyMotionSetting` call, new `SettingsTests.TheMotionPreviewLoopsAgainOnceTheChoiceIsFull`, `MainWindowTests.TheMotionSettingReachesTheFlagThroughTheWindowBinding`, `MainWindowTests.AnOsChangeUnderSystemRedriftsTheBackdrop` (flip the probe, call `RefreshSystemReducedMotion`).
7. Gallery: delete `ViewModels/MotionPreference.cs` and use the core enum (`LaunchOptions` included); make `MainViewModel.Motion` a plain observable property initialized to `Full`; bind `presto:Motion.Preference="{Binding Motion}"` on `MainView`; reorder the `MotionPicker` items to System / Full / Reduced to match `SelectedIndex`; update `GalleryNav`'s help text. Tests: `GalleryPageTests.TheMotionPickerSetsReducedMotionAndItSurvivesNavigation` (Reduced is now index 2) and `TheOsPreferenceIsShownWhicheverChoiceIsPicked` reset the preference rather than the flag; check that page tests which set the flag directly are not overwritten by a `MainView` binding.
8. Docs: ARCHITECTURE §5 (the code-only exceptions list loses the motion-preview replay and `App.ApplyMotionSetting`; "The motion setting previews itself" and "Reduced motion is a first-class setting" describe the binding and the replay), Tempo's README code-only list, and the README's Reduced motion section and Framer Motion mapping row (`reducedMotion="user"` maps to `Motion.Preference="System"`).
9. Visual check on the Desktop heads: Tempo's settings flyout switching between Full, Reduced and System, with the dot and the backdrop drift changing at once, then toggling Windows' "Animation effects" under System and activating the window; the Gallery with `--motion system` and the same OS toggle on a page with an `OnAttached` loop.

## Open questions

None.
