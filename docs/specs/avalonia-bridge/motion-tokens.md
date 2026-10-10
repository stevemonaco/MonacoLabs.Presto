---
id: BRIDGE-TOKENS
title: Motion tokens
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/MotionTokens.cs
types:
  - MotionTokens
tests:
  - RecipeTests
  - FollowerTests
  - ScrollToTests
  - ProgressRingTests
  - BuilderRecipePagesTests
upstream: []
depends:
  - CORE-EASES
  - CORE-COLORS
---

# Motion tokens

## Purpose

`MotionTokens` is a motion system's named durations, eases and travel in one immutable record. `Recipes.Tokens` (BRIDGE-RECIPES) holds the one every recipe, and most bridge controls, read their defaults from instead of literals; three presets cover brisk, expressive and toned-down motion.

## Requirements

### The record

- **BRIDGE-TOKENS-001** — `MotionTokens` shall be an immutable record, so `with` derives a variant without changing the original and two tokens with equal members compare equal.
  - Tests: `RecipeTests.SwappingTheTokensRestylesLaterCallsButNotBuildersAlreadyMade`, `BuilderRecipePagesTests.MotionTokensReplaysTheRecipesWithTheSelectedPresetAndFlagAndRestoresThePresetOnDetach`
- **BRIDGE-TOKENS-002** — When `ColorSpace` and `Hue` are not given, they shall default to `ColorSpace.Oklab` and `HueInterpolation.Shorter`.
  - Tests: untested

### Presets

- **BRIDGE-TOKENS-003** — `Standard` shall be `Short` 200, `Medium` 450, `Long` 650, `Ease` `Eases.OutCubic`, `EmphasizedEase` `Eases.OutBack()`, `Distance` 24, `Scale` 0.9, `BlurRadius` 12, `StaggerStep` 50.
  - Tests: `RecipeTests.AnExitEndsAtItsStatedValues`, `RecipeTests.StaggerDelaysEachElementOfABatchByTheTokenStep`, `RecipeTests.ReflowTakesItsTimingFromTheTokensAndIsALayoutMotion`
- **BRIDGE-TOKENS-004** — `Expressive` shall be `Short` 450, `Medium` 900, `Long` 1200, `Ease` `Eases.OutBack(2.2)`, `EmphasizedEase` `Eases.OutElastic(1, .5)`, `Distance` 64, `Scale` 0.6, `BlurRadius` 24, `StaggerStep` 90.
  - Tests: `FollowerTests.EachTimingSourceResolves`, `BuilderRecipePagesTests.MotionTokensReplaysTheRecipesWithTheSelectedPresetAndFlagAndRestoresThePresetOnDetach`
- **BRIDGE-TOKENS-005** — `Reduced` shall be `Short` 120, `Medium` 150, `Long` 200, both eases `Eases.None`, `Distance` 0, `Scale` 1, `BlurRadius` 0, `StaggerStep` 0, giving short linear moves with no travel.
  - Tests: untested
- **BRIDGE-TOKENS-006** (inherited) — Each preset property shall return the same instance on every read.
  - Tests: `BuilderRecipePagesTests.RecipeGalleryPlaysTilesInTheirReducedFormsAndRestoresThePresetOnDetach`
- **BRIDGE-TOKENS-007** (inherited) — Choosing the `Reduced` preset shall not set `PrestoDefaults.ReducedMotion`, and setting the flag shall not change `Recipes.Tokens`.
  - Tests: `BuilderRecipePagesTests.RecipeGalleryPlaysTilesInTheirReducedFormsAndRestoresThePresetOnDetach`

### Consumers

- **BRIDGE-TOKENS-008** — Every recipe shall take its default durations, eases, travel and stagger step from `Recipes.Tokens` as BRIDGE-RECIPES states per family.
  - Tests: `RecipeTests.SwappingTheTokensRestylesLaterCallsButNotBuildersAlreadyMade`, `RecipeTests.ReorderLiftGrowsOverARaisedShadowWithTheTokenTimingReadAtCallTime`
- **BRIDGE-TOKENS-009** — `Tints.Recolor` shall mix in `ColorSpace` with `Hue` unless given its own space and hue; no other bridge code reads these two members.
  - Tests: `RecipeTests.RecolorFollowsTheTokensSpaceFromTheCurrentBackgroundAndEndsExactly`
- **BRIDGE-TOKENS-010** — When `FollowParams.Token` is the only timing given, a follower shall ease over the tokens' `Medium` with their `Ease`.
  - Tests: `FollowerTests.EachTimingSourceResolves`
- **BRIDGE-TOKENS-011** — An animated scroll with no duration of its own shall last `Recipes.Tokens.Short` plus 150 ms per viewport traveled, capped at `Long`, on `Ease`.
  - Tests: `ScrollToTests.TheDefaultDurationGrowsWithTheViewportsTraveledAndCaps`
- **BRIDGE-TOKENS-012** — The bridge controls and helpers that animate on their own (`Accordion`, `Pane`, `Flip`, `Modal`, `PagerIndicator`, `ProgressRing`, `PullToRefresh`, `Sheet`, `Ripple`, `Selection`, `SubmitButton`, swipe and transformable release) shall read their default timings from `Recipes.Tokens` when each move starts; their specs give the members.
  - Tests: `ProgressRingTests.AHoldFillsTheRingAndConfirmsAtOneThenRewindsOnRelease`

## Invariants

- A `MotionTokens` instance never changes; restyling means assigning a new one to `Recipes.Tokens`.

## Edge cases

- Tokens with `Distance` 0 or `Scale` 1 make the travelling recipes fade in place.
- Zero `StaggerStep` makes `stagger: true` start every element together.
- Negative or NaN members are not validated; the recipes pass them through.

## Threading and lifetime

- Immutable and thread-agnostic; `Recipes.Tokens` is a plain static read on the UI thread.

## Reduced motion

- Not affected: tokens are data. `MotionTokens.Reduced` is a style for apps that tone motion down without the flag; the flag's reduced forms read `Short` (and for loading, `Long`) from whatever tokens are current.

## Allocation

- Not on the tick path.

## Deviations from anime.js

- No upstream counterpart.

## Decisions

- **Motion tokens live in the library.** The public `MotionTokens` record, moved from the sample, has `Standard`, `Expressive` and `Reduced` presets, and `Recipes.Tokens` supplies every recipe's default duration, ease, distance, scale, blur radius and stagger step; named arguments override one call. Reason: replacing `Recipes.Tokens` restyles the whole app.
- **The eases are named `Ease` and `EmphasizedEase`.** Reason: `Standard` is already a preset name.
- **`ColorSpace` and `Hue` are trailing optional parameters.** They default to `Oklab` and `Shorter`, and `Recipes.Recolor`, a one-shot Tints recipe, mixes in them from each element's current color; reduced motion keeps it. Reason: trailing optional parameters keep the presets source-compatible; no existing recipe had a color state with a known `from` (Glow starts from current, FlashTint snaps, particle palettes are fixed), so a new recipe was needed, and a color change is not motion.
- **Recipes are factories that read the tokens at call time.** See BRIDGE-RECIPES.
- **The default scroll motion grows with the viewports traveled.** See BRIDGE-SCROLL.

## Non-goals

- Per-scope or per-engine tokens: there is one `Recipes.Tokens`, read at call time.
- Change notification when `Recipes.Tokens` is replaced: builders already made keep their values.

## Open items

- The `ColorSpace` XML doc says "the space color states interpolate in", but only `Recolor` reads it; `Motion` color states do not.
- The `Reduced` preset and the `ColorSpace`/`Hue` defaults have no direct test.
