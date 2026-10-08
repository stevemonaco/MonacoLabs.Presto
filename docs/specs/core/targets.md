---
id: CORE-TARGETS
title: Targets, adapters and codecs
package: Monaco.Presto
namespaces:
  - Monaco.Presto
  - Monaco.Presto.Targets
  - Monaco.Presto.Values
sources:
  - Monaco.Presto/Targets
  - Monaco.Presto/Values/DiscreteCodec.cs
  - Monaco.Presto/DiscreteRule.cs
  - Monaco.Presto/Presto.cs
  - Monaco.Presto/Animation.cs
types:
  - ITargetAdapter
  - IPropertyAccessor
  - IValueCodec
  - IReconcilingValueCodec
  - ObjectTargetAdapter
  - TargetParser
  - DiscreteCodec
  - DiscreteRule
tests:
  - TargetsTests
  - ObjectTargetAdapterTests
  - DiscreteValuesTests
  - CodecTests
  - PropertyNamingTests
upstream:
  - src/core/targets.js
  - tests/suites/targets.test.js
depends:
  - CORE-VALUES
---

# Targets, adapters and codecs

## Purpose

The core animates any object through small adapter interfaces: an `ITargetAdapter` resolves a property name on a target to an `IPropertyAccessor` that reads, writes and reverts it, and an optional `IValueCodec` turns a typed property value into tweenable channels and back. Adapters registered in `Presto.Adapters` are asked first; `ObjectTargetAdapter` (dictionaries and reflection) handles the rest, with codecs from `Presto.Codecs` and `DiscreteCodec` for types nothing interpolates. `TargetParser` flattens the targets argument. CORE-VALUES parses through the codecs; CORE-ANIMATION builds tweens on the accessors; the bridge's adapter is specified in its own spec. Also specifies `Presto.Adapters` and `Presto.Codecs`.

## Requirements

### Target parsing

- **CORE-TARGETS-001** — When the targets argument is `null`, `TargetParser.Parse` shall return an empty list.
  - Tests: `TargetsTests.NullTargetsAreEmpty`, `TargetsTests.AnimatingNullHasNoTargets`
- **CORE-TARGETS-002** — When the targets argument is not an `IEnumerable<object>` (a single object, a string, a dictionary), `TargetParser.Parse` shall return it as the one target.
  - Tests: `TargetsTests.SingleObjectIsOneTarget`, `TargetsTests.StringIsOneTarget`, `TargetsTests.DictionaryIsOneTarget`
- **CORE-TARGETS-003** — `TargetParser.Parse` shall flatten every `IEnumerable<object>` recursively, in order.
  - Tests: `TargetsTests.NestedArraysAreFlattened`, `TargetsTests.AnimatingNestedArraysFlattensThem`
- **CORE-TARGETS-004** — `TargetParser.Parse` shall skip `null` entries.
  - Tests: `TargetsTests.NullEntriesAreSkipped`
- **CORE-TARGETS-005** — `TargetParser.Parse` shall keep a target that appears more than once (by reference) only at its first position, at any list size.
  - Tests: `TargetsTests.DuplicatesKeepTheirFirstPosition`, `TargetsTests.DuplicatesKeepTheirFirstPositionAtAnySize`
- **CORE-TARGETS-006** — The animation shall build one tween per parsed target and property, in parse order, and expose the parsed list as `Targets`.
  - Tests: `TargetsTests.AnimatingASingleObjectBuildsOneTween`, `TargetsTests.AnimatingAnArrayBuildsOneTweenPerTarget`

### Adapter resolution

- **CORE-TARGETS-007** — When resolving a property, the core shall ask the adapters in `Presto.Adapters` in order and use the first non-null accessor, falling back to `ObjectTargetAdapter`.
  - Tests: `ObjectTargetAdapterTests.RegisteredAdaptersResolveBeforeTheObjectFallback`
- **CORE-TARGETS-008** — The core shall key a tween by the first non-null `Canonicalize` result among the adapters, or by the name as given when none returns one, so every spelling of one property composes against the others.
  - Tests: `PropertyNamingTests.TheQualifiedSpellingSharesTheSameKey`, `PropertyNamingTests.ATypedPropertyOverridesATweenDeclaredByName`, `PropertyNamingTests.ANamedPropertyOverridesATweenDeclaredByHandle`
