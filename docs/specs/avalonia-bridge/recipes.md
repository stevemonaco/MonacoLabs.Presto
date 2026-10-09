---
id: BRIDGE-RECIPES
title: Recipes
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge
  - Monaco.Presto.AvaloniaBridge.Prebuilt
sources:
  - Monaco.Presto.AvaloniaBridge/Recipes.cs
  - Monaco.Presto.AvaloniaBridge/Prebuilt/Attention.cs
  - Monaco.Presto.AvaloniaBridge/Prebuilt/Entrances.cs
  - Monaco.Presto.AvaloniaBridge/Prebuilt/Exits.cs
  - Monaco.Presto.AvaloniaBridge/Prebuilt/Interactions.cs
  - Monaco.Presto.AvaloniaBridge/Prebuilt/Layouts.cs
  - Monaco.Presto.AvaloniaBridge/Prebuilt/Loading.cs
  - Monaco.Presto.AvaloniaBridge/Prebuilt/Loops.cs
  - Monaco.Presto.AvaloniaBridge/Prebuilt/Pages.cs
  - Monaco.Presto.AvaloniaBridge/Prebuilt/Popovers.cs
  - Monaco.Presto.AvaloniaBridge/Prebuilt/Reveals.cs
  - Monaco.Presto.AvaloniaBridge/Prebuilt/TextReveals.cs
  - Monaco.Presto.AvaloniaBridge/Edge.cs
  - Monaco.Presto.AvaloniaBridge/FadeDirection.cs
types:
  - Recipes
  - Attention
  - Entrances
  - Exits
  - Interactions
  - Layouts
  - Loading
  - Loops
  - Pages
  - Popovers
  - Reveals
  - TextReveals
  - Edge
  - FadeDirection
tests:
  - RecipeTests
  - PageTransitionTests
  - PageBloomTests
  - PopoverRecipeTests
  - TextRevealTests
  - PresenceTextExitTests
  - RollDigitsTests
  - MorphTextTests
  - RecipeMarkupTests
  - PopoverFlyoutTests
  - RevealTests
  - BuilderRecipePagesTests
upstream: []
depends:
  - BRIDGE-TOKENS
  - BRIDGE-REDUCED
  - CORE-BUILDER
  - CORE-REDUCED
  - CORE-STAGGER
  - BRIDGE-TRANSFORMS
  - BRIDGE-EFFECTS
  - BRIDGE-THREAD
  - BRIDGE-PAGES
  - BRIDGE-POPOVER
  - BRIDGE-TEXT
  - BRIDGE-TEXTREVEAL
  - BRIDGE-MOTION
---

# Recipes

## Purpose

Named, parameterized motion: each recipe is a factory that returns a new `AnimationBuilder` (or, for pages, a `PrestoPageTransition`) whose defaults come from `Recipes.Tokens` (BRIDGE-TOKENS) and which carries its own reduced form (CORE-REDUCED). The families live in `Prebuilt`; the `Recipes` facade forwards to them so one `using Monaco.Presto.AvaloniaBridge;` is enough. This spec covers the facade, the rules every recipe shares and the motion families; the media families (`Draws`, `Particles`, `Sprites`, `Tints`) are in BRIDGE-RECIPES-MEDIA and the XAML forms in BRIDGE-MARKUP.

## Requirements

### Shared rules

- **BRIDGE-RECIPES-001** — Each builder recipe shall return a new `AnimationBuilder` per call, so chaining onto one never changes another call's builder.
  - Tests: `RecipeTests.SwappingTheTokensRestylesLaterCallsButNotBuildersAlreadyMade`, `RecipeMarkupTests.EachElementGetsItsOwnBuilder`
- **BRIDGE-RECIPES-002** — `Recipes.Tokens` shall default to `MotionTokens.Standard`, and a recipe shall read it when called: replacing it shall restyle later calls and leave builders already made unchanged.
  - Tests: `RecipeTests.SwappingTheTokensRestylesLaterCallsButNotBuildersAlreadyMade`, `BuilderRecipePagesTests.MotionTokensReplaysTheRecipesWithTheSelectedPresetAndFlagAndRestoresThePresetOnDetach`
- **BRIDGE-RECIPES-003** — When a `duration`, `ease`, `distance`, `scale`, `radius` or `intensity` argument is given, the recipe shall use it in place of its token default for that call only.
  - Tests: `RecipeTests.NamedArgumentsOverrideTheTokens`, `RecipeTests.IntensityScalesTheTravel`
- **BRIDGE-RECIPES-004** — When `delay` is given and `stagger` is false, the recipe shall set it as the builder's delay; when both are unset, it shall set no delay.
  - Tests: untested
- **BRIDGE-RECIPES-005** — When `stagger` is true, the recipe shall delay each element of a batch by `Tokens.StaggerStep` more than the one before, starting at `delay` (`Presto.Stagger(step, new() { Start = delay })`).
  - Tests: `RecipeTests.StaggerDelaysEachElementOfABatchByTheTokenStep`
- **BRIDGE-RECIPES-006** — The intensity-taking recipes shall multiply their change from rest by `intensity` (default 1).
  - Tests: `RecipeTests.IntensityScalesTheTravel`, `RecipeTests.ReorderLiftGrowsOverARaisedShadowWithTheTokenTimingReadAtCallTime`
- **BRIDGE-RECIPES-007** — Every recipe in this spec that animates shall carry a `WhenReduced` alternative, picked by `ToParams` while `PrestoDefaults.ReducedMotion` is set (CORE-REDUCED), except `PageBloom`'s exit and the stateful text recipes, which read the flag themselves.
  - Tests: `RecipeTests.EachReducedFormOnlyFades`, `RecipeTests.EachReducedFormDropsTravelAndScaleAndOnlyLoadingLoops`
