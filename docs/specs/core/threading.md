---
id: CORE-THREADING
title: Engine instancing and threading
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Engine.cs
  - MonacoLabs.Presto/EngineScope.cs
  - MonacoLabs.Presto/Core/Clock.cs
  - MonacoLabs.Presto/Core/Globals.cs
  - MonacoLabs.Presto/Timer.cs
  - MonacoLabs.Presto/Scope.cs
types:
  - EngineScope
tests:
  - EnginesTests
  - IntrospectionTests
upstream: []
depends:
  - CORE-ENGINE
  - CORE-TIMER
  - CORE-SCOPE
  - CORE-COMPOSITION
  - CORE-CALLBACKS
---

# Engine instancing and threading

## Purpose

The core is single-threaded per engine, like anime.js, but a process may run several engines, one per thread that ticks it. This spec fixes how a call finds its engine (`Engine.Current`, `MakeCurrent`), how a timer stays bound to the engine it was created on, what state is per engine and what stays process-wide, and the Debug-only thread pin. Engine behavior itself is CORE-ENGINE. Also specifies `Engine.Current`, `Engine.MakeCurrent` and `Timer.Engine`.

## Requirements

### Ambient engine

- **CORE-THREADING-001** — `Engine.Current` shall return the engine that `MakeCurrent` last installed on the calling thread, or `Presto.Engine` when none is installed.
  - Tests: `EnginesTests.CurrentIsTheMainEngineUntilMakeCurrentNestsOne`
- **CORE-THREADING-002** — When the `EngineScope` returned by `MakeCurrent` is disposed, the engine that was current on that thread when the scope was taken shall become current again, so scopes nest.
  - Tests: `EnginesTests.CurrentIsTheMainEngineUntilMakeCurrentNestsOne`
- **CORE-THREADING-003** — `MakeCurrent` on one thread shall not change `Engine.Current` on any other thread.
  - Tests: `EnginesTests.ATimerTickedOnAWorkerThreadStaysOnItsOwnEngine`
- **CORE-THREADING-004** (inherited) — When a default `EngineScope` is disposed, `Engine.Current` on that thread shall fall back to `Presto.Engine`.
  - Tests: `EnginesTests.DisposingADefaultScopeFallsBackToTheMainEngine`
- **CORE-THREADING-005** — While `Scope.Execute` runs its callback, the scope's engine shall be current on the calling thread, and the previous engine shall be restored when the callback returns or throws.
  - Tests: `EnginesTests.ScopeExecuteMakesItsOwnEngineAmbientOutsideAMakeCurrentBlock`

### Engine capture

- **CORE-THREADING-006** — When a root timer, animation or timeline is constructed, it shall bind to `Engine.Current` and register with that engine when it plays.
  - Tests: `EnginesTests.TimersCreatedUnderAnEngineRegisterWithItAndNotTheMainEngine`
- **CORE-THREADING-007** — When a timeline child is constructed, it shall bind to its parent's engine, whatever engine is current.
  - Tests: `EnginesTests.ATimelineChildBindsToItsTimelinesEngineWhateverEngineIsCurrent`
- **CORE-THREADING-008** — A timer shall resolve every later operation (resume, rebase, tick time, defaults for its children) through the engine it bound to, not through the engine current at the call.
  - Tests: `EnginesTests.ResumingAfterTheBlockEndsReRegistersWithTheEngineTheTimerWasCreatedUnder`
- **CORE-THREADING-009** — `Timer.Engine` shall return the engine the timer bound to at construction.
  - Tests: `IntrospectionTests.TimerGettersMatchTheParameters`, `EnginesTests.ATimerTickedOnAWorkerThreadStaysOnItsOwnEngine`
- **CORE-THREADING-010** — A `Scope` and a source `MotionValue` from `Presto.CreateMotionValue` shall bind to `Engine.Current` when constructed.
  - Tests: `EnginesTests.ScopeExecuteMakesItsOwnEngineAmbientOutsideAMakeCurrentBlock`

### Per-engine state

- **CORE-THREADING-011** — Two engines with their own frame sources shall tick independently: advancing one shall not move the other's timers.
  - Tests: `EnginesTests.AdvancingOneEngineLeavesTheOtherWhereItWas`
- **CORE-THREADING-012** — Each engine shall own its `Defaults`; changing one engine's defaults shall not change another's.
  - Tests: `EnginesTests.DefaultsStartAsACopyOfTheMainEnginesAndThenDiverge`
