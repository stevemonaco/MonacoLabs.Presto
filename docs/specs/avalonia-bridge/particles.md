---
id: BRIDGE-PARTICLES
title: Particle drawing and particle effects
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge
  - Monaco.Presto.AvaloniaBridge.Compositor
sources:
  - Monaco.Presto.AvaloniaBridge/Compositor/ParticleScene.cs
  - Monaco.Presto.AvaloniaBridge/Compositor/ParticleShape.cs
  - Monaco.Presto.AvaloniaBridge/Compositor/ParticleOverlay.cs
  - Monaco.Presto.AvaloniaBridge/ParticleEffect.cs
  - Monaco.Presto.AvaloniaBridge/Prebuilt/Particles.cs
types:
  - ParticleScene
  - ParticleShape
  - ParticleEffect
tests:
  - ParticleSceneTests
  - ParticleRecipeTests
  - AllocationTests
  - ReferencePagesTests
  - BuilderRecipePagesTests
  - MicroInteractionsPageTests
  - StorefrontPageTests
upstream: []
depends:
  - CORE-PARTICLES
  - BRIDGE-SCENES
  - BRIDGE-RECIPES
  - BRIDGE-TOKENS
  - CORE-REDUCED
  - CORE-CALLBACKS
---

# Particle drawing and particle effects

## Purpose

`ParticleScene` is a `CompositorScene` (BRIDGE-SCENES) that steps any number of core `ParticleSystem`s (CORE-PARTICLES, which owns the simulation, emitter parameters and the per-system cap) and draws their particles on the render thread as one of the `ParticleShape`s. The recipes `Recipes.Confetti`, `Sparkles` and `Burst` (BRIDGE-RECIPES owns their facade) emit into one overlay scene per window and return a `ParticleEffect` handle that can be stopped, paused, canceled and awaited. Under reduced motion they spawn nothing. Also specifies `Recipes.Confetti`, `Recipes.Sparkles` and `Recipes.Burst`, and their copies on `Prebuilt.Particles`.

## Requirements

### Scene

- **BRIDGE-PARTICLES-001** — When attached, a scene shall step every system it holds once per scene frame with the scene engine's `DeltaTime`, and redraw.
  - Tests: `ParticleSceneTests.ABurstDrawsAroundItsOriginAndNothingOnceItFinishes`
- **BRIDGE-PARTICLES-002** — `Add(parameters, origin)` shall start a system emitting from `origin`, in canvas coordinates, and return it.
  - Tests: `ParticleSceneTests.ABurstDrawsAroundItsOriginAndNothingOnceItFinishes`
- **BRIDGE-PARTICLES-003** — The scene shall never hold more than `MaxLive` live particles across all its systems; spawns over the cap shall be dropped, not queued.
  - Tests: `ParticleSceneTests.SeveralSystemsNeverExceedTheSceneCap`
- **BRIDGE-PARTICLES-004** — `MaxLive` shall default to 4 000, or 1 000 on Browser, Android and iOS.
  - Tests: `ParticleSceneTests.SeveralSystemsNeverExceedTheSceneCap`
- **BRIDGE-PARTICLES-005** — `Live` shall be the number of live particles summed over the scene's systems.
  - Tests: `ParticleSceneTests.SeveralSystemsNeverExceedTheSceneCap`
- **BRIDGE-PARTICLES-006** — When a system finishes, the scene shall drop it and stop drawing its particles.
  - Tests: `ParticleSceneTests.ABurstDrawsAroundItsOriginAndNothingOnceItFinishes`, `ParticleSceneTests.EachSystemReportsItsFinishOnTheUIThreadAndIsDropped`
- **BRIDGE-PARTICLES-007** — When a system added with a completion callback finishes, the scene shall run the callback on the UI thread.
  - Tests: `ParticleSceneTests.EachSystemReportsItsFinishOnTheUIThreadAndIsDropped`
- **BRIDGE-PARTICLES-008** — While a system is paused, the scene shall neither step nor drop it, and shall keep drawing its particles where they are.
  - Tests: untested

