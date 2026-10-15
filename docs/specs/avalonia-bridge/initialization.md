---
id: BRIDGE-INIT
title: Bridge initialization, frame source and hidden signal
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/AvaloniaFrameSource.cs
  - MonacoLabs.Presto.Avalonia/AppBuilderExtensions.cs
  - MonacoLabs.Presto.Avalonia/PrestoAvalonia.cs
  - MonacoLabs.Presto.Avalonia/Platform/ActivationHook.cs
  - MonacoLabs.Presto.Avalonia/Platform/HiddenSignal.cs
  - MonacoLabs.Presto.Avalonia/Platform/InertiaWake.cs
types:
  - AppBuilderExtensions
  - AvaloniaFrameSource
tests:
  - AvaloniaFrameSourceTests
  - PrestoAvaloniaTests
  - LifetimeHiddenTests
  - LifetimeSpikeTests
  - InertiaWakeTests
  - AnimationBuilderBridgeTests
  - CodecTests
  - WhenDetachedTests
upstream:
  - src/engine/engine.js
  - tests/suites/engine.test.js
depends:
  - CORE-ENGINE
  - CORE-PRESTO
  - BRIDGE-TARGETS
  - BRIDGE-CODECS
  - BRIDGE-HELPERS
---

# Bridge initialization, frame source and hidden signal

## Purpose

Installs the bridge into the core's statics: points `Presto.Engine` at an `AvaloniaFrameSource`, registers the Avalonia target adapter (BRIDGE-TARGETS) and value codecs (BRIDGE-CODECS), sets `Engine.Hidden` while the app cannot be seen, and hooks the detach tracker (BRIDGE-HELPERS). The frame source is the bridge's `requestAnimationFrame`: it pumps the engine (CORE-ENGINE) from `TopLevel.RequestAnimationFrame`, or from a render-priority `DispatcherTimer` while no `TopLevel` can be found. While a touch scroll coasts, the bridge also runs the dispatcher's `Input`-priority jobs itself, working around an Avalonia Android dispatcher stall. Also specifies `PrestoAvalonia.EnsureInitialized`.

## Requirements

### EnsureInitialized

- **BRIDGE-INIT-001** — The first time `PrestoAvalonia.EnsureInitialized` is called, it shall set `Presto.Engine.FrameSource` to a new `AvaloniaFrameSource`, replacing whatever source the engine had.
  - Tests: `PrestoAvaloniaTests.EnsureInitializedInstallsOneAvaloniaFrameSource`
- **BRIDGE-INIT-002** — When `EnsureInitialized` has already run, a later call shall do nothing, leaving the installed frame source, adapters, codecs and hooks as they are.
  - Tests: `PrestoAvaloniaTests.EnsureInitializedInstallsOneAvaloniaFrameSource`
- **BRIDGE-INIT-003** — The first call shall append the `AvaloniaObjectTargetAdapter` and the compositor scratch adapter (BRIDGE-THREAD) to `Presto.Adapters`.
  - Tests: `AnimationBuilderBridgeTests.AnimatingAControlWithACoreOnlyBuilderInstallsTheBridge`
- **BRIDGE-INIT-004** — The first call shall register codecs in `Presto.Codecs` for `Color`, `IBrush`, `BoxShadows`, `Geometry`, `Thickness`, `CornerRadius`, `Point`, `Size`, `Rect`, `Vector`, `RelativePoint` and `GridLength`, overwriting any codec registered earlier for those types.
  - Tests: `CodecTests.GeometryStructsInterpolateEveryChannel`, `CodecTests.ColorTweensFromAHexStringAndFromATypedColor`
- **BRIDGE-INIT-005** — The first call shall install the hidden signal (BRIDGE-INIT-020 to BRIDGE-INIT-027).
  - Tests: `LifetimeHiddenTests.MinimizingTheOnlyWindowPausesAndRestoringResumesAtTheSameProgress`
- **BRIDGE-INIT-006** — The first call shall set the engine's internal timer-created hook to the detach tracker, replacing any hook set before, so `WhenDetached` applies to animations created afterwards (BRIDGE-HELPERS).
  - Tests: `WhenDetachedTests.ADetachedLoopPausesAndTheEngineIdles`
- **BRIDGE-INIT-007** — The bridge shall call `EnsureInitialized` itself from the `AvaloniaAnimationParams` and `AvaloniaAnimatableParams` constructors, every `AnimateExtensions` entry point, every `PropertyBuilderExtensions` method and the `PrestoAvalonia` factories, so no explicit setup is needed.
  - Tests: `AnimationBuilderBridgeTests.AnimatingAControlWithACoreOnlyBuilderInstallsTheBridge`
