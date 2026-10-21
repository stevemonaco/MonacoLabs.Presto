# MonacoLabs.Presto

A .NET animation library inspired by and initially ported from [anime.js v4](https://animejs.com/documentation) ([source](https://github.com/juliangarnier/anime), MIT). The goal is an API that is as simple to use from C# as anime.js is from JavaScript: `Presto.Animate(target, ...)`, timelines, staggering, a rich easing set, and full playback control.

The architecture and the anime.js → Presto provenance map live in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md); what each feature does and why is in the specs ([docs/specs/](docs/specs/README.md)); work not done yet is in [docs/BACKLOG.md](docs/BACKLOG.md); new work starts as a change proposal in [docs/changes/](docs/changes/README.md).

## Layout

| Path | Purpose |
|------|---------|
| `MonacoLabs.Presto/` | **Core.** Framework-agnostic engine: clock, engine, timer, animation, timeline, tweens, values, eases, stagger, utils. No Avalonia dependency, ever. |
| `MonacoLabs.Presto.Avalonia/` | **Avalonia adapter.** Frame pump, `AvaloniaObject`/`AvaloniaProperty` targets, render-transform composition, color/brush/thickness interpolation, control helpers. Custom controls (`Pager`, `Sheet`, `ToastHost`, the image controls…) live in `Controls/` (`MonacoLabs.Presto.Controls`). Root namespace `MonacoLabs.Presto`, shared with the core: namespaces never carry an `Avalonia` segment (it would shadow the `Avalonia.*` namespaces), and folders map onto the root namespace. |
| `samples/PrestoGallery/PrestoGallery*/` | Gallery app (shared project + Desktop/Browser/Android/iOS heads). `Pages/` hold the Reference section, one page per feature; `Builder/` holds fluent-builder pages that print their own code, plus the XAML attach demo; `Showcase/` holds the shared kit (`ShowcasePage`, `CodePanel`, `Snippet`) and `Showcase/Pages/` the multi-feature demos. Desktop is the primary dev target. |
| `samples/PrestoGallery/PrestoGallery.SnippetGenerator/` | Sample-only source generator: `<!-- snippet: Name -->` (XAML) and `#region snippet Name` (C#) regions become `PrestoGallery.Showcase.Snippets` consts. |
| `samples/Tempo/Tempo*/` | Standalone focus-timer app (Semi.Avalonia Dark) that dogfoods Presto as an outside consumer would: shared project `Tempo/` plus heads `Tempo.*/`, Desktop the primary. All its motion is in `Motion/TempoMotion.cs`. Headless tests in `tests/Tempo.Tests/`; decisions in ARCHITECTURE §5. |
| `tests/MonacoLabs.Presto.Tests/` | xunit v3 tests for the core. Bridge tests (Avalonia.Headless) live in `tests/MonacoLabs.Presto.Avalonia.Tests/`. |
| `tests/MonacoLabs.Presto.AotProbe/` | NativeAOT console app on Avalonia.Headless: one `PASS`/`FAIL` line per trim/AOT hazard, exit code = failures. Run by the CI `aot` job. |
| `docs/` | `ARCHITECTURE.md` (goals, architecture, testing strategy, project-wide decisions, samples, provenance appendix), `BACKLOG.md` (open work), `changes/` (change proposals for work in progress). |
| `docs/specs/` | Feature specs, the source of truth for behavior: one folder per package (`core/`, `avalonia-bridge/`, `lottie/`, `devtools/`), each with an `index.md`. Format in `docs/specs/README.md`. |

Build configuration is shared through root `Directory.Build.props` (language/nullable/version) and `Directory.Packages.props` (central package management — add package versions there, never in a csproj).

## Commands (PowerShell)

```powershell
dotnet build MonacoLabs.Presto.slnx
dotnet test --project tests/MonacoLabs.Presto.Tests          # MTP mode (global.json); note --project, not a bare path
dotnet test --solution MonacoLabs.Presto.slnx
tests/PrestoGallery.Tests/bin/Debug/net10.0/PrestoGallery.Tests.exe --report-xunit --results-directory <dir>   # per-test times (after a build)
dotnet run --project samples/PrestoGallery/PrestoGallery.Desktop
dotnet watch --project samples/PrestoGallery/PrestoGallery.Desktop      # hot reload (see below)
dotnet run --project samples/Tempo/Tempo.Desktop               # the Tempo app; -- --skip-intro starts on the main view
dotnet publish tests/MonacoLabs.Presto.AotProbe -c Release -r win-x64 -o artifacts/aot-probe -p:TrimmerSingleWarn=false
artifacts/aot-probe/MonacoLabs.Presto.AotProbe.exe            # exit code = failing cases
dotnet publish samples/PrestoGallery/PrestoGallery.Desktop -c Release -r win-x64 -p:PublishAot=true -o artifacts/gallery
samples/PrestoGallery/aot-smoke.ps1 artifacts/gallery/PrestoGallery.Desktop.exe   # every page for 6 s
```

A head (gallery Desktop, Tempo.Desktop) publishes with NativeAOT through `-p:PublishAot=true`; the probe sets it in its csproj. When ILC finds no platform linker, point `PATH` and `LIB` at an installed MSVC toolset and Windows SDK and pass `-p:IlcUseEnvironmentalTools=true`.

The Desktop head takes launch options after `--`, e.g. `dotnet run --project samples/PrestoGallery/PrestoGallery.Desktop -- --page carousel --motion reduced --size 1400x1000`: `--page` takes a slug or title, `--motion` is `full|reduced|system`, `--frame-pacing on|off` sets `AvaloniaFrameSource.Paced` for A/B checks, and `--list-pages` prints the slugs. Its output shows under `dotnet run`, though it is a WinExe, so launched directly its output appears only when redirected; bad arguments exit with code 2. Use these to start DevTools checks on a page instead of navigating the sidebar.

CI (`.github/workflows/ci.yml`, Windows) runs the `aot` job and `build-test-pack`, which builds by project name, runs the four suites and uploads the CI-versioned `.nupkg`/`.snupkg` files as each run's `packages` artifact; `release.yml` publishes to nuget.org on a `v*` tag once approved in the `nuget` environment, then calls `samples.yml` (also weekly and on demand), which publishes the NativeAOT Windows and WASM AOT browser samples, attaches the Windows zips to the release and deploys to GitHub Pages.

Tests use xunit v3 on Microsoft.Testing.Platform (`UseMicrosoftTestingPlatformRunner`), which the .NET 10 SDK requires; test projects are `Exe` and can also be run directly.

Android/iOS/Browser heads exist but are not part of the day-to-day loop; build them only when asked. Hot reload does not support WebAssembly.

**Specs:** before changing a feature's behavior, read its spec in `docs/specs/` (and the specs in its `depends`), and update its requirements and `Tests:` lines in the same change; a change that diverges from its spec without updating it is incomplete. Every design decision, with its reason, lives in the Decisions section of the spec it governs; a requirement marked `(inherited)` records behavior nobody decided. New work starts as a change proposal in `docs/changes/`, and a new feature as a draft spec (requirements before code). Every public type is owned by exactly one spec's `types`. `SpecTests` (core tests) and `SpecCoverageTests` (bridge tests) enforce the format, the cited tests and paths, and type ownership.

**Implementation workflow:** `.claude/workflows/implement-plan.js` implements a change end to end (decompose against the specs → implement item by item, updating specs, with a concision review after each → whole-diff review → full test run with fix rounds → delete the change proposal and the backlog lines it resolved). Run it with `Workflow({ name: "implement-plan", args: "docs/changes/<name>.md" })` (or a backlog item, or plan text) once a change is agreed; it never commits. `.claude/workflows/implement-changes.js` chains it over several proposals in order (`args: ["docs/changes/a.md", "docs/changes/b.md"]`), stopping at the first red one, then publishes and runs the AOT probe.

## Dev loop: DevTools and Hot Reload

**Developer Tools** (`AvaloniaUI.DiagnosticsSupport` 2.2.x) is enabled in `samples/PrestoGallery/PrestoGallery/App.axaml.cs` via `this.AttachDeveloperTools()` under `#if DEBUG`. With the sample running, press F12 to launch the `avdt` tool, or use the `avalonia_devtools` MCP server (`avdt mcp`) from Claude Code to inspect the tree, read/set properties, take screenshots and send input. Prefer `attach-to-app` against the running Desktop sample.

**Hot Reload** (`AvaloniaUI.DiagnosticsSupport.HotReload` 1.0.0) is referenced by the shared `samples/PrestoGallery/PrestoGallery` project only in Debug **and only when an `AvaloniaUILicenseKey` MSBuild property is present** — it is a licensed package and the app builds and runs normally without it. The package auto-initializes via a generated module initializer; `.axaml` edits are patched into live instances (verified: same process, properties updated in place). `AvaloniaHotReloadEnableFileWatcher` is on so `.axaml` edits reload even without `dotnet watch` (e.g. when launched from Rider). C# edits additionally require `dotnet watch` and follow normal .NET Hot Reload "rude edit" rules. Hot reload diagnostics are echoed to the console (`App.axaml.cs`, under `PRESTO_HOTRELOAD`), so `dotnet watch` output shows what was reloaded.

Two non-obvious constraints, both learned the hard way:
- The package must be referenced from the project that **owns the XAML** (`PrestoGallery`), not the executable head. Its source generator only registers the referencing project's `.axaml` files; referenced projects are collected only if they also reference the package. Its build/licensing targets flow to the heads transitively, so the head needs nothing.
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
- **Time is `double` milliseconds**, the unit every API takes. Seconds mode is a scale factor on the engine, not a second code path.
- **The timing model is covered by tests ported from upstream; change it deliberately, with a spec decision.** The structure those tests exercise (Clock → Timer → Animation/Timeline, linked-list children, `tick()` in render) is subtle and allocation-free; a change that breaks a ported test is a behavior change, not a cleanup.
- **anime.js 4.5.0 is provenance, not the authority.** The specs decide behavior. A git-ignored checkout lives at `tools/anime/` (`src/`, `tests/suites/`); recreate it with `git clone --depth 1 --branch v4.5.0 https://github.com/juliangarnier/anime tools/anime` if missing. When touching a ported module or its ported tests, read the upstream file and its test suite first.

## API conventions

- Public entry point is the static `Presto` class (`Presto.Animate`, `Presto.CreateTimer`, `Presto.CreateTimeline`, `Presto.Stagger`, `Presto.Defaults`, `Presto.Engine`). Users should need one `using MonacoLabs.Presto;`, which the core and the bridge share.
- Parameters are `init`-only records/classes with the anime.js names in PascalCase (`Duration`, `Delay`, `Loop`, `Alternate`, `Ease`, `OnComplete`…). Animated properties are supplied through an indexer/collection initializer so calls read like the JS object literal. The specs give the exact behavior.
- Anything in anime.js that can be a function-based value (`(target, i, total) => …`) accepts a delegate here; `Stagger` returns such delegates.
- Eases are `Ease` delegates (`double → double`) exposed as `Eases.OutQuad`, `Eases.Out(3)`, `Eases.Spring(...)`, plus a string parser (`"inOutQuint"`, `"out(2)"`) for XAML and defaults.
- Mirror anime.js method names on instances: `Play`, `Pause`, `Resume`, `Restart`, `Reverse`, `Alternate`, `Seek`, `Stretch`, `Complete`, `Cancel`, `Revert`, `Reset`, `Refresh`, plus `Task Then()` / awaitable for `.then()`.

## Code conventions (project-specific)

- File-scoped namespaces; folder structure equals namespace; one public type per file.
- Types are `sealed` unless designed for inheritance. Internal plumbing is `internal` and exposed to tests via `InternalsVisibleTo`.
- Hot-path code (tick, tween render) avoids allocations and LINQ; keep the upstream linked-list approach.
- Libraries and samples stay trim- and AOT-clean: no new reflection, string-path or reflection bindings, `MakeGenericType` or reflection JSON, and no IL2xxx/IL3xxx suppressions. A library type animated by name implements `IMemberTarget`; a sample's plain-object target calls `Presto.RegisterTarget<T>()`. Reach Avalonia internals with `[UnsafeAccessor]`.
- A public API change updates the project's `PublicAPI.Unshipped.txt` (the RS0016 code fix writes it).
- Test names describe behavior and, when porting, cite the upstream suite in the class name (e.g. `TimingsTests` ↔ `timings.test.js`).
- After changes to the core, the tests are the verification step. After changes to the bridge or sample, verify visually via the Desktop sample (DevTools screenshots are fine).
