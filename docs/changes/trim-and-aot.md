# Trimming and NativeAOT

## Why

No Presto app can be published with NativeAOT today, and a trimmed app breaks once the libraries are marked trimmable. A NativeAOT publish of the Desktop gallery (2026-10-16: SDK 10.0.401, Avalonia 12.1.3, ILCompiler 10.0.12) starts, but 22 of its 80 pages crash on load, and Tempo crashes on its first layout. The same crash appears in a trimmed, non-AOT publish once `IsTrimmable` is set, which is what `release-readiness.md` planned for preview.1. The Browser head works today only because the libraries are not trimmable, so a partial trim keeps them whole.

The main cause is one the analyzers cannot see. The libraries animate their own plain objects by member name, through the reflection-based `ObjectTargetAdapter`, and the name is a string (`["V"]`, `["X"]`, `["CurrentTime"]`):
- `Animatable`'s `CallbacksTarget.V`, which every animatable uses. That covers Sheet, Draggable, Pager and Carousel, Springs, Follow and DevTools.
- `Timeline.Sync`, which animates `Timer.CurrentTime`.
- `CompositorScratch`, used by every compositor animation and layout transition.
- `Pager`, which animates `Draggable.X`.
- `Draggable.Coords` (overshoot and auto-scroll), `ContentSizeTransition.Inset` and `Selection.Edges`.
- `FollowerHolder<T>.Value`.

Under NativeAOT a member is unreachable even when the app uses it statically, because the compiler emits reflection metadata only for members it sees reflected on. Release-readiness's fix for plain objects, a type-level `[DynamicallyAccessedMembers]`, was tested and does not work: both ILLink and ILC ignore it when the static type at the `GetType()` call is `object`. A `[DynamicallyAccessedMembers]` generic parameter does work.

The analyzers also report sites release-readiness's table missed or got wrong:
- `GlyphContours.PathOf` returns null under AOT, so `GlyphShards.Of` throws and the kinetic-poem and particle-type pages crash.
- `Distort` silently stops tracking child redraws, because `Assembly.GetType` returns null and every use is `?.`.
- DevTools' `TickableList` builds 8 reflection bindings on `TickableRow`, so its rows render empty.
- Lottie's `FromDotLottie` serializes a `JsonNode` with reflection JSON. That is disabled in every trimmed app, Browser included, and the `catch` reports it as a misleading `FormatException: Invalid dotLottie archive`.
- `FollowerHolder<double>` fails too ("missing native code or metadata"), not only struct channels.

Two build blockers turned up as well. `-p:PublishAot=true` or `-p:PublishTrimmed=true` on the command line reaches the netstandard2.0 snippet generator and fails restore (NETSDK1207/NETSDK1124). And on the dev machine, ILC finds no linker (see Tasks). The samples have hazards of their own: GlyphLab reflects on `PlatformImpl` and `FillPath`, LifecyclePage uses a path-less `new Binding()`, the `OrbitScene.Dot` and `Particle` scene classes are plain objects, and Tempo's `ReflectionBinding` at `ShellView.axaml.cs:122` means its detail drawer never opens under AOT.

Avalonia 12, SkiaSharp, Skottie and CommunityToolkit.Mvvm report no trim or AOT warnings, so the work is all ours. An experiment proved the diagnosis: `[DynamicDependency]` on 9 internal types, a closed `switch` in `Follower` and an `UnsafeAccessor` in `GlyphContours`. With those, every library crash in the load-only smoke run went away, and only the three sample issues remained. It took four rebuild rounds to find them all, which is the argument for the design below.

This was split out of `release-readiness.md` and lands before it: preview.1 ships trimmable and AOT-compatible.

## What

**Library types never reach the reflection adapter.** The core gets an internal interface, `IMemberTarget`, with one method, `IPropertyAccessor? Member(string name)`. `ObjectTargetAdapter.Resolve` asks it first. A target that implements it answers from a static table of accessors. A name the table does not hold throws the unknown-member `ArgumentException`, listing the names it does hold, and never falls through to reflection.