- **CORE-THREADING-013** — When an engine is constructed, its defaults shall be copied from `Presto.Defaults`, even inside another engine's `MakeCurrent` block.
  - Tests: `EnginesTests.AnEngineCreatedInsideAnotherEnginesBlockCopiesTheMainEnginesDefaults`
- **CORE-THREADING-014** — Each engine shall own its current scope, so `Scope.Execute` swaps only its own engine's scope and defaults.
  - Tests: `EnginesTests.ScopeExecuteSwapsOnlyItsOwnEnginesDefaults`
- **CORE-THREADING-015** — Each engine shall own its additive lookups and additive animation, so additive tweens on one engine are invisible to another.
  - Tests: `EnginesTests.AdditiveTweensAreInvisibleToAnotherEngine`
- **CORE-THREADING-016** (inherited) — When an engine is constructed while seconds mode is on, it shall start at the same internal speed as the main engine, so it plays durations in seconds.
  - Tests: `EnginesTests.AnEngineCreatedInSecondsModeRunsAtTheSameSpeedAsTheMainOne`

### Process-wide state

- **CORE-THREADING-017** — Setting `TimeUnit` on the main engine shall change the time scale and forced-render threshold for every engine in the process.
  - Tests: `EnginesTests.TheMainEnginesTimeUnitSetsTheTimeScaleAndTickThresholdOfEveryEngine`
- **CORE-THREADING-018** — Setting `Precision` on any engine shall change the reported precision for every engine in the process.
  - Tests: `EnginesTests.PrecisionSetOnAnyEngineAppliesToEveryEngine`
- **CORE-THREADING-029** — When `TimeUnit` is set on an engine other than `Presto.Engine`, the engine shall throw `InvalidOperationException` and change nothing, even when the unit is already in force.
  - Tests: `EnginesTests.SettingTheTimeUnitOnAnotherEngineThrowsAndLeavesTheTimeScale`
- **CORE-THREADING-019** — Each engine shall keep its own replace-composition sibling lists, so animations of one target property on two engines never replace each other.
  - Tests: `EnginesTests.ReplaceTweensDoNotComposeAcrossEngines`
- **CORE-THREADING-020** — `Presto.Adapters` and `Presto.Codecs` shall be shared by every engine and read without locks, so they must be filled before any engine animates.
  - Tests: `EnginesTests.ACodecRegisteredOnceServesAnimationsOnEveryEngine`
- **CORE-THREADING-021** — Timer, animation and tween id counters and the `Utils` random seed counter shall be incremented atomically, so ids stay unique across threads.
  - Tests: `EnginesTests.IdsAndRandomSeedsStayUniqueAcrossThreads`
- **CORE-THREADING-022** — The scratch decomposition buffers used while building animations and reading values shall be per thread.
  - Tests: `EnginesTests.AnimationsBuiltOnSeveralThreadsAtOnceResolveTheirOwnValues`
- **CORE-THREADING-023** — The ease parser cache and the reflection accessor cache shall be safe for concurrent use.
  - Tests: `EnginesTests.TheEaseAndAccessorCachesFillFromSeveralThreadsAtOnce`

### Thread pin

- **CORE-THREADING-024** — In a Debug build of the library, an engine shall pin itself to the thread of its first `Update`.
  - Tests: `EnginesTests.APinnedEngineAndItsTimersFailFromAnotherThread`
- **CORE-THREADING-025** — In a Debug build, once pinned, the engine's `Pause`, `Resume`, `Freeze`, `Thaw`, `Speed`, `TimeUnit`, `Precision`, `Hidden` and `PauseOnHidden` setters and its timers' `Pause`, `Resume`, `Seek` and `Reset` shall fail a debug assertion when called from another thread.
  - Tests: `EnginesTests.APinnedEngineAndItsTimersFailFromAnotherThread`, `EnginesTests.ThePinnedMainEngineRejectsATimeUnitChangeFromAnotherThread`
- **CORE-THREADING-026** (inherited) — When `FrameSource` is replaced, the engine shall drop its thread pin and re-pin on its next `Update`.
  - Tests: `EnginesTests.ReplacingTheFrameSourceDropsThePinUntilTheNextUpdate`
