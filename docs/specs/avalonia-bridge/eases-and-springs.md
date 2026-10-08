---
id: BRIDGE-EASES
title: Avalonia easing adapter
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge
sources:
  - Monaco.Presto.AvaloniaBridge/PrestoEasing.cs
types:
  - PrestoEasing
tests:
  - CodecTests
  - CompositorCompileTests
upstream: []
depends:
  - CORE-EASES
  - CORE-SPRING
  - BRIDGE-THREAD
---

# Avalonia easing adapter

## Purpose

`PrestoEasing` lets a Presto `Ease` (CORE-EASES) stand wherever Avalonia expects an `Easing`, which is how compositor animations (BRIDGE-THREAD) keep Presto's curves.

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

- ~~**BRIDGE-EASES-005**~~ — Moved to CORE-SPRING.
- ~~**BRIDGE-EASES-006**~~ — Moved to CORE-SPRING.
- ~~**BRIDGE-EASES-007**~~ — Covered by BRIDGE-FOLLOW-001.
- ~~**BRIDGE-EASES-008**~~ — Moved to CORE-SPRING.

### Spring text

- ~~**BRIDGE-EASES-009**~~ — Moved to BRIDGE-FOLLOW.
- ~~**BRIDGE-EASES-010**~~ — Moved to BRIDGE-FOLLOW.
- ~~**BRIDGE-EASES-011**~~ — Moved to BRIDGE-FOLLOW.
- ~~**BRIDGE-EASES-012**~~ — Moved to BRIDGE-FOLLOW.

## Invariants

- `PrestoEasing` holds the delegate it was given and adds no state.

## Edge cases

- None beyond the wrapped ease's own.

## Threading and lifetime

- `PrestoEasing.Ease` runs on the render thread when used by a compositor animation, so the wrapped ease must be pure; the compiler passes a spring's `PureEase` for that reason (Only pure eases cross to the render thread).

## Reduced motion

- Not affected: eases carry no reduced form.

## Allocation

- `PrestoEasing.Ease` allocates nothing per call.

## Deviations from anime.js

- No upstream counterpart.

## Decisions

- **Presto eases adapt to Avalonia `Easing`.** `PrestoEasing` wraps a Presto `Ease` as an Avalonia `Easing`; the other direction is the `Easing.Ease` method group. Reason: interop with Avalonia's own animation system, so Presto curves work in `Transitions` and compositor animations.
- **Only pure eases cross to the render thread.** See BRIDGE-THREAD.

## Non-goals

- Wrapping Avalonia easings as Presto eases (the method group already is one).

## Open items

- `PrestoEasing`'s reverse direction (an Avalonia `Easing` used as a Presto `Ease`) has no test.