The accessors come from the adapter's existing member accessor, split in two. Today it finds the member by reflection and then classifies the write. After the split, the write side takes typed get and set delegates and applies the same rules as today: numeric writes in the member's own type, integral rounding, `string`, `PrestoColor` and `object` writes, the codec or `DiscreteCodec`, and the stale-codec rebuild (CORE-TARGETS-022 to -030). Reflection becomes one way to supply the delegates, and a typed internal factory the other. A frame stays a delegate call with no boxing for numbers, as with today's compiled expressions. These types implement the interface:

| Type | Members |
|---|---|
| `Animatable.CallbacksTarget` (core) | `V` |
| `Timer` (core; `Animation` and `Timeline` inherit it) | `CurrentTime`, `IterationCurrentTime`, `Progress`, `IterationProgress`, `CurrentIteration` |
| `CompositorScratch` | `X`, `Y`, `Rotate` (the existing angle accessor), `Scale`, `ScaleX`, `ScaleY`, `Opacity`. `CompositorScratch.Adapter` leaves `Presto.Adapters`. |
| `Draggable` | `X`, `Y`, `ProgressX`, `ProgressY` |
| `Draggable.Coords` | `X`, `Y` |
| `ContentSizeTransition.Inset` | `Right`, `Bottom` |
| `Selection.Edges` | `Left`, `Top`, `Right`, `Bottom` |
| `FollowerHolder<T>` | `Value`, with one static accessor per closed `T` |

`Timer` and `Draggable` are public, so users who animate `timer.CurrentTime` or `draggable.X` (both anime.js idioms) get reflection-free accessors as well. A user adapter registered in `Presto.Adapters` still wins over them (CORE-TARGETS-007). `Follower.Rebuild` replaces `MakeGenericType` with a `switch` that `new`s each `FollowerHolder<T>` over the closed channel set plus `double`, `float`, `int` and `IBrush`.

In Debug builds, the reflection path checks that the type it is about to reflect on does not come from one of the four library assemblies, and throws if it does. The check matches assembly names exactly, so the test assemblies are not caught. Because the suites run in Debug, any new internal target that slips through fails the first test that animates it.

**Plain user objects opt in with `Presto.RegisterTarget<T>()`.**

```csharp
Presto.RegisterTarget<Ship>();   // once, before the first animation; keeps Ship's public properties and fields
Presto.Animate(ship, new AnimationParams { ["X"] = 320 });
```

The generic parameter is annotated `[DynamicallyAccessedMembers(PublicProperties | PublicFields)]`, and that annotation is the contract: ILLink and ILC keep the type's public properties and fields, and their metadata. At run time the call records nothing. `Presto.Animate(object, …)` keeps its signature and carries no `[RequiresUnreferencedCode]`. `ObjectTargetAdapter`'s reflection gets one `[UnconditionalSuppressMessage]`, whose justification names `RegisterTarget<T>` and the Debug check. The unknown-member `ArgumentException` adds: "In a trimmed or NativeAOT app, call `Presto.RegisterTarget<T>()` for this type or register an `ITargetAdapter`." Under NativeAOT, the adapter's expression trees run interpreted. The experiment showed that is correct, but it is slower and may allocate per frame. A plain object that needs allocation-free AOT frames registers an `ITargetAdapter`, which stays the zero-reflection route.

