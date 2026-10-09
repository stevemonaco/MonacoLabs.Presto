# Typed property keys and timeline labels

## Why

Two everyday spellings still rely on bare strings:

- **Property keys.** The indexer (`["X"] = 100`), `Property("Draw", …)`, `Animatable.Get("X")`, `Presto.Get`/`Remove` and `Timeline.Remove` take property names as strings. The typed builder methods (`.X(100)`) cover the most-used shorthands, but every other key (`"Warp"`, `"WarpOrigin"`, `"ClipInset"` in an indexer, a shorthand passed to `Animatable`) is a typo away from a run-time `ArgumentException`, and nothing lists the shorthands at the call site. The samples have 282 string-keyed indexer entries.
- **Timeline labels and positions.** A label is named once in `.Label("feel", 0)` and then repeated inside position strings (`"feel"`, `"feel+=1250"`, `"feel+=1750"`); offsets are string arithmetic, and the previous-child anchors are `"<"`/`"<<"` sigils. The kinetic poem sample (`KineticPoemPage`) repeats 7 label names across 24 position strings.

An earlier attempt, a `Shorthands` class of string constants, was built and withdrawn (`bc0f00f`): it typed only the indexer and added a third spelling beside strings and builder methods. This change instead introduces one key type that every key-taking API accepts.

## What

**`PropertyKey` (core).** A small value type wrapping a property name, with an implicit conversion from `string`. It replaces `string` as the property-name parameter on every public core API that names a property: the `AnimationParams`, `Keyframe` and `AnimatableParams` indexers, the four `PropertyBuilder.Property` overloads, `Animatable.Get`/`GetChannels`/`ValueOf`/`Set`/`Fling`, `Presto.Get`/`Remove`/`GetAnimations` and `Timeline.Remove`. Because the conversion is implicit, every existing call (`["X"] = 100`, `.Property("Opacity", 1)`) compiles unchanged. A key behaves exactly as its name: params snapshot, composition, canonicalization and removal see the same string. The adapter interfaces (`ITargetAdapter`, `IPropertyAccessor`) keep `string`; keys are unwrapped at the public boundary.

**`PrestoKeys` (bridge).** A static class with one `PropertyKey` per bridge shorthand and pseudo-property: transforms (`X`, `Y`, `Rotate`, `Scale`, `ScaleX`, `ScaleY`, `SkewX`, `SkewY`, `RotateX`, `RotateY`, `Perspective`), effects (`Blur`, `ShadowX`, …), reveals (`ClipInset`, `ClipCircle`, `MaskSweep`, …), warp (`Warp`, `WarpPhase`, `WarpOrigin`, `WarpRadius`), `Draw`, `Text`, `Data`, `ScrollX` and `ScrollY`. `PrestoKeys.Of(AvaloniaProperty)` returns the encoded key of a registered property, for places that hold keys as values (a list of keys, a `Recolor` argument). The typed builder methods write their key through `PrestoKeys`, so the builder, the indexer and `Property(...)` share one source of truth. With `using static Monaco.Presto.AvaloniaBridge.PrestoKeys;` an indexer reads `[X] = 100`. The bridge's string-keyed APIs (`CompositorAnimationParams` indexer, `PrestoAvalonia.Get`/`Remove`, `Recipes.Recolor`/`Tints.Recolor`) take `PropertyKey` too.

**`TimelineLabel` and typed positions (core).** `TimelineLabel` is a named handle to a label. `Timeline.Label(name, out TimelineLabel label, position)` places a label and hands back its handle in the same chain, and `Timeline.Label(TimelineLabel, position)` places a handle declared earlier. `timeline.Seek(label)` seeks to the label's time. A label converts implicitly to a `TimelinePosition`, and `label + ms` / `label - ms` give the relative forms. `TimelinePosition` gains `End`, `PreviousEnd` and `PreviousStart` (the typed `"+=0"`, `"<"` and `"<<"`), plus `+`/`-` with a number. Each typed form resolves exactly as its string twin. The kinetic poem's chain becomes:

