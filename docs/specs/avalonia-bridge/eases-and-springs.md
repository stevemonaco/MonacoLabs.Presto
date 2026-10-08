---
id: BRIDGE-EASES
title: Avalonia easing adapter and named springs
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge
sources:
  - Monaco.Presto.AvaloniaBridge/PrestoEasing.cs
  - Monaco.Presto.AvaloniaBridge/Springs.cs
types:
  - PrestoEasing
  - Springs
tests:
  - CodecTests
  - CompositorCompileTests
  - FollowerTests
upstream: []
depends:
  - CORE-EASES
  - CORE-SPRING
  - BRIDGE-THREAD
  - BRIDGE-FOLLOW
---

# Avalonia easing adapter and named springs

## Purpose

Two small bridges between Presto's eases and the rest of the bridge. `PrestoEasing` lets a Presto `Ease` (CORE-EASES) stand wherever Avalonia expects an `Easing`, which is how compositor animations (BRIDGE-THREAD) keep Presto's curves. `Springs` names four ready-made `Spring`s (CORE-SPRING) for value-driven motion and parses the `Spring="…"` text that `FollowParams` and `{presto:Follow}` (BRIDGE-FOLLOW) accept.

## Requirements

### PrestoEasing

- **BRIDGE-EASES-001** — `PrestoEasing.Ease(p)` shall return the wrapped ease's value at `p`, unchanged, for every `p`.
  - Tests: `CodecTests.PrestoEasingSamplesTheWrappedEase`
- **BRIDGE-EASES-002** — When the bridge compiles a tween for the compositor, each segment's Presto ease shall be carried as a `PrestoEasing` on the key frame.
  - Tests: `CompositorCompileTests.ASinglePlainTweenCompilesToTwoExactKeyFrames`
- **BRIDGE-EASES-003** — When the compiled ease is a `Spring`, the `PrestoEasing` shall wrap its pure solver, so sampling it never marks the spring completed or raises its `OnComplete`.
  - Tests: `CompositorCompileTests.ASpringEaseCompilesWithoutTheSamplingCompletingIt`
- **BRIDGE-EASES-004** — The other direction shall need no adapter: an Avalonia `Easing`'s `Ease` method group converts to a Presto `Ease` delegate.
  - Tests: untested

### Springs

- **BRIDGE-EASES-005** — `Gentle` shall be a critically damped spring (bounce 0) over 500 ms, `Snappy` bounce 0.15 over 300 ms, `Bouncy` bounce 0.35 over 500 ms and `Stiff` bounce 0 over 250 ms.
  - Tests: `FollowerTests.SpringPresetsParseByNameOrBounceAndDuration`, `FollowerTests.EachTimingSourceResolves`
- **BRIDGE-EASES-006** — Each read of a preset shall return a new `Spring`, since a spring carries completion state.
  - Tests: `FollowerTests.SpringPresetsParseByNameOrBounceAndDuration`
- **BRIDGE-EASES-007** — `Gentle` shall be the timing a follower uses when its `FollowParams` give none.
  - Tests: `FollowerTests.EachTimingSourceResolves`
- **BRIDGE-EASES-008** — A preset shall be usable as any ease, such as `Presto.Build().Ease(Springs.Snappy)` or an `Animatable` property's `Ease`.
  - Tests: `FollowerTests.EachTimingSourceResolves`

### Springs.Parse

- **BRIDGE-EASES-009** — When `text`, trimmed, names a preset ignoring case (`gentle`, `snappy`, `bouncy`, `stiff`), `Parse` shall return a new spring of that preset.
  - Tests: `FollowerTests.SpringPresetsParseByNameOrBounceAndDuration`
- **BRIDGE-EASES-010** — When `text` is two comma-separated numbers in the invariant culture, surrounding spaces allowed, `Parse` shall return `new Spring(bounce: first, duration: second)`.
  - Tests: `FollowerTests.SpringPresetsParseByNameOrBounceAndDuration`, `FollowerTests.EachTimingSourceResolves`
- **BRIDGE-EASES-011** (inherited) — When `text` is neither, including a single number, more than two parts, or a preset name followed by a duration, `Parse` shall throw `FormatException` naming the presets and the `"bounce,duration"` form.
  - Tests: `FollowerTests.SpringPresetsParseByNameOrBounceAndDuration`, `FollowerTests.ConflictingTimingSourcesThrow`
- **BRIDGE-EASES-012** (inherited) — `Parse` shall be reached only through `FollowParams.Spring` (and the `{presto:Follow Spring=…}` member that sets it); `Springs` exposes no public parser.
  - Tests: `FollowerTests.EachTimingSourceResolves`

## Invariants

- `PrestoEasing` holds the delegate it was given and adds no state.
- A parsed or preset spring is never shared between two reads.

## Edge cases

- A parsed bounce outside -1..1 and a duration outside the spring's range are clamped by `Spring` itself (CORE-SPRING), not rejected.
- `Parse` with a decimal comma (`"0,2,400"`) has three parts and throws.

## Threading and lifetime

- `PrestoEasing.Ease` runs on the render thread when used by a compositor animation, so the wrapped ease must be pure; the compiler passes a spring's `PureEase` for that reason (Only pure eases cross to the render thread).
- `Springs` is stateless.

## Reduced motion

- Not affected: eases carry no reduced form. A follower's reduced timing comes from `FollowParams.WhenReduced` (BRIDGE-FOLLOW).

## Allocation

- `PrestoEasing.Ease` allocates nothing per call. Each preset read allocates a `Spring`; read presets when building, not per frame.

## Deviations from anime.js

- No upstream counterpart: anime.js has no named spring presets; `createSpring`'s parameters are CORE-SPRING's.

## Decisions

- **Presto eases adapt to Avalonia `Easing`.** `PrestoEasing` wraps a Presto `Ease` as an Avalonia `Easing`; the other direction is the `Easing.Ease` method group. Reason: interop with Avalonia's own animation system, so Presto curves work in `Transitions` and compositor animations.
- **Spring presets live in the bridge, fresh per read.** A static `Springs` class in the bridge root namespace names four presets, each read returning a new `Spring`, and XAML names a preset (`Spring=Gentle`) or gives `"bounce,duration"`; the core keeps `Eases.Spring` as the one way to build a spring. Reason: a `Spring` carries completion state, so it cannot be shared, and the presets serve XAML.
- **Only pure eases cross to the render thread.** See BRIDGE-THREAD.

## Non-goals

- Wrapping Avalonia easings as Presto eases (the method group already is one).
- More presets, or a string grammar beyond `"bounce,duration"`.

## Open items

- `PrestoEasing`'s reverse direction (an Avalonia `Easing` used as a Presto `Ease`) has no test.
- `Springs.Parse` throws `NullReferenceException` for null text; its only caller never passes null.