### Drawing

- **BRIDGE-PARTICLES-009** (inherited) — The scene shall draw each particle as its system's `EmitterParams.Shape` when that is a `ParticleShape`, and as `Circle` otherwise.
  - Tests: `ParticleSceneTests.EveryShapeDraws`
- **BRIDGE-PARTICLES-010** (inherited) — A particle's size shall be the width of a `Rect`, `RoundedRect`, `Bitmap` or `FromGeometry` shape, the diameter of a `Circle`, the thickness of a `Streak` and the font size of a `Glyph`, centered on the particle.
  - Tests: `ParticleSceneTests.EveryShapeDraws`
- **BRIDGE-PARTICLES-011** — Every shape except `Streak` shall rotate by the particle's `Rotation` in degrees; while the emitter's `Flutter` is not zero, it shall also scale horizontally by `cos(FlutterPhase)`.
  - Tests: untested
- **BRIDGE-PARTICLES-012** (inherited) — A `Streak` shall point along the particle's velocity and trail behind it by its travel over 24 ms, never shorter than its size, with rounded ends.
  - Tests: `ParticleSceneTests.EveryShapeDraws`
- **BRIDGE-PARTICLES-013** — A particle shall draw in its color at its opacity; when the emitter has both `ColorMix` and `EndColor`, its color shall be quantized to 4 bits per channel.
  - Tests: untested
- **BRIDGE-PARTICLES-014** — A `Bitmap` shape shall draw the image at the particle's size, keeping its aspect ratio, at the particle's opacity and ignoring its color.
  - Tests: `ParticleSceneTests.EveryShapeDraws`
- **BRIDGE-PARTICLES-015** — `ParticleShape.Glyph` shall throw `ArgumentOutOfRangeException` for a character outside printable ASCII (`'!'` to `'~'`).
  - Tests: untested
- **BRIDGE-PARTICLES-016** — `ParticleShape.FromGeometry(geometry, size)` shall rasterize the geometry once, filled white in a box of `size`, and draw it as a `Bitmap` shape.
  - Tests: `ParticleSceneTests.EveryShapeDraws`

### Window overlay

- **BRIDGE-PARTICLES-017** — The particle recipes shall emit into one overlay scene per top level, created on first use in the window's `OverlayLayer`, sized to the top level and following its size, with coordinates equal to top-level coordinates.
  - Tests: `ParticleRecipeTests.ConfettiLaunchesFromTheAnchorsCenterInTopLevelCoordinates`, `ParticleRecipeTests.OneOverlayIsSharedPerWindowAndRemovedOnceIdle`
- **BRIDGE-PARTICLES-018** — The overlay shall not take input (`IsHitTestVisible` false).
  - Tests: `ParticleRecipeTests.OneOverlayIsSharedPerWindowAndRemovedOnceIdle`
- **BRIDGE-PARTICLES-019** — When the overlay's last running system finishes, the overlay shall be removed one second later, unless another effect starts in that window first.
  - Tests: `ParticleRecipeTests.OneOverlayIsSharedPerWindowAndRemovedOnceIdle`
- **BRIDGE-PARTICLES-020** — When the anchor is not in a top level with an overlay layer, a recipe shall emit nothing, play nothing on the anchor, and return an effect that is already complete.
  - Tests: untested

### Recipes

- **BRIDGE-PARTICLES-021** — `Confetti` shall launch from the anchor's center, in top-level coordinates, two systems: `count − count / 4` fluttering, spinning rects and `count / 4` streaks, in `colors` or a six-color accent palette, aimed up with gravity.
  - Tests: `ParticleRecipeTests.ConfettiLaunchesFromTheAnchorsCenterInTopLevelCoordinates`
- **BRIDGE-PARTICLES-022** — When `from` is given, `Confetti` shall launch along the middle of that window edge, spread over its length and aimed into the window.
  - Tests: untested
- **BRIDGE-PARTICLES-023** — `Sparkles` shall emit `rate` small fading circles a second (default gold) over the anchor's bounds until the effect is stopped.
  - Tests: `ParticleRecipeTests.SparklesRunUntilStopped`