- **BRIDGE-RECIPES-008** — Recipes that blur, filter, mask, clip, draw a shadow or rotate in 3D (`BlurIn`, `BlurOut`, `FlipIn`, `FlipOut`, `Develop`, `FlashTint`, `Lift`, `ReorderLift`, `Glow`, `Colorize`, `Shimmer`, the reveals, `WordBlur`/`WordBlurOut`, `LineWipe`/`LineWipeOut`) shall be rejected by `AnimateOnCompositor` with `NotSupportedException`; every other entrance, exit, attention, loop, interaction and loading recipe shall run on both the UI thread and the compositor.
  - Tests: `RecipeTests.EachRecipeRunsOnTheUIThreadAndOnTheCompositorUnlessDocumentedOtherwise`, `RevealTests.TheCompositorRejectsTheShorthandsOnlyTheBridgeRenders`
- **BRIDGE-RECIPES-009** — `Edge` shall map to a signed offset on one transform shorthand: `Left` → `X` negative, `Right` → `X` positive, `Top` → `Y` negative, `Bottom` → `Y` positive.
  - Tests: `RecipeTests.AnExitEndsAtItsStatedValues`, `RecipeMarkupTests.ARecipeOnAPageTransitionsExitsIsTheExitForm`

### Entrances

- **BRIDGE-RECIPES-010** — Each entrance shall state only start values, `Opacity` from 0 among them, so it lands on the element's own resting values.
  - Tests: `RecipeTests.AnEntranceLandsOnTheElementsRestingValues`
- **BRIDGE-RECIPES-011** — An entrance shall default to `Tokens.Medium` and `Tokens.Ease`; `PopIn`, `FlipIn` and `Gather` shall default to `Tokens.EmphasizedEase`, and `Develop` to `Tokens.Long`.
  - Tests: `RecipeTests.SwappingTheTokensRestylesLaterCallsButNotBuildersAlreadyMade`, `RecipeTests.TheImageRecipesTargetTheFilters`
- **BRIDGE-RECIPES-012** — `FadeUp` shall start `Y` at `+distance`, `FadeDown` at `-distance`, `FadeLeft` `X` at `+distance` and `FadeRight` at `-distance`, `distance` defaulting to `Tokens.Distance`.
  - Tests: `RecipeTests.NamedArgumentsOverrideTheTokens`, `RecipeMarkupTests.AFadeIsAnEntranceOnEnterAndAnExitOnExit`
- **BRIDGE-RECIPES-013** — `ScaleIn` and `PopIn` shall start `Scale` at `scale` (default `Tokens.Scale`); `BlurIn` shall start `Blur` at `radius` (default `Tokens.BlurRadius`).
  - Tests: `RecipeTests.AnEntranceLandsOnTheElementsRestingValues`
- **BRIDGE-RECIPES-014** — `SlideIn(edge)` shall start the edge's shorthand at its offset towards `edge` (BRIDGE-RECIPES-009), `distance` defaulting to four times `Tokens.Distance`.
  - Tests: `RecipeTests.AnEntranceLandsOnTheElementsRestingValues`
- **BRIDGE-RECIPES-015** — `FlipIn` shall start `RotateX` at `angle` degrees and set `Perspective` 800 as a to-value.
  - Tests: `RecipeTests.AnEntranceLandsOnTheElementsRestingValues`
- **BRIDGE-RECIPES-016** — `Develop` shall start a `PrestoImageBase` at `Sepia` 0.8, `Brightness` 1.6 and `Blur` `Tokens.BlurRadius`, and settle on its resting values.
  - Tests: `RecipeTests.DevelopSettlesFromSepiaBrightnessAndBlurOntoTheImagesRestingValues`
- **BRIDGE-RECIPES-017** — While `ReducedMotion` is set, an entrance shall become `Opacity` from 0 over `Tokens.Short`, keeping its delay or stagger.
  - Tests: `RecipeTests.EachReducedFormOnlyFades`, `RecipeTests.ReducedMotionMakesAPresenceEnterRecipePlayItsFade`, `RecipeTests.ReducedDevelopAndFlashTintOnlyFadeOrBlink`

### Exits

- **BRIDGE-RECIPES-018** — Each exit shall use to-values only, ending at `Opacity` 0, over `Tokens.Short` with `Tokens.Ease` (`PopOut`, `FlipOut`: `Tokens.EmphasizedEase`; `Scatter` and `Fall`: BRIDGE-RECIPES-070, BRIDGE-RECIPES-071).
  - Tests: `RecipeTests.AnExitEndsAtItsStatedValues`
- **BRIDGE-RECIPES-019** — `FadeOutUp` shall end `Y` at `-distance`, `FadeOutDown` at `+distance`, `FadeOutLeft` `X` at `-distance`, `FadeOutRight` at `+distance`; `ScaleOut`/`PopOut` end `Scale` at `scale`; `BlurOut` ends `Blur` at `radius`; `SlideOut(edge)` ends at its offset towards `edge` (default four times `Distance`); `FlipOut` ends `RotateX` at `angle` (default -90) with `Perspective` 800. The defaults are those of the entrances.
  - Tests: `RecipeTests.AnExitEndsAtItsStatedValues`
- **BRIDGE-RECIPES-020** — While `ReducedMotion` is set, an exit shall become `Opacity` to 0 over `Tokens.Short`.
  - Tests: `RecipeTests.EachReducedFormOnlyFades`

### Attention

- **BRIDGE-RECIPES-021** — Each attention recipe shall be per-property keyframes, each frame `rest + offset × intensity`, ending at the property's identity, over `Tokens.Long` with `Tokens.Ease` on each keyframe.
  - Tests: `RecipeTests.AnAttentionRecipeEndsAtIdentity`, `RecipeTests.IntensityScalesTheTravel`
- **BRIDGE-RECIPES-022** (inherited) — The offsets shall be: `Shake` `X` -10, 10, -6, 6, -3, 3; `Pulse` `Scale` +0.06; `Wobble` `X` -16, 12, -9, 6, -3 with `Rotate` -5, 3, -3, 2, -1; `Jelly` `ScaleX` +0.25, -0.25, +0.15, -0.05, +0.05 with `ScaleY` negated; `Heartbeat` `Scale` +0.15, 0, +0.15; `Tada` `Scale` -0.1, +0.1, +0.1, +0.1, +0.1 with `Rotate` -3, 3, -3, 3, -3; `Bounce` `Y` -20, 0, -10, 0, -4.
  - Tests: `RecipeTests.IntensityScalesTheTravel`