- **CORE-TARGETS-009** — The animation shall resolve the accessor with the canonical name, once per target and property when it builds its tweens.
  - Tests: untested
- **CORE-TARGETS-010** — `Presto.Remove` and `Presto.GetAnimations` shall match a named property by its canonical name.
  - Tests: `PropertyNamingTests.RemoveByNameCancelsATweenDeclaredByHandle`, `PropertyNamingTests.RemoveByHandleCancelsATweenDeclaredByName`
- **CORE-TARGETS-011** (inherited) — When `Presto.Remove` is called without a renderable, it shall call every adapter's `Removed` with the parsed targets and the property (null for all), after the tweens are removed.
  - Tests: `TargetsTests.RemovingEverythingTellsTheAdapters`
- **CORE-TARGETS-012** (inherited) — When `Presto.Remove` names a renderable, it shall not call `Removed`.
  - Tests: `TargetsTests.RemovingOneRenderableLeavesTheAdaptersAlone`
- **CORE-TARGETS-013** (inherited) — An adapter that does not implement `Canonicalize` or `Removed` shall canonicalize nothing and ignore removals.
  - Tests: `TargetsTests.RemovingEverythingTellsTheAdapters`

### Accessor contract

- **CORE-TARGETS-014** — When an accessor's `GetValue` returns `null`, the core shall take the current value as the number 0.
  - Tests: `ValuesTests.FalsyValuesDecomposeToZero`
- **CORE-TARGETS-015** (inherited) — The renderer shall call `SetValue` with the modified, interpolated number for number and unit tweens, and with NaN for color and complex tweens, whose channels are on the tween.
  - Tests: `ObjectTargetAdapterTests.WritesColorChannelsToColorAndStringMembers`, `ObjectTargetAdapterTests.WritesComplexValuesToStringMembers`
- **CORE-TARGETS-016** (inherited) — When an animation is reverted, the core shall call `Revert` with the value `GetValue` returned before the animation first wrote, unless that value was `null`.
  - Tests: `ObjectTargetAdapterTests.RevertRestoresTheValueReadBeforeTheAnimation`, `DiscreteValuesTests.RevertRestoresTheOriginalValue`
- **CORE-TARGETS-017** — When an accessor has a `Codec`, the core shall decompose the property's values through it (CORE-VALUES) and the accessor shall build each frame's typed value with `Encode`.
  - Tests: `ObjectTargetAdapterTests.RegisteredCodecDecodesAndEncodesItsType`

### ObjectTargetAdapter

- **CORE-TARGETS-018** — When the target is an `IDictionary<string, object?>`, the adapter shall read a missing key as `null`.
  - Tests: `ObjectTargetAdapterTests.ReadsMembersAndTreatsMissingDictionaryKeysAsNull`
- **CORE-TARGETS-019** — When the target is an `IDictionary<string, object?>`, the adapter shall write the entry, creating it if missing, as a `double` for a number tween and as the composed string otherwise, with no codec.
  - Tests: `ObjectTargetAdapterTests.DictionaryEntriesHoldNumbersAndComposedStrings`
- **CORE-TARGETS-020** — For any other target, the adapter shall reach a public instance property that is both readable and writable, or else a public instance field, of that name.
  - Tests: `ObjectTargetAdapterTests.WritesNumericMembersInTheirOwnType`, `ObjectTargetAdapterTests.ReadsMembersAndTreatsMissingDictionaryKeysAsNull`
- **CORE-TARGETS-021** (inherited) — When no such member exists, the adapter shall throw `ArgumentException`.
  - Tests: `ObjectTargetAdapterTests.UnknownMemberThrows`
- **CORE-TARGETS-022** — When the member is `double`, `float`, `decimal`, `int`, `long`, `short` or `byte`, the adapter shall write the number in the member's own type.
  - Tests: `ObjectTargetAdapterTests.WritesNumericMembersInTheirOwnType`
- **CORE-TARGETS-023** — When the member is integral, the adapter shall round the number half up (toward +∞ on ties, as JavaScript `Math.round`) before writing.
  - Tests: `ObjectTargetAdapterTests.RoundsIntegralMembersHalfUp`