- **BRIDGE-PARTICLES-024** — `Burst` shall emit `count` circles at once from the anchor's center in a ring that slows and fades, in `color` or the first accent.
  - Tests: `ParticleRecipeTests.OneOverlayIsSharedPerWindowAndRemovedOnceIdle`
- **BRIDGE-PARTICLES-025** — While `PrestoDefaults.ReducedMotion` is set when a recipe is called, it shall spawn nothing, create no overlay, play `reduced` (default `Recipes.Pulse()`) on the anchor and return an effect that is already complete.
  - Tests: `ParticleRecipeTests.UnderReducedMotionNothingSpawnsAndTheReducedFormPlays`, `BuilderRecipePagesTests.SubmitFlowThrowsConfettiOnSuccessUnlessMotionIsReduced`, `MicroInteractionsPageTests.LikingBurstsParticlesUnlessMotionIsReducedWhenTheHeartFadesIn`

### ParticleEffect

- **BRIDGE-PARTICLES-026** — `Then()` shall complete on the UI thread once the last of the effect's systems has finished and been dropped.
  - Tests: `ParticleRecipeTests.OneOverlayIsSharedPerWindowAndRemovedOnceIdle`
- **BRIDGE-PARTICLES-027** — `Stop()` shall end the effect's emission and let its live particles finish.
  - Tests: `ParticleRecipeTests.SparklesRunUntilStopped`
- **BRIDGE-PARTICLES-028** — `Cancel()` shall remove every particle of the effect and complete `Then()` at once.
  - Tests: `ParticleRecipeTests.CancelClearsAndCompletesAtOnce`
- **BRIDGE-PARTICLES-029** — `Revert()` shall do what `Cancel()` does.
  - Tests: untested
- **BRIDGE-PARTICLES-030** — `Pause()` shall freeze the effect's particles and emission where they are, leaving other effects in the same overlay running, and `Resume()` shall continue them.
  - Tests: untested
- **BRIDGE-PARTICLES-031** (inherited) — Once the effect is complete, `Stop`, `Cancel`, `Pause` and `Resume` shall do nothing.
  - Tests: untested
- **BRIDGE-PARTICLES-032** — The effect shall be awaitable, completing as `Then()` does.
  - Tests: untested

### Defaults

- **BRIDGE-PARTICLES-033** — When `count` is not given, `Confetti` shall launch 120 particles.
  - Tests: untested
- **BRIDGE-PARTICLES-034** — When `rate` is not given, `Sparkles` shall emit 30 particles a second.
  - Tests: untested
- **BRIDGE-PARTICLES-035** — When `count` is not given, `Burst` shall emit 24 particles.
  - Tests: untested

## Invariants

- A scene's live count never exceeds `MaxLive`, and each system's never exceeds its `MaxParticles` (CORE-PARTICLES).
- Systems are touched only on the render thread; `ParticleEffect` reaches them through `CompositorCanvas.Invoke`.
- An overlay exists for a window exactly while some effect in it is running or within the idle second after the last one finished.

## Edge cases

- `Add` and `Live` belong to the render thread: call `Add` from a scene callback or `CompositorCanvas.Invoke`; reading `Live` from the UI thread races.
- Under reduced motion the `reduced` builder plays even when the anchor is outside a window; outside reduced motion such an anchor plays nothing.
- The overlay's offset from the top level is measured once, when it is created.
- `Glyph` particles are about two orders of magnitude slower to draw than the other shapes; keep them to a few dozen live particles.

## Threading and lifetime

- Recipes and `ParticleEffect` methods run on the UI thread; stepping and drawing run on the render thread on the scene's own engine (see BRIDGE-SCENES). Completion callbacks and `Then()` resolve on the UI thread.
- Each overlay is held per `TopLevel` in a weak table; its idle timer runs on `Presto.Engine`.

## Reduced motion

- The recipes spawn nothing and play their `reduced` builder on the anchor (BRIDGE-PARTICLES-025). A hand-built `ParticleScene` is the app's own drawing and is not affected by `ReducedMotion` or `ReduceAll`, as plain timers are not (Reduced motion emits nothing; hand-built scenes are exempt).