- **BRIDGE-INIT-008** (inherited) — `EnsureInitialized` shall configure only `Presto.Engine`; an `Engine` constructed by the application keeps the frame source it was given.
  - Tests: untested
- **BRIDGE-INIT-009** — `UsePresto` shall register an `AfterSetup` callback that calls `EnsureInitialized`, and return the same `AppBuilder` for chaining.
  - Tests: manual — the Desktop, Browser and Android sample heads call it; check that a page animates on first show.

### Frame source

- **BRIDGE-INIT-010** — `Now` shall return the milliseconds elapsed since the source was constructed, read from its `TimeProvider` (the system clock by default), so it is valid outside a frame callback.
  - Tests: `AvaloniaFrameSourceTests.NowFollowsTheTimeProvider`
- **BRIDGE-INIT-011** (inherited) — When `Start` is called while already started, the source shall ignore the call and keep the first callback.
  - Tests: untested
- **BRIDGE-INIT-012** — When the source needs a frame, it shall take the `TopLevel` from the application lifetime: a classic desktop lifetime's `MainWindow`, else its first window; a single-view lifetime's `MainView` top level.
  - Tests: manual — headless tests have no application lifetime (`LifetimeSpikeTests.TheHeadlessPlatformOffersNoActivatableLifetimeAndNoApplicationLifetime`); check on the Desktop sample.
- **BRIDGE-INIT-013** — When the lifetime yields no `TopLevel` (no lifetime, an activity lifetime, no window yet), the source shall use the `TopLevel` of the last `Visual` the target adapter resolved a property on, and when that visual has none (detached), the last `TopLevel` it found that way; it holds both weakly.
  - Tests: `AvaloniaFrameSourceTests.FramesComeFromTheTopLevelOfAnAnimatedControl`, `AvaloniaFrameSourceTests.ADetachedAnimatedVisualKeepsThePumpOnTheLastTopLevel`
- **BRIDGE-INIT-014** — While started with a `TopLevel`, the source shall call `RequestAnimationFrame` once, invoke the callback when it fires, and request the next frame after the callback returns, re-resolving the `TopLevel` each time.
  - Tests: `AvaloniaFrameSourceTests.FramesComeFromTheTopLevelOfAnAnimatedControl`
- **BRIDGE-INIT-015** (inherited) — When `Start` follows a `Stop` while a frame request is still outstanding, the source shall reuse that request instead of adding a second, so each frame invokes the callback at most once.
  - Tests: `AvaloniaFrameSourceTests.RestartingReusesTheFrameAlreadyRequested`
- **BRIDGE-INIT-016** — While no `TopLevel` can be found, the source shall pump from a `DispatcherTimer` on `Dispatcher.UIThread` at `DispatcherPriority.Render` with a 1000/60 ms interval.
  - Tests: `AvaloniaFrameSourceTests.StartFallsBackToADispatcherTimerWhenNoTopLevelExists`
- **BRIDGE-INIT-017** — When a `TopLevel` can be found after a fallback frame, the source shall stop the timer and continue from `RequestAnimationFrame`.
  - Tests: `AvaloniaFrameSourceTests.TheFallbackTimerHandsThePumpOverOnceAnAnimatedVisualHasATopLevel`
- **BRIDGE-INIT-018** — After `Stop`, including a `Stop` from inside the callback, the source shall invoke the callback no more and shall stop the fallback timer.
  - Tests: `AvaloniaFrameSourceTests.FramesComeFromTheTopLevelOfAnAnimatedControl`, `AvaloniaFrameSourceTests.StartFallsBackToADispatcherTimerWhenNoTopLevelExists`
- **BRIDGE-INIT-019** (inherited) — When the frame callback throws, the exception shall propagate to the dispatcher, and unless the source was stopped it shall re-arm the pump from a `Render`-priority dispatcher post, on either delivery path.
  - Tests: `AvaloniaFrameSourceTests.AThrowingFrameCallbackDoesNotRetireTheTopLevelPump`, `AvaloniaFrameSourceTests.AThrowingFrameCallbackDoesNotRetireTheFallbackTimer`

### Hidden signal

- **BRIDGE-INIT-020** — While the classic desktop lifetime has at least one window and every one of them is `Minimized`, the bridge shall hold `Presto.Engine.Hidden` true.
  - Tests: `LifetimeHiddenTests.MinimizingTheOnlyWindowPausesAndRestoringResumesAtTheSameProgress`, `LifetimeHiddenTests.OneOfTwoWindowsMinimizedKeepsTicking`
- **BRIDGE-INIT-021** — The bridge shall re-evaluate the minimized state on every `Window.WindowState` change and, in a dispatcher post, on every window opened or closed.
  - Tests: `LifetimeHiddenTests.MinimizingTheOnlyWindowPausesAndRestoringResumesAtTheSameProgress`
