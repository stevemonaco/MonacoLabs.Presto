---
id: BRIDGE-MARKUP
title: Recipe markup extensions
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/RecipeExtension.cs
  - MonacoLabs.Presto.Avalonia/BlurExtension.cs
  - MonacoLabs.Presto.Avalonia/CharCascadeExtension.cs
  - MonacoLabs.Presto.Avalonia/FadeExtension.cs
  - MonacoLabs.Presto.Avalonia/GlowExtension.cs
  - MonacoLabs.Presto.Avalonia/LiftExtension.cs
  - MonacoLabs.Presto.Avalonia/LineMaskExtension.cs
  - MonacoLabs.Presto.Avalonia/LineRiseExtension.cs
  - MonacoLabs.Presto.Avalonia/LineWipeExtension.cs
  - MonacoLabs.Presto.Avalonia/PageBloomExtension.cs
  - MonacoLabs.Presto.Avalonia/PageFadeExtension.cs
  - MonacoLabs.Presto.Avalonia/PageFadeThroughExtension.cs
  - MonacoLabs.Presto.Avalonia/PagePushExtension.cs
  - MonacoLabs.Presto.Avalonia/PageSharedAxisExtension.cs
  - MonacoLabs.Presto.Avalonia/PageSlideExtension.cs
  - MonacoLabs.Presto.Avalonia/PlayFramesExtension.cs
  - MonacoLabs.Presto.Avalonia/PopExtension.cs
  - MonacoLabs.Presto.Avalonia/PopoverExtension.cs
  - MonacoLabs.Presto.Avalonia/PressInExtension.cs
  - MonacoLabs.Presto.Avalonia/RecolorExtension.cs
  - MonacoLabs.Presto.Avalonia/ReflowExtension.cs
  - MonacoLabs.Presto.Avalonia/ScaleExtension.cs
  - MonacoLabs.Presto.Avalonia/ScatterExtension.cs
  - MonacoLabs.Presto.Avalonia/ShakeExtension.cs
  - MonacoLabs.Presto.Avalonia/ShimmerExtension.cs
  - MonacoLabs.Presto.Avalonia/SlideExtension.cs
  - MonacoLabs.Presto.Avalonia/WordBlurExtension.cs
  - MonacoLabs.Presto.Avalonia/WordFadeExtension.cs
types:
  - RecipeExtension
  - BlurExtension
  - CharCascadeExtension
  - FadeExtension
  - GlowExtension
  - LiftExtension
  - LineMaskExtension
  - LineRiseExtension
  - LineWipeExtension
  - PageBloomExtension
  - PageFadeExtension
  - PageFadeThroughExtension
  - PagePushExtension
  - PageSharedAxisExtension
  - PageSlideExtension
  - PlayFramesExtension
  - PopExtension
  - PopoverExtension
  - PressInExtension
  - RecolorExtension
  - ReflowExtension
  - ScaleExtension
  - ScatterExtension
  - ShakeExtension
  - ShimmerExtension
  - SlideExtension
  - WordBlurExtension
  - WordFadeExtension
tests:
  - RecipeMarkupTests
  - RecipeTests
upstream: []
depends:
  - BRIDGE-RECIPES
  - BRIDGE-RECIPES-MEDIA
  - BRIDGE-TEXTREVEAL
  - BRIDGE-PRESENCE
  - BRIDGE-POPOVER
  - BRIDGE-PAGES
  - CORE-EASEPARSER
  - CORE-STAGGER
---

# Recipe markup extensions

## Purpose

The XAML forms of the recipes, `{presto:Fade Direction=Up}` and friends: each extension calls its `Recipes` factory (BRIDGE-RECIPES) and provides the builder or page transition it returns. The `RecipeExtension` base adds the shared settings and picks a recipe's exit form from the property it is set on. The text reveal extensions derive from `TextRevealExtension` (BRIDGE-TEXTREVEAL), which also configures the block.

## Requirements

### RecipeExtension

- **BRIDGE-MARKUP-001** (inherited) — `ProvideValue` shall call its recipe's factory on each call, so each element the extension is applied to gets its own builder.
  - Tests: `RecipeMarkupTests.EachElementGetsItsOwnBuilder`
