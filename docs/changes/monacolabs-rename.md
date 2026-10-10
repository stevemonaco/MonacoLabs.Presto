# Rename to MonacoLabs.Presto and retire the AvaloniaBridge name

## Why

The product is being renamed from `Monaco.Presto` to `MonacoLabs.Presto`, along with the GitHub repo. The Avalonia adapter is also called `Monaco.Presto.AvaloniaBridge`. That name exists only because `Monaco.Presto.Avalonia` as a namespace would shadow `Avalonia.*` for all code inside it. "Bridge" then shows up in every consumer's `using`, in the XAML `clr-namespace`s and in the package ID, though it means nothing to a user. The rename is the cheapest time to fix this, before anything ships under the old name.

## What

Package IDs, assembly names and project folders say what each package is. Namespaces say where a type sits in the API, and no namespace contains an `Avalonia` segment.

| Today (package / root namespace) | After: package and assembly | After: root namespace |
|---|---|---|
| `Monaco.Presto` / `Monaco.Presto` | `MonacoLabs.Presto` | `MonacoLabs.Presto` |
| `Monaco.Presto.AvaloniaBridge` / same | `MonacoLabs.Presto.Avalonia` | `MonacoLabs.Presto` |
| `Monaco.Presto.AvaloniaBridge.Lottie` / same | `MonacoLabs.Presto.Avalonia.Lottie` | `MonacoLabs.Presto.Lottie` |
| `Monaco.Presto.AvaloniaBridge.DevTools` / same | `MonacoLabs.Presto.Avalonia.DevTools` | `MonacoLabs.Presto.DevTools` |
| `Monaco.Presto.Tests` | `MonacoLabs.Presto.Tests` | `MonacoLabs.Presto.Tests` |
| `Monaco.Presto.AvaloniaBridge.Tests` | `MonacoLabs.Presto.Avalonia.Tests` | `MonacoLabs.Presto.Tests` |

The bridge's sub-namespaces move up one level: `…AvaloniaBridge.Controls` becomes `MonacoLabs.Presto.Controls`, and likewise for `Bindings`, `Compositor`, `Draggables`, `Effects`, `Events`, `Gestures`, `Platform`, `Prebuilt`, `Shapes`, `Text` and `Transforms`. The bridge's `Targets` and `Values` merge with the core namespaces of the same name. A consumer then needs one `using MonacoLabs.Presto;` for the core and the bridge together. The solution becomes `MonacoLabs.Presto.slnx`.

What stays the same: type names (`PrestoAvalonia`, `AvaloniaFrameSource`, …), behavior, the `BRIDGE-`/`LOTTIE-`/`DEVTOOLS-` spec ids, the `docs/specs/avalonia-bridge/` folder, the `PrestoSample*` projects and namespaces, and the rule that the core does not reference Avalonia. Project references still enforce that rule; the namespace never did.

## Decisions

These are project-wide, so they replace the "bridge's root namespace is `Monaco.Presto.AvaloniaBridge`" decision in [ARCHITECTURE.md](../ARCHITECTURE.md) §5.