- **BRIDGE-RECIPES-023** — `FlashTint` shall snap `TintColor` to `color` (default `DodgerBlue`) with zero duration and run `Tint` 0 → `intensity` → 0.
  - Tests: `RecipeTests.FlashTintRaisesTheTintInItsColorAndEndsAtIdentity`, `RecipeTests.TheImageRecipesTargetTheFilters`
- **BRIDGE-RECIPES-024** — While `ReducedMotion` is set, an attention recipe shall become `Opacity` keyframes 0.6 → 1 over `Tokens.Short`.
  - Tests: `RecipeTests.EachReducedFormDropsTravelAndScaleAndOnlyLoadingLoops`, `RecipeTests.ReducedDevelopAndFlashTintOnlyFadeOrBlink`

### Loops and loading

- **BRIDGE-RECIPES-025** — `Loops` and `Loading` recipes shall loop until canceled, each iteration lasting twice `Tokens.Long` with `InOutSine` unless stated otherwise.
  - Tests: `RecipeTests.EachRecipeRunsOnTheUIThreadAndOnTheCompositorUnlessDocumentedOtherwise`
- **BRIDGE-RECIPES-026** (inherited) — `Float` shall alternate `Y` to `-(distance ?? Distance / 2)`; `Spin` shall turn `Rotate` to 360 linearly over three times `Long`; `Breathe` shall alternate `Scale` to `1 + 0.05 × intensity`; `Blink` shall alternate `Opacity` to `max(0, 1 - 0.7 × intensity)`.
  - Tests: `RecipeTests.EachRecipeRunsOnTheUIThreadAndOnTheCompositorUnlessDocumentedOtherwise`
- **BRIDGE-RECIPES-027** — While `ReducedMotion` is set, a `Loops` recipe shall animate no property and play one iteration without alternating.
  - Tests: `RecipeTests.EachReducedFormDropsTravelAndScaleAndOnlyLoadingLoops`
- **BRIDGE-RECIPES-028** — `Shimmer` shall sweep `MaskSweep` 0 → 1; `Spinner` shall turn `Rotate` to 360 linearly over `Tokens.Long`; `Indeterminate(distance)` shall move `X` from `-distance` to `distance`.
  - Tests: `RecipeTests.EachRecipeRunsOnTheUIThreadAndOnTheCompositorUnlessDocumentedOtherwise`
- **BRIDGE-RECIPES-029** — `Dots` shall hop each element of a batch to `Y = -(distance ?? Distance / 3)` and back, each a sixth of an iteration after the one before, starting at `delay`.
  - Tests: `RecipeTests.DotsHopOneAfterAnother`
- **BRIDGE-RECIPES-030** — While `ReducedMotion` is set, a `Loading` recipe shall keep looping as an alternating `Opacity` pulse to 0.6 over three times `Long` with `InOutSine`, keeping `Dots`' stagger.
  - Tests: `RecipeTests.EachReducedFormDropsTravelAndScaleAndOnlyLoadingLoops`

### Scatter, fall and wave

- **BRIDGE-RECIPES-070** — `Scatter` shall end each target at a random `X` and `Y` offset, each within ±`distance` (default four times `Tokens.Distance`), a random `Rotate` within ±`spin` degrees (default 180), `Scale` 0.6 and `Opacity` 0, over `Tokens.Medium` with `Tokens.EmphasizedEase`; `Gather` shall start each target from the values `Scatter` with the same arguments ends it at.
  - Tests: `RecipeTests.ScatterEndsEachElementWithinItsDistanceAndSpin`, `RecipeTests.AnExitEndsAtItsStatedValues`, `RecipeTests.AnEntranceLandsOnTheElementsRestingValues`, `RecipeTests.ASeedReplaysTheSameOffsetsAndGatherStartsWhereScatterEnds`, `PresenceTextExitTests.AScatterExitFliesOneGhostPerCharacter`
- **BRIDGE-RECIPES-071** — `Fall` shall end each target at `Y = distance` (default six times `Tokens.Distance`) with `Eases.In(2)` over `Tokens.Long`, a random `X` drift within ±`distance / 4` and a random `Rotate` within ±`spin` degrees (default 45), and fade `Opacity` to 0 over the last third of its duration.
  - Tests: `RecipeTests.FallDropsItsDistanceWithASmallDriftAndFadesOverItsLastThird`, `RecipeTests.AnExitEndsAtItsStatedValues`
- **BRIDGE-RECIPES-072** — When `seed` is given, `Scatter`, `Gather` and `Fall` shall give the target at each index the same values on every play, its draws being numbers `3 × index` onward of `Utils.CreateSeededRandom(seed)`'s stream; without one, each value shall be drawn from `Utils.Random` when the animation is built.
  - Tests: `RecipeTests.ASeedReplaysTheSameOffsetsAndGatherStartsWhereScatterEnds`, `RecipeTests.ScatterEndsEachElementWithinItsDistanceAndSpin`
- **BRIDGE-RECIPES-073** — `Wave` shall alternate `Y` to `-(distance ?? Distance / 2)` forever, each iteration `Tokens.Long` with `InOutSine`, delaying the target at each index by `delay + index × (step ?? StaggerStep)`; it takes no `stagger`.
  - Tests: `RecipeTests.WaveDelaysEachElementByItsIndexTimesTheStep`, `RecipeTests.EachRecipeRunsOnTheUIThreadAndOnTheCompositorUnlessDocumentedOtherwise`, `RecipeTests.EachReducedFormDropsTravelAndScaleAndOnlyLoadingLoops`

### Interactions

- **BRIDGE-RECIPES-031** — Interaction recipes shall use absolute to-values over `Tokens.Short` with `Tokens.Ease`, so a `Motion` state holds them and returns to rest when left.
  - Tests: `RecipeTests.AnInteractionWorksAsAHoverStateAndReturnsToRest`