- **BRIDGE-MARKUP-002** — When `Out` is null and the target property is `Presence.ExitProperty` or `Popover.CloseProperty`, the extension shall provide the recipe's exit form.
  - Tests: `RecipeMarkupTests.AFadeIsAnEntranceOnEnterAndAnExitOnExit`, `RecipeMarkupTests.PopoverIsAnEntranceOnOpenAndAnExitOnClose`, `RecipeMarkupTests.TextRevealsAreTheirExitFormOnExitAndTheirEntranceOnEnter`
- **BRIDGE-MARKUP-003** — When `Out` is null and the target is a `PrestoPageTransition`'s `Exit` or `BackExit` property, the extension shall provide the exit form; on `Enter` and `BackEnter` the entrance form.
  - Tests: `RecipeMarkupTests.ARecipeOnAPageTransitionsExitsIsTheExitForm`
- **BRIDGE-MARKUP-004** — When `Out` is null and the target is anything else, including a style `Setter` and a call with no service provider, the extension shall provide the entrance form.
  - Tests: `RecipeMarkupTests.AFadeIsAnEntranceOnEnterAndAnExitOnExit`
- **BRIDGE-MARKUP-005** — When `Out` is true the extension shall provide the exit form, and when false the entrance form, whatever the target.
  - Tests: `RecipeMarkupTests.OutTrueGivesTheExitFormInAStyleSetter`, `RecipeMarkupTests.TextRevealExtensionsProvideTheirRecipe`