```csharp
.Label("feel", out var atFeel, 0)
.Add(feel, b => b.Draw(from: "0 0", to: "0 1").Duration(1.5 * Beat) …, atFeel)
.Add(feel, b => b.Property(Shape.FillProperty, TweenValue.Of(Feel.Foreground)) …, atFeel + 1250)
.Add(feel, Recipes.FadeOut(), atFeel + 1750)
```

**What does not change.** Strings still work everywhere, with their anime.js forms (`"label+=100"`, `"*=2"`, `"<<-=50"`), so ported upstream tests and the `Pages/` mirrors of the anime.js docs stay as they are. The builder's typed methods remain the documented first choice; `PrestoKeys` is the typed spelling wherever a key is passed as a value. No tick-path code changes.

## Decisions

- **One key type for every key-taking API.** `PropertyKey` replaces `string` (not an overload beside it) on every public API that names a property, with an implicit conversion from `string`. Reason: the withdrawn constants typed only the indexer and became a third spelling; a key type accepted everywhere is the same spelling in every position, and replacing rather than overloading keeps one indexer. Rejected: string constants again (no type at the parameter, no place to hang `Of(AvaloniaProperty)`); a second indexer overload beside `this[string]` (the objection recorded in CORE-BUILDER); an analyzer (could check only shorthands).
- **The core defines the key type, the bridge defines the keys.** `PropertyKey` lives in `Monaco.Presto`; `PrestoKeys` lives in the bridge. Reason: shorthands are bridge concepts ("Core names only what core knows", CORE-BUILDER), but the parameter type must exist in the core where the indexers live.
- **`PrestoKeys` names shorthands and pseudo-properties, not Avalonia properties.** Real properties are keyed by their `AvaloniaProperty` field (`[Visual.OpacityProperty]`, `Property(Shape.FillProperty, …)`), or `PrestoKeys.Of(...)` when a key value is needed. Reason: the property fields are already typed and refactor-safe; mirroring them would add a second typed spelling. Rejected: `PrestoKeys.Opacity` and friends.
- **No implicit `AvaloniaProperty` → `PropertyKey` conversion.** The bridge keeps its `this[AvaloniaProperty]` indexers and `Property(AvaloniaProperty, …)` extensions. Reason: a conversion operator must be declared on one of the two types, and the core cannot see `AvaloniaProperty`; it becomes possible only if the assemblies merge.
- **A key is its name.** Keys compare by ordinal name, and every API unwraps the name before canonicalization, so `PrestoKeys.X`, `"X"` and `new PropertyKey("X")` are indistinguishable to the engine. Reason: tweens are keyed by canonical name (CORE-TARGETS); a key that carried more would need a second composition path.
- **Labels are handles, positions stay one type.** `TimelineLabel` is its own type with a `Name` and implicit conversion to `TimelinePosition`; arithmetic yields a `TimelinePosition`. Reason: a handle reads as a label at call sites, can be stored and seeked to (`timeline.Seek(atFeel)`) and passed as a stagger start (`Start = atFeel.Name`), while `Add` keeps one position parameter type. Rejected: making a label a bare `TimelinePosition` (loses the name); a `CallerArgumentExpression`-named label (unreliable on `out var`).
- **The `out` overload places and names a label in one call.** `Label(name, out label, position)` keeps the fluent chain unbroken; the declared-first `Label(TimelineLabel, position)` serves labels stored as fields. There is no implicit `string` → `TimelineLabel` conversion. Reason: it would make `Label("x", 0)` ambiguous between the two overloads.
- **Typed positions resolve exactly as their string twins.** `label + n` is `"label+=n"`, `PreviousEnd - n` is `"<-=n"`, `End + n` is `"+=n"`, and a typed label not on the timeline falls back to the timeline's end like an unknown string label (CORE-TIMELINE-016). Successive `+`/`-` fold into one offset. Reason: one resolution rule; the `out` overload makes an unplaced label rare. Rejected: throwing for an unplaced typed label (a second rule for the same lookup).
- **`Seek(TimelineLabel)` seeks to the label's raw time and throws for an unknown label.** It is `Seek(Labels[label.Name], muteCallbacks)` returning the timeline, and a label not on the timeline throws `KeyNotFoundException`, as the `Labels` lookup it replaces does. Reason: seeking to a section is what labels are kept for (the kinetic poem's seek buttons), and a seek has no upstream end-fallback to stay consistent with; silently seeking to the end would hide the mistake. Rejected: `TimeOf(TimelinePosition)` (resolving relative anchors like `PreviousEnd` at seek time has no clear meaning once children are added); seeking to the end for an unknown label.
- **No typed `*=`.** Multiplication stays string-only. Reason: rarely used, and it composes poorly with folded offsets (`(label + 100) * 2` has no string twin).