- **BRIDGE-RECIPES-032** (inherited) — `Lift` shall set `ShadowBlur` 12 × intensity and `ShadowOpacity` 0.25 and move `Y` to `-(distance ?? Distance / 6) × intensity`; reduced, it shall keep only the shadow.
  - Tests: `RecipeTests.TogglingReducedMotionTakesEffectAtTheNextTransition`, `RecipeMarkupTests.LiftPlaysOnHoverAndShakePlaysOnAttach`
- **BRIDGE-RECIPES-033** — `Grow` shall scale to `1 + 0.04 × intensity` and do nothing reduced; `PressIn` shall set `Opacity` `1 - 0.2 × intensity` and scale to `1 - 0.04 × intensity`, keeping only the opacity reduced.
  - Tests: `RecipeTests.AnInteractionWorksAsAHoverStateAndReturnsToRest`
- **BRIDGE-RECIPES-034** — `ReorderLift` shall set `ShadowBlur` 16 × intensity, `ShadowY` 6 × intensity, `ShadowOpacity` 0.3 and `Scale` `1 + ((scale ?? 1.03) - 1) × intensity`, leaving `X` and `Y` alone; reduced, only the three shadow properties.
  - Tests: `RecipeTests.ReorderLiftGrowsOverARaisedShadowWithTheTokenTimingReadAtCallTime`, `RecipeTests.ReducedReorderLiftOnlyRaisesTheShadow`
- **BRIDGE-RECIPES-035** — `Glow` (`ShadowBlur` 16 × intensity, `ShadowOpacity` 0.8, `ShadowColor` `color ?? DodgerBlue`), `Dim` (`Opacity` `1 - 0.4 × intensity`) and `Colorize` (`Grayscale` 0, `Saturate` 1) shall be the same with and without reduced motion.
  - Tests: `RecipeTests.AnInteractionWorksAsAHoverStateAndReturnsToRest`, `RecipeTests.ColorizeHoversAGrayscaleImageToFullColorAndBack`

### Layout, popovers and reveals

- **BRIDGE-RECIPES-036** — `Reflow` shall set only timing (`duration ?? Medium`, `delay`, `ease ?? Ease`), usable as a layout motion; reduced, its duration and delay shall be 0.
  - Tests: `RecipeTests.ReflowTakesItsTimingFromTheTokensAndIsALayoutMotion`, `RecipeTests.ReducedReflowMovesInstantly`
- **BRIDGE-RECIPES-037** — `PopoverIn` shall start at `Opacity` 0, `Scale` `scale ?? Tokens.Scale` and an `X`/`Y` offset of `distance ?? Distance / 2` towards the anchor from the presenter's edge facing it; `PopoverOut` shall end at the same values; both over `Tokens.Short`.
  - Tests: `PopoverRecipeTests.ThePresenterSlidesFromAndBackIntoTheEdgeFacingItsAnchor`, `RecipeMarkupTests.PopoverIsAnEntranceOnOpenAndAnExitOnClose`
- **BRIDGE-RECIPES-038** — When the popover recipe resolves the facing edge, it shall read the presenter's placed rect for `PopoverOut`, and for `PopoverIn` predict it from `Popup.Placement`, flipped where the presenter would not fit in the window (or screen for a native popup); a `Center` placement shall travel nowhere and a target that is not a popup's child shall use `Top`.
  - Tests: `PopoverRecipeTests.ThePresenterSlidesFromAndBackIntoTheEdgeFacingItsAnchor`, `PopoverRecipeTests.AContextMenuAtThePointerSlidesDownFromItsTopEdge`, `PopoverRecipeTests.AComboBoxDropDownStyledThroughItsTemplateSlidesDownFromItsTopEdge`
- **BRIDGE-RECIPES-039** — While `ReducedMotion` is set, `PopoverIn` shall fade `Opacity` from 0 and `PopoverOut` to 0, over `Tokens.Short`.
  - Tests: `PopoverFlyoutTests.UnderReduceAllCloseIsInstantAndOpenPlaysOnlyTheReducedFade`
- **BRIDGE-RECIPES-040** — `CircularReveal` shall run `ClipCircle` 0 → 1 around a fixed `ClipCircleCenter` (the element's center by default, or `from`'s center translated into the target) over `Tokens.Long`; reduced, `Opacity` from 0 over `Short`.
  - Tests: `PageBloomTests.ACircularRevealFromAnElementGrowsFromItsCenter`, `PageBloomTests.ReducedFadesTheNewPageInWithNoBloom`
- **BRIDGE-RECIPES-041** — `ThemeReveal` shall snapshot `surface`, run `change`, lay out, snapshot again and reveal the new picture over the old one in the surface's overlay layer from `from`'s center, removing both pictures and disposing them when the reveal ends.
  - Tests: `PageBloomTests.AThemeRevealShowsTheNewLookInACircleOverTheOldThenRemovesItsPictures`
- **BRIDGE-RECIPES-042** — When a `ThemeReveal` starts on a surface whose earlier reveal is still running, the earlier one shall be removed first.
  - Tests: `PageBloomTests.ASecondThemeRevealReplacesTheFirst`
- **BRIDGE-RECIPES-043** (inherited) — When `surface` has no overlay layer, `ThemeReveal` shall throw `InvalidOperationException` before running `change`.
  - Tests: untested

### Pages

- **BRIDGE-RECIPES-044** — Each page recipe shall return a new `PrestoPageTransition` whose sides default to `Tokens.Medium` and `Tokens.Ease`, and whose properties all have composition counterparts except `PageBloom`'s.
  - Tests: `RecipeTests.EveryPropertyOfAPageRecipeHasACompositionCounterpart`, `RecipeMarkupTests.PageMarkupExtensionsProduceTransitionsWithTheirSettings`
- **BRIDGE-RECIPES-045** — `PageFade` shall fade the incoming page from 0 and the outgoing one to 0 over `duration`.
  - Tests: `RecipeMarkupTests.PageMarkupExtensionsProduceTransitionsWithTheirSettings`
- **BRIDGE-RECIPES-046** — `PageFadeThrough` shall fade the outgoing page out over the first 30% of `duration`, then fade the incoming one in growing from 0.92 over the remaining 70%, and play the same pair going back.
  - Tests: `RecipeMarkupTests.PageMarkupExtensionsProduceTransitionsWithTheirSettings`
- **BRIDGE-RECIPES-047** — `PageSharedAxis` shall fade both pages, the incoming over `Medium` and the outgoing over `Short`; along `X` or `Y` the incoming one arrives from `+distance` (default `Distance`) and the outgoing one leaves to `-distance`; along `Z` the incoming one grows from `Tokens.Scale` and the outgoing one to `2 - Tokens.Scale`.
  - Tests: `RecipeMarkupTests.PageMarkupExtensionsProduceTransitionsWithTheirSettings`, `PageTransitionTests.ASharedAxisScaleOnTheCompositorHonorsTheHostsOrigin`
- **BRIDGE-RECIPES-048** — `PageSlide(edge)` shall bring the incoming page in from a full host size towards `edge` and move the outgoing one a full size out the opposite side, the size read from the host's visual parent when the transition starts.
  - Tests: `RecipeTests.APageSlideTravelsTheWidthOfTheHostsParent`
- **BRIDGE-RECIPES-049** — `PagePush(edge)` shall slide the incoming page as `PageSlide` does while the outgoing one moves a third of the size out the opposite side and dims to `Opacity` 0.6.
  - Tests: `RecipeMarkupTests.PageMarkupExtensionsProduceTransitionsWithTheirSettings`
- **BRIDGE-RECIPES-050** — While `ReducedMotion` is set, each side of `PageFade`, `PageFadeThrough`, `PageSharedAxis`, `PageSlide` and `PagePush` shall be an `Opacity` fade (in from 0, out to 0) over `Short` with no delay.
  - Tests: `RecipeTests.ReducedMotionPlaysEachPageRecipesShortCrossfade`
- **BRIDGE-RECIPES-051** — `PageBloom` shall reveal the incoming page with `IncomingAbove` stacking in a `ClipCircle` over `Long` with `InOutQuart`, from the last pointer press in its window, or the host's center when a key was pressed since; the bloom that starts spends the press.
  - Tests: `PageBloomTests.TheClipGrowsFromThePressedPoint`, `PageBloomTests.TheKeyboardOriginIsTheCenter`, `PageBloomTests.ThePressIsSpentByTheBloomItStarted`
- **BRIDGE-RECIPES-052** — `PageBloom`'s exit shall finish the outgoing page's circle to 1 over `Short`, so interrupted blooms never stack, and both directions shall bloom.
  - Tests: `PageBloomTests.RapidNavigationLeavesNoStackedLayers`
- **BRIDGE-RECIPES-053** — When a bloom begins and `ReducedMotion` is clear, the children of the incoming page's first `Panel` shall rise from `Y = Distance` and fade in over `Medium`, staggered by `StaggerStep` from the center, starting at 65% of `Long`; reduced, the page shall fade in over `Short` and the children shall not move.
  - Tests: `PageBloomTests.TheNewPagesChildrenRiseInFromTheCenterAt65Percent`, `PageBloomTests.ReducedFadesTheNewPageInWithNoBloom`

### Text reveals

- **BRIDGE-RECIPES-054** — Each text reveal shall delay its pieces by `TextSplitter.StaggerByLine(lineStep ?? 3 × StaggerStep, pieceStep, delay)` and take no `stagger`.
  - Tests: `TextRevealTests.WordFadeStartsEachLineAfterEveryWordOfTheLineBefore`, `PresenceTextExitTests.CharCascadeOutStartsEachLinesGhostsAfterTheLineBefore`
- **BRIDGE-RECIPES-055** — `LineRise` shall start lines at `Opacity` 0 one line height down; `LineMask` one line height down without fading; `LineWipe` at `ClipInset` `"0 0 100% 0"` to `"0 0 0% 0"`; all over `Long` with `pieceStep` 0.
  - Tests: `TextRevealTests.LineMaskRisesEachLineBehindItsClipToRest`, `TextRevealTests.LineWipeEndsWithNoClipOnAnyLine`
- **BRIDGE-RECIPES-056** — `WordFade` shall start words at `Opacity` 0 and `Y = Distance`, `WordBlur` at `Opacity` 0 and `Blur = BlurRadius`, both over `Medium` with `pieceStep` half `StaggerStep`; `CharCascade` shall start characters at `Opacity` 0 and `Y = -Distance` over `Medium` with `EmphasizedEase` and `pieceStep` a fifth of `StaggerStep`.
  - Tests: `TextRevealTests.WordFadeStartsEachLineAfterEveryWordOfTheLineBefore`, `TextRevealTests.CharCascadeAnimatesEveryCharacter`
- **BRIDGE-RECIPES-057** — Each `Out` form shall mirror its reveal with to-values and the same timing: `LineRiseOut` to `Opacity` 0 one line up, `LineMaskOut` one line up, `LineWipeOut` `ClipInset` `"0% 0 0 0"` → `"100% 0 0 0"`, `WordFadeOut` to `Y = -Distance`, `WordBlurOut` to `Blur = BlurRadius`, `CharCascadeOut` to `Y = +Distance`.
  - Tests: `PresenceTextExitTests.TextExitRecipesAnimateEveryGhostToItsEndAndDismissTheHost`
- **BRIDGE-RECIPES-058** — While `ReducedMotion` is set, a text reveal shall become `Opacity` from 0 and an `Out` form `Opacity` to 0, over `Short`.
  - Tests: `TextRevealTests.ReducedTextRevealsFadeTheBlockUnsplit`, `PresenceTextExitTests.ReducedTextExitRecipesFadeOneWholeGhostOverShort`

### Stateful text recipes

- **BRIDGE-RECIPES-059** — `Typewriter` shall split the block into characters, fade each in from 0 over `duration ?? charDelay`, each `charDelay ?? StaggerStep` after the one before starting at `delay`, and revert the split when the animation completes.
  - Tests: `RecipeTests.TypewriterRevealsTheCharactersInOrder`