- **BRIDGE-MARKUP-006** — When the exit form is asked for (BRIDGE-MARKUP-002/003/005) of an extension whose recipe has none (`Shake`, `Lift`, `PressIn`, `Glow`, `Shimmer`, `Recolor`, `Reflow`, `PlayFrames`, and BRIDGE-REORDER's `Reorder`), `ProvideValue` shall throw `NotSupportedException`; asked for the entrance form, it shall provide its recipe.
  - Tests: `RecipeMarkupTests.AnExtensionWithNoExitFormThrowsWhereTheExitFormIsAskedFor`
- **BRIDGE-MARKUP-007** — `Duration` and `Delay` shall be passed to the factory, null leaving the recipe's token defaults.
  - Tests: `RecipeMarkupTests.TextRevealExtensionsProvideTheirRecipe`, `RecipeMarkupTests.ReflowTakesItsDurationPositionallyAndParsesItsEase`
- **BRIDGE-MARKUP-008** (inherited) — `Ease` shall be parsed with `EaseParser.Parse` and passed to the factory; an unparseable string shall throw the parser's exception from `ProvideValue`.
  - Tests: `RecipeMarkupTests.EaseIsParsedAndStaggerStepsEachElement`
- **BRIDGE-MARKUP-009** — When `Stagger` is set, the provided builder's delay shall be replaced by `Presto.Stagger(Stagger, new() { Start = Delay })`.
  - Tests: `RecipeMarkupTests.EaseIsParsedAndStaggerStepsEachElement`
- **BRIDGE-MARKUP-010** (inherited) — `ProvideValue` shall call `Configure` with the `IProvideValueTarget` (null without a service provider) before building.
  - Tests: `RecipeMarkupTests.TextRevealExtensionsConfigureTheBlockUnlessAlreadySet`

### Motion extensions

- **BRIDGE-MARKUP-011** — `{presto:Fade}` shall provide `Fade` for `Direction=None` (default) and `FadeUp`/`FadeDown`/`FadeLeft`/`FadeRight` for the other directions, with `Distance`; as an exit, `FadeOut` and the matching `FadeOutUp`…`FadeOutRight`.
  - Tests: `RecipeMarkupTests.AFadeIsAnEntranceOnEnterAndAnExitOnExit`, `RecipeMarkupTests.OutTrueGivesTheExitFormInAStyleSetter`
- **BRIDGE-MARKUP-012** — `{presto:Scale From=…}` shall provide `ScaleIn(From)` or `ScaleOut(From)`; `{presto:Pop Scale=…}` `PopIn`/`PopOut`; `{presto:Blur Radius=…}` `BlurIn`/`BlurOut`; `{presto:Slide Edge=… Distance=…}` `SlideIn`/`SlideOut`.
  - Tests: `RecipeMarkupTests.ARecipeOnAPageTransitionsExitsIsTheExitForm`, `RecipeMarkupTests.EaseIsParsedAndStaggerStepsEachElement`
- **BRIDGE-MARKUP-013** — `{presto:Popover Distance=… Scale=…}` shall provide `PopoverIn` or, as an exit, `PopoverOut`.
  - Tests: `RecipeMarkupTests.PopoverIsAnEntranceOnOpenAndAnExitOnClose`
- **BRIDGE-MARKUP-014** — `{presto:Shake}`, `{presto:Lift}`, `{presto:PressIn}` and `{presto:Glow}` shall provide their recipe with `Intensity` (default 1); `Lift` keeps its default distance and `Glow` its default color.
  - Tests: `RecipeMarkupTests.LiftPlaysOnHoverAndShakePlaysOnAttach`
- **BRIDGE-MARKUP-015** — `{presto:Shimmer}` shall provide `Shimmer`.
  - Tests: `RecipeMarkupTests.ShimmerProvidesALoopingMaskSweep`
- **BRIDGE-MARKUP-016** — `{presto:Recolor To=… Property=… Space=… Hue=…}` shall provide `Recolor` with those members (defaults `DodgerBlue`, `"Background"`, the token space and hue).
  - Tests: `RecipeTests.RecolorIsTheSameUnderReducedMotionAndFromXaml`
- **BRIDGE-MARKUP-017** — `{presto:Reflow}` shall provide `Reflow`, and `{presto:Reflow 320}` shall set `Duration` positionally.
  - Tests: `RecipeMarkupTests.ReflowTakesItsDurationPositionallyAndParsesItsEase`
- **BRIDGE-MARKUP-018** (inherited) — `{presto:PlayFrames Fps=… Loop=… Reverse=…}` shall provide `PlayFrames` with `Delay`; a set `Duration` or `Ease` shall replace the frame timing.
  - Tests: `RecipeMarkupTests.PlayFramesReadsEachSheetsFrameCount`, `RecipeMarkupTests.PlayFramesTakesAnExplicitDurationAndEaseOverTheFrameTiming`

- **BRIDGE-MARKUP-026** — `{presto:Scatter Distance=… Spin=… Seed=…}` shall provide `Gather` or, as an exit, `Scatter`, with those members.
  - Tests: `RecipeMarkupTests.ScatterGathersOnEnterAndScattersOnExitOrWithOutTrue`

### Text reveal extensions

- **BRIDGE-MARKUP-019** — `{presto:LineRise}`, `{presto:LineMask}`, `{presto:LineWipe}`, `{presto:WordFade}`, `{presto:WordBlur}` and `{presto:CharCascade}` shall provide the same-named recipe, or its `Out` form as an exit, passing `Duration`, `Delay`, `Ease`, `LineStep` and `PieceStep`.
  - Tests: `RecipeMarkupTests.TextRevealExtensionsProvideTheirRecipe`, `RecipeMarkupTests.TextRevealsAreTheirExitFormOnExitAndTheirEntranceOnEnter`
- **BRIDGE-MARKUP-020** — When set on a `TextBlock`, a text reveal extension shall set `TextReveal.Pieces` to its recipe's pieces (`Lines`, `Words` or `Chars`), and `{presto:LineMask}` also `TextReveal.Mask`, unless the block already sets them.
  - Tests: `RecipeMarkupTests.TextRevealExtensionsConfigureTheBlockUnlessAlreadySet`

### Page extensions

- **BRIDGE-MARKUP-021** — `{presto:PageFade Duration=…}`, `{presto:PageFadeThrough Duration=…}`, `{presto:PageSharedAxis Axis=… Distance=… Duration=…}`, `{presto:PageSlide Edge=…}`, `{presto:PagePush Edge=…}` and `{presto:PageBloom}` shall each provide a new `PrestoPageTransition` from the same-named recipe with those settings.
  - Tests: `RecipeMarkupTests.PageMarkupExtensionsProduceTransitionsWithTheirSettings`
- **BRIDGE-MARKUP-022** (inherited) — The page extensions shall not derive from `RecipeExtension`: they take no `Delay`, `Ease`, `Stagger` or `Out`.
  - Tests: `RecipeMarkupTests.PageExtensionsTakeNoneOfTheRecipeSettings`

### Defaults

- **BRIDGE-MARKUP-023** — When `Fps`, `Loop` or `Reverse` is not set, `{presto:PlayFrames}` shall play at 24 fps, looping, forwards, as `Recipes.PlayFrames` does.
  - Tests: `RecipeMarkupTests.PlayFramesDefaultsTo24FpsLoopingForwards`
- **BRIDGE-MARKUP-024** (inherited) — When `Edge` is not set, `{presto:Slide}`, `{presto:PageSlide}` and `{presto:PagePush}` shall use `Edge.Left`.
  - Tests: `RecipeMarkupTests.SlideAndThePageSlidesDefaultToTheLeftEdge`
- **BRIDGE-MARKUP-025** (inherited) — When `Axis` is not set, `{presto:PageSharedAxis}` shall use `PageAxis.X`.
  - Tests: `RecipeMarkupTests.PageSharedAxisDefaultsToTheXAxis`

## Invariants

- The extension holds no builder: every `ProvideValue` returns a fresh object.
- The exit/entrance choice is made once, in `ProvideValue`; the provided builder does not change with where it is later used.

## Edge cases

- In a style `Setter` the target property is the setter's, not `Presence.Exit`, so detection gives the entrance form; `Out=True` is required (see Decisions).
- Compiled XAML names a CLR property on a plain object (the page transition's `Exit`) by an `IPropertyInfo` that lacks the declaring type, so detection matches it by name on a `PrestoPageTransition` target.
- A text reveal extension in a style `Setter` cannot configure the block; the block sets `TextReveal.Pieces` itself.
- `PlayFrames` takes no duration or ease argument, so its extension chains `Duration` and `Ease` onto the factory's builder instead.

## Threading and lifetime

- Runs during XAML loading on the UI thread. Holds nothing after `ProvideValue`.

## Reduced motion

- Not affected here: the provided builders carry their recipe's reduced form (BRIDGE-RECIPES), picked when they become params.

## Allocation

- Not on the tick path.

## Provenance

- No upstream counterpart.

## Decisions

- **One markup extension per family verb, with the exit form detected from the target.** Each derives from `RecipeExtension`, reads `IProvideValueTarget` and provides the exit form under `Presence.Exit`, else the entrance form; `Out` states the form explicitly, and with no service provider the entrance form is provided. Reason: one attribute should be the whole ceremony, and inside a style `Setter` the target is the setter rather than the property, so the form cannot be detected there and `Out="True"` is needed.
- **An extension with no exit form throws where the exit form is asked for.** Each extension declares whether its recipe has an exit form (`RecipeExtension.HasExitForm`), and `ProvideValue` throws `NotSupportedException` when the target or `Out="True"` asks for one it lacks. Reason: settings without a meaning are rejected, not dropped; before, `{presto:Shake}` on `Presence.Exit` silently provided its entrance-style builder and shook on exit.
- **The text reveal extensions have exit forms.** Under `Presence.Exit` they provide the recipe's `*Out` form; earlier they had none, replaced when text exits gained per-piece ghosts. Reason: not recorded beyond that.
- **A page transition's exit properties take the exit form.** On a `PrestoPageTransition`'s `Exit` or `BackExit` the extension provides the exit form, detected as a `PrestoPageTransition` target object plus the property's name. Reason: in compiled XAML a CLR property reports a property info that names the property but not its declaring type.
- **Markup: `{presto:Scatter}` only.** It provides `Gather` on enter and `Scatter` on exit; `Fall` and `Wave` are code-only, like `Grow`, `Dim` and `Pulse`. Reason: one extension per family verb, and no family has an extension for every recipe.
- **The XAML namespace URI follows the repo**: `https://github.com/stevemonaco/MonacoLabs.Presto`, prefix `presto`. Reason: nothing had been published when the repo was renamed, so changing the URI cost nothing, and a URI naming the old repo would mislead; it is an opaque identifier and is never fetched.
- **Reduced text reveals never split, and one attribute configures the block.** See BRIDGE-TEXTREVEAL.
- **`{presto:Reflow}` is the shorthand, and its reduced form is instant.** See BRIDGE-DECLLAYOUT.
- **`ColorSpace` and `Hue` are trailing optional parameters.** See BRIDGE-TOKENS.
- **No timelines or keyframes in XAML.** See BRIDGE-ATTACH.

## Non-goals

- Timelines or keyframes authored in XAML (see BRIDGE-ATTACH).
- Extensions for every recipe: attention recipes other than `Shake`, the loops, the other loading recipes, `Colorize`, `Dim`, `Fall`, `ReorderLift` (BRIDGE-REORDER owns `{presto:Reorder}`), `Develop` and the particle and stateful text recipes have none.

## Open items

- No extension exposes `Lift`'s distance or `Glow`'s color, so XAML cannot set them.