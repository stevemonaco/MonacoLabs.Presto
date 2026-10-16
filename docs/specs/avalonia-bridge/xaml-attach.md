---
id: BRIDGE-ATTACH
title: XAML attach triggers (Animate.*)
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/Animate.cs
types:
  - Animate
tests:
  - AnimateAttachedTests
  - TextRevealTests
  - BuilderPagesTests
  - BuilderRecipePagesTests
  - ActivityFeedPageTests
upstream: []
depends:
  - BRIDGE-INIT
  - BRIDGE-TARGETS
  - BRIDGE-TEXTREVEAL
  - BRIDGE-DECLLAYOUT
  - BRIDGE-CONTENTSIZE
  - BRIDGE-PRESENCE
  - CORE-BUILDER
  - CORE-ANIMATION
  - CORE-SCOPE
  - CORE-DIAGNOSTICS
---

# XAML attach triggers (Animate.*)

## Purpose

`Animate` is the static class of attached properties that play an `AnimationBuilder` on a visual from XAML or code without code-behind: `OnAttached` (the "while attached" trigger for loops and timers tied to being on screen), `OnTapped`, and `OnChanged` with its `Watch` value. Each property is typed `AnimationBuilder?`, so `x:Static`, a markup extension or a binding can supply it. The class also hosts the declarative layout members (`Layout`, `LayoutMode`, `ItemLayout`), specified by BRIDGE-DECLLAYOUT, and `ContentSize`, specified by BRIDGE-CONTENTSIZE. Classified entrances and exits belong to `Presence` (BRIDGE-PRESENCE), not to these triggers.

## Requirements

### OnAttached

- **BRIDGE-ATTACH-001** — When `OnAttached` is set on a visual that is attached to the visual tree, `Animate` shall play the builder on the visual at once, as one animation.
  - Tests: `AnimateAttachedTests.SettingOnAnAttachedControlPlaysOneAnimationToItsEnd`
- **BRIDGE-ATTACH-002** — When `OnAttached` is set on a visual that is not attached, `Animate` shall play nothing until the visual attaches, and then play the builder.
  - Tests: `AnimateAttachedTests.SettingBeforeAttachmentWaitsForTheAttach`
- **BRIDGE-ATTACH-003** — When the visual detaches, `Animate` shall revert its `OnAttached` animation, restoring the values the animation changed.
  - Tests: `AnimateAttachedTests.DetachingRevertsAndReattachingPlaysFromTheStart`
- **BRIDGE-ATTACH-004** — When the visual attaches again, `Animate` shall play a new animation of the current builder from its start.
  - Tests: `AnimateAttachedTests.DetachingRevertsAndReattachingPlaysFromTheStart`
- **BRIDGE-ATTACH-005** — The `OnAttached` trigger shall play on every attach, unclassified, including a virtualizing panel re-attaching a recycled row's content as it scrolls.
  - Tests: `ActivityFeedPageTests.UseOnAttachedReplaysOnScrollingBackOverSeenRows`
- **BRIDGE-ATTACH-006** — While the visual stays attached, an `OnAttached` animation shall keep its end values when it completes; it is reverted only by a detach, a new value or `null`.
  - Tests: `AnimateAttachedTests.SettingOnAnAttachedControlPlaysOneAnimationToItsEnd`
- **BRIDGE-ATTACH-007** — When `OnAttached` changes from one builder to another on an attached visual, `Animate` shall revert (cancel) the previous animation and play the new builder.
  - Tests: `AnimateAttachedTests.ChangingTheValueRevertsTheFirstAnimationAndPlaysTheSecond`
- **BRIDGE-ATTACH-008** — When `OnAttached` is set to `null`, `Animate` shall revert the running animation and stop listening, so later attaches play nothing.
  - Tests: `AnimateAttachedTests.SettingNullRevertsAndStopsListening`
- **BRIDGE-ATTACH-009** — When one builder instance is set on several visuals, each visual shall get its own independent animation built from fresh params (`ToParams()` per play).
  - Tests: `AnimateAttachedTests.OneBuilderOnTwoControlsGivesIndependentAnimations`

### OnTapped

- **BRIDGE-ATTACH-010** — When `OnTapped` is set, `Animate` shall play nothing until the element raises `Tapped`, and shall play the builder on each tap.
  - Tests: `AnimateAttachedTests.OnTappedPlaysOnATapAndRevertsWhenItCompletes`
- **BRIDGE-ATTACH-011** — When an `OnTapped` animation completes, `Animate` shall revert it, returning the element to its base values, after the builder's own `OnComplete` has run.
  - Tests: `AnimateAttachedTests.OnTappedPlaysOnATapAndRevertsWhenItCompletes`
- **BRIDGE-ATTACH-012** — When the element is tapped while its `OnTapped` animation is running, `Animate` shall revert (cancel) the running animation and play a new one from the start.
  - Tests: `AnimateAttachedTests.OnTappedRestartsOnEachTap`
