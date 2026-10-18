---
id: BRIDGE-PARTICLES
title: Particle drawing and particle effects
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
  - MonacoLabs.Presto.Compositor
sources:
  - MonacoLabs.Presto.Avalonia/Compositor/ParticleScene.cs
  - MonacoLabs.Presto.Avalonia/Compositor/ParticleFormation.cs
  - MonacoLabs.Presto.Avalonia/Compositor/FormationTransition.cs
  - MonacoLabs.Presto.Avalonia/Compositor/ParticleShape.cs
  - MonacoLabs.Presto.Avalonia/Compositor/ParticleOverlay.cs
  - MonacoLabs.Presto.Avalonia/ParticleEffect.cs
  - MonacoLabs.Presto.Avalonia/Prebuilt/Particles.cs
  - MonacoLabs.Presto.Avalonia/RecipesGlyphs.cs
  - MonacoLabs.Presto.Avalonia/BurstExtension.cs
types:
  - ParticleScene
  - ParticleFormation
  - FormationTransition
  - ParticleShape
  - ParticleEffect
  - BurstExtension
tests:
  - ParticleSceneTests
  - ParticleFormationTests
  - ParticleRecipeTests
  - BridgeAllocationTests
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
  - BRIDGE-GLYPHS
  - BRIDGE-ATTACH
---

# Particle drawing and particle effects

## Purpose

`ParticleScene` is a `CompositorScene` (BRIDGE-SCENES) that steps any number of core `ParticleSystem`s (CORE-PARTICLES, which owns the simulation, emitter parameters and the per-system cap) and draws their particles on the render thread as one of the `ParticleShape`s. It also holds `ParticleFormation`s: particles that each spring towards a point of their own, can be pushed by a repel point and retargeted to new points, so together they draw a shape such as text. The recipes `Recipes.Confetti`, `Sparkles` and `Burst` (BRIDGE-RECIPES owns their facade) emit into one overlay scene per window, and `Recipes.Disintegrate` and `Assemble` blow a text block's letters away as a formation or gather them back; each returns a `ParticleEffect` handle that can be stopped, paused, canceled and awaited. Under reduced motion they spawn nothing. Also specifies `Recipes.Confetti`, `Recipes.Sparkles`, `Recipes.Burst`, `Recipes.Disintegrate`, `Recipes.Assemble`, the copies of the first three on `Prebuilt.Particles`, and `{presto:Burst}`, a `CueAction` (BRIDGE-ATTACH) that bursts from the element it sits on.

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
- **BRIDGE-PARTICLES-020** — When `ReducedMotion` is clear (the reduced check runs first, BRIDGE-RECIPES-MEDIA-010) and the anchor is not in a top level with an overlay layer, `Confetti`, `Sparkles` and `Burst` shall emit nothing, play nothing on the anchor, and return an effect that is already complete.
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
- **BRIDGE-PARTICLES-052** — `{presto:Burst}` shall provide a `CueAction` that, for a value that is not first, calls `Recipes.Burst` on its target with `Count` (default 24), `Color` and `Reduced`, does nothing for a first value, and throws `InvalidOperationException` for a target that is not a `Control`.
  - Tests: `ParticleRecipeTests.BurstOnACueEmitsFromItsTarget`, `ParticleRecipeTests.BurstSkipsAFirstValueAndThrowsOffAControl`

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

### Formations

- **BRIDGE-PARTICLES-036** — `AddFormation(points, color, size)` shall place one particle on each point, in canvas coordinates; when the room left under `MaxLive` is smaller than the point count, it shall place one on each point at index `⌊i × points / room⌋` for each `i` below the room, so the points it uses spread evenly over the whole list.
  - Tests: `ParticleFormationTests.AFormationSpringsOntoItsPointsAndDrawsThere`, `ParticleFormationTests.FormationsCountAgainstTheSceneCap`, `ParticleFormationTests.ACappedFormationThinsItsPointsEvenly`