**Analyzer sites.** These carry over from release-readiness, corrected:
- `MotionObjectValue.FindCodec` scans the interface keys of `Presto.Codecs` with `IsAssignableFrom` instead of calling `GetInterfaces()`.
- `Follower.Convert` passes values already of `_type` through and converts numeric `IConvertible` values with `Convert.ChangeType`. `TypeUtilities.TryConvert` stays for the rest, under a justified suppression.
- `Follow.BindCell` uses a path-less `CompiledBinding` over the source, or `FollowBinding` takes the motion value directly.
- `Distort` reaches `CompositionDrawListVisual.DrawList` and `Compositor.AfterCommit` through `[UnsafeAccessor]`, naming the internal types with `[UnsafeAccessorType]`, so a renamed member throws `MissingMethodException` instead of switching redraw tracking off.
- `GlyphContours.PathOf` reads `FillPath` through `[UnsafeAccessor(Method, Name = "get_FillPath")]` on `[UnsafeAccessorType("Avalonia.Skia.GeometryImpl, Avalonia.Skia")]`, the abstract base, which dispatches to its 7 overrides. This was verified under AOT. The experiment's leftover `Unused()` method goes.
- `ReorderList.MoverOf` calls the closed type's own `ObservableCollection<T>.Move(int, int)`, found once per runtime type and rooted by `[DynamicDependency]`.
- `RootInspector.AppFrame` uses `DiagnosticMethodInfo.Create(frame)`, which is AOT-safe and returns null when a frame has no metadata. No suppression is needed.
- `TickableList` replaces its 8 `new Binding(string)` cells with compiled bindings (`CompiledBindingPathBuilder`) or with writes from `TickableRow.PropertyChanged`, whichever is smaller.
- `LottieComposition.FromDotLottie` writes the animation node with `JsonNode.WriteTo(Utf8JsonWriter)` into an `ArrayBufferWriter<byte>`, with no `JsonSerializer`.

**Flags.** Once the fixes above are in, the four libraries set `IsTrimmable` and `IsAotCompatible`, and the IL2xxx/IL3xxx ids they report go into `WarningsAsErrors`.

**Build plumbing.** `PrestoGallery.SnippetGenerator.csproj` takes `TreatAsLocalProperty="PublishAot;PublishTrimmed"` and sets both to `false`, so a command-line publish of a head works. The gallery's Desktop head and Tempo log to `Console.Error` at `Warning` when built in Release, so a redirected NativeAOT run reports binding and visual errors. Debug keeps `LogToTrace`.

**Regression guard.** A new console app, `tests/MonacoLabs.Presto.AotProbe`, sets `PublishAot` in its own csproj. It runs Avalonia.Headless with Skia and exercises every path above: an animatable `Set`, `Timeline.Sync` on a timer and on an animation, a compositor animation and a layout transition, `Pager` travel, a draggable's `X` and its overshoot and auto-scroll animations, a `ContentSizeTransition`, a selection glide, a `Follow` on each channel type, a `Distort` redraw subscription, `GlyphShards.Of`, a reorder move on an `ObservableCollection<int>`, `FromDotLottie` on an embedded `.lottie`, a DevTools tickable row, and a user type registered with `RegisterTarget<T>()`. It prints one `PASS` or `FAIL` line per case and exits with the number of failures. CI gains an `aot` job on `windows-latest`. It publishes the probe in Release with `TrimmerSingleWarn=false` and IL warnings as errors, then runs it. The gallery smoke script (every slug for 6 s, stderr checked for exceptions and binding errors) is committed as `samples/PrestoGallery/aot-smoke.ps1`. It runs in a `workflow_dispatch` and weekly CI job that AOT-publishes the gallery, and by hand before a release.

**Docs and README.**
- README "Platforms and status": the Browser row drops "with no trim warnings", which came from a build whose libraries were not trimmable, if release-readiness has not already dropped it. Once this change's Browser publish repeats the check with trimmable libraries, the row says so.
- README "Platforms and status": the Desktop row adds that a Windows x64 NativeAOT publish runs the probe in CI and passed the gallery smoke run.
- A new README section, "Trimming and NativeAOT", covers `RegisterTarget<T>()`, the `CompositorScene` classes that need it, `ITargetAdapter` for allocation-free frames, and the published sizes: a NativeAOT gallery is 53 MB and does not shrink against a trimmed self-contained build, 53.2 MB; it buys startup and no JIT.
- README "A plain object" quick start gains one line pointing there.

What does not change: `Presto.Animate`'s signature, `Presto.Adapters` defaults (CORE-TARGETS-049: still empty in the core), what any property write does under JIT, and package ids and target frameworks.

## Decisions