- **BRIDGE-ATTACH-013** — When `OnTapped` is set to `null`, `Animate` shall revert the running animation and stop listening for taps.
  - Tests: `AnimateAttachedTests.ClearingOnTappedStopsListening`
- **BRIDGE-ATTACH-014** — When `OnTapped` is set to a builder on a `Visual` that is not an `InputElement`, `Animate` shall throw `InvalidOperationException`.
  - Tests: `AnimateAttachedTests.OnTappedOnAVisualThatIsNotAnInputElementThrows`

### OnChanged and Watch

- **BRIDGE-ATTACH-015** — When `OnChanged` is set before `Watch` has a value, `Animate` shall treat the first `Watch` value (such as a binding's initial value) as seen and play nothing for it.
  - Tests: `AnimateAttachedTests.OnChangedSkipsTheInitialBindingAndPlaysOnEachLaterChange`, `BuilderRecipePagesTests.CounterTilesBumpOnEachRefreshButNotTheFirstFill`
- **BRIDGE-ATTACH-016** (inherited) — When `OnChanged` is set after `Watch` already has a value, `Animate` shall play on the next `Watch` change.
  - Tests: `AnimateAttachedTests.OnChangedSetAfterTheFirstValuePlaysOnTheNextChange`
- **BRIDGE-ATTACH-017** — When `Watch` changes after its first value while the visual is attached, `Animate` shall revert any running `OnChanged` animation and play the builder from the start.
  - Tests: `AnimateAttachedTests.OnChangedSkipsTheInitialBindingAndPlaysOnEachLaterChange`
- **BRIDGE-ATTACH-018** — When an `OnChanged` animation completes, `Animate` shall revert it, after the builder's own `OnComplete` has run.
  - Tests: `AnimateAttachedTests.OnChangedSkipsTheInitialBindingAndPlaysOnEachLaterChange`
- **BRIDGE-ATTACH-019** (inherited) — When `Watch` changes while the visual is detached, `Animate` shall play nothing for that change, though the change still counts as seen.
  - Tests: untested
- **BRIDGE-ATTACH-020** — When `OnChanged` is set to `null`, `Animate` shall revert the running animation and stop playing on `Watch` changes.
  - Tests: `AnimateAttachedTests.ClearingOnChangedStopsListening`
- **BRIDGE-ATTACH-021** (inherited) — `Watch` alone, without `OnChanged`, shall play nothing.
  - Tests: untested

### Shared trigger rules

- **BRIDGE-ATTACH-022** — When a visual with a running `OnTapped` or `OnChanged` animation detaches, `Animate` shall revert that animation.
  - Tests: `AnimateAttachedTests.DetachingRevertsTheTriggers`
- **BRIDGE-ATTACH-023** — When `OnTapped` or `OnChanged` changes from one non-null builder to another, `Animate` shall play the new builder on the next tap or `Watch` change, and, while an animation of the old builder is running, revert it and play the new builder from the start.
  - Tests: `AnimateAttachedTests.ChangingOnTappedPlaysTheNewBuilderOnTheNextTapAndRestartsARunningOneWithIt`, `AnimateAttachedTests.ChangingOnChangedMidPlayRevertsItAndPlaysTheNewBuilder`
- **BRIDGE-ATTACH-024** — When the target is a `TextBlock` whose `TextReveal.Pieces` is not `None` and the builder is not a reduced form, `OnAttached` and `OnTapped` shall play the builder on the block's split pieces as a text reveal (BRIDGE-TEXTREVEAL) instead of on the block, and a tap mid-reveal shall restart it.
  - Tests: `TextRevealTests.OnTappedRevealsThePiecesAndATapMidRevealRestartsIt`, `TextRevealTests.DetachingRevertsTheSplit`, `TextRevealTests.DetachingBeforeTheSplitReleasesTheHold`
- **BRIDGE-ATTACH-025** — `OnChanged` shall always animate the visual itself, never its text pieces.
  - Tests: untested
- **BRIDGE-ATTACH-026** — While `Design.IsDesignMode` is true, setting any trigger property shall create no trigger and play nothing.
  - Tests: `AnimateAttachedTests.DoesNothingInDesignMode`
- **BRIDGE-ATTACH-027** — Before playing, a trigger shall initialize the bridge (`PrestoAvalonia.EnsureInitialized`), so XAML use needs no explicit setup.
  - Tests: untested
- **BRIDGE-ATTACH-028** — Every animation a trigger creates shall carry the diagnostics origin `"Animate"` (CORE-DIAGNOSTICS).
  - Tests: untested
- **BRIDGE-ATTACH-029** — Trigger animations shall run on the UI thread through `Presto.Animate`; the triggers take no `AnimationThread` (see The thread is an argument on bridge entry points).
  - Tests: untested

### Builder sources

- **BRIDGE-ATTACH-030** — The trigger properties shall accept a builder from `x:Static`, from a markup extension's `ProvideValue` and from a binding, and play each the same way.
  - Tests: `AnimateAttachedTests.EachXamlSpellingAnimates`, `BuilderPagesTests.XamlPageAnimatesEveryAttachedElement`

### Defaults

- **BRIDGE-ATTACH-031** (inherited) — When `OnAttached`, `OnTapped`, `OnChanged` or `Watch` is not set, it shall be null, so the visual has no trigger and nothing plays.
  - Tests: untested

## Invariants

- A visual has at most one live animation per trigger property; playing again always reverts the previous one first.
- After a detach, no trigger animation of the visual is left running or holding values.
- Clearing a trigger property to `null` removes every event subscription that trigger made.

## Edge cases

- Setting the same builder instance again raises no property change, so nothing replays.
- A builder that is invalid for its target throws from `Presto.Animate` at play time (attach, tap or change), not when the property is set.
- `Watch` values are compared by Avalonia's property system: a value equal to the current one raises no change and plays nothing.
- A looping `OnTapped`/`OnChanged` builder never completes, so it is reverted only by the next trigger, a detach or `null`.

## Threading and lifetime

- UI thread only, like the rest of the bridge. Animations run on the current engine (CORE-SCOPE).
- The trigger object lives in a private attached property on the visual and holds the visual's events; it is released when the property becomes `null`. A visual that is collected takes its trigger with it.

## Reduced motion

- Triggers play `builder.ToParams()`, which resolves the builder's `WhenReduced` form and `ReduceAll` (CORE-REDUCED); a reduced form on a text block plays on the block unsplit (BRIDGE-TEXTREVEAL).

## Allocation

- Not on the tick path. Each play allocates its params and animation.

## Provenance

- No upstream counterpart: anime.js has no declarative or XAML trigger layer.

## Decisions

- **One attached property per trigger, typed `AnimationBuilder?`, takes three spellings.** The same property takes an `x:Static` recipe, a markup extension (a fresh builder per element from `ProvideValue`) or a binding. Reason: the spellings are not rival designs; statics are safe to share because every terminal snapshots the builder, a markup extension gives "builder as a class" without unsealing, and a binding lets a core-only view model pick the animation with no Avalonia reference. Rejected: an `IAnimationRecipe` interface; unsealing `AnimationBuilder`.
- **The XAML attach ships in the bridge as attached properties.** `Animate` is part of the bridge, not the sample, and there is no separate behaviors layer. Reason: an attach that only exists in a sample is copy-paste for every user, and its lifetime rules need bridge tests. Rejected: a behaviors-style package or layer.
- **The trigger is visual-tree attachment, not `Loaded`.** Reason: `Unloaded` never fires for a root detached before its `Loaded` job runs.
- **`OnAttached` lifetime is predictable over seamless.** Setting it on an attached visual (or attaching with a value) plays the builder; a detach reverts the animation so a re-attached visual starts clean; a new value reverts the previous animation, then plays; `null` reverts and unsubscribes; design mode does nothing. Reason: a mid-flight swap restarts from the visual's own values, and the previewer must not animate the design surface. Rejected: a seamless mid-flight handover on a value change.
- **`OnAttached` is the "while attached" trigger.** It plays on every attach with no classification and is meant for loops and timers tied to being on screen (shimmer, countdown); entrances and exits live on `Presence`, states on `Motion`. Reason: it plays on every attach, including a virtualizing panel's recycling, which `Presence` deliberately ignores; triggers are separated by meaning.
- **One-shot triggers stay on `Animate`, separate from states.** `OnTapped` plays and restarts on each tap; `OnChanged` with `Watch` plays on each change of the watched value but not on its first value; both revert when they complete. Reason: `Animate` is the raw-trigger class, and skipping the first value is what pulsing counters and bumping badges need.
- **A one-shot trigger's builder swap replays only a running animation.** Changing `OnTapped` or `OnChanged` reverts a running animation and plays the new builder, as an `OnAttached` change does; with nothing running it plays nothing until the next tap or change. Reason: a running animation stands for a trigger that already fired, so it follows the latest builder; playing with nothing running would fire a trigger nobody raised. Rejected: leaving the running animation alone until the next trigger, which kept showing the replaced builder.
- **`OnTapped` on a visual that is not an `InputElement` throws `InvalidOperationException`.** Reason: such a visual raises no `Tapped`, so the trigger was silently inert; the builder is valid and the host is not, as with two layout arms on one container.
- **No timelines or keyframes in XAML.** Reason: builders and recipes already reach XAML as values, multi-step sequences usually need code-side targets and state, and XAML elements would add verbosity without capability.
- **The piece trigger handle is an `IRevertible`, not an `Animation`.** See BRIDGE-TEXTREVEAL.
- **The thread is an argument on bridge entry points.** See BRIDGE-THREAD.

## Non-goals

- Classifying arrivals (initial load, enter, scroll reveal) or playing exits: that is `Presence` (BRIDGE-PRESENCE). `OnAttached` is deliberately unclassified (see `OnAttached` is the "while attached" trigger).
- State triggers (hover, press, classes): that is `Motion` (BRIDGE-MOTION).
- A thread choice for trigger animations (see The thread is an argument on bridge entry points).

## Open items

- BRIDGE-ATTACH-019, -021, -025, -027, -028 and -029 have no test.