- **BRIDGE-PARTICLES-037** — Each frame, every particle of a formation shall accelerate towards its point by `0.0004 × s² × displacement − 0.028 × s × velocity` (DIPs and ms), where `s` is 400 divided by the formation's `Duration`, integrated in steps of at most 16 ms over a frame time capped at 100 ms.
  - Tests: `ParticleFormationTests.AFormationSpringsOntoItsPointsAndDrawsThere`, `ParticleFormationTests.DurationScalesTheSpringInTime`
- **BRIDGE-PARTICLES-048** — A formation's `Duration` shall default to 400, and setting it to 0 or less shall throw `ArgumentOutOfRangeException`.
  - Tests: `ParticleFormationTests.DurationScalesTheSpringInTime`
- **BRIDGE-PARTICLES-038** — While `Repel` is set, a particle closer to it than `RepelRadius` (default 80) shall also accelerate away from it by `0.04 × (1 − distance / RepelRadius)` DIP/ms²; particles farther away shall not be pushed.
  - Tests: `ParticleFormationTests.ARepelPointPushesOnlyTheParticlesWithinItsRadius`
- **BRIDGE-PARTICLES-039** — `Retarget(points, transition)` shall give each point, in order, a particle not yet given one and not fading out: the nearest one, or with `Swarm` the next in a shuffle of those particles seeded with 0; each point left over shall spawn a particle on itself that fades in over 300 ms, and each particle left over shall fade out over 300 ms and be removed at opacity 0. When the points outnumber the formation's particles not fading out plus the room left under `MaxLive`, it shall first thin them to that many as `AddFormation` does.
  - Tests: `ParticleFormationTests.RetargetPairsNearestAndSpawnsOrFadesTheDifference`, `ParticleFormationTests.FormationsCountAgainstTheSceneCap`, `ParticleFormationTests.RetargetPairsAsAFullNearestScanWouldWithTiesToTheLowerIndex`, `ParticleFormationTests.SwarmPairsBySeededShuffleRatherThanDistance`, `ParticleFormationTests.ACappedFormationThinsItsPointsEvenly`
- **BRIDGE-PARTICLES-049** — With `Sweep`, `Retarget` shall hold each particle's spring and fade for `0.6 × Duration` times its distance from the formation's leftmost particle as a share of the formation's width.
  - Tests: `ParticleFormationTests.SweepSetsParticlesOffLeftToRight`