- **BRIDGE-RECIPES-060** — When `ReducedMotion` is set as `Typewriter` is called, it shall play `Fade(delay: delay)` on the whole block instead.
  - Tests: `RecipeTests.ReducedTypewriterFadesTheWholeBlock`
- **BRIDGE-RECIPES-061** — `CountUp` shall tween `Text` from the number the block shows (parsed in the current culture, or 0) to `to`, formatting each value with `format ?? "N0"` in the current culture, over `Long`; reduced, it shall set the formatted end value on the first frame.
  - Tests: `RecipeTests.CountUpEndsOnTheFormattedValue`, `RecipeTests.ReducedCountUpSetsTheEndValueOnTheFirstFrame`
- **BRIDGE-RECIPES-062** — `RollDigits` shall roll only the characters that change, each column's old glyph leaving and new glyph entering by one line height (upwards when counting up, downwards when counting down) inside a clip, columns starting right to left `StaggerStep` apart, over `Medium`, and end as the plain formatted text.
  - Tests: `RollDigitsTests.OnlyTheChangedDigitsRoll`, `RollDigitsTests.MidRollTheOldGlyphIsAboveAndTheNewBelowInsideTheClip`, `RollDigitsTests.ColumnsStartRightToLeft`, `RollDigitsTests.CountingDownRollsTheOtherWay`, `RollDigitsTests.EndsAsThePlainFormattedText`, `RollDigitsTests.AddingADigitAddsAColumnThatRollsIn`
- **BRIDGE-RECIPES-063** — While rolling, the block shall keep the plain text's line box.
  - Tests: `RollDigitsTests.TheTextKeepsItsLineBoxWhileRolling`
- **BRIDGE-RECIPES-064** — When `RollDigits` is called on a block that is still rolling, it shall complete the running roll first and roll from its end value.
  - Tests: `RollDigitsTests.RollingAgainMidRollEndsOnTheSecondValue`
- **BRIDGE-RECIPES-065** — When `ReducedMotion` is set as `RollDigits` is called, it shall play `CountUp`'s reduced form, setting the end text on the first frame.
  - Tests: `RollDigitsTests.ReducedSetsTheEndValueOnTheFirstFrame`
- **BRIDGE-RECIPES-074** — `MorphText` shall split the block into characters and pair them by grapheme, the k-th occurrence of a grapheme in the new text with the k-th in the old one, in reading order, comparing with `StringComparer.OrdinalIgnoreCase` when `ignoreCase` is true and ordinally otherwise.
  - Tests: `MorphTextTests.TheKthOccurrenceOfALetterPairsWithTheKthInTheNewText`, `MorphTextTests.IgnoreCasePairsLettersThatDifferOnlyInCase`
- **BRIDGE-RECIPES-075** — Each paired new character shall start at `X`/`Y` = its partner's old slot minus its own slot (`TextSplitter.SlotOf`), plus how far the block moved in its window, so it is drawn where its partner was, on any line, and move to 0 over `Tokens.Long` with `Tokens.EmphasizedEase`.
  - Tests: `MorphTextTests.MatchedLettersStartAtTheirOldSlotsAndEndAtTheirNewOnes`, `MorphTextTests.LettersFlyBetweenLines`
- **BRIDGE-RECIPES-076** — Each old character without a partner shall leave as a snapshot at its old slot in the window's overlay layer, playing `Scatter()`; each new character without a partner shall play `PopIn()`; both start with the morph.
  - Tests: `MorphTextTests.UnmatchedOldLettersLeaveAsGhostsThatAreDismissed`, `MorphTextTests.UnmatchedNewLettersPopIn`
- **BRIDGE-RECIPES-077** — With `explode`, each paired character shall first move over `Tokens.Medium` with `Tokens.EmphasizedEase` to its partner's place offset by `Scatter`'s `X`, `Y` and `Rotate` for seed 0 at its index among the paired characters, hold there for `Tokens.Short`, then move `X`, `Y` and `Rotate` to 0 over `Tokens.Long`.
  - Tests: `MorphTextTests.ExplodeHoldsTheMatchedLettersAtSeededPointsBeforeGathering`
- **BRIDGE-RECIPES-078** — When the morph completes, the block shall be unsplit with `Text` equal to `text`, and the ghosts removed from the overlay layer with their bitmaps disposed.
  - Tests: `MorphTextTests.EndsAsThePlainNewText`, `MorphTextTests.UnmatchedOldLettersLeaveAsGhostsThatAreDismissed`
- **BRIDGE-RECIPES-079** — When `MorphText` is called on a block that is still morphing, it shall complete the running morph first and morph from its end text.
  - Tests: `MorphTextTests.MorphingAgainMidMorphCompletesTheFirst`
- **BRIDGE-RECIPES-080** — When `ReducedMotion` is set as `MorphText` is called, it shall not split the block and shall return a timeline that fades `Opacity` to 0 over `Tokens.Short`, sets `Text`, and fades from 0 back to the block's opacity over `Tokens.Short`.
  - Tests: `MorphTextTests.ReducedCrossfadesTheBlock`

### Defaults

- **BRIDGE-RECIPES-066** (inherited) — When `angle` is not given, `FlipIn` shall start `RotateX` at 90 degrees.
  - Tests: untested
- **BRIDGE-RECIPES-067** — When `ease` is not given, the popover recipes, `CircularReveal`, `ThemeReveal`, the text reveals other than `CharCascade` and `CharCascadeOut`, `Typewriter`, `CountUp` and `RollDigits` shall use `Tokens.Ease`, and `ThemeReveal` shall last `Tokens.Long` when `duration` is not given.
  - Tests: untested
- **BRIDGE-RECIPES-068** (inherited) — When `from` is not given, `ThemeReveal` shall reveal from the surface's center.
  - Tests: untested
- **BRIDGE-RECIPES-069** — When `format` is not given, `RollDigits` shall format with `"N0"` in the current culture.
  - Tests: `RollDigitsTests.AddingADigitAddsAColumnThatRollsIn`

## Invariants