- **Internal targets declare typed member tables; reflection is for user types only.** Each library type the library animates by name implements the internal `IMemberTarget`, whose static accessors share the reflection path's write rules through typed delegates. Reason: it removes the reflection dependency instead of rooting it. It is checked at compile time, frames cost what compiled expressions cost (a delegate call, no boxing for numbers), the core stays single-threaded and lock-free (the tables are static and immutable), and `Presto.Adapters` stays empty in the core. Rejected:
  - `[DynamicDependency]` on each internal type, the experiment's stopgap. A new internal target regresses silently, as `Draggable`, `Inset` and `Edges` did between the experiment's rounds 2 and 3, and the targets would still run on interpreted expression trees under AOT.
  - A registered `ITargetAdapter` per type. Each one adds a check to every resolution of every target, and the core's own types would need default registrations, against CORE-TARGETS-049.
  - A source generator. It adds an analyzer package to ship, with Roslyn pinned no newer than the SDK's compiler, for nine types written by hand once. Reconsider only if a typed public registration is ever wanted.
  - Dictionary targets for the small ones. A dictionary write boxes the number every frame (CORE-TARGETS allocation), and `CallbacksTarget` runs on every animatable frame.
- **Member tables are closed.** A name a table lacks throws instead of falling back to reflection. Reason: a fallback would put the library's own types back on the path the Debug check forbids, and would work under JIT while failing under AOT. Before this change, reflection also reached `Timer.Reversed`, `Timer.Canceled` and the callback properties as discrete values. That was never specified and is dropped.
- **Library types reaching reflection fail Debug runs.** The check matches the four library assembly names exactly, and lives in Debug only, like the engine's `[Conditional("DEBUG")]` assertions. Reason: the suites run in Debug (release-readiness), so a regression fails the first test that animates the new target, with no AOT build. Rejected: a name-prefix match, which catches the test assemblies; relying on the AOT probe alone, which covers only the paths it drives.
- **Plain objects opt in through `Presto.RegisterTarget<T>()`, and nothing on the `Animate` path is `[RequiresUnreferencedCode]`.** Reason: a `[DynamicallyAccessedMembers]` generic parameter is the one annotation both ILLink and ILC honor for a value whose static type is `object`. `Animate(object, …)` mostly receives Avalonia controls, which never reach reflection, so `[RequiresUnreferencedCode]` there would warn on every call to protect a minority path. It cannot sit on `ObjectTargetAdapter.Resolve` either, because an annotation that `ITargetAdapter.Resolve` lacks is a mismatch (IL2046). Rejected:
  - A type-level `[DynamicallyAccessedMembers]`, release-readiness's plan, which was verified ignored.
  - Generic `Animate<T>` overloads. Overload resolution would route controls and arrays through them, and `Set`, `Get`, `Remove`, `CreateAnimatable` and `Timeline.Add` would each need a generic twin.
  - Both, meaning registration plus `[RequiresUnreferencedCode]` on `Animate`, for the warning noise above.