- **BRIDGE-INIT-022** (inherited) — With no desktop lifetime, or one with no windows, the bridge shall treat the app as not minimized.
  - Tests: untested
- **BRIDGE-INIT-023** — When the application's `IActivatableLifetime` raises a `Background` deactivation, the bridge shall treat the app as backgrounded until a `Background` activation.
  - Tests: `LifetimeHiddenTests.BackgroundingPausesAndForegroundingResumesAtTheSameProgress`
- **BRIDGE-INIT-024** — The bridge shall ignore activations and deactivations of any kind other than `Background` (`File`, `OpenUri`).
  - Tests: `LifetimeHiddenTests.FileAndOpenUriActivationsChangeNothing`
- **BRIDGE-INIT-025** — `Hidden` shall be true while the app is minimized or backgrounded, and false only once both have cleared.
  - Tests: `LifetimeHiddenTests.MinimizedAndBackgroundedStaysHiddenUntilBothClear`
- **BRIDGE-INIT-026** — The bridge shall write `Presto.Engine.Hidden` only when the combined state changes, so a `Background` activation while nothing is hidden changes nothing.
  - Tests: `LifetimeHiddenTests.AnInitialBackgroundActivationWithNothingHiddenChangesNothing`
- **BRIDGE-INIT-027** — The bridge shall read the minimized state once when the signal is installed, and look up the application's `IActivatableLifetime` then and again on every `EnsureInitialized` call, frame-source `Start` and reduced-motion read or refresh (BRIDGE-REDUCED-008) until it finds one, and subscribe to that one once.
  - Tests: `LifetimeHiddenTests.ALifetimeFoundAfterInstallIsHookedOnceFromTheNextEntryPoint`
- **BRIDGE-INIT-028** — What `Hidden` does to timers (pause, resume without a jump, no effect without `PauseOnHidden`) shall be the core engine's behavior (CORE-ENGINE).
  - Tests: `LifetimeHiddenTests.WithoutPauseOnHiddenAMinimizedWindowKeepsTicking`, `LifetimeHiddenTests.AThenStartedBeforeHidingCompletesAfterShowing`
- **BRIDGE-INIT-029** — When the hidden state changes, an attached `CompositorCanvas` shall forward it to its scene's engine (BRIDGE-SCENES).
  - Tests: `LifetimeHiddenTests.ASceneEnginePausesWhileHiddenAndResumesWhereItWas`, `LifetimeHiddenTests.ACanvasDetachedWhileHiddenIsCollectable`

### Inertia wake

- **BRIDGE-INIT-030** — From a `ScrollGestureInertiaStarting` event until every gesture that started coasting has raised `ScrollGestureEnded` with its id, the bridge shall run a `DispatcherPriority.Default` `DispatcherTimer` every 16 ms that runs the dispatcher's jobs of `Input` priority and above (`Dispatcher.RunJobs(DispatcherPriority.Input)`).
  - Tests: `InertiaWakeTests.ACoastingScrollWakesTheUIThreadUntilItsGestureEnds`, `InertiaWakeTests.OverlappingGesturesWakeUntilTheLastEnds`
- **BRIDGE-INIT-031** — Ten seconds after the last inertia start, the bridge shall stop the timer and forget the coasting gestures, even if no ended event arrived.
  - Tests: manual — ten seconds of real time; the cap only guards a gesture Avalonia never ends.

## Invariants

- At most one frame request is outstanding per source, and at most one of the two delivery paths (request or timer) is live.
- `Now` is monotonic and independent of whether frames are being delivered.
- After `EnsureInitialized`, `Presto.Engine.Hidden` equals "minimized or backgrounded" as last observed.

## Edge cases

- Android's activity lifetime publishes no view, so the timer pumps until the first visual is animated (BRIDGE-INIT-013).
- A minimized Win32 window keeps delivering animation frames (`LifetimeSpikeTests.AMinimizedHeadlessWindowStillDeliversAnimationFrames` shows the headless equivalent); stopping them is the core's job once `Hidden` is set.
- The `TimeSpan` passed by `RequestAnimationFrame` is ignored; frame time is always `Now`.
- A frame source constructed by the app pumps from the same last-animated visual and last top level as the installed one, since both are static.

## Threading and lifetime

- UI thread only: the pump, the timer and the hidden-signal handlers run on `Dispatcher.UIThread`, and the source has no locks.
- The last animated visual and the last top level are held through `WeakReference`s, so the source never keeps a control or window alive.
- The hidden signal's class handlers are static and live for the process.