- A recipe never caches a builder: two calls with the same arguments return distinct, equal builders.
- Every recipe reads `Recipes.Tokens` once per call; the tokens of one call are never mixed with another's.
- Entrances and text reveals state from-values only; exits, attention, loops, interactions and popover-out state to-values only.

## Edge cases

- `Recipes.Tokens = MotionTokens.Reduced` gives zero travel and linear timing without setting `ReducedMotion`.
- `Dots` and `Indeterminate` take no `stagger`/need a `distance` respectively; `Dots`' `delay` becomes its stagger start.
- `RollDigits`, `Typewriter` and `MorphText` need a laid-out block: `RollDigits` reads `TextLayout.TextLines[0]`, the others split by wrapped lines.
- `MorphText` lays the block out with the new text before splitting it, so the new split does not re-split when the block resizes; a width change mid-morph re-splits, and the morph finishes on the replaced pieces. A block in no window morphs without ghosts and without the window-position correction.
- `PageBloom` with no `Panel` in the incoming page reveals the page and animates no children.

## Threading and lifetime

- UI thread only, like all bridge calls. Builders hold no element; the stateful text recipes and `ThemeReveal` return a playing `Animation` (`MorphText` a `Timeline`) on the target's engine.
- `PageBloom` installs one tunneling `PointerPressed`/`KeyDown` class handler on `TopLevel` on first use, process-wide, and keys the last press per window in a weak table.
- `RollDigits`, `MorphText` and `ThemeReveal` track the running animation per block/surface in a weak table.

## Reduced motion

- Each family's reduced form is a requirement above: entrances, exits, text reveals, popovers, reveals and pages fade over `Short`; attention blinks; loops are static; loading pulses; interactions keep color, opacity and shadow; `Reflow` lands at once; `MorphText` crossfades the unsplit block.
- The reduced forms inherit the full form's settings, so delays, staggers and callbacks survive (CORE-REDUCED).

## Allocation

- Not on the tick path: each call allocates a builder and its closures. The function values recipes install (`Across`, popover `Travel`, `OneLineDown`, the random offsets) run once per target when the animation is built.

## Deviations from anime.js

- No upstream counterpart: anime.js ships no recipes. The family namespace `Prebuilt` replaces the planned `...AvaloniaBridge.Recipes`, because a namespace of that name cannot sit beside the `Recipes` type (CS0101).

## Decisions