- **CORE-TARGETS-024** — When the member is a `string`, the adapter shall write the composed string: the formatted number, the number and unit, the `rgba()` color or the joined complex value, or the `TextModifier` result.
  - Tests: `ObjectTargetAdapterTests.WritesUnitValuesToStringMembers`, `ObjectTargetAdapterTests.WritesComplexValuesToStringMembers`, `ObjectTargetAdapterTests.WritesColorChannelsToColorAndStringMembers`
- **CORE-TARGETS-025** — When the member is a `PrestoColor`, the adapter shall write the tween's four unrounded channels.
  - Tests: `ObjectTargetAdapterTests.WritesColorChannelsToColorAndStringMembers`, `ColorsTests.AnimatesATypedColorMemberToTheParsedChannels`
- **CORE-TARGETS-026** (inherited) — When the member is an `object`, the adapter shall write a number tween as a `double` and any other tween as its composed string.
  - Tests: `ObjectTargetAdapterTests.ObjectMembersHoldNumbersAndComposedStrings`
- **CORE-TARGETS-027** — When the member is of any other type, the adapter shall use the codec registered for that exact type in `Presto.Codecs`.
  - Tests: `ObjectTargetAdapterTests.RegisteredCodecDecodesAndEncodesItsType`
- **CORE-TARGETS-028** — When no codec is registered for the member's type, the adapter shall use a `DiscreteCodec` for that type with the `Midpoint` rule.
  - Tests: `ObjectTargetAdapterTests.MemberTypeWithoutCodecIsDiscrete`
- **CORE-TARGETS-029** — The adapter shall cache one accessor per (target type, property name) for the process, shared by every instance and engine, and shall return a new accessor per call for dictionary targets.
  - Tests: `ObjectTargetAdapterTests.AccessorsAreCachedPerTypeAndProperty`
- **CORE-TARGETS-030** (inherited) — The adapter shall choose a member's codec when its accessor is first created, so a codec registered afterwards does not apply to an accessor already cached.
  - Tests: untested
- **CORE-TARGETS-031** — `Revert` shall write the original value back through the member or dictionary entry.
  - Tests: `ObjectTargetAdapterTests.RevertRestoresTheValueReadBeforeTheAnimation`

### Reconciling codecs

- **CORE-TARGETS-032** — When a property's codec is an `IReconcilingValueCodec`, the parser shall offer it every string end value that does not start with `+=`, `-=` or `*=` before parsing the string itself.
  - Tests: `CodecTests.BoxShadowStringsParseAndInterpolate`
- **CORE-TARGETS-033** — The animation shall call `Reconcile(from, to)` once both ends are decomposed and relative values resolved, before kinds, units and channel counts are reconciled by the core (CORE-VALUES); either end may still be the number 0 of a missing value.
  - Tests: `CodecTests.GradientsWithDifferentStopCountsInterpolateOnTheUnionOfTheirOffsets`, `CodecTests.AnAddedShadowGrowsInItsOwnColorFromNothing`

### Discrete values

- **CORE-TARGETS-034** — A discrete tween shall run one `Number` channel from 0 to 1 and write its from or its to value whole, through the codec's `Encode`.
  - Tests: `DiscreteValuesTests.ABoolSwitchesHalfwayByDefault`
- **CORE-TARGETS-035** — The discrete tween shall follow its `TweenParams.Discrete` rule when set, and the codec's `Rule` otherwise.
  - Tests: `DiscreteValuesTests.EachRuleSwitchesAtItsOwnPoint`, `DiscreteValuesTests.ABoolSwitchesHalfwayByDefault`
- **CORE-TARGETS-036** — Under `Midpoint`, the tween shall write the to value once its eased progress reaches 0.5.
  - Tests: `DiscreteValuesTests.ABoolSwitchesHalfwayByDefault`
- **CORE-TARGETS-037** — Under `AtStart` the tween shall write the to value once progress is above 0, and under `AtEnd` only once it reaches 1.
  - Tests: `DiscreteValuesTests.EachRuleSwitchesAtItsOwnPoint`
- **CORE-TARGETS-038** — Under `TrueThroughout`, when exactly one end is `true`, the tween shall hold `true` everywhere strictly between the ends; `FalseThroughout` shall do the same for `false`.
  - Tests: `DiscreteValuesTests.EachRuleSwitchesAtItsOwnPoint`
- **CORE-TARGETS-039** — When neither or both ends are the rule's `bool`, `TrueThroughout` and `FalseThroughout` shall switch as `Midpoint`.
  - Tests: `DiscreteValuesTests.ThroughoutRulesFallBackToTheMidpointForAnEnum`
