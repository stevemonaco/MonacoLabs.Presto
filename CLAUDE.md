# Monaco.Presto

A .NET animation library inspired by [anime.js v4](https://animejs.com/documentation) ([source](https://github.com/juliangarnier/anime), MIT). The goal is an API that is as simple to use from C# as anime.js is from JavaScript: `Presto.Animate(target, ...)`, timelines, staggering, a rich easing set, and full playback control.

The implementation plan and architecture live in [docs/PLAN.md](docs/PLAN.md). Read it before starting any porting work and keep its phase checklist current.

## Layout

| Path | Purpose |
|------|---------|
| `Monaco.Presto/` | **Core.** Framework-agnostic engine: clock, engine, timer, animation, timeline, tweens, values, eases, stagger, utils. No Avalonia dependency, ever. |
| `Monaco.Presto.AvaloniaBridge/` | **Avalonia adapter.** Frame pump, `AvaloniaObject`/`AvaloniaProperty` targets, render-transform composition, color/brush/thickness interpolation, control helpers. Root namespace `Monaco.Presto.AvaloniaBridge` (deliberately not `...Avalonia`, which would shadow the `Avalonia.*` namespaces). |
| `samples/PrestoSample*/` | Gallery app (shared project + Desktop/Browser/Android/iOS heads). `Pages/` mirror the anime.js documentation examples; `Builder/` holds fluent-builder pages that print their own code, plus the XAML attach demo; `Showcase/` holds the shared kit (`ShowcasePage`, `CodePanel`, `Snippet`) and `Showcase/Pages/` the multi-feature demos. Desktop is the primary dev target. |
| `samples/PrestoSample.SnippetGenerator/` | Sample-only source generator: `<!-- snippet: Name -->` (XAML) and `#region snippet Name` (C#) regions become `PrestoSample.Showcase.Snippets` consts. |
| `tests/Monaco.Presto.Tests/` | xunit v3 tests for the core. Bridge tests (Avalonia.Headless) will live in `tests/Monaco.Presto.AvaloniaBridge.Tests/`. |
| `docs/` | Plan, design notes, API reference as it grows. |

Build configuration is shared through root `Directory.Build.props` (language/nullable/version) and `Directory.Packages.props` (central package management — add package versions there, never in a csproj).

## Commands (PowerShell)

```powershell
dotnet build Monaco.Presto.slnx
dotnet test --project tests/Monaco.Presto.Tests          # MTP mode (global.json); note --project, not a bare path
dotnet test --solution Monaco.Presto.slnx
dotnet run --project samples/PrestoSample.Desktop
dotnet watch --project samples/PrestoSample.Desktop      # hot reload (see below)
```

The Desktop head takes launch options after `--`, e.g. `dotnet run --project samples/PrestoSample.Desktop -- --page carousel --motion reduced --size 1400x1000`: `--page` takes a slug or title, `--motion` is `full|reduced|system`, and `--list-pages` prints the slugs. Its output shows under `dotnet run`, though it is a WinExe, so launched directly its output appears only when redirected; bad arguments exit with code 2. Use these to start DevTools checks on a page instead of navigating the sidebar.

Tests use xunit v3 on Microsoft.Testing.Platform (`UseMicrosoftTestingPlatformRunner`), which the .NET 10 SDK requires; test projects are `Exe` and can also be run directly.

Android/iOS/Browser heads exist but are not part of the day-to-day loop; build them only when asked. Hot reload does not support WebAssembly.

**Implementation workflow:** `.claude/workflows/implement-plan.js` implements a plan end to end (decompose → implement item by item with a concision review after each → whole-diff review → full test run with fix rounds → tick the PLAN.md checklist). Run it with `Workflow({ name: "implement-plan", args: "Phase 1" })` (or a plan file path / plan text) once a plan is agreed; it never commits.

## Dev loop: DevTools and Hot Reload

**Developer Tools** (`AvaloniaUI.DiagnosticsSupport` 2.2.x) is enabled in `samples/PrestoSample/App.axaml.cs` via `this.AttachDeveloperTools()` under `#if DEBUG`. With the sample running, press F12 to launch the `avdt` tool, or use the `avalonia_devtools` MCP server (`avdt mcp`) from Claude Code to inspect the tree, read/set properties, take screenshots and send input. Prefer `attach-to-app` against the running Desktop sample.

**Hot Reload** (`AvaloniaUI.DiagnosticsSupport.HotReload` 1.0.0) is referenced by the shared `samples/PrestoSample` project only in Debug **and only when an `AvaloniaUILicenseKey` MSBuild property is present** — it is a licensed package and the app builds and runs normally without it. The package auto-initializes via a generated module initializer; `.axaml` edits are patched into live instances (verified: same process, properties updated in place). `AvaloniaHotReloadEnableFileWatcher` is on so `.axaml` edits reload even without `dotnet watch` (e.g. when launched from Rider). C# edits additionally require `dotnet watch` and follow normal .NET Hot Reload "rude edit" rules. Hot reload diagnostics are echoed to the console (`App.axaml.cs`, under `PRESTO_HOTRELOAD`), so `dotnet watch` output shows what was reloaded.

Two non-obvious constraints, both learned the hard way:
- The package must be referenced from the project that **owns the XAML** (`PrestoSample`), not the executable head. Its source generator only registers the referencing project's `.axaml` files; referenced projects are collected only if they also reference the package. Its build/licensing targets flow to the heads transitively, so the head needs nothing.
- The generator also emits an auto-initializer into the heads, and the runtime reads its flags from the **first assembly found** (the entry assembly). `AvaloniaHotReloadEnableFileWatcher` therefore lives in `Directory.Build.props` so every project compiles the same value.

The key is picked up from the `AvaloniaUILicenseKey` environment variable (MSBuild exposes env vars as properties; this is how the primary dev machine is configured). On a machine without the env var, create a git-ignored `Directory.Build.local.props` next to `Directory.Build.props` instead:

```xml
<Project>
  <PropertyGroup>
    <AvaloniaUILicenseKey>...key from the Avalonia portal...</AvaloniaUILicenseKey>
  </PropertyGroup>
</Project>
```

The DevTools MCP server separately reads `AVALONIA_TOOLS_LICENSE_KEY`. Never commit a key, print it, or paste it into chat; when checking whether it is configured, test for presence only.

## Architecture rules

- **Core knows nothing about Avalonia.** It animates "targets" through small adapter interfaces (property accessors + value interpolation) and is driven by an externally supplied frame source. Anything that touches `Avalonia.*` belongs in the bridge.
- **Single-threaded by design**, exactly like anime.js. Everything runs on the UI thread; the core has no locks. The bridge guarantees ticks happen on `Dispatcher.UIThread`.
- **Time is `double` milliseconds** (anime.js `timeUnit: 'ms'` default). Seconds mode is a scale factor on the engine, not a second code path.
- **Port behaviour faithfully first, idiomatically second.** anime.js's tick/loop/alternate/seek math is subtle and well tested; keep its structure (Clock → Timer → Animation/Timeline, linked-list children, `tick()` in render) so the upstream tests can be ported one-to-one. Rename to C# conventions, but do not "improve" semantics until parity tests pass.
- **Reference source is anime.js 4.5.0.** A git-ignored checkout lives at `tools/anime/` (`src/`, `tests/suites/`); recreate it with `git clone --depth 1 --branch v4.5.0 https://github.com/juliangarnier/anime tools/anime` if missing. When porting a module, read the upstream file and its test suite first.

## API conventions

- Public entry point is the static `Presto` class (`Presto.Animate`, `Presto.CreateTimer`, `Presto.CreateTimeline`, `Presto.Stagger`, `Presto.Defaults`, `Presto.Engine`). Users should need one `using Monaco.Presto;` (plus `using Monaco.Presto.AvaloniaBridge;` for controls).
- Parameters are `init`-only records/classes with the anime.js names in PascalCase (`Duration`, `Delay`, `Loop`, `Alternate`, `Ease`, `OnComplete`…). Animated properties are supplied through an indexer/collection initializer so calls read like the JS object literal. See PLAN.md for the exact shapes.
- Anything in anime.js that can be a function-based value (`(target, i, total) => …`) accepts a delegate here; `Stagger` returns such delegates.
- Eases are `Ease` delegates (`double → double`) exposed as `Eases.OutQuad`, `Eases.Out(3)`, `Eases.Spring(...)`, plus a string parser (`"inOutQuint"`, `"out(2)"`) for XAML and defaults.
- Mirror anime.js method names on instances: `Play`, `Pause`, `Resume`, `Restart`, `Reverse`, `Alternate`, `Seek`, `Stretch`, `Complete`, `Cancel`, `Revert`, `Reset`, `Refresh`, plus `Task Then()` / awaitable for `.then()`.

## Code conventions (project-specific)

- File-scoped namespaces; folder structure equals namespace; one public type per file.
- Types are `sealed` unless designed for inheritance. Internal plumbing is `internal` and exposed to tests via `InternalsVisibleTo`.
- Hot-path code (tick, tween render) avoids allocations and LINQ; keep the upstream linked-list approach.
- Test names describe behaviour and, when porting, cite the upstream suite in the class name (e.g. `TimingsTests` ↔ `timings.test.js`).
- After changes to the core, the tests are the verification step. After changes to the bridge or sample, verify visually via the Desktop sample (DevTools screenshots are fine).
