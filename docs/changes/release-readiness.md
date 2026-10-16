# Release readiness for 0.1.0-preview.1

## Why

The four packable projects (`MonacoLabs.Presto`, `.Avalonia`, `.Avalonia.DevTools`, `.Avalonia.Lottie`) have never been published, and the repo is not ready to publish them. There is no CI (no `.github/` at the root), so nothing outside the dev machine has built or tested a commit. Nothing guards the public API, although the specs README says "API breaks are checked by tooling". The version is a bare `0.1.0` with no changelog, every package ships the 1 775-line repo README (relative links, and Avalonia content in the core package), and the README claims more than the backlog's `[PLATFORM]` items say was checked. An early preview should build in public CI, record its API and claim only what was verified.

Trimming and NativeAOT were part of this change and moved to [`trim-and-aot.md`](trim-and-aot.md): an investigation found that marking the libraries trimmable breaks every animatable at run time, through sites no analyzer reports, so that work needs its own design and verification lane.

## What

**CI.** `.github/workflows/ci.yml` runs on every push and pull request, on `windows-latest`, with no secrets:

1. `actions/setup-dotnet` from `global.json`. A NuGet cache is keyed on `Directory.Packages.props`.
2. Build in Debug: the Desktop gallery head, Tempo and the four test projects. Their references cover every library, `PrestoGallery`, and the snippet generator. The Android, iOS and Browser heads are not built. They need the `android` (plus a JDK), `ios` and `wasm-tools` workloads, and a full iOS build needs macOS. For the same reason CI never restores or builds `MonacoLabs.Presto.slnx` as a whole: restoring the mobile heads without their workloads fails with NETSDK1147.
3. `dotnet test --project <p> --no-build --report-xunit --results-directory TestResults` for each suite: core, bridge, `PrestoGallery.Tests` (about 92 s) and `Tempo.Tests`. Test results are uploaded when a run fails.
4. `dotnet pack` for each library in Release with `-p:PrestoCiBuild=${{ github.run_number }}`. The `.nupkg`/`.snupkg` files are uploaded as the `packages` artifact.

Hot reload stays off in CI with no special handling: `PrestoHotReload` needs `AvaloniaUILicenseKey`, which CI does not have, and the key never becomes a repository secret. `ContinuousIntegrationBuild` already switches on under `GITHUB_ACTIONS`. `global.json` moves from `10.0.100` to the feature band the dev machine actually resolves (10.0.401 today, through `latestFeature`). setup-dotnet would otherwise install 10.0.100, whose compiler may be older than the `Microsoft.CodeAnalysis.CSharp` 5.9.0 that the snippet generator builds against, and `Directory.Packages.props` says that version must be no newer than the SDK's compiler.

**Release.** `.github/workflows/release.yml` runs on a `v*` tag push, plus `workflow_dispatch` with a `dry-run` input. It repeats the build and tests, then packs without `PrestoCiBuild`. It fails unless the tag equals `v` + the packed `Version` and every sibling dependency in the nuspecs is an exact `[x]` range. It then waits on a GitHub environment `nuget` that requires the owner's approval. Once approved, it authenticates through nuget.org Trusted Publishing (`NuGet/login`, OIDC, so no long-lived key exists), pushes with `--skip-duplicate` (the `.snupkg` files go along), and creates a GitHub release whose notes are that version's CHANGELOG section.

**Versioning and changelog.** `Directory.Build.props` replaces `<Version>0.1.0</Version>` with:

```xml
<VersionPrefix>0.1.0</VersionPrefix>
<VersionSuffix>preview.1</VersionSuffix>
<VersionSuffix Condition="'$(PrestoCiBuild)' != '' and '$(VersionSuffix)' != ''">$(VersionSuffix).ci.$(PrestoCiBuild)</VersionSuffix>
<VersionSuffix Condition="'$(PrestoCiBuild)' != '' and '$(VersionSuffix)' == ''">ci.$(PrestoCiBuild)</VersionSuffix>
<PackageReleaseNotes>https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/CHANGELOG.md</PackageReleaseNotes>
```