- **CORE-TARGETS-040** — The tween shall pick its value from the eased progress on every render, so seeking and reversing cross the switch at the same point both ways.
  - Tests: `DiscreteValuesTests.SeekingAndReversingCrossTheSwitchBothWays`
- **CORE-TARGETS-041** — Discrete keyframes shall each switch by the rule within their own segment.
  - Tests: `DiscreteValuesTests.KeyframesSwitchHalfwayThroughEachSegment`
- **CORE-TARGETS-042** — `DiscreteCodec` shall accept a value of its type as is, convert a number to a `bool` (non-zero is `true`) or to an enum (truncated to its underlying value).
  - Tests: `DiscreteValuesTests.SetWritesABoolAndAnEnum`, `DiscreteValuesTests.AnAnimatableRetargetSnaps`
- **CORE-TARGETS-043** — When a discrete value is relative (`"+=1"`), `DiscreteCodec` shall throw `ArgumentException`.
  - Tests: `DiscreteValuesTests.RelativeValuesThrow`
- **CORE-TARGETS-044** (inherited) — When a discrete value is neither of the type nor a convertible number, `DiscreteCodec` shall throw `ArgumentException`.
  - Tests: untested
- **CORE-TARGETS-045** — When a discrete property's composition is `Blend`, the animation shall throw `ArgumentException`.
  - Tests: `DiscreteValuesTests.BlendingThrows`
- **CORE-TARGETS-046** — `Presto.Set` shall write a discrete value at once, and `Revert` shall restore the original value.
  - Tests: `DiscreteValuesTests.SetWritesABoolAndAnEnum`, `DiscreteValuesTests.RevertRestoresTheOriginalValue`
- **CORE-TARGETS-047** — `Refresh` shall re-read a function-valued discrete end.
  - Tests: `DiscreteValuesTests.RefreshReReadsAFunctionValue`
- **CORE-TARGETS-048** — A discrete tween's frame shall not allocate; both ends are boxed once when the tween is built.
  - Tests: `AllocationTests.DiscreteTweenTicksWithoutAllocating`

### Defaults

- **CORE-TARGETS-049** (inherited) — When nothing has been registered, `Presto.Adapters` and `Presto.Codecs` shall be empty, so every property resolves through `ObjectTargetAdapter` and every member type without a built-in write is discrete.
  - Tests: `UtilsTargetsTests.GetObjectProperties`, `ObjectTargetAdapterTests.MemberTypeWithoutCodecIsDiscrete`
- **CORE-TARGETS-050** — When `rule` is not given, a `DiscreteCodec`'s `Rule` shall be `Midpoint`, as CSS does.
  - Tests: `DiscreteValuesTests.ABoolSwitchesHalfwayByDefault`

## Invariants

- `Presto.ResolveAccessor` never returns null: the fallback adapter either returns an accessor or throws.
- A tween's `Property` is always the canonical name, and its accessor was resolved for that name.
- `TargetParser.Parse` never returns `null` entries or the same reference twice.

## Edge cases

- A collection of value types (`int[]`, `List<double>`) is not an `IEnumerable<object>` and is one target; each boxed value-type target is distinct by reference.
- A `Dictionary<string, double>` is not an `IDictionary<string, object?>`, so it is reached by reflection and its keys throw `ArgumentException`.
- A read-only or write-only property is not reached; a public field of the same name is used instead if there is one.
- A dictionary key that was missing before the animation is not removed on `Revert` (its original is `null`), so it keeps the last written value.
- A unit tween into a numeric member writes the number without its unit; a number to value on a `PrestoColor` member is made transparent black (CORE-VALUES).
- A discrete tween's `Modifier` applies to its 0–1 channel, so it moves the switch point.
- An enum value given as a string (`"Right"`) is not parsed; it throws `ArgumentException`.
- A codec whose `Encode` returns another type makes the member write throw `InvalidCastException`.

## Threading and lifetime

- `Presto.Adapters` and `Presto.Codecs` are plain collections read from every engine's thread without locks: populate them before any animation starts and do not change them while animations may resolve.
- The `ObjectTargetAdapter` accessor cache is a `ConcurrentDictionary` held for the process; accessors hold compiled delegates, not targets, so they keep no target alive.
- `Removed` runs on the thread that called `Presto.Remove`.

