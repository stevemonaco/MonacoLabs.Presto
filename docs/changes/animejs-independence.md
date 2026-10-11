# anime.js independence

## Why

Presto started as a port of anime.js 4.5.0 and is now its own library: the fluent builder, the Avalonia bridge, controls, recipes, motion values, gestures and DevTools have no upstream counterpart. The docs, specs and samples still describe it as a port and justify decisions by "parity with anime.js", which misstates the goals and keeps JavaScript idioms in the C# surface. One example prompted this: the samples write `await Presto.CreateTimer(new() { Duration = 700 })` where a C# reader expects `Task.Delay`, a JS idiom (anime.js has no sleep) carried over without a reason given.

## What

anime.js is credited as the inspiration and the initial basis of the port (README, ARCHITECTURE, LICENSE notice), and nowhere described as a spec Presto must follow. Decisions stand on their own reasons; where the reason was "parity", the spec states the actual behavioral reason or the decision is reopened.

What does not change: the timing model, behavior, public API names (`Play`, `Seek`, `Stretch`…), the ported tests, or their provenance notes. Upstream tests stay as regression tests for behavior Presto keeps; they are no longer the acceptance bar for new work.

Waits in samples follow one rule:

- **Simulated work** (standing in for a request or I/O) uses `Task.Delay`: it is wall-clock, like the work it imitates, and DevTools slow-motion should not stretch it.
- **Choreography holds** (a beat between two animations) use engine time, so DevTools freeze/dilation, `Engine.Speed` and test clocks (`Frames.Advance`) keep the sequence coherent. These go through a new `Presto.Delay(double duration, CancellationToken cancellationToken = default)` returning `Task`, which reads like `Task.Delay` and replaces `await Presto.CreateTimer(...)`.

## Decisions

- **Keep upstream provenance, drop upstream authority.** Test class names citing suites (`TimingsTests` ↔ `timings.test.js`) and spec notes saying where a behavior came from stay; "Reason: parity with anime.js" does not. Alternative rejected: scrubbing every mention, which loses useful history and the MIT attribution.
- **`Presto.Delay` over `Task.Delay` for holds.** `Task.Delay` ignores engine time, so a hold would desync from its animations under DevTools dilation and would never complete in headless tests driven by `Frames.Advance` (`SubmitButton.Run`'s outcome hold is one). Alternative rejected: keep `await Presto.CreateTimer(...)`, which works but reads as a workaround.
- **`Composition.None` becomes `Overlap`, `Blend` becomes `Additive`.** `None` reads as "no animation" rather than "tweens overlap without composing"; `Additive` is the standard term for summed animation. `LoopDelay` and `Alternate` keep their names: they are already clear in C#.
- **The Reference gallery section stops mirroring the anime.js documentation.** It becomes Presto's own feature reference, organized by Presto's specs. The anime.js docs links on those pages go.

## Spec changes

- CORE-PRESTO: add a requirement for `Presto.Delay`: completes after `duration` of the current engine's time; scales with `Engine.Speed`; cancellation faults the task with `OperationCanceledException` and cancels the underlying timer; `duration <= 0` completes synchronously.
- CORE-TIMER: add a Decision noting that awaiting a timer remains supported, with `Presto.Delay` the idiomatic wait.
- All specs: rewrite Decisions whose only reason is anime.js parity (engine, timer, scope-and-defaults, scramble, eases, stagger, draggable, text, animation-thread are the heaviest). Requirements marked `(inherited)` that came from upstream keep the marker.
- docs/specs/README.md and _template.md: drop any instruction to cite upstream as the source of truth.

## Tasks

1. README, ARCHITECTURE §1–2, CLAUDE.md: reframe as "inspired by and initially ported from anime.js 4.5.0"; ARCHITECTURE §2's module map becomes a provenance appendix; the "port faithfully first" architecture rule becomes "the timing model is covered by tests ported from upstream; change it deliberately, with a spec decision".
2. `Presto.Delay` in core with tests (engine time, `Speed`, cancellation, zero duration).
3. Replace timer-as-wait in `SubmitButton.Run` and the samples: `Task.Delay` in `FormViewModel.Submit`, `SubmitFlowPage` (1400), `MicroInteractionsPage` (`Run` argument), `MorphClockPage.Send` (1100); `Presto.Delay` in `SubmitFlowPage` (700), `MicroInteractionsPage.SaveAsync`, `MorphClockPage.Send` (1400), `SubmitButton.Run`. Looping timers (`MorphClockPage` ticks, `ValueDrivenPage`, `StressBenchPage`) and debounces stay timers.
4. Spec Decisions pass (see Spec changes).
5. Code comments and csproj descriptions that call Presto a port (about 30 files).
6. Gallery: rename/re-describe the Reference section (`GalleryNav.axaml`, `MainView.axaml`, `GalleryPage.cs`, `MainViewModel.cs`) and update `GalleryPageTests`.
7. `implement-plan.js`: stop instructing agents to treat upstream as authoritative.
8. Visual check of Submit flow, Micro-interactions and Morph clock pages on Desktop.
9. Rename `Composition.None` → `Overlap` and `Composition.Blend` → `Additive` (`Replace` stays) across core, bridge, samples, tests, XAML strings/parsers and specs; doc comments describe the behavior rather than the upstream string. Any string parsing of composition names accepts only the new names.

## Open questions

None. Settled: `Composition.None` → `Composition.Overlap` and `Composition.Blend` → `Composition.Additive` (task 9); `LoopDelay` and `Alternate` keep their names; `tools/anime/` and `tools/anime-fixtures/` stay, documented as provenance tooling.
