---
id: CORE-DIAGNOSTICS
title: Engine diagnostics
package: Monaco.Presto
namespaces:
  - Monaco.Presto.Diagnostics
sources:
  - Monaco.Presto/Diagnostics
  - Monaco.Presto/Engine.cs
  - Monaco.Presto/Timer.cs
  - Monaco.Presto/Scope.cs
  - Monaco.Presto/EaseParser.cs
types:
  - EngineDiagnostics
  - FrameStats
  - OriginScope
  - PrestoDiagnostics
tests:
  - EngineDiagnosticsTests
  - OriginTests
  - IntrospectionTests
  - AllocationTests
  - DevToolsListTests
upstream: []
depends:
  - CORE-ENGINE
  - CORE-TIMER
  - CORE-SCOPE
  - CORE-EASES
  - CORE-ANIMATABLE
  - CORE-MOTIONVALUE
---

# Engine diagnostics

## Purpose

Opt-in, pay-for-play observability of what an engine runs, for inspectors such as the DevTools overlay (DEVTOOLS-OVERLAY): a weak registry of root timers and animatables, the recently ended roots, the moving motion values, per-frame statistics, and who created each root (`Origin`, `CreationTrace`, `Scope`). With diagnostics off it costs one branch per timer created and one per engine update, and allocates nothing. The engine time base the overlay also uses (freeze, step, dilation) is specified in CORE-ENGINE; the read-only timer and tween getters in CORE-TIMER and CORE-ANIMATION; ease names in CORE-EASES. Also specifies `Engine.Diagnostics`, `Timer.Origin`, `Timer.CreationTrace` and `Timer.Scope`.

## Requirements

### Enabling

- **CORE-DIAGNOSTICS-001** — Each engine shall have its own `Diagnostics`, disabled by default.
  - Tests: `AllocationTests.CreatingATimerWithDiagnosticsOffNeverTouchesTheRegistry`
- **CORE-DIAGNOSTICS-002** — When `Enabled` turns on, the registry shall register the roots currently in the engine's list, and no paused, unplayed or finished ones.
  - Tests: `EngineDiagnosticsTests.EnablingRegistersTheRunningRoots`
- **CORE-DIAGNOSTICS-003** — When `Enabled` turns off, the registry shall forget its roots, animatables and recently ended roots and reset `LastFrame` to its default.
  - Tests: `EngineDiagnosticsTests.DisablingClearsEverything`
- **CORE-DIAGNOSTICS-004** — `PrestoDiagnostics.Active` shall be true while any engine has diagnostics enabled.
  - Tests: `EngineDiagnosticsTests.DisablingClearsEverything`
- **CORE-DIAGNOSTICS-005** (inherited) — Setting `Enabled` to its current value shall do nothing.
  - Tests: untested

### Registry

- **CORE-DIAGNOSTICS-006** — While enabled, every root timer created on the engine shall be registered in `Roots`, in creation order and in any state, including paused and never-played ones; timeline children are not.
  - Tests: `EngineDiagnosticsTests.PausedAndUnplayedRootsAreListed`
- **CORE-DIAGNOSTICS-007** — While disabled, creating a timer shall not register it or allocate its diagnostics state.
  - Tests: `AllocationTests.CreatingATimerWithDiagnosticsOffNeverTouchesTheRegistry`, `EngineDiagnosticsTests.EnablingRegistersTheRunningRoots`
- **CORE-DIAGNOSTICS-008** — `Roots` and `Animatables` shall hold their entries weakly, dropping collected ones, and return a new list on each read.
  - Tests: `EngineDiagnosticsTests.CollectedRootsDropOut`
- **CORE-DIAGNOSTICS-009** — While enabled, every animatable created on the engine shall be listed in `Animatables` (see CORE-ANIMATABLE).
  - Tests: `EngineDiagnosticsTests.DisablingClearsEverything`
- **CORE-DIAGNOSTICS-010** — `ActiveValues` shall be the engine's list of moving motion values (stepping springs and velocity samples), enabled or not.
  - Tests: untested
- **CORE-DIAGNOSTICS-011** — `TimerCreated` shall be raised once per registered root, after the factory has initialized it (so an autoplaying animation is already playing), and not for roots registered by enabling.
  - Tests: `EngineDiagnosticsTests.TimerCreatedFiresOncePerRootAfterInit`