## Spec changes

**CORE-TARGETS** — add `PropertyKey` to `types` and `sources`.
- Add: The `PropertyKey` conversion from a string shall wrap that string as the key's name, and from `null` shall throw `ArgumentNullException`.
- Add: Two keys shall be equal when their names are ordinally equal.
- Add: A public API given a `PropertyKey` shall behave exactly as it did given the key's name: resolution, canonicalization, composition and removal see the same string.
- Add: When an API is given `default(PropertyKey)`, it shall throw `ArgumentException`.

**CORE-ANIMATION** — the indexer takes `PropertyKey`. Decision "Params are init-only records with a string indexer" reworded to "a key indexer", naming CORE-TARGETS for the key type.

**CORE-KEYFRAMES**, **CORE-ANIMATABLE**, **CORE-PRESTO** — the `Keyframe` and `AnimatableParams` indexers, the `Animatable` methods and `Presto.Get`/`Remove`/`GetAnimations` take `PropertyKey`; requirements that name a property "by name" now say "by key". No behavior change.

**CORE-BUILDER** — the `Property` overloads take `PropertyKey`.
- Change CORE-BUILDER-002: `Property(key, to)` shall store `to` as the bare value the indexer `[key] = to` stores, for every value kind.
- Decision "The builder is the documented, typed spelling; the indexer stays beside it": strike "a key struct (needs a second indexer overload)" from Rejected and add that indexer keys are typed by `PropertyKey` (CORE-TARGETS) with the bridge's `PrestoKeys`; the constants entry stays rejected, with the reason it was withdrawn.
- Non-goal "Typed property names in the core" stays: the core gains a key type, not names.

**CORE-TIMELINE** — add `TimelineLabel` to `types`, `Monaco.Presto/TimelineLabel.cs` to `sources`.
- Add: `Label(name, out label, position)` shall store the label as `Label(name, position)` does and give a `TimelineLabel` with that name.
- Add: `Label(label, position)` shall store the label under `label.Name` as `Label(name, position)` does.
- Add: A `TimelineLabel` position shall resolve as its name does, and `label + n` / `label - n` as `"name+=n"` / `"name-=n"`.
- Add: `TimelinePosition.End`, `PreviousEnd` and `PreviousStart` shall resolve as the default position, `"<"` and `"<<"`, and `+ n` / `- n` on them as the relative operator after that anchor.
- Add: `+` and `-` applied to an absolute position shall shift it, and applied again to a typed position shall fold into one offset.
- Add: `Seek(label, muteCallbacks)` shall seek the timeline to the time stored under `label.Name` and return the timeline; when no label of that name exists, it shall throw `KeyNotFoundException`.
- CORE-TIMELINE-016 (unknown label): extend to typed label positions.
- Decision added: "Labels are handles, positions stay one type", "Typed positions resolve exactly as their string twins", "`Seek(TimelineLabel)` seeks to the label's raw time and throws for an unknown label", "No typed `*=`" (from above).