## Allocation

- Drawing a scene of 500 live particles allocates nothing per frame once the brush cache is warm; stepping them allocates nothing (CORE-PARTICLES).
  - Pinned by `AllocationTests.AParticleSceneOf500DrawsWithoutAllocating`.

## Deviations from anime.js

- No upstream counterpart.

## Decisions

- **Particles are drawn on the render thread from a limited shape set.** `ParticleScene` is a public sealed `CompositorScene` holding any number of systems, stepped by a `Timer` on the scene engine with `Engine.DeltaTime`; the shapes are `Rect`, `RoundedRect`, `Circle`, `Streak`, `Glyph` and `Bitmap`, an arbitrary path is rasterized once on the UI thread (`FromGeometry`) and drawn as a bitmap, and rotation and flutter go through `PushPreTransform`. Reason: `ImmediateDrawingContext` has rectangles, ellipses, lines, glyph runs and bitmaps but no geometry, and `PushPreTransform` returns a struct, so it allocates nothing.
- **Color-over-life is quantized to keep the brush cache.** Palette colors are drawn as-is; a color-over-life curve is quantized to 4 bits per channel. Reason: `CompositorScene.Brush` keys on the exact color and quantizes only opacity, so quantizing lets brushes be reused.
- **One-call helpers share one overlay canvas per top level.** `Confetti`, `Sparkles` and `Burst` measure the anchor on the UI thread and post an emitter to one hit-test-invisible `CompositorCanvas` per `TopLevel` in its `OverlayLayer`, as exit ghosts are, created on first use and removed after a second with no live particle; each returns a `ParticleEffect` handle. Reason: not recorded.
- **Particle caps drop over-cap spawns.** `ParticleScene.MaxLive` defaults to 4 000 on desktop and 1 000 on Browser, Android and iOS (`EmitterParams.MaxParticles`, 500, bounds one system); spawns over a cap are dropped, not queued. Reason: starting points from the field sample's measurements (60 fps to about 7 500 drifting particles on the dev machine, lower on Browser), with desktop's 4 000 confirmed by measurement on the Particles page.
- **Reduced motion emits nothing; hand-built scenes are exempt.** Under `ReducedMotion` the recipes spawn nothing, resolve `Then()` at once and play an optional `reduced` builder on the anchor (default `Recipes.Pulse()`); `ReduceAll` does not affect a hand-built `ParticleScene`. Reason: a hand-built scene is the app's own drawing, exempt as `Presto.Set` and plain timers are.
- **Particles are not seekable; pause is per effect.** A particle simulation cannot be sought or reversed; `ParticleEffect` pauses and resumes through an internal flag on each of its systems, and a seeded burst can be replayed from the start. Reason: a simulation stepped by `dt` has no seekable timeline, and per-system pause keeps other effects in the same shared overlay running. Earlier, pause went through the effect's timer; replaced because one timer steps the whole overlay.
- **No XAML emitter surface.** Continuous emission is code plus `Recipes.Sparkles`. Reason: not recorded. Rejected: an attached `Particles.Emitter` started while attached or hovered.
- **Simulation in the core, drawing in the bridge.** See CORE-PARTICLES.

## Non-goals

- Seeking or reversing emitted particles.
- A XAML emitter surface; code plus `Recipes.Sparkles` covers continuous emission.
- A fill or color for `FromGeometry`, and colored `Bitmap` particles.

## Open items

- Paused systems (BRIDGE-PARTICLES-008, -030), rotation and flutter (-011), color quantization (-013), `Glyph` validation (-015), an anchor outside a window (-020), edge confetti (-022), `Revert` (-029), calls after completion (-031) and awaiting (-032) have no test.
- The planned API listed `ParticleShape.Bitmap(IImage)`; the code takes a `Bitmap` (code wins).
- manual — the 1 000 particle cap for Browser, Android and iOS is unmeasured; only desktop's 4 000 was measured on the Particles page.
