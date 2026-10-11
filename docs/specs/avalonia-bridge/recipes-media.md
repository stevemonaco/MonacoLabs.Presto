---
id: BRIDGE-RECIPES-MEDIA
title: Media recipes (draws, particles, sprites, tints)
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto.Prebuilt
sources:
  - MonacoLabs.Presto.Avalonia/Prebuilt/Draws.cs
  - MonacoLabs.Presto.Avalonia/Prebuilt/Particles.cs
  - MonacoLabs.Presto.Avalonia/Prebuilt/Sprites.cs
  - MonacoLabs.Presto.Avalonia/Prebuilt/Tints.cs
types:
  - Draws
  - Particles
  - Sprites
  - Tints
tests:
  - DrawRecipeTests
  - ParticleRecipeTests
  - RecipeTests
  - RecipeMarkupTests
  - BuilderRecipePagesTests
upstream: []
depends:
  - BRIDGE-RECIPES
  - BRIDGE-TOKENS
  - CORE-REDUCED
  - CORE-KEYFRAMES
  - CORE-COLORS
  - BRIDGE-SHAPES
  - BRIDGE-PARTICLES
  - BRIDGE-FRAMES
---

# Media recipes (draws, particles, sprites, tints)

## Purpose

The recipe families that drive a medium rather than a transform: stroke drawing on shapes, particle bursts in a window overlay, frame-by-frame playback of sprite sheets and image sequences, and color changes along a perceptual mix. They follow BRIDGE-RECIPES' shared rules (new builder per call, tokens read at call time, `stagger`), and each is forwarded from `Recipes` under the same name.

## Requirements

### Draws

- **BRIDGE-RECIPES-MEDIA-001** — `DrawIn` shall animate a shape's `Draw` property from `"0 0"` to `"0 1"`, so the stroke is drawn from its start to its end, over `Tokens.Medium` with `Tokens.Ease`.
  - Tests: `DrawRecipeTests.DrawInEndsDrawnAndDrawOutErased`
- **BRIDGE-RECIPES-MEDIA-002** — `DrawOut` shall animate `Draw` to `"1 1"`, erasing the stroke from its start.
  - Tests: `DrawRecipeTests.DrawInEndsDrawnAndDrawOutErased`
- **BRIDGE-RECIPES-MEDIA-003** — While `ReducedMotion` is set, `DrawIn` shall land on `"0 1"` and `DrawOut` on `"1 1"` with zero duration.
  - Tests: `DrawRecipeTests.DrawInEndsDrawnAndDrawOutErased`
- **BRIDGE-RECIPES-MEDIA-004** (inherited) — Each read of `Draws.Check` and `Draws.Cross` shall return a new geometry within a 20 × 20 box.
  - Tests: `DrawRecipeTests.TheMarksAreFreshGeometriesInATwentyUnitBox`

### Particles

- **BRIDGE-RECIPES-MEDIA-005** — A particle recipe shall emit into one shared overlay per window, laid over the anchor in top-level coordinates, and return the `ParticleEffect` for its emitters.
  - Tests: `ParticleRecipeTests.ConfettiLaunchesFromTheAnchorsCenterInTopLevelCoordinates`, `ParticleRecipeTests.OneOverlayIsSharedPerWindowAndRemovedOnceIdle`
- **BRIDGE-RECIPES-MEDIA-006** — `Confetti` shall launch two systems from the anchor's center: three quarters of `count` as spinning, fluttering rectangles and one quarter as streaks, in `colors` (default a six-color accent palette), aimed upward with a 35° spread, falling under gravity over 1.8–2.6 s.
  - Tests: `ParticleRecipeTests.ConfettiLaunchesFromTheAnchorsCenterInTopLevelCoordinates`
- **BRIDGE-RECIPES-MEDIA-007** — When `from` is given, `Confetti` shall launch from the middle of that window edge, spread along its whole length, aimed into the window, with a 20° spread.
  - Tests: untested
- **BRIDGE-RECIPES-MEDIA-008** — `Sparkles` shall emit `rate` small circles a second in `color` (default gold) over the anchor's bounds, each living `Medium` to `Long` and fading, until the effect is stopped.
  - Tests: `ParticleRecipeTests.SparklesRunUntilStopped`
- **BRIDGE-RECIPES-MEDIA-009** — `Burst` shall emit `count` circles in `color` (default the first accent) out of the anchor's center in a full ring that slows and fades over `Medium` to `Long`.
  - Tests: `ParticleRecipeTests.OneOverlayIsSharedPerWindowAndRemovedOnceIdle`
- **BRIDGE-RECIPES-MEDIA-010** — While `ReducedMotion` is set, a particle recipe shall emit nothing, play `reduced` (default `Recipes.Pulse()`, itself reduced to a blink) on the anchor, and return an effect that is already complete.
  - Tests: `ParticleRecipeTests.UnderReducedMotionNothingSpawnsAndTheReducedFormPlays`, `BuilderRecipePagesTests.SubmitFlowThrowsConfettiOnSuccessUnlessMotionIsReduced`
- **BRIDGE-RECIPES-MEDIA-011** — When the anchor is not in a window and `ReducedMotion` is clear, a particle recipe shall emit nothing and return an effect that is already complete.
  - Tests: untested