### Recently ended

- **CORE-DIAGNOSTICS-012** — When a registered root completes and leaves the engine, is canceled, or is reverted, it shall be appended to `RecentlyEnded`.
  - Tests: `EngineDiagnosticsTests.CompletedCanceledAndRevertedRootsEnd`
- **CORE-DIAGNOSTICS-013** — `RecentlyEnded` shall keep the latest 32 roots, oldest first, dropping the oldest.
  - Tests: `EngineDiagnosticsTests.RecentlyEndedKeepsTheLatestInOrder`
- **CORE-DIAGNOSTICS-014** — When a root that is already listed ends again, it shall move to the end rather than appear twice.
  - Tests: `EngineDiagnosticsTests.ARootThatEndsAgainIsListedOnceAsTheLatest`
- **CORE-DIAGNOSTICS-015** (inherited) — A repeated `Cancel` of an already canceled root shall not end it again.
  - Tests: untested

### Frame statistics

- **CORE-DIAGNOSTICS-016** — While enabled, every engine update shall record `LastFrame`: the engine time, the delta, the real time the update took, the roots in the engine list after the update, the tweens they and their children own, the motion values stepped and the sinks flushed.
  - Tests: `EngineDiagnosticsTests.LastFrameCountsRootsTweensValuesAndSinks`
- **CORE-DIAGNOSTICS-017** — While disabled, an update shall not record `LastFrame`.
  - Tests: `EngineDiagnosticsTests.DisablingClearsEverything`

### Origin, creation trace and scope

- **CORE-DIAGNOSTICS-018** — While `Active`, `PrestoDiagnostics.Origin(tag)` shall tag the root timers created on the calling thread with `tag` until the returned handle is disposed, which restores the previous tag, so tags nest.
  - Tests: `OriginTests.TheTagAppliesInsideTheScopeAndNests`
- **CORE-DIAGNOSTICS-019** — While not `Active`, `Origin` shall return an empty handle that tags nothing, allocates nothing and does nothing on dispose.
  - Tests: `OriginTests.InactiveOriginAllocatesNothingAndTagsNothing`
- **CORE-DIAGNOSTICS-020** — `Timer.Origin` shall be the root's tag, the root's origin for a child, and null for an untagged root or one created while diagnostics were off.
  - Tests: `OriginTests.ChildrenInheritTheirRootsOrigin`, `OriginTests.TheTagAppliesInsideTheScopeAndNests`
- **CORE-DIAGNOSTICS-021** — The core shall tag its own roots: the additive composition animation as `"Additive"`, and an animatable's callbacks animation as `"Animatable"` unless an outer tag is set.
  - Tests: `OriginTests.TheAdditiveAnimationIsTagged`
- **CORE-DIAGNOSTICS-022** — When both the engine's diagnostics and `CaptureCreationTrace` are on, a root timer shall record the stack trace of its creation, with file information, in `CreationTrace`; otherwise it shall be null.
  - Tests: `OriginTests.CreationTraceNeedsBothFlags`
- **CORE-DIAGNOSTICS-023** — When a registered root is created inside a scope, `Timer.Scope` shall be that scope; otherwise null.
  - Tests: `OriginTests.TheScopeIsRecorded`

### Ease names

- **CORE-DIAGNOSTICS-024** — While `Active`, ease factories and the ease parser shall name the eases they create; while not, they shall not, and shall allocate no more for it.
  - Tests: `IntrospectionTests.EaseNamesForFactoriesAreKeptOnlyWhileDiagnosticsAreActive`, `AllocationTests.EaseFactoriesAndParserAllocateNoMoreWithDiagnosticsInactive`
- **CORE-DIAGNOSTICS-025** — When the first engine enables diagnostics, eases the parser cached earlier shall be named from their strings.
  - Tests: `IntrospectionTests.TheDefaultEaseAndEasesParsedBeforeEnablingAreNamed`

### Cost

- **CORE-DIAGNOSTICS-026** — Engine ticks shall stay allocation-free with diagnostics enabled.
  - Tests: `AllocationTests.MotionValueGraphFlushesWithoutAllocating`, `AllocationTests.ParticleSystemStepsWithoutAllocating`, `AllocationTests.ChannelMomentumTicksWithoutAllocating`