- **Recipes are factories that read the tokens at call time.** Each recipe call returns a new `AnimationBuilder` (a page recipe a new `PrestoPageTransition`) reading `Recipes.Tokens` when called; stateful recipes are methods returning the playing `Animation` or small bridge types. Reason: chaining `.Delay(…)` onto one call never touches another caller's copy, and replacing the tokens restyles later calls while builders already made keep their values. Rejected: shared static builders.
- **The families live in `Prebuilt`, the facade in the root namespace.** The catalog is in the bridge, one class per family in `Monaco.Presto.AvaloniaBridge.Prebuilt`; `Recipes` (a forwarding facade), `Edge` and the markup extensions are in the root namespace. Reason: nearly every recipe names a bridge shorthand; a `Monaco.Presto.AvaloniaBridge.Recipes` namespace cannot sit beside the `Recipes` type (CS0101), and root placement lets one `using` cover `Recipes.SlideIn(Edge.Left)` and the existing `presto` XAML prefix cover the extensions. Rejected: the namespace `Monaco.Presto.AvaloniaBridge.Recipes`.
- **Entrances state from-values; every other family states to-values.** `Connected.DefaultAnchorMotion` is from-only for the same reason. Reason: a from-only entrance lands on the element's own resting values (a card resting at `Opacity` 0.6 lands on 0.6), and a `Motion` state rejects from-only values.
- **Entrances and exits are compositor-safe, with documented exceptions.** They use transform, opacity and per-property keyframes only, so they run through `AnimateOnCompositor` as well as `Presence` and `Animate.OnAttached`; the blur and flip recipes and `Develop` are UI-thread only. Reason: the compositor has no `RotateX`, `Perspective` or filters.
- **Every recipe takes the same optional arguments, and `stagger` is a bool.** `duration`, `delay`, `ease`, `distance` or `intensity`, and `stagger`, which delays each element by `Tokens.StaggerStep` after `delay`; another step is chained with `.Delay(Presto.Stagger(…))`. Interactions take no `stagger`, and `Dots` staggers itself. Reason: a `Motion` state applies to one element, and `Dots` staggers its batch by a sixth of an iteration. Rejected: `stagger` as a step value.
- **Every recipe carries its own reduced form.** Entrances and exits become short fades, attention blinks `Opacity` 1 → 0.6 → 1, loops are static, loading indicators become a slow `Opacity` pulse, and interactions keep color and shadow but drop travel and scale. Reason: loading pulses so a wait still shows; otherwise not recorded.
- **Loops ease with `InOutSine`, not the token ease.** `Loops` and `Loading` iterate over twice `Long` with `InOutSine`, the spins linearly. Reason: the token ease would stall a loop at each turn.
- **The image recipes follow their families and have no markup extensions.** `Colorize` is an interaction, `Develop` an entrance that also fades in over `Long`, and `FlashTint` an attention recipe that sets `TintColor` with a zero-duration tween; reduced, `FlashTint` keeps the attention blink and `Colorize` its color change. Reason: interactions keep color when reduced like the others; no family has an extension for every recipe (`Grow`, `Dim`, `FlipIn`, `Pulse` have none).
- **`Typewriter` reads the reduced flag at call time.** It plays `Fade` on the whole block when reduced instead of using `WhenReduced`; `CountUp` uses `WhenReduced`. Reason: `Typewriter`'s reduced form animates the block rather than the split characters.
- **`Typewriter` stays the code-first form.** It keeps returning an `Animation` on a laid-out block, with `CharCascade` as the trigger form. Reason: rebuilding it on the retargeting machinery would change its return type.
- **`RollDigits` builds its own column row.** It builds clipped columns inside one `InlineUIContainer`, with margins from the first line's metrics so the line box matches the plain text, and rolls straight from old to new. Reason: a `TextSplitter` piece holds a single glyph and cannot gain or lose a column; without the margins Consolas drew 3 px taller on desktop. Rejected: `TextSplitter`; counting through the values between, as `CountUp` does.
- **`Reflow` is a `Layouts` family of its own and takes no `stagger`.** Reason: the `Interactions` family's "short, reduced keeps color" shape does not fit, and a stagger makes the delay per-target, which `LayoutMotion.From` rejects. Rejected: placing it in `Interactions`.
- **Page recipes are transform and opacity only.** They live in `Prebuilt.Pages` and return a new transition reading the tokens when called. Reason: so they run on the compositor.
- **Page recipe shapes.** `PageFadeThrough` spends 30% of its duration on the exit, delays the enter by as much, and plays the same pair going back; `PageSharedAxis` exits over `Short`, enters over `Medium` and ignores `distance` along Z; `PagePush` dims the outgoing page to 0.6. Reason: fade-through stages the same way both ways; shared-axis timing matches the entrance and exit families.
- **`PageBloom` is a page recipe whose origin is the last press.** It returns a `PrestoPageTransition` (a `ClipCircle` reveal, `IncomingAbove`); a tunneling `TopLevel` handler records the last press per window, a key press clears it and the bloom that uses it spends it; its tiles are the first `Panel`'s children. Reason: it inherits the take-over and revert rules of the page recipes, and a transition is handed only the two hosts. Rejected: a transition type of its own; a colored disk over the old page.
- **`PopoverIn` predicts the facing edge from the placement.** It reads `Popup.Placement`, flipped by measured fit; a `Center` placement does not slide. Reason: nothing reports a constraint flip, and at open the placed rects do not exist yet.
- **Random recipes are seeded.** `Scatter`, `Gather` and `Fall` take an optional `seed`; with one, the target at each index draws from a fixed position of that seed's stream, so the values do not depend on how often the builder was played, and without one they draw from the shared generator. `X` and `Y` are drawn independently, so `Scatter` fills a square. Reason: reproducible tests, and a replay that looks the same. Rejected: one generator consumed across plays, which would give a second play new values.
- **`Wave` steps by index, not by line.** Reason: it is a loop recipe used on any batch, and on a split line the index is reading order. A multi-line wave can chain `.Delay(TextSplitter.StaggerByLine(…))`.
- **Recipes vs. not.** `Scatter`, `Gather`, `Fall`, `Wave` and `MorphText` are recipes because each is a reusable verb on any batch or block; `TextPath` is layout, not motion (it holds state, has no duration and is driven by other animations), and `SplitFlap` is a control that owns per-cell state and draws tiles a `TextBlock` cannot. A "slam" entrance is `PopIn(scale: 2.5)`. Rejected: `Recipes.Slam`, a `Recipes.AlongPath` builder.
- **`MorphText` pairs by k-th occurrence, case-sensitive unless `ignoreCase`.** Pairing is in reading order. Reason: it is deterministic and cheap, reading order keeps letters from crossing needlessly, and `ignoreCase` lets "Listen" → "Silent" pair the `L`. Rejected: nearest-slot matching, which is order-dependent and costs O(n²) for little visual gain.
- **`MorphText` follows `RollDigits`' lifetime rules.** It returns the playing `Timeline`; a second call on a block still morphing completes the first; it reads the reduced flag at call time and, when reduced, crossfades the block (`Opacity` to 0, set the text, back from 0, `Tokens.Short` each). Reason: one consistent rule for stateful text recipes (BRIDGE-RECIPES-064, the `Typewriter` decision).
- **`MorphText` FLIPs over `SlotOf`.** It captures the old slots, sets the new text, lays the block out, splits it and starts each paired piece at its partner's old slot, corrected by how far the block moved in its window (a centered title moves as its width changes). Ghosts are snapshots in the overlay layer, as `Presence` exits leave. Reason: nothing is measured offscreen, and the old split is gone before the new one exists. Rejected: `TextSplitter.Measure(block, text)`, a second layout path that would have to match the real one.
- **`MorphText` starts every part at once and seeds `explode` with 0.** Ghosts scatter and new letters pop in as the paired ones fly. Reason: a child placed later on the timeline does not write its from-values before it starts, so a delayed `PopIn` would show its letter at rest until then; the fixed seed makes the same pair of texts burst the same way on every play, as the seeded recipes do.
- **Motion tokens live in the library.** See BRIDGE-TOKENS.
- **Reduced motion is an app-set flag with a per-builder alternative.** See CORE-REDUCED.
- **`{presto:Reflow}` is the shorthand, and its reduced form is instant.** See BRIDGE-DECLLAYOUT.
- **One markup extension per family verb, with the exit form detected from the target.** See BRIDGE-MARKUP.
- **Reduced text reveals never split, and one attribute configures the block.** See BRIDGE-TEXTREVEAL.

## Non-goals

- Shared static builders, recipe registration by name, and timelines or keyframes authored in XAML (BRIDGE-ATTACH).
- A `stagger` on state recipes: a `Motion` state applies to one element.

## Open items

- `Interactions.PressIn`'s summary says it shrinks to 0.96, but the full form also dims `Opacity` to 0.8 (the "kept" part is applied to both forms); code wins.
- `Loading`'s remarks say each iteration lasts twice `Long`, but `Spinner` defaults to one `Long`.
- `PageBloom`'s exit (`ClipCircle` to 1 over `Short`) has no `WhenReduced` alternative, contrary to the rule that every recipe supplies a reduced form (Every recipe carries its own reduced form).
- `PageSharedAxis` takes no duration: its sides are fixed at `Medium` and `Short`, unlike `PageFade`/`PageFadeThrough`.
- The recorded decision that entrances and exits are compositor-safe named only `BlurIn`/`BlurOut`/`FlipIn`/`FlipOut` as exceptions; `Develop` is UI-thread-only too, and the decision below includes it.
- `ThemeReveal` throwing without an overlay layer, delay-only (no stagger) timing, and the `PageSharedAxis` `Y`/`X` exit travel lack direct tests.