A new root `CHANGELOG.md` uses the Keep a Changelog layout: `## [Unreleased]`, then `## [0.1.0-preview.1] - <date>`, which summarizes the first preview by area (core engine, Avalonia bridge, controls, Lottie, DevTools) and links to the README sections rather than listing every type. It is not packed.

**Trimming and AOT.** Done in [`trim-and-aot.md`](trim-and-aot.md), which lands before this change: preview.1 ships with the four libraries marked `IsTrimmable` and `IsAotCompatible`, and the package READMEs and the repo README state the trimming and NativeAOT support that change verifies.

**Public API baseline.** `Microsoft.CodeAnalysis.PublicApiAnalyzers` is versioned in `Directory.Packages.props` (no newer than the SDK's compiler, like the snippet generator's Roslyn packages). Each of the four csprojs references it with `PrivateAssets="all"`. Each library gets `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, starting with `#nullable enable`, and the RS0016 code fix fills `Unshipped.txt`. At each release, the release commit moves the Unshipped lines into Shipped. Between previews, breaking changes stay allowed but show up as RS0017 diffs in review. `EnablePackageValidation` is switched on now. `PackageValidationBaselineVersion` is set to `0.1.0-preview.1` once that version is on nuget.org.

**Sibling packages pin exactly.** The core grants `InternalsVisibleTo` to the bridge, and the bridge grants it to Lottie and DevTools. The bridge uses about a hundred core internals (`Tween._numbers`, `Timer._engine`, `Renderer`…), so a bridge running against a different core build fails at run time. Pack emits `>=` for project references. A target in `Directory.Build.props` makes it emit `[x]`:

```xml
<Target Name="PinSiblingPackages" AfterTargets="_GetProjectReferenceVersions" Condition="'$(IsPackable)' != 'false'">
  <ItemGroup>
    <_ProjectReferencesWithVersions Update="@(_ProjectReferencesWithVersions)" ProjectVersion="[%(ProjectVersion)]" />
  </ItemGroup>
</Target>
```