## Reduced motion

- Not affected: initialization does not read or set reduced motion (see BRIDGE-REDUCED for following the OS setting).

## Allocation

- The frame-request delegate is cached at construction; a frame allocates nothing in the source. The fallback timer and its handler are allocated once per fallback period.

## Provenance

- Upstream ticks from `requestAnimationFrame` in a browser and `setImmediate` elsewhere; here `TopLevel.RequestAnimationFrame` with a `DispatcherTimer` fallback, since `RequestAnimationFrame` is the host's equivalent and needs a `TopLevel` that may not exist yet.
- Upstream's `visibilitychange` listener becomes the hidden signal, driven by minimized windows and `Background` activations, because desktop minimize is visible only through `WindowState` and mobile or Browser backgrounding only through `ActivationKind.Background` (see CORE-ENGINE for what `Hidden` does).

## Decisions

- **The frame source is `RequestAnimationFrame`, with a timer fallback.** `AvaloniaFrameSource` re-requests `TopLevel.RequestAnimationFrame` each frame while the engine has children, from any live `TopLevel`, and pumps from a render-priority `DispatcherTimer` while none exists; it is installed on first use, or by `UsePresto()`. Reason: `RequestAnimationFrame` ticks in step with the host's rendering, once per drawn frame, the timer covers the time before a `TopLevel` exists, and any `TopLevel` works because the request is a one-shot on the global `MediaContext`.
- **Without a lifetime `TopLevel`, the pump takes one from an animated visual.** On Android (and wherever the lifetime yields none) the frame source resolves its `TopLevel` from a visual the bridge animates, the last one a property was resolved on; the timer pumps until then. The handover is tested through a seeded visual. Once found, that `TopLevel` is kept for when the last animated visual is detached. Reason: `IActivityApplicationLifetime` publishes only a `MainViewFactory` (`MainView` stays null) and `ISingleTopLevelApplicationLifetime` is `[PrivateApi]`; lifetime interfaces are `[NotClientImplementable]`, so a test cannot fake one. The kept `TopLevel` is needed because an animated visual is often detached (a measuring `TextBlock`, a page navigated away from), and on Android the 60 Hz render-priority fallback timer keeps the main looper from going idle, which is the only time Avalonia runs `Input`-priority jobs: touch scroll inertia stalled until it was kept.
- **The bridge runs `Input`-priority jobs while a touch scroll coasts.** `InertiaWake` runs `Dispatcher.RunJobs(DispatcherPriority.Input)` from a 16 ms `Default`-priority `DispatcherTimer` between a scroll gesture's inertia start and its end. Reason: in Avalonia 12.1 each inertia step is an `Input`-priority job, and the Android dispatcher runs those only while `MessageQueue.IsIdle` holds; on the Android 16 test device that is false from the UI thread whenever any message is queued, even a timer due later, so a fling stalled (11 of 18 adb flings on Activity feed) while any `DispatcherTimer` was armed, and renders fell to `Input` priority after Avalonia's one-second starvation guard. A `Default` timer runs above that check. It lives in the bridge because every Android app on Presto hits it, and it only runs while a gesture coasts. Rejected: re-requesting animation frames (renders starve with the inertia step) and waking the looper from another thread (the woken dispatcher still skips `Input` jobs). Remove once Avalonia checks pending input reliably.
- **The activation lifetime is looked up again from the entry points until found.** One hook, shared by the hidden signal and the reduced-motion refresh, retries the `IActivatableLifetime` lookup from `EnsureInitialized`, the frame source's `Start` and each reduced-motion read or refresh, and stops once it has subscribed. Reason: `Application.Current` is bound later by `AppBuilder`, so bridge use from a static initializer, `Program.Main` or the `App` constructor found no lifetime and never heard `Background` (de)activations; the retry costs a null check and one feature lookup. Rejected: a dispatcher post at install, which would race headless and static use, where no dispatcher turn may follow before the app is bound.
- **Bridge builder extensions self-initialize.** See BRIDGE-HELPERS.
- **Hidden pauses the engine.** See CORE-ENGINE.
- **Wall-clock reads take a `TimeProvider`.** See CORE-ENGINE.

## Non-goals

- No per-window or per-`TopLevel` engines: one engine is pumped from any one live `TopLevel`.
- No configurable fallback rate.

## Open items

- `UsePresto` has no test (BRIDGE-INIT-009).
- The lifetime-based `TopLevel` choice (BRIDGE-INIT-012) and the open/close re-evaluation of the hidden signal are untested: the headless platform has no application lifetime.
- The frame-source and Android decisions were recorded as resolving from "the first animated control"; the code keeps the last visual any property was resolved on, so the Decisions record the code.