**BRIDGE-HELPERS** — add `PrestoKeys` to `types`, `Monaco.Presto.AvaloniaBridge/PrestoKeys.cs` to `sources`.
- Add: `PrestoKeys` shall hold one key for every shorthand and pseudo-property `AvaloniaObjectTargetAdapter` resolves before the property registry, and nothing else.
- Add: Each `PrestoKeys` key shall resolve on a target of the type it applies to, and canonicalize to its own name.
- Add: `PrestoKeys.Of(property)` shall return the key BRIDGE-TARGETS-007 encodes.
- Change BRIDGE-HELPERS-011: each typed builder method shall write its `PrestoKeys` key.
- Strike non-goal "No key constants, key struct or analyzer for indexer keys"; replace with "No analyzer for indexer keys".
- Open item "Phase 71 asked for a test that fails when a shorthand is added without a typed builder method": resolved in part by the `PrestoKeys` completeness test (a shorthand without a key fails); a shorthand without a builder method is still unchecked.
- Decision "Bridge builder extensions self-initialize": `PrestoKeys` access does not install the bridge (a key is only a name; the API it is passed to installs it).

**BRIDGE-TARGETS** — strike non-goal "No typed key constants for shorthands"; the `CompositorAnimationParams` indexer and `PrestoAvalonia.Get`/`Remove` take `PropertyKey`. BRIDGE-TARGETS-007 adds `PrestoKeys.Of` to the list of places that encode a typed property.

**BRIDGE-RECIPES** and the spec owning `Tints` (`recipes-media.md`) — `Recolor`'s property parameter becomes `PropertyKey?`, `null` meaning `Background`, since a struct parameter cannot default to a string constant.

## Tasks

1. **`PropertyKey`.** Add the type; switch the core indexers, `PropertyBuilder.Property`, `Animatable`, `Presto.Get`/`Remove`/`GetAnimations` and `Timeline.Remove` to it. Tests: `PropertyKeyTests` (conversion, null and default throw, ordinal equality); one cross-API test that a key and its string give equal params (`AnimationBuilderTests`). The existing suite must compile and pass unchanged.
2. **`PrestoKeys`.** Add the class and `Of`; route the typed builder methods, `CompositorAnimationParams`, `PrestoAvalonia.Get`/`Remove` and `Recolor` through `PropertyKey`. Tests: `PrestoKeysTests` — completeness both ways against the adapter's shorthand tables (transform, effect, reveal, warp, draw, scroll), each key resolves and canonicalizes to itself, `Of` matches `Encode`; `AnimationBuilderBridgeTests.TypedMethodsWriteTheIndexerKeys` asserts against `PrestoKeys`.
3. **`TimelineLabel` and typed positions.** Add the type, the two `Label` overloads, `Seek(TimelineLabel)`, `End`/`PreviousEnd`/`PreviousStart` and the operators; keep the string parse path untouched. Tests: a `TimelinePositionTests` theory laying out each typed form beside its string twin (label, label ± n, `End` ± n, `PreviousEnd`/`PreviousStart` ± n, folded offsets, absolute ± n, unknown typed label), the default position unchanged, and `TimelineTests` for `Seek(label)` matching `Seek(Labels[name])` (including under `muteCallbacks`) and throwing for an unknown label.
4. **Samples.** Rewrite the `KineticPoemPage` timeline with label handles, `Draw(from:, to:)`, `Property(Shape.FillProperty, …)`, `Property(Shape.StrokeThicknessProperty, …)` and `PrestoKeys.Warp`, and its seek buttons to `Seek(label)` (keeping labels as fields); move string shorthand keys in `Showcase/` and `Builder/` pages to builder methods or `PrestoKeys` (the `Pages/` anime.js mirrors keep strings). `PrestoSample.Tests` must pass (snippets regenerate).
5. **Visual check.** Desktop sample, `--page` kinetic poem: the timeline plays as before and every label seek button lands on its section; spot-check two migrated Showcase pages.
6. **Docs.** Update the README's indexer and timeline examples to show `PrestoKeys` and label handles beside the builder.

## Open questions

None.