### Defaults

- **CORE-DIAGNOSTICS-027** — When `CaptureCreationTrace` is not set, it shall be off so no root records a creation trace.
  - Tests: untested

## Invariants

- `Roots` never extends a timer's lifetime; `RecentlyEnded` holds at most 32 strong references.
- A timer has diagnostics state (origin, trace, scope) only if it was registered as a root while enabled.
- `PrestoDiagnostics.Active` equals "the number of enabled engines is positive".

## Edge cases

- An `Animatable`'s property animation ends again on every settled `Set` and moves to the end of `RecentlyEnded` each time.
- A root created while diagnostics were off and still running when they are enabled is registered but has no origin, trace or scope.
- Disposing `OriginScope` handles out of order restores whatever tag each captured, not a stack.
- The default `OriginScope` (`default(OriginScope)`) disposes as a no-op.

## Threading and lifetime

- `EngineDiagnostics` is single-threaded like its engine; `Enabled`'s setter asserts thread affinity in debug builds.
- The origin tag is per thread (`[ThreadStatic]`); `Active` and `CaptureCreationTrace` are process-wide.
- An engine that is collected while enabled never decrements the enabled-engine count.

## Reduced motion

- Not affected: diagnostics observe and never change playback.

## Allocation

- Off: no allocation per timer or frame, 8 bytes per timer for the side-object field (measured when the feature landed).
- On: one side object and one weak reference per root; `Roots`/`Animatables` allocate a list per read; `CaptureCreationTrace` allocates a stack trace per root. The tick itself stays allocation-free (CORE-DIAGNOSTICS-026).

## Deviations from anime.js

- No upstream counterpart.

## Decisions

- **Diagnostics are opt-in and pay-for-play.** With diagnostics off, the hot path gains one branch in the timer constructor and one in the engine update, no allocation and no thread-static read; a timer's origin, creation trace and scope live in one lazily created side object behind a single field (8 bytes per timer). Reason: a release app must not pay for the inspector, and the tick stays allocation-free with diagnostics on or off.
- **Weak registry of roots plus a strong recently-ended ring.** Only root timers are registered (children are reached through `Timer.Children`), held weakly and compacted lazily; a ring of 32 strong references keeps roots that completed, were canceled or reverted, and is cleared on disable. Enabling discovers only what is in the engine list at that moment. Reason: weak so the inspector never extends a lifetime; the ring keeps short animations inspectable after they finish.
- **Inspectors poll state instead of receiving per-frame events.** `LastFrame` is a struct filled by the engine update while enabled, and timer state changes are read by polling public flags, not hooked into the renderer's tick; a later break-on-event feature would add one internal notification behind the enabled branch. Reason: the overlay redraws every frame anyway, and polling keeps the tick allocation-free, avoids callbacks inside the update, and leaves parity-critical tick code untouched.
- **Origins are tagged through an ambient scope.** `PrestoDiagnostics.Origin(tag)` is a disposable scope shaped like `Engine.MakeCurrent()`; bridge features tag their timers, the core tags the additive animation, and untagged means app code. `CaptureCreationTrace` is off by default and honored only while enabled. Reason: without an origin, library timers flood the list indistinguishably from the app's; an inactive `Origin()` returns an empty struct so release apps pay nothing; stack traces are expensive by design.
- **Frame cost is measured on real time.** The update cost in `LastFrame` is read from a `Stopwatch`, not from the frame source's `TimeProvider`. Reason: diagnostics measure real cost, not animation time.
- **Ease names are registered only while diagnostics are active.** See CORE-EASES.

## Non-goals

- No per-frame events: inspectors poll `LastFrame` and the timer flags (see Decisions).
- No retroactive discovery of paused, unplayed or finished roots, or of animatables created before enabling.
- No visibility into render-thread engines from another thread.

## Open items

- The enabled-engine counter behind `PrestoDiagnostics.Active` is never decremented for an engine collected while enabled, so `Active` (and ease naming) can stay on for the rest of the process.
- An animatable's property animations are not tagged `"Animatable"`; only its callbacks animation is. Untested either way.
- No test for `ActiveValues`, idempotent `Enabled` sets, or a repeated `Cancel` not re-ending a root.