- **BRIDGE-RECIPES-MEDIA-012** — Canceling a particle effect shall clear its particles and complete it at once.
  - Tests: `ParticleRecipeTests.CancelClearsAndCompletesAtOnce`

### Sprites

- **BRIDGE-RECIPES-MEDIA-013** — `PlayFrames` shall tween `Frame` linearly from 0 to each target's frame count over `frameCount × 1000 / fps` ms, reading the count per target (`SpriteSheet.EffectiveFrameCount`, `ImageSequence.FrameCount`, 1 for any other target).
  - Tests: `RecipeTests.PlayFramesPlaysAnImageSequencesFrames`, `RecipeMarkupTests.PlayFramesReadsEachSheetsFrameCount`
- **BRIDGE-RECIPES-MEDIA-014** — `PlayFrames` shall loop when `loop` (default true), play reversed when `reverse`, and set `delay` only when given; it takes no tokens and no `stagger`.
  - Tests: `RecipeMarkupTests.PlayFramesReadsEachSheetsFrameCount`
- **BRIDGE-RECIPES-MEDIA-015** — While `ReducedMotion` is set, `PlayFrames` shall drop the loop and the reverse and set `Frame` to 0 with zero duration.
  - Tests: `RecipeTests.ReducedPlayFramesHoldsFrameZero`

### Tints

- **BRIDGE-RECIPES-MEDIA-016** — `Recolor` shall animate `property` (`Background` when null) from each element's current color, read when the animation starts, to `to`, along the `PrestoColor.Mix` path in `space ?? Tokens.ColorSpace` with `hue ?? Tokens.Hue`, expanded by `TweenKeyframesBuilder.Color` over `Medium` with `Ease`, ending exactly on `to`.
  - Tests: `RecipeTests.RecolorFollowsTheTokensSpaceFromTheCurrentBackgroundAndEndsExactly`, `RecipeTests.RecolorAnimatesBackgroundWithoutAPropertyAndAnExplicitKeyOtherwise`
- **BRIDGE-RECIPES-MEDIA-017** (inherited) — When resolving the start color, `Recolor` shall use a solid brush's color with its alpha multiplied by the brush opacity, a `Color` property's value, and transparent for anything else.
  - Tests: `RecipeTests.RecolorStartsEachElementFromItsOwnColorAndStaggers`
- **BRIDGE-RECIPES-MEDIA-018** — `Recolor` shall be the same while `ReducedMotion` is set, since a color change is not motion.
  - Tests: `RecipeTests.RecolorIsTheSameUnderReducedMotionAndFromXaml`

### Defaults

- **BRIDGE-RECIPES-MEDIA-019** — When `count` is not given, `Confetti` shall launch 120 pieces.
  - Tests: untested
- **BRIDGE-RECIPES-MEDIA-020** — When `rate` is not given, `Sparkles` shall emit 30 circles a second.
  - Tests: untested
- **BRIDGE-RECIPES-MEDIA-021** — When `count` is not given, `Burst` shall emit 24 circles.
  - Tests: untested
- **BRIDGE-RECIPES-MEDIA-022** — When `fps` is not given, `PlayFrames` shall play 24 frames a second.
  - Tests: untested

## Invariants

- Every draw, sprite and tint call returns a new builder; every particle call returns a new effect (or the shared completed one).
- No recipe here runs on the compositor: draws, tints and frames are UI-thread properties, and particles draw on their own render-thread scene.

## Edge cases

- `Recolor` on a gradient or null brush starts from transparent.
- `PlayFrames` on a target that is neither sprite control plays one frame.
- `Confetti(count: 3)` launches 3 rectangles and no streaks (`count / 4` is 0).

## Threading and lifetime

- UI thread. The particle overlay is held per `TopLevel` in a weak table and removed by an idle timer after its last system ends (BRIDGE-PARTICLES).

## Reduced motion

- Draws land at once; particles emit nothing and play `reduced` on the anchor; sprites hold frame 0 (deliberately unlike Lottie's end frame; see Reduced playback holds frame 0); tints are unchanged.

## Allocation

- Not on the tick path for the builders. `PlayFrames`' duration and end are function values evaluated once per target. Particle emission cost is BRIDGE-PARTICLES'.

## Provenance

- No upstream counterpart.

## Decisions

- **`ColorSpace` and `Hue` are trailing optional parameters.** See BRIDGE-TOKENS.
- **`Recolor` takes a nullable key, null meaning `Background`.** Its `property` is a `PropertyKey?` defaulting to null, and `RecolorExtension` keeps its string `Property`, which converts. Reason: a struct parameter cannot default to a string constant, and a key parameter takes `PrestoKeys.Of(...)` as well as a name.
- **`Play` and `PlayFrames` are thin wrappers.** See BRIDGE-FRAMES.
- **Reduced playback holds frame 0.** See BRIDGE-FRAMES.

## Non-goals

- Glyph or bitmap particle recipes; arbitrary marks beyond `Check` and `Cross`.
- `Recolor` as a `Motion` state: it is a one-shot builder whose start is the current color.

## Open items

- `Confetti` from an edge, and an anchor outside a window, have no tests.
- The particle design says an anchor outside a window "does not play `reduced`"; that holds only while `ReducedMotion` is clear, because the reduced check runs first and plays `reduced` regardless of the window.