- **BRIDGE-PARTICLES-050** — With `Burst`, `Retarget` shall add to each particle a velocity away from the formation's mean position of `0.35 × the formation's larger side × √stiffness`, scaled per particle between 0.6 and 1.4.
  - Tests: `ParticleFormationTests.BurstThrowsEveryParticleOutwardBeforeItGathers`
- **BRIDGE-PARTICLES-051** — When `transition` is not given, `Retarget` shall pair as `Nearest`.
  - Tests: `ParticleFormationTests.RetargetPairsNearestAndSpawnsOrFadesTheDifference`
- **BRIDGE-PARTICLES-040** — A formation's particles shall count towards `Live` and `MaxLive`, for systems' spawns as for the formation's own.
  - Tests: `ParticleFormationTests.FormationsCountAgainstTheSceneCap`
- **BRIDGE-PARTICLES-041** — The scene shall draw each formation particle as a square of the formation's `size` centered on the particle, in its color at its opacity, and shall drop a formation once it holds no particle.
  - Tests: `ParticleFormationTests.AFormationSpringsOntoItsPointsAndDrawsThere`, `ParticleFormationTests.RetargetPairsNearestAndSpawnsOrFadesTheDifference`

### Text particles

- **BRIDGE-PARTICLES-042** — `Disintegrate(block)` and `Assemble(block)` shall sample each character's outline (as `PrestoAvalonia.GlyphOutline` reads it) with `GlyphPoints.Sample(outline, max(2, FontSize / 24), Fill, index)` (BRIDGE-GLYPHS) at the character's slot, in top-level coordinates, set the block's `Opacity` to 0 at once, and add a formation of those points to the window's overlay, colored as the block's `Foreground` (black when it is not a solid color) with particles as wide as the spacing.
  - Tests: `ParticleFormationTests.DisintegrateHidesTheBlockAndBlowsItsFillPointsAway`, `ParticleFormationTests.AssembleConvergesOnTheLettersThenShowsTheBlock`
- **BRIDGE-PARTICLES-043** — `Disintegrate` shall release its particles from their points left to right over the first half of twice `Tokens.Long`; a released particle shall drift at (0.12, −0.06) DIP/ms, each axis scaled per particle between 0.5 and 1.5, pushed by a turbulence field of 0.0008 DIP/ms², and fade out over the second half; the effect shall complete once the last has faded, leaving the block at `Opacity` 0.
  - Tests: `ParticleFormationTests.DisintegrateHidesTheBlockAndBlowsItsFillPointsAway`
- **BRIDGE-PARTICLES-044** — `Assemble` shall start each particle away from its point along the drift by `Tokens.Long` to twice that, plus up to `Tokens.Distance` either way on each axis, drawn from seed 0, fading in over 300 ms; once every particle is opaque, within 0.5 DIP of its point and slower than 0.02 DIP/ms, the block shall fade to `Opacity` 1 over 300 ms with `Tokens.Ease` while the particles fade out, and the effect shall complete once they have.
  - Tests: `ParticleFormationTests.AssembleConvergesOnTheLettersThenShowsTheBlock`
- **BRIDGE-PARTICLES-045** — When `ReducedMotion` is set as `Disintegrate` or `Assemble` is called, or the block has no overlay layer, it shall spawn nothing, create no overlay and return an effect that is already complete, and `Disintegrate` shall fade the block's `Opacity` to 0, `Assemble` from 0 to 1, over `Tokens.Short` with `Tokens.Ease`.
  - Tests: `ParticleFormationTests.ReducedTheBlockFadesAndNothingSpawns`
- **BRIDGE-PARTICLES-046** — On an effect holding a formation, `Cancel()` and `Revert()` shall remove its particles, `Pause()` and `Resume()` shall freeze and continue it, and `Stop()` shall do nothing.
  - Tests: untested
- **BRIDGE-PARTICLES-047** — When `Disintegrate` or `Assemble` is called on a block whose previous `Disintegrate` or `Assemble` effect is still running, it shall cancel that effect first, so its particles are removed, its `Then()` completes and it no longer changes the block's `Opacity`.
  - Tests: `ParticleFormationTests.StartingAgainMidEffectCancelsTheFirst`

## Invariants

- A scene's live count, formations included, never exceeds `MaxLive`, and each system's never exceeds its `MaxParticles` (CORE-PARTICLES).
- Systems and formations are touched only on the render thread; `ParticleEffect` reaches them through `CompositorCanvas.Invoke`.
- An overlay exists for a window exactly while some effect in it is running or within the idle second after the last one finished.

## Edge cases

- `Add`, `AddFormation`, `Live` and every member of a `ParticleFormation` belong to the render thread: call them from a scene callback or `CompositorCanvas.Invoke`; reading them from the UI thread races.
- `Retarget` finds each point's particle through a grid of cells about two particles wide, searched outward from the point, so retargeting thousands of particles costs a few milliseconds on the render thread once; points far from every remaining particle search more cells.
- A character with no outline, such as an emoji, spawns no particles in `Disintegrate` or `Assemble`; it disappears with the block and comes back with it.
- Canceling a `Disintegrate` or `Assemble` effect removes its particles but leaves the block at `Opacity` 0.
- Under reduced motion the `reduced` builder plays even when the anchor is outside a window; outside reduced motion such an anchor plays nothing.
- The overlay's offset from the top level is measured once, when it is created.
- `Glyph` particles are about two orders of magnitude slower to draw than the other shapes; keep them to a few dozen live particles.

## Threading and lifetime

- Recipes and `ParticleEffect` methods run on the UI thread; stepping and drawing run on the render thread on the scene's own engine (see BRIDGE-SCENES). Completion callbacks and `Then()` resolve on the UI thread.
- Each overlay is held per `TopLevel` in a weak table; its idle timer runs on `Presto.Engine`.

## Reduced motion

- The recipes spawn nothing and play their `reduced` builder on the anchor (BRIDGE-PARTICLES-025); `Disintegrate` and `Assemble` fade the block out or in (BRIDGE-PARTICLES-045). A hand-built `ParticleScene` is the app's own drawing and is not affected by `ReducedMotion` or `ReduceAll`, as plain timers are not (Reduced motion emits nothing; hand-built scenes are exempt).

## Allocation

- Drawing a scene of 500 live particles allocates nothing per frame once the brush cache is warm; stepping them allocates nothing (CORE-PARTICLES).
  - Pinned by `BridgeAllocationTests.AParticleSceneOf500DrawsWithoutAllocating`.
- Stepping and drawing a formation of 500 particles pushed by a repel point allocates nothing per frame; `AddFormation` and `Retarget` allocate their buffers once.
  - Pinned by `BridgeAllocationTests.AFormationOf500StepsAndDrawsWithoutAllocating`.

## Provenance

- No upstream counterpart. The planned API listed `ParticleShape.Bitmap(IImage)`; the code takes a `Bitmap`.

## Decisions

- **Particles are drawn on the render thread from a limited shape set.** `ParticleScene` is a public sealed `CompositorScene` holding any number of systems, stepped by a `Timer` on the scene engine with `Engine.DeltaTime`; the shapes are `Rect`, `RoundedRect`, `Circle`, `Streak`, `Glyph` and `Bitmap`, an arbitrary path is rasterized once on the UI thread (`FromGeometry`) and drawn as a bitmap, and rotation and flutter go through `PushPreTransform`. Reason: `ImmediateDrawingContext` has rectangles, ellipses, lines, glyph runs and bitmaps but no geometry, and `PushPreTransform` returns a struct, so it allocates nothing.
- **Color-over-life is quantized to keep the brush cache.** Palette colors are drawn as-is; a color-over-life curve is quantized to 4 bits per channel. Reason: `CompositorScene.Brush` keys on the exact color and quantizes only opacity, so quantizing lets brushes be reused.
- **One-call helpers share one overlay canvas per top level.** `Confetti`, `Sparkles` and `Burst` measure the anchor on the UI thread and post an emitter to one hit-test-invisible `CompositorCanvas` per `TopLevel` in its `OverlayLayer`, as exit ghosts are, created on first use and removed after a second with no live particle; each returns a `ParticleEffect` handle. Reason: not recorded.
- **Particle caps drop over-cap spawns.** `ParticleScene.MaxLive` defaults to 4 000 on desktop and 1 000 on Browser, Android and iOS (`EmitterParams.MaxParticles`, 500, bounds one system); spawns over a cap are dropped, not queued. Reason: starting points from the field sample's measurements (60 fps to about 7 500 drifting particles on the dev machine, lower on Browser), with desktop's 4 000 confirmed by measurement on the Particles page.
- **Reduced motion emits nothing; hand-built scenes are exempt.** Under `ReducedMotion` the recipes spawn nothing, resolve `Then()` at once and play an optional `reduced` builder on the anchor (default `Recipes.Pulse()`); `ReduceAll` does not affect a hand-built `ParticleScene`. Reason: a hand-built scene is the app's own drawing, exempt as `Presto.Set` and plain timers are.
- **Particles are not seekable; pause is per effect.** A particle simulation cannot be sought or reversed; `ParticleEffect` pauses and resumes through an internal flag on each of its systems, and a seeded burst can be replayed from the start. Reason: a simulation stepped by `dt` has no seekable timeline, and per-system pause keeps other effects in the same shared overlay running. Earlier, pause went through the effect's timer; replaced because one timer steps the whole overlay.
- **No XAML emitter surface.** Continuous emission is code plus `Recipes.Sparkles`; a one-off burst is `{presto:Burst}` on a cue. Reason: not recorded. Rejected: an attached `Particles.Emitter` started while attached or hovered.
- **Simulation in the core, drawing in the bridge.** See CORE-PARTICLES.
- **`{presto:Burst}` skips a first watched value.** Reason: a burst marks a moment, and a first value under `DoOnChanged` is the state the view starts in, not a change. A cue never passes a first value, so every raise bursts.
- **Particle text is a formation in `ParticleScene`.** Text sampled into points is drawn by the render-thread scene as one `ParticleFormation` per call. Reason: thousands of points need the render-thread scene, which the Particle field showcase already shows holding thousands of positioned particles. Rejected: one control per point.
- **Formation particles are the scene's own buffer, not core particles.** A `ParticleFormation` keeps its particles in an array of its own, stepped and drawn by the scene beside its systems and counted against the same `MaxLive`. Reason: a core particle is emitted with a lifetime and has no point to return to, and the core does not change for a bridge feature; a separate buffer keeps the spring step allocation-free. Rejected: adding targets to `ParticleSystem`.
- **Formation springs are slightly underdamped and scaled in time by `Duration`.** At the default `Duration` of 400, stiffness 0.0004 /ms² and damping 0.028 /ms (a damping ratio of 0.7, settling in about 400 ms from 150 DIPs); another `Duration` scales stiffness by the square and damping by the ratio, which keeps the damping ratio and the shape of the motion. The 0.04 DIP/ms² repel push is not scaled, and steps are at most 16 ms. Reason: a light overshoot reads as particles landing rather than sliding, and one time knob is what a page needs (the Particle type page has a duration slider); a push of 0.04 moves a particle under the repel point about half the default radius; capped steps keep the explicit integration stable after a slow frame. Rejected: separate stiffness and damping options, which let a caller lose the landing feel.
- **Transitions change how particles are paired or set off, not the spring.** `Nearest` is the least travel; `Sweep` and `Burst` keep nearest pairing and add a left-to-right hold or an outward throw; `Swarm` pairs by a fixed shuffle so particles cross the formation. Reason: nearest pairing already reads well, and the others are variations on one physics model, so spawns, fades, repel and settling behave the same in all four. Rejected: pairing by left-to-right rank for `Sweep`, which moves particles little more than nearest pairing.
- **A capped formation thins its points evenly.** When `MaxLive` leaves fewer particles than points, `AddFormation` and `Retarget` take points by a stride over the whole list. Reason: `GlyphPoints.Sample` orders fill points row by row, so taking the first ones cut off the end of a word's bottom rows (the Particle type page's TYPE lost the bottom of its E); a stride keeps the whole shape, only sparser. Rejected: the page sampling coarser until the word fits, which every caller would have to repeat.
- **`ParticleShape.Bitmap` takes a `Bitmap`, not an `IImage`.** Reason: the shape draws through the render thread's immediate context, which draws bitmaps; an `IImage` such as a `DrawingImage` would have to be rasterized first, as `FromGeometry` does.
- **`Retarget` pairs greedily by nearest particle.** Each point in order takes the nearest free particle; spawned particles fade in on their point and surplus particles fade out where they head. Reason: simple, and close enough to the optimal assignment for word-to-word moves. Rejected: optimal assignment (Hungarian algorithm), cubic in the particle count.
- **`Disintegrate` and `Assemble` return a `ParticleEffect`.** Reason: their particles are a render-thread simulation that cannot be sought or reversed, like the other particle recipes, and the effect is awaitable and cancelable as theirs is. Rejected: a `Timeline`, which would promise seeking. Their particles tile the letters: one per fill point at a spacing of `FontSize / 24` (at least 2 DIPs), as wide as the spacing, so a letter has about the same number of particles at any size. Their reduced form fades the block, since disintegrating is the explosive motion reduced motion opts out of.

## Non-goals

- Seeking or reversing emitted particles.
- A XAML emitter surface; code plus `Recipes.Sparkles` covers continuous emission.
- A fill or color for `FromGeometry`, and colored `Bitmap` particles.

## Open items

- Paused systems (BRIDGE-PARTICLES-008, -030), rotation and flutter (-011), color quantization (-013), `Glyph` validation (-015), an anchor outside a window (-020), edge confetti (-022), `Revert` (-029), calls after completion (-031), awaiting (-032) and the handle's calls on a formation (-046) have no test.
- manual — the 1 000 particle cap for Browser, Android and iOS is unmeasured; only desktop's 4 000 was measured on the Particles page. The Particle type page's frame rate on the Browser head is unmeasured too.
- `ParticleScene.AddFormation` takes one color (no per-point color or gradient), and a formation's color cannot change after it is created, so the Particle type page uses a single color rather than a gradient by x and rebuilds the scene on a color change.