- **`RegisterTarget<T>()` is annotation only.** It records nothing and builds no accessors. Reason: the annotation is what keeps the members, and eager accessors would compile delegates for members nobody animates. The cost is a public method whose body is empty, which its doc comment explains.
- **Under NativeAOT, plain objects keep interpreted expression trees.** Reason: the experiment showed them correct, and the library's own targets no longer use them. Rejected: release-readiness's separate `PropertyInfo` get/set path when dynamic code is unsupported. It boxes too, and adds a second path to test. Allocation-free AOT frames for a user type come from an `ITargetAdapter`.
- **Avalonia internals are reached only through `[UnsafeAccessor]`.** This decision moved here from release-readiness. Reason: the trimmer and AOT understand accessors, they cost no `Invoke` allocation, and a renamed member throws on first use instead of silently degrading. That is the same fail-loud stance the test setup takes on `CompositorClock`. Rejected: keeping reflection with `DynamicDependency` on internal Avalonia types, which still degrades silently, as `Distort` does today.
- **The flags land last, in this change, and do not ship before it.** `IsTrimmable` and `IsAotCompatible` are set only after every site above is fixed. Reason: setting `IsTrimmable` without the runtime fixes breaks every animatable in the trimmed Browser app, and the analyzer cannot see those sites. preview.1 waits for this change and ships with both flags set.
- **Warnings as errors follow the first `IsAotCompatible` build.** This decision moved here from release-readiness. If the libraries build close to warning-free, they set `TreatWarningsAsErrors`. Otherwise only the reported IL ids go into `WarningsAsErrors`, widened later.
- **The regression guard is a probe app in CI, and the gallery smoke run is scheduled.** The probe publishes in a few minutes and drives each hazard by code, input-dependent ones included, with no display. Reason: a load-only gallery run cannot drag, reorder or load a `.lottie`, and takes over ten minutes with the gallery's ILC time. It still catches what nobody listed, so it runs weekly and before releases. Rejected: the gallery smoke run on every push (slow, and blind to interaction); analyzer warnings alone (they missed 8 of the runtime sites).
- **preview.1 waits for this change and ships trimmable and AOT-compatible.** Reason: NativeAOT is a supported target from the first preview, so the internal targets are fixed once rather than shipped broken and patched; the AOT probe's CI job blocks a merge that regresses it. Rejected: shipping preview.1 untrimmable and landing this for preview.2.

## Spec changes

**CORE-TARGETS** (`docs/specs/core/targets.md`):
- Added before CORE-TARGETS-020: "When the target is a `Timer`, the adapter shall reach `CurrentTime`, `IterationCurrentTime`, `Progress`, `IterationProgress` and `CurrentIteration` without reflection, and throw `ArgumentException` for any other name." Tests: `ObjectTargetAdapterTests.TimerMembersResolveWithoutReflection`.
- Added: "A library type the library animates by name shall be resolved through its own member table, with the same writes CORE-TARGETS-022 to -030 give a reflected member." Tests: `ObjectTargetAdapterTests.TypedMembersWriteLikeReflectedOnes`.
- Added: "`Presto.RegisterTarget<T>()` shall keep `T`'s public properties and fields for the adapter in trimmed and NativeAOT apps, and shall change nothing at run time." Tests: `ObjectTargetAdapterTests.RegisteringATargetChangesNoResolution`. The trimmed and AOT half is covered by the AOT probe, noted under Open items.
- CORE-TARGETS-021 changed to: "When no such member exists, the adapter shall throw `ArgumentException` whose message names trimming and NativeAOT and `Presto.RegisterTarget<T>()`."
- Edge cases, added: "In a trimmed or NativeAOT app, a plain type not passed to `RegisterTarget<T>()` may have no reachable members, even ones the app uses, and throws CORE-TARGETS-021."
- Edge cases, added: "Under NativeAOT a reflected member's expression trees run interpreted: correct, slower, and possibly allocating per frame."
- Purpose: also specifies `Presto.RegisterTarget`.
- Decisions: the decisions on member tables, closed tables, the Debug check, `RegisterTarget<T>()`, annotation only and interpreted expression trees, as above.
- Threading and lifetime: the member tables are static and immutable. `RegisterTarget<T>()` may be called from any thread.

**CORE-ANIMATABLE**: no requirement changes. The callbacks target is a typed member table, so `AllocationTests` keeps pinning its frames.

**CORE-TIMELINE**: no requirement changes. `Sync` resolves `CurrentTime` through `Timer`'s table.

**CORE-MOTIONVALUE-013** changed to: "The outputs shall be decoded once, through the `Presto.Codecs` entry found by exact type, then base type, then the first registered interface type the output implements, or through the core value parser for strings."

**BRIDGE-DRAGGABLE**: added "A `Draggable`'s `X`, `Y`, `ProgressX` and `ProgressY` shall be animatable by name without reflection, and any other name shall throw `ArgumentException`." Tests: `DraggableTests.PositionAnimatesByNameThroughItsTable`.

**BRIDGE-THREAD**: `CompositorScratch` resolves its seven properties through its member table, and `CompositorScratch.Adapter` is no longer registered. Requirements that cite the adapter are reworded. Behavior is unchanged: `Rotate` still converts units.

