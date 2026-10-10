---
id: CORE-EXAMPLE
title: Example feature
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Example.cs
types:
  - Example
  - ExampleParams
tests:
  - ExampleTests
upstream:
  - src/example/example.js
  - tests/suites/example.test.js
depends:
  - CORE-TIMER
---

# Example feature

## Purpose

Two or three sentences: what the feature is for and where it sits (which spec it builds on, which builds on it). When the spec specifies members of a type another spec owns, one line names them.

## Requirements

### Group name

- **CORE-EXAMPLE-001** — When `Value` is set during a tick, the example shall apply it on the next render, not the current one.
  - Tests: `ExampleTests.ValueSetDuringATickRendersNextFrame`
- **CORE-EXAMPLE-002** — The example shall never fire `OnComplete` twice for one iteration.
  - Tests: untested
- **CORE-EXAMPLE-003** — When the window is minimized, the example shall stop requesting frames.
  - Tests: manual — needs a real minimized window; checked on Desktop in Phase 60.
- ~~**CORE-EXAMPLE-004**~~ — Removed in Phase 80: superseded by CORE-EXAMPLE-005.

### Defaults

- **CORE-EXAMPLE-005** (inherited) — When `Duration` is not set, the example shall last 400 ms.
  - Tests: untested

## Invariants

- What holds after every call and on every frame.

## Edge cases

- Zero durations, empty targets, reentrant calls from callbacks, NaN, revert mid-flight…

## Threading and lifetime

- Which thread, which engine, what keeps the object alive, what releases it.

## Reduced motion

- The reduced form, or "Not affected: …" with the reason.

## Allocation

- The hot-path rules that apply, or "Not on the tick path."

## Deviations from anime.js

- Each deviation, with its reason. "None." or "No upstream counterpart." when that is the case.

## Decisions

- **A short title.** The decision. Reason: why. Rejected: the alternatives and why, when known.

## Non-goals

- What the feature deliberately does not do.

## Open items

- Disagreements between code, tests, plans and docs found while writing the spec; behavior with no test that should have one; pending manual checks. "None." when empty.