- **Avalonia packages share the core's namespace.** The bridge's root namespace is `MonacoLabs.Presto`, and its package and assembly are `MonacoLabs.Presto.Avalonia`. Reasons: an `Avalonia` namespace segment would shadow `Avalonia.*` for every qualified name and generated source inside it, and a single `using` matches the API convention. Separate assemblies contributing to one namespace is ordinary .NET practice (every `Microsoft.Extensions.*` package adds to `Microsoft.Extensions.DependencyInjection`). Checked: no core type shares a name with a bridge type in the merged namespaces (root, `Targets`, `Values`), and no type is named like a sibling namespace. Rejected: `MonacoLabs.Presto.Avalonia` with `global::` at each qualified reference (works, but the hazard stays for every future file and generator); `.Ava`/`.Xaml`/`.UI` (they read as workarounds, and `.UI` would not fit a second UI-framework adapter); merging the assemblies (the core would gain an Avalonia dependency).
- **Package names carry `Avalonia`; namespaces never do.** Lottie and DevTools are packaged as `MonacoLabs.Presto.Avalonia.Lottie`/`.DevTools`, with namespaces `MonacoLabs.Presto.Lottie`/`.DevTools`. Reason: on NuGet, the package name is how people find the adapter for their UI framework. A future adapter for another framework would be `MonacoLabs.Presto.<Framework>`, again in the `MonacoLabs.Presto` namespace.
- **Folders match the root namespace, not the project name.** In `MonacoLabs.Presto.Avalonia/`, `Controls/` holds `MonacoLabs.Presto.Controls`. The "folder structure equals namespace" convention is read relative to each project's `RootNamespace`.
- **The bridge test project uses the `MonacoLabs.Presto.Tests` namespace.** Reason: `MonacoLabs.Presto.Avalonia.Tests` as a namespace would reintroduce the shadowing in the most Avalonia-heavy code, and the project already compiles the core tests' `EngineTestBase`, `TestHelpers` and `SpecCatalog` in that namespace. The bridge's `AllocationTests` is renamed `BridgeAllocationTests` so that no test class name exists in both projects.
- **The XAML namespace URI follows the repo.** It becomes `https://github.com/stevemonaco/MonacoLabs.Presto`, prefix `presto` unchanged. Reason: nothing has been published, so breaking the URI costs nothing, and a URI naming the old repo would mislead. The URI is an opaque identifier and is never fetched.
- **No migration path for the old names.** Nothing was published to NuGet under `Monaco.Presto*`, so there are no packages to deprecate and no type forwarders to add.
- **Spec ids and the `avalonia-bridge/` folder keep their names.** Reason: ids are cited across specs, tests and history, and must never be renumbered; "bridge" stays a fine internal word for the adapter layer.

## Spec changes

- `docs/specs/README.md`: the folder table's Package column takes the new package IDs.
- Every spec's front matter: `package` becomes the new ID; `namespaces` maps as in What; `sources` takes the new folder paths. Requirements are unchanged. Wherever requirement or Purpose text names a namespace or assembly, it takes the new name.
- `docs/ARCHITECTURE.md`: §5 swaps the AvaloniaBridge decision for the first three decisions above; the module map (§2), §3.2's heading and §4's test-project paths take the new names.

## Tasks

1. **Move the files, with no content edits.** `git mv` the four library folders, the two test folders, each `.csproj` inside them, and the `.slnx`. Commit this on its own, so git's rename detection and blame follow every file.
2. **Update project files.** `RootNamespace`, `InternalsVisibleTo`, `ProjectReference` and linked `Compile` paths; `.slnx` project paths; `Directory.Build.props` `Product`, `RepositoryUrl`, `PackageProjectUrl`. `AssemblyName` and `PackageId` default to the project name.
3. **Rewrite namespaces** in the library and test sources (`namespace` and `using` lines), and rename the bridge's `AllocationTests` to `BridgeAllocationTests`. Drop any `using` lines made redundant by the merge.
4. **Update the XAML surface.** `XmlnsDefinition`/`XmlnsPrefix` in both `AssemblyInfo.cs` files, with the new URI and namespaces. In the sample: the 46 `xmlns` uses of the URI, and the `clr-namespace:…;assembly=…` in `LiquidTypePage.axaml`. In the tests: the `avares://Monaco.Presto.AvaloniaBridge.Tests/…` URIs in `LottieCompositionTests` and `LottieXamlView.axaml`.
5. **Update the sample's C#** `using` lines and any fully qualified names.
6. **Update the tooling:** `SpecCatalog.Folders` package names, plus `SpecTests`/`SpecCoverageTests` if they name packages.
7. **Update the docs:** the spec front matter and text above, `ARCHITECTURE.md`, `BACKLOG.md`, the other open proposals in `docs/changes/`, `README.md`, the `LICENSE` notice line, `CLAUDE.md` (Layout table, commands, the root-namespace note), `.claude/workflows/implement-plan.js` (solution and test-project paths), and `tools/anime-fixtures/`.
8. **Sweep:** `git grep -n "Monaco\.Presto\|AvaloniaBridge"` returns nothing outside git history.
9. **Verify:** `dotnet build MonacoLabs.Presto.slnx` with no new warnings; `dotnet test --solution MonacoLabs.Presto.slnx` green (`SpecTests` and `SpecCoverageTests` cover the front matter and type ownership). Run the Desktop sample and spot-check the Liquid Type page (it uses a `clr-namespace` import) and a Lottie page through DevTools, since XAML namespace errors surface only at load time.

## Open questions

None. The repo stays under `stevemonaco` as `MonacoLabs.Presto` and is renamed on GitHub before this change lands. This change lands before the other open proposals; task 7 updates their text.