**BRIDGE-FOLLOW**: no requirement changes if the `Convert` fast path keeps today's results. If the analyzer forces `TryConvert` out entirely, add a requirement stating which conversions the follower performs.

**BRIDGE-REORDER-021** changed to: "When no command is set and `ItemsSource` is (or derives from) an `ObservableCollection<T>`, each uncanceled step shall call that collection's `Move(old, new)`, found once per runtime type; any other items source refuses the step." The Allocation line "The mover delegate is created once per items-source type by reflection" becomes "Each step's `Move` call allocates its argument array."

**BRIDGE-DISTORT**:
- The two Decisions that say "read by reflection" and "subscribes, by reflection" say "through `[UnsafeAccessor]`".
- Edge cases: "When Avalonia no longer has `CompositionDrawListVisual.DrawList` or `Compositor.AfterCommit`, the first active effect throws `MissingMethodException`."

**BRIDGE-GLYPHS**:
- The decision "Outlines are read through SkiaSharp" adds that a geometry's `SKPath` is read from Avalonia.Skia's `GeometryImpl` through `[UnsafeAccessor]`.
- Edge cases: "When Avalonia.Skia no longer has `GeometryImpl.FillPath`, the first outline read throws `MissingMethodException`."

**BRIDGE-SCENES**: Edge cases: "A plain object a scene animates by name is reached by reflection; in a trimmed or NativeAOT app its type is passed to `Presto.RegisterTarget<T>()` (CORE-TARGETS)."

**LOTTIE-PLAYBACK**: added "`FromDotLottie` shall read an archive with reflection-based JSON serialization disabled." Tests: untested, with an Open item saying the AOT probe covers it. The switch is process-wide and read once, so an in-suite test cannot turn it off reliably.

**DEVTOOLS-OVERLAY**: Edge cases: "In a trimmed or AOT app a frame without method metadata is skipped, so the `created at` line may be missing (DEVTOOLS-OVERLAY-039)." The tickable list's cells are not reflection bindings. This is a Decision only, with no requirement.

**ARCHITECTURE.md**:
- §3 replaces "The library itself keeps no reflection" with: the library reaches Avalonia internals only through `[UnsafeAccessor]`, animates its own types through typed member tables (checked in Debug), and keeps reflection only in the plain-object adapter and the `ObservableCollection<T>.Move` lookup. §3 also describes the AOT probe and the scheduled gallery smoke run.
- §4 gains the `UnsafeAccessor`, `RegisterTarget<T>()` and warnings-as-errors decisions.

**CLAUDE.md**:
- Commands:
  - The probe publish and run commands. A head publishes with `-p:PublishAot=true`.
  - A note for when ILC finds no platform linker: point `PATH` and `LIB` at an installed MSVC toolset and Windows SDK, and pass `-p:IlcUseEnvironmentalTools=true`.
- Code conventions: "Libraries stay trim- and AOT-clean: no new reflection. A library type animated by name implements `IMemberTarget`. Reach Avalonia internals with `[UnsafeAccessor]`."

## Tasks

1. **Build plumbing.** Add `TreatAsLocalProperty` to the snippet generator and the Release-only `Console.Error` logging to the two `Program.cs` files. Verify: `dotnet publish samples/PrestoGallery/PrestoGallery.Desktop -c Release -r win-x64 -p:PublishAot=true` gets past restore. The full suite is green.
2. **Typed member tables (core).** Split the member accessor into reflection discovery and typed write rules. Add `IMemberTarget`, implemented by `CallbacksTarget` and `Timer`, and the Debug reflection check. Tests:
   - New `ObjectTargetAdapterTests.TypedMembersWriteLikeReflectedOnes`, covering number, integral, string, color, object, codec and discrete writes through both routes.
   - New `ObjectTargetAdapterTests.TimerMembersResolveWithoutReflection`, which also shows an unknown timer member throws.
   - New `ObjectTargetAdapterTests.ReflectingOnALibraryTypeFailsInDebug`, for a library type with a public writable member that has no table.
   - `AnimatablesTests`, `TimelinesTests` and `AllocationTests` pass unchanged.