`_ProjectReferencesWithVersions` is a NuGet-internal item (NuGet/Home#5556). If NuGet renames it, the target would silently stop working, so the release workflow checks the nuspecs. `InternalsVisibleTo` to the test assemblies stays: it is harmless metadata.

**Package hygiene.**
- Each library gets a short package `README.md` in its own folder (roughly 40–80 lines): what it is, install, one quick start, and absolute GitHub links for everything else. `Directory.Build.props` packs `$(MSBuildProjectDirectory)/README.md` instead of the root README. The root README stays the full manual.
- `LICENSE` stays packed beside `PackageLicenseExpression` MIT, because it carries anime.js's notice, which MIT requires to travel with the copy.
- Add `<Copyright>`.
- Remove the bridge csproj's leftover `<Folder Include="Controls\" />`.
- Rename the Android sample's `com.CompanyName.PrestoGallery` template id.
- `tools/` cannot reach a package. Pack takes only project-folder items plus the two explicit root files. `tools/anime/` and `tools/avalonia/` are git-ignored, so CI never sees them, and `tools/anime-fixtures/` is outside every project.

**README.**
- The package table and Install list gain DevTools (and Install gains Lottie).
- "built against Avalonia 12.1.2" becomes 12.1.3, the version in `Directory.Packages.props`.
- "Version 0.1.0, pre-release" becomes `0.1.0-preview.1`.
- The Platforms table claims only what was run:
  - The Browser row drops "with no trim warnings", which came from a build whose libraries were not trimmable; `trim-and-aot.md` owns any trimming claim. It also names what the backlog lists as unchecked: the Compositor and Lottie pages, the showcase pages, blur cost, image filters and the hidden signal.
  - The WebAssembly note's render-thread list says that Android and iOS are by Avalonia's design, not runs (iOS has never run).
  - The Desktop row names Windows only, the one desktop OS driven.

What does not change: no library behavior, no trimming or AOT flags, no target framework, no package ids, and no feature APIs.

## Decisions

- **CI runs on Windows only, at first.** Reason: the bridge and sample suites render text in the headless framebuffer with the system default font, and their expectations were taken on Windows, so a Linux runner would fail on font metrics rather than on code. Rejected for now: a Linux/macOS matrix. Its first step is a backlog item (pin Inter in the test app builders).
- **CI builds the Desktop-relevant projects by name, not the solution.** Reason: restoring the mobile and browser heads needs workloads, and iOS needs macOS. Building the test projects and the Desktop head reaches every library and sample that runs on desktop. Rejected: installing every workload (slow, and still no iOS build on Windows); a `.slnf` over the `.slnx` (solution-filter support for `.slnx` is not something this repo has tried).
- **All four test suites run on every push.** Reason: the sample suites are the only automated check on the gallery and Tempo, which are the README's demos and the outside-consumer dogfood, and about 92 s is cheap in CI. Rejected: core and bridge only, which lets a sample break unseen until someone opens it.
- **Tests run in Debug and packages are packed in Release.** Reason: the core and the compositor registry carry `[Conditional("DEBUG")]` assertions, and `CompositorRegistryTests` has `#if DEBUG` cases, so Debug tests check more. Packages ship Release.
- **The committed version is always the next release, and CI appends `.ci.<run>`.** Reason: a CI package can never collide with a published one, and a release needs no version edit beyond the CHANGELOG. CI packages are artifacts only, never pushed to a feed, so it does not matter that they sort above the release they lead to. Right after a release, `VersionSuffix` moves to the next preview.
- **Releases are tag-triggered, approval-gated and keyless.** A `v*` tag runs the release workflow, which waits for approval in the `nuget` environment and pushes through Trusted Publishing. Reason: the tag ties a package to a commit, the approval keeps a human on every push to nuget.org, and OIDC leaves no API key to leak or rotate. Rejected: pushing from a laptop (not reproducible, and the key lives on disk); publishing on every merge to `main` (a preview is a decision, not a side effect).
- **Sibling packages depend on each other at exactly the same version.** Reason: they share internals through `InternalsVisibleTo`, so only the build they were compiled with works. Rejected: making those internals public (a large, premature API commitment); `>=` ranges (a mismatched upgrade fails at run time).
- **One short README per package; the repo README stays the manual.** Reason: nuget.org breaks relative links, a core-only user should not get an Avalonia manual, and 1 775 lines is a manual, not a package page. Rejected: one shared short README (Lottie and DevTools would read as afterthoughts).
- **No strong naming.** Reason: the packages target only `net10.0`, whose runtime ignores strong names.
- **The public API analyzer's ids are errors.** RS0016 and RS0017 go into `WarningsAsErrors`. The rest of the warnings-as-errors policy, which follows the first `IsAotCompatible` build, moved to `trim-and-aot.md`.
- **preview.1 ships without a package icon.** A real icon comes before the first non-preview release. Rejected: a placeholder.
- **The README claims only what was run.** The Desktop row names Windows only, the one desktop OS driven; no platform check blocks preview.1, and the `[PLATFORM]` Browser/Android checks stay in the backlog.
- **The `MonacoLabs` package-id prefix is reserved on nuget.org before the first push.** It takes days and does not block the release.

## Spec changes

**ARCHITECTURE.md §4 (Project-wide decisions)** gains the decisions above on CI, versioning and release, exact sibling pins, the public API baseline and package READMEs. **§3** adds that CI runs all four suites on Windows. The spec changes for trimming and AOT (CORE-TARGETS, CORE-MOTIONVALUE-013, BRIDGE-REORDER-021, BRIDGE-DISTORT, BRIDGE-GLYPHS, BRIDGE-FOLLOW, DEVTOOLS-OVERLAY, and §3's "keeps no reflection") moved to `trim-and-aot.md`.

**CLAUDE.md**:
- Commands: a line on CI (what runs, where to find the `packages` artifact).
- Code conventions:
  - "A public API change updates the project's `PublicAPI.Unshipped.txt` (the RS0016 code fix writes it)."

## Tasks

1. **Versioning.** Bump `global.json` to the dev machine's band. Split `Version` into `VersionPrefix`/`VersionSuffix` plus `PrestoCiBuild`, add `PackageReleaseNotes` and `Copyright`, and add the `CHANGELOG.md` skeleton. Verify:
   - `dotnet msbuild MonacoLabs.Presto/MonacoLabs.Presto.csproj -getProperty:Version` prints `0.1.0-preview.1`, and `0.1.0-preview.1.ci.7` with `-p:PrestoCiBuild=7`.
   - The full local test run is green on the pinned SDK.
2. **CI workflow** (`.github/workflows/ci.yml`). Verify on GitHub: a branch push and a PR both go green, the run takes the expected SDK (logged by `dotnet --info`), and the `packages` artifact holds four `.nupkg` and four `.snupkg` files versioned `0.1.0-preview.1.ci.<run>`.
3. **Exact sibling pins.** Add the `PinSiblingPackages` target. Verify: the bridge's packed nuspec lists `MonacoLabs.Presto` at `[0.1.0-preview.1]`, and Lottie's and DevTools' list the bridge the same way.
4. **Public API baseline.** Do this after every other change adding public types has landed. Add the analyzer to `Directory.Packages.props` and the four csprojs, generate `Unshipped.txt` with the RS0016 fix-all, and enable package validation. Verify: the build reports no RS0016/RS0017, a scratch public member fails with RS0016 (then removed), and `dotnet pack` passes package validation.
5. **Package hygiene.** Write the four package READMEs, switch the README pack item, remove the empty `Folder` include and rename the Android application id. Verify by opening each `.nupkg` as a zip:
   - It contains `README.md`, `LICENSE`, `lib/net10.0/*.dll` and `*.xml`.
   - The nuspec carries the license expression, the readme, the release notes, and a repository commit (SourceLink).
   - Each `.snupkg` holds its `.pdb`.
   - Nothing from `tools/`, `docs/` or `samples/` is present.
   - The README renders with working links in NuGet Package Explorer.
6. **Repo README.** Update the packages table, Install, the Avalonia version, the version line and the Platforms section as described. Verify: every Platforms claim traces to a run recorded in this change or the git history, and every unchecked head names its open `[PLATFORM]`/smoke-run backlog item.
7. **Docs.** Make the ARCHITECTURE §3/§4, spec and CLAUDE.md edits listed above. Verify: `SpecTests` and `SpecCoverageTests` pass.
8. **Release workflow.** Add `.github/workflows/release.yml` and the `nuget` environment with a required reviewer. The owner creates the Trusted Publishing policy on nuget.org (repo `stevemonaco/MonacoLabs.Presto`, workflow `release.yml`). Verify: a `workflow_dispatch` dry run packs, passes the tag/version and exact-pin checks, and stops before the push.
9. **Release.** Move `Unshipped.txt` into `Shipped.txt`, date the CHANGELOG entry, tag `v0.1.0-preview.1`, and approve the run. Verify:
   - The four packages appear on nuget.org with their READMEs.
   - A fresh `dotnet new avalonia.app` that adds `MonacoLabs.Presto.Avalonia` from nuget.org animates a control.
   - Then bump `VersionSuffix` to `preview.2` and set `PackageValidationBaselineVersion`.
10. **Backlog.** Add items for what this change leaves out: a Linux/macOS CI leg (Inter pinned in the test app builders), and a scheduled workflow that builds the Browser and Android heads with their workloads.

## Open questions

None.