- **CORE-THREADING-027** — In a Release build of the library, no thread affinity shall be checked.
  - Tests: untested

### Cross-thread use

- **CORE-THREADING-028** — A timer created and ticked on a worker thread under that thread's engine shall complete there without touching `Presto.Engine`.
  - Tests: `EnginesTests.ATimerTickedOnAWorkerThreadStaysOnItsOwnEngine`

## Invariants

- A timer's engine never changes after construction.
- `Engine.Current` is never null.
- An engine's children, scope, defaults and composition state are touched only from the thread that ticks it.

## Edge cases

- `EngineScope.Dispose` restores the engine it recorded without checking order, so disposing nested scopes out of order leaves a stale engine current; disposing the same scope twice restores the same engine twice.
- A timer created on the main engine and touched from a worker thread is not marshaled; in Debug it fails the pin, in Release it races.

## Threading and lifetime

- One engine per ticking thread, no locks. Whoever owns a thread installs its engine with `MakeCurrent`, as `Scope.Execute` installs defaults, so the `Presto` factories keep upstream's call shape without an engine parameter.
- `Then()` completes its task with continuations run asynchronously, so awaiting a timer on another engine resumes off that engine's tick (CORE-CALLBACKS).
- An engine retired for good is dropped by its owner; nothing static holds an engine except `Presto.Engine` and the thread-static current slot.

## Reduced motion

- Not affected: reduced motion is a per-engine default (CORE-REDUCED); this spec only says defaults are per engine.

## Allocation

- `EngineScope` is a struct, so `using (engine.MakeCurrent())` allocates nothing on a per-frame path.
- The thread pin compiles out of Release builds.

## Provenance

- No upstream counterpart: anime.js has one module-level engine. Presto makes engines instantiable so a render-thread scene can own one, the only way the full timing model keeps running while the UI thread is stalled.

## Decisions

- **The engine is ambient, captured per timer.** `Engine.Current` is thread-static and falls back to `Presto.Engine`, `MakeCurrent()` installs another engine for a block, and every timer captures its engine at construction (its parent's, else the ambient one) and uses it afterwards; there is one engine per ticking thread and no locks. Reason: whoever owns a thread sets its engine, as `Scope.Execute` installs defaults, so the factories keep anime.js's call shape, and capturing at construction means a call from another thread on a timer never resolves to the wrong clock. Rejected: an engine parameter on every factory, which breaks the anime.js call shape.
- **Defaults, scope and composition state are per engine; a few statics are process-wide and documented, not locked.** Each engine owns its `Defaults` (a copy of the main engine's at construction, the snapshot rule `Scope` applies), current scope, children, replace-composition lookups and additive table. The time scale, precision, tick threshold and `Presto.Adapters`/`Presto.Codecs` are process-wide, to be configured before a second engine exists. The time scale is set through the main engine's `TimeUnit` only (see "TimeUnit is set on the main engine only"). Reason: the process-wide values are read on hot and construction paths and written once at initialization; composition is per engine because its start-time offsets are on each engine's own clock and a replace that cancels another engine's timer would do so from the wrong thread. Rejected: process-wide replace lookups, as upstream's single engine has.
- **TimeUnit is set on the main engine only.** Setting `TimeUnit` on any other engine throws, even to the unit already in force. Reason: it changes the process-wide time scale but rescaled only the engine it was set on, leaving the main engine's speed and default duration out of step; throwing on every set catches the misuse wherever it is written. Rejected: a silent no-op for the unit already in force, which would hide the misuse until the unit changes.
- **Other shared statics are made thread-safe.** Scratch decomposition values are thread-static, id counters are interlocked, the accessor and ease-parser caches are locked or concurrent, and a `Spring` stays single-threaded (one animation each). Reason: a second ticking thread now reaches these statics.
- **Each engine pins its thread in Debug.** An engine arms a Debug-only thread pin on its first `Update`. Reason: not recorded.
- **Render-thread work is a separate API named "Compositor", and a scene owns its engine.** See BRIDGE-SCENES.
- **`Then()` completes asynchronously.** See CORE-CALLBACKS.

## Non-goals

- No locking or marshaling of calls between threads.
- No engine parameter on the `Presto` factories.
- No per-engine `TimeUnit` or `Precision`.

## Open items

- CORE-THREADING-027 (no thread check in a Release build) has no test: the test suite builds the library in Debug, where the pin is compiled in.