3. **Typed member tables (bridge).**
   - `CompositorScratch` implements the table, and its adapter is unregistered.
   - `Draggable`, `Draggable.Coords`, `ContentSizeTransition.Inset`, `Selection.Edges` and `FollowerHolder<T>` implement it too, and `Follower.Rebuild` gets its `switch`.
   - `Draggable.cs` now carries the drop-zone hit testing and `Reparent` (BRIDGE-DRAGGABLE drop zones); keep them on the typed path.

   Tests: the full bridge, gallery and Tempo suites pass with the Debug check on. That check is what proves no internal target was missed. New `DraggableTests.PositionAnimatesByNameThroughItsTable`. `BridgeAllocationTests` passes unchanged.
4. **Plain-object opt-in.** Add `Presto.RegisterTarget<T>()`, the suppression and its justification, and the new exception text. Tests: new `ObjectTargetAdapterTests.RegisteringATargetChangesNoResolution`, and `ObjectTargetAdapterTests.UnknownMemberThrows` asserts the new message.
5. **Analyzer sites.** `FindCodec`, `Follower.Convert`, `Follow.BindCell`, `Distort`, `GlyphContours`, `ReorderList`, `RootInspector`, `TickableList` and `FromDotLottie`, as listed in What. Tests:
   - `DistortTests`, the glyph-outline tests, `ReorderTests` and the Follow tests pass unchanged.
   - New `FollowBindingTests.EveryChannelTypeEasesThroughItsHolder`, if no existing test covers each channel type.
6. **Flags.** Set `IsTrimmable` and `IsAotCompatible` on the four libraries and put their IL ids in `WarningsAsErrors`. Verify: the libraries build in Release with no IL2xxx/IL3xxx warnings, and the full suite is green.
7. **Samples.**
   - GlyphLab reads `PlatformImpl` and `FillPath` through its own `[UnsafeAccessor]`s, as the bridge does.
   - LifecyclePage's binding becomes compiled.
   - Tempo's `ReflectionBinding` becomes a compiled binding or a direct subscription.
   - `OrbitScene`, `ParticleFieldScene` and any other scene with a plain-object target call `Presto.RegisterTarget<T>()` for it, so the samples show the pattern.
   - Verify: the gallery and Tempo publish with NativeAOT with zero IL warnings, and `PrestoGallery.Tests` and `Tempo.Tests` are green.
8. **AOT probe.** Add `tests/MonacoLabs.Presto.AotProbe` with the cases in What. If Avalonia.Headless does not run under NativeAOT, the probe opens an off-screen desktop window instead, since `windows-latest` has a desktop session. Verify:
   - The probe publishes with NativeAOT and passes every case.
   - Each case fails when its fix is reverted locally, checked for `CallbacksTarget`, `FromDotLottie` and `RegisterTarget<T>`.
9. **CI.** Add the `aot` job to `.github/workflows/ci.yml`, which release-readiness creates; if this change lands first, it creates the file with this job alone. Add the scheduled and `workflow_dispatch` gallery smoke job, and commit `aot-smoke.ps1`. Verify on GitHub: the `aot` job goes green on a push, and goes red on a branch that removes `CallbacksTarget`'s table.
10. **Platform runs.**
    - The gallery smoke run under NativeAOT shows no exception or binding error on any page. Tempo starts with and without `--skip-intro` and its detail drawer opens.
    - A trimmed Release publish of `PrestoGallery.Browser` with `TrimmerSingleWarn=false` shows no warnings from Presto assemblies.
    - In Chrome, the Animatable, Carousel, Compositor, Follow, Typography, Distort (child redraws update, not stale) and text-morph pages behave as on Desktop.
11. **Docs.** Make the README, ARCHITECTURE, spec and CLAUDE.md edits listed above. Verify: `SpecTests` and `SpecCoverageTests` pass, and every README trimming or AOT claim traces to a run in task 10 or the probe.

## Open questions

None.