## Reduced motion

- Not affected: resolution, codecs and discrete rules do not depend on duration. A discrete tween collapsed by `ReduceAll` lands on its to value (CORE-REDUCED).

## Allocation

- Resolution allocates (reflection and expression compilation on the first resolve of a (type, property) pair, a new accessor per dictionary resolve) and runs when tweens are built.
- Per frame, writes to numeric members and `PrestoColor` members and discrete writes allocate nothing; string, `object` and dictionary writes allocate the string or the boxed number.

## Deviations from anime.js

- Targets are objects only; there are no selectors or node lists, so a string is a target rather than a query.
- Upstream's non-browser `parseTargets` flattens without skipping `null` or removing duplicates; here both are always done, as upstream's browser path does.
- Upstream picks a tween type (CSS, attribute, transform, object) per property; here adapters, accessors and codecs take that role, and typed members are written in their own type.
- Discrete values and reconciling codecs have no upstream counterpart: typed `bool` and enum members could not otherwise be animated, and padding differently shaped values with zeros interpolates them wrongly. Upstream treats a non-numeric string as a complex value, which this port keeps for strings.

## Decisions

- **Property keys are canonicalized, not keyed on accessors.** Every spelling of a property (a name, a typed property handle) resolves to one canonical string key wherever a caller-supplied name meets a target: tween construction, the `Remove` filter and `Animatable`'s lookups. Reason: two spellings landed in different sibling registries, so composition never fired between them and `Remove` missed tweens declared the other way; canonicalizing fixes all three with no core change, keeps the upstream composition port intact and keeps plain-object targets working. Rejected: keying the composer on the resolved accessor or a property-key struct (structurally cleaner, but it moves the port away from upstream and touches tweens, animations, the composer and `Animatable`).
- **Types without a codec animate as discrete values.** `DiscreteCodec` runs one channel from 0 to 1 and writes the from or to object whole, both boxed once on the tween; non-numeric strings keep upstream's complex path. Reason: `bool` and enum properties could not be set by timelines, keyframes or `Presto.Set`, while CSS (`allow-discrete`) and GSAP handle them; frames stay allocation-free. Rejected: throwing `NotSupportedException`.
- **The discrete switch rule is a hybrid.** `TweenParams.Discrete` overrides the rule the codec was built with; the core default is CSS's `Midpoint`, and an adapter may build a property's codec with another (a visibility flag with `TrueThroughout`). The rule is read from eased progress, and the throughout rules act as `Midpoint` unless exactly one end is that `bool`. Reason: a fade-out should stay visible until it ends and a fade-in show from its first frame, CSS's midpoint is the general default, and reading eased progress makes seeks, reversals and keyframe segments switch at the same point.
- **Discrete values reject relative values and `Blend`.** Both throw, and an `Animatable` over a discrete member snaps to each new value. Reason: not recorded beyond there being no meaningful arithmetic on discrete values.
- **Codecs can reconcile ends of different shapes.** An `IReconcilingValueCodec` claims non-relative string end values before the core parses them and makes both ends the same kind and channel count before the core pads them; discrete fields ride as channels. Reason: the core can only pad the shorter channel list with zeros, which is wrong for colors and offsets when shapes differ (two- against three-stop gradients, one shadow against two), and it misparses strings like `"0 4 16 0 #40000000"` as a complex value whose numbers include hex digits.
- **No scene-graph adapter ships.** The adapter interface allows one, but upstream's three.js adapter is not ported. Reason: not recorded.

## Non-goals

- Selector parsing or querying any tree.
- Private, static or indexer members.
- Parsing enum names or other typed values from strings in the fallback adapter.

## Open items

- A codec registered in `Presto.Codecs` after a (type, property) accessor was first resolved never applies to it, since the process-wide accessor cache keeps the codec chosen then (CORE-TARGETS-030); no test, no decision.
- `Revert` skips `null` originals, so a dictionary key added by an animation survives the revert; `Animation` claims to capture originals unconditionally so "a plain 0 or empty is restored too", which holds for 0 and `""` but not for a missing key.
- `IReconcilingValueCodec` has no core test; it is pinned only through the bridge's codecs (`CodecTests`).
- CORE-TARGETS-009 (one resolution per target and property, by canonical name) and CORE-TARGETS-044 (non-convertible discrete value throws) have no test.
