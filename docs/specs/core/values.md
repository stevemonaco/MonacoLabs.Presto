---
id: CORE-VALUES
title: Values, parsing and unit conversion
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
  - MonacoLabs.Presto.Values
sources:
  - MonacoLabs.Presto/TweenValue.cs
  - MonacoLabs.Presto/FunctionValue.cs
  - MonacoLabs.Presto/Values/ValueKind.cs
  - MonacoLabs.Presto/Values/ValueParser.cs
  - MonacoLabs.Presto/Values/UnitConverter.cs
  - MonacoLabs.Presto/Values/DecomposedValue.cs
  - MonacoLabs.Presto/Values/JsNumber.cs
  - MonacoLabs.Presto/Animation.cs
  - MonacoLabs.Presto/Tween.cs
  - MonacoLabs.Presto/Core/Renderer.cs
types:
  - TweenValue
  - ValueKind
  - ValueParser
  - UnitConverter
  - DecomposedValue
  - FunctionValue
tests:
  - TweenValueTests
  - ValuesTests
  - UnitsTests
  - FunctionBasedValuesTests
  - KeyframesTests
upstream:
  - src/core/values.js
  - src/core/units.js
  - tests/suites/values.test.js
  - tests/suites/units.test.js
  - tests/suites/function-based-values.test.js
depends:
  - CORE-COLORS
  - CORE-TARGETS
---

# Values, parsing and unit conversion

## Purpose

`TweenValue` is the raw property value: a number, a string (`"+=50"`, `"50%"`, `"1turn"`, `"#f00"`, `"blur(4px)"`), an array (from/to pair or keyframes), a `TweenParams`, a `FunctionValue`, or a typed value wrapped with `Of`. `ValueParser` decomposes such a value into a `DecomposedValue` (number, unit, color channels or complex numbers and strings), the animation reconciles a tween's two ends into one kind and unit, and the tween recomposes each frame into what the target receives. Colors are parsed by CORE-COLORS; typed values reach the parser through the codecs of CORE-TARGETS. CORE-ANIMATION builds on this to create tweens.

## Requirements

### TweenValue conversions

- **CORE-VALUES-001** — The `TweenValue` shall store every numeric implicit conversion (`double`, `int`, `float`, `long`) as a `double`.
  - Tests: `ValuesTests.NumberTypeTweenValuesFromNumbersAndStrings`
- **CORE-VALUES-002** — When `TweenValue.Of` is given an `int`, `float`, `long`, `short`, `byte` or `decimal`, it shall store the value as a `double`; given a `TweenValue` it shall return it unchanged; given anything else, `null` included, it shall store the object as is.
  - Tests: `TweenValueTests.CoerceFollowsGetFunctionValue`, `ValuesTests.TweenEndValueTypes`
- **CORE-VALUES-003** — The implicit conversion from `PrestoColor` shall store the color itself, so its channels stay exact.
  - Tests: `ColorsTests.AnImplicitColorKeepsItsFractionalChannels`, `ColorKeyframesTests.AnOklchFullTurnCirclesTheHue`
- **CORE-VALUES-004** — When a property value is an array of one raw item, the animation shall treat it as that single value.
  - Tests: `KeyframesTests.AnArrayOfOneRawValueIsASimpleValue`
- **CORE-VALUES-005** — When a property value is an array of two raw items, or is built with `TweenValue.FromTo`, the animation shall treat it as a from/to pair.
  - Tests: `KeyframesTests.AnArrayOfTwoRawValuesBecomesFromToValues`, `ValuesTests.FromToValuesShorthand`
- **CORE-VALUES-006** — When a property value is an array of three or more raw items, the animation shall treat it as keyframes whose first frame is the from/to pair of the first two items.
  - Tests: `KeyframesTests.TheFirstOfMoreThanTwoRawValuesIsUsedAsAFromValue`
- **CORE-VALUES-007** — When the first item of a property's array is a `TweenParams`, the animation shall treat every item as a keyframe, whatever the count.
  - Tests: `KeyframesTests.AnArrayOfTwoObjectValuesBecomesKeyframes`, `ValuesTests.RelativeValuesInsideKeyframes`
- **CORE-VALUES-008** — When an array property value contains an `Inertia` or a `Projectile`, the animation shall throw `ArgumentException`.
  - Tests: `InertiaTests.AnInertiaInAKeyframeArrayThrows`, `ProjectileTests.AKeyframeOrFromToPairThrows`

### Decomposition

- **CORE-VALUES-009** — `ValueParser.Decompose` shall reset `into` (`DecomposedValue.Reset`: the number 0, no unit, no operator, no arrays, no discrete value) before filling it.
  - Tests: `ValuesTests.DecomposeResetsPreviousState`
- **CORE-VALUES-010** — When the raw value is `null`, `""`, `0` or `false`, the parser shall produce the number 0.
  - Tests: `ValuesTests.FalsyValuesDecomposeToZero`
- **CORE-VALUES-011** — When the raw value is a numeric primitive or a `bool`, the parser shall produce that number (`true` as 1), with NaN replaced by 0.
  - Tests: `ValuesTests.NumberTypeValuesFromNumbersAndStrings`
- **CORE-VALUES-012** — When the raw value is a string that converts to a number by JavaScript unary `+` rules (invariant culture, exponents allowed, whitespace-only as 0), the parser shall produce that number.
  - Tests: `ValuesTests.NumberTypeValuesFromNumbersAndStrings`, `ValuesTests.NumberTypeTweenValuesFromNumbersAndStrings`
- **CORE-VALUES-013** — When a non-numeric string's second character is `=`, the parser shall take its first character as the relative operator and parse the rest of the string.
  - Tests: `ValuesTests.RelativeValueOperators`
- **CORE-VALUES-014** — When the (operator-stripped) string has no space and is a number immediately followed by letters or `%`, the parser shall produce a `Unit` value with that number and unit, keeping the unit's case.
  - Tests: `ValuesTests.UnitTypeValues`, `ValuesTests.UnitParsingShouldMatch`, `ValuesTests.UnitParsingShouldNotMatch`
- **CORE-VALUES-015** — When a relative string has no unit, the parser shall produce a `Number` value from the rest of the string, NaN when it does not parse.
  - Tests: `ValuesTests.RelativeValueOperators`
- **CORE-VALUES-016** — When an absolute string is a color by `ValueParser.IsColor` (a hex color, or one starting with `rgb` or `hsl` that ends in `)` or is not followed by more values), the parser shall produce a `Color` value with the four channels CORE-COLORS parses.
  - Tests: `ValuesTests.ColorTypeValues`, `ColorsTests.BoxShadowShouldNotBeParsedAsColorValue`
- **CORE-VALUES-017** — Otherwise the parser shall produce a `Complex` value: every embedded number in `Numbers` and the text between them in `Strings`, which has one more entry than `Numbers`.
  - Tests: `ValuesTests.ComplexWhiteSpaceValues`, `ValuesTests.ComplexMixedTypeValues`, `ValuesTests.ComplexCssFilterValues`
- **CORE-VALUES-018** — When a complex string has no numbers, the parser shall produce empty `Numbers` and the whole string as the single entry of `Strings`.
  - Tests: `ValuesTests.ComplexValueWithoutNumbersKeepsTheWholeString`
- **CORE-VALUES-019** — When a complex string contains a number with an exponent, the parser shall read every exponent digit, so `"1e10"` is one number.
  - Tests: `ValuesTests.ComplexExponentsKeepEveryDigit`
- **CORE-VALUES-020** — When the raw value is a `PrestoColor`, the parser shall produce a `Color` value with its unrounded channels.
  - Tests: `ValuesTests.PrestoColorDecomposesToColor`
- **CORE-VALUES-021** (inherited) — When the raw value is a `double[]`, the parser shall produce a `Complex` value of a copy of the numbers separated by single spaces.
  - Tests: `ValuesTests.DoubleArrayDecomposesToSpaceSeparatedComplex`
- **CORE-VALUES-022** — When the raw value is of any other type, the parser shall hand it to the codec's `TryDecode`, and shall throw `NotSupportedException` when there is no codec or `TryDecode` returns false.
  - Tests: `ValuesTests.UnsupportedTypesThrow`, `ObjectTargetAdapterTests.RegisteredCodecDecodesAndEncodesItsType`
- **CORE-VALUES-023** — When the codec is a `DiscreteCodec`, the parser shall hand it every non-null raw value, strings and numbers included (CORE-TARGETS).
  - Tests: `DiscreteValuesTests.SetWritesABoolAndAnEnum`
- **CORE-VALUES-024** — When the codec is an `IReconcilingValueCodec` and a string does not start with a relative operator, the parser shall offer the string to the codec's `TryParse` first and parse it itself only when that returns false (CORE-TARGETS).
  - Tests: `CodecTests.BoxShadowStringsParseAndInterpolate`
- **CORE-VALUES-025** — `ValueParser.DecomposeTween` shall copy a tween's end value (kind, number, unit, numbers, strings and discrete value) with no operator, as the from value of the keyframe after it.
  - Tests: `ValuesTests.RelativeValuesInsideKeyframes`
- **CORE-VALUES-026** — `ValueParser.GetRelativeValue(x, y, op)` shall return `x - y` for `'-'`, `x + y` for `'+'` and `x * y` for any other operator.
  - Tests: `ValuesTests.GetRelativeValueAppliesOperator`
- **CORE-VALUES-027** — `ValueParser.IndexOfRelativeOperator` shall return the index of the first `+=`, `-=` or `*=` in the text, or -1.
  - Tests: `DiscreteValuesTests.RelativeValuesThrow`

### Resolving a tween's two ends

- **CORE-VALUES-028** — When a tween's from value is relative, the animation shall apply it to the previous sibling tween's end number on the same property, or to the target's current value when there is none.
  - Tests: `ValuesTests.RelativeFromValues`, `ValuesTests.RelativeFromToValues`
- **CORE-VALUES-029** — When a tween's to value is relative, the animation shall apply it to the resolved from number.
  - Tests: `ValuesTests.RelativeValuesWithOperators`, `ValuesTests.RelativeFromToValues`, `ValuesTests.NumberTypeValuesFromRelativeValuesOperators`
- **CORE-VALUES-030** — When a from/to pair's from value is a plain number, the animation shall give it the unit of the previous sibling tween when that is a unit value, or else the unit of the target's current value.
  - Tests: untested
- **CORE-VALUES-031** — When the two ends differ in kind and either is `Complex`, the animation shall make the other end complex with the complex end's strings and its own number in every slot.
  - Tests: `ValuesTests.ComplexTypeValues`
- **CORE-VALUES-032** — Otherwise, when the ends differ in kind and either is `Unit`, the animation shall give the other end that unit.
  - Tests: `UnitsTests.ShouldMatchAnyUnitsFromOriginalValues`, `UnitsTests.ShouldMatchAnyUnitsFromComplexOriginalValues`
- **CORE-VALUES-033** — Otherwise, when the ends differ in kind and either is `Color`, the animation shall make the other end transparent black (`0, 0, 0, 0`).
  - Tests: untested
- **CORE-VALUES-034** — When the ends carry different units, the animation shall convert the from value to the to value's unit, or give the to value the from value's unit when the to value has none; the tween's unit is the to value's.
  - Tests: `UnitsTests.ConvertsAngleUnitsAgainstTheOriginalValue`, `UnitsTests.ShouldMatchAnyUnitsSetInThePropertyValue`, `UnitsTests.SpecifiedUnitOnASimpleTween`, `UnitsTests.UnitsInheritanceOnFromToValues`
- **CORE-VALUES-035** — When the ends' `Numbers` differ in length, the animation shall pad the shorter with zeros and give it the longer end's strings.
  - Tests: untested
- **CORE-VALUES-036** — The animation shall take a complex tween's strings from its to value, so a string with no numbers (`"left"` to `"right"`) writes the end string from its first frame.
  - Tests: `ValuesTests.NonNumericStringsStayComplexAndWriteTheEndStringFromTheFirstFrame`

### Unit conversion

- **CORE-VALUES-037** — When the value is already a `Unit` value in the requested unit, `UnitConverter.Convert` shall leave it unchanged.
  - Tests: `UnitsTests.SameUnitIsUnchanged`
- **CORE-VALUES-038** — When both units are angles, `UnitConverter.Convert` shall scale the number by `deg` = 1, `rad` = 180/π and `turn` = 360.
  - Tests: `UnitsTests.ConvertsBetweenAngleUnits`, `UnitsTests.RadToTurn`
- **CORE-VALUES-039** — When either unit is not an angle (or the value has none), `UnitConverter.Convert` shall keep the number and adopt the requested unit 1:1.
  - Tests: `UnitsTests.NonAngleUnitsAdoptTargetUnitOneToOne`, `UnitsTests.PlainNumberAdoptsTargetUnit`
- **CORE-VALUES-040** — `UnitConverter.Convert` shall always leave the value as a `Unit` value carrying the requested unit.
  - Tests: `UnitsTests.PlainNumberAdoptsTargetUnit`

### Recomposition

- **CORE-VALUES-041** — While a unit or complex tween is between its ends, the renderer shall round each interpolated number to 4 decimals; a number tween's value and either end shall not be rounded.
  - Tests: untested
- **CORE-VALUES-042** — The tween shall recompose a unit value as the number in JavaScript `Number` string form followed by the unit.
  - Tests: `UnitsTests.ValuesSetWithUnitsShouldBeProperlyApplied`, `ValuesTests.RelativeValuesWithOperators`
- **CORE-VALUES-043** — The tween shall recompose a complex value by joining its strings around its current numbers.
  - Tests: `ValuesTests.ValuesWithWhiteSpace`, `ValuesTests.ComplexCssValues`
- **CORE-VALUES-044** — When the target holds untyped values, the tween shall write a number tween as a `double`, and every other kind, or a number tween with a `TextModifier`, as a string.
  - Tests: `ObjectTargetAdapterTests.DictionaryEntriesHoldNumbersAndComposedStrings`, `FunctionBasedValuesTests.TextModifiersWriteStringsAndSettleOnTheValueAtOne`
- **CORE-VALUES-045** — Numbers written as strings shall use the shortest round-trip form with a JavaScript-style exponent (`1e+38`, `1e-7`), `NaN`, `Infinity` and `-Infinity`, and negative zero as `0`.
  - Tests: `ValuesTests.JsNumberFormatMatchesJavaScript`, `ValuesTests.JsNumberFormatsNegativeZeroAsZero`
- **CORE-VALUES-046** — The interpolation shall return either end exactly at progress 0 and 1.
  - Tests: `ValuesTests.LerpReturnsEndpointsExactly`

### FunctionValue

- **CORE-VALUES-047** — When a property or parameter value is a `FunctionValue`, the animation shall call it once per target with the target, its index and the target count.
  - Tests: `FunctionBasedValuesTests.BasicFunctionBasedValues`
- **CORE-VALUES-048** — The animation shall coerce a function's result (upstream `getFunctionValue`): `null` and NaN become 0, a string that converts to a number becomes that number, and anything else is kept.
  - Tests: `TweenValueTests.CoerceFollowsGetFunctionValue`, `TweenValueTests.CoerceKeepsObjects`, `FunctionBasedValuesTests.FunctionBasedStringValuesAreConvertedToNumbers`
- **CORE-VALUES-049** — When a function returns a from/to pair, the animation shall use both ends of it for that target.
  - Tests: `FunctionBasedValuesTests.FunctionBasedValuesReturningFromToArrayValues`
- **CORE-VALUES-050** — When the items of a from/to pair are functions, the animation shall evaluate each per target.
  - Tests: `FunctionBasedValuesTests.FunctionBasedValuesInsideFromToArrayValues`
- **CORE-VALUES-051** — When a keyframe's values are functions, the animation shall evaluate them per target and per keyframe.
  - Tests: `FunctionBasedValuesTests.FunctionBasedKeyframesValues`
- **CORE-VALUES-052** — When a function returns a `TweenParams` with a `To`, the animation shall use it as that target's tween parameters.
  - Tests: untested
- **CORE-VALUES-053** — The animation shall keep a tween's from and to functions and re-evaluate them on `Refresh`, re-applying a relative to value against the refreshed from value; a tween with no from function re-reads the target's current value.
  - Tests: `DiscreteValuesTests.RefreshReReadsAFunctionValue`
- **CORE-VALUES-054** — `Refresh` shall reconcile the refreshed ends as the build does (codec `Reconcile`, kind homogenizing, unit conversion and padding), except for the unitless-end rule in Decisions.
  - Tests: `FunctionBasedValuesTests.RefreshConvertsTheFromValueToARefreshedEndsUnit`, `FunctionBasedValuesTests.RefreshPadsAFromValueShorterThanARefreshedEnd`, `ControlsTests.RefreshAnAnimationWithFromToUnitConversionAndRelativeOperator`

## Invariants

- A `Complex` `DecomposedValue` has `Strings.Length == Numbers.Length + 1`; a `Color` one has four `Numbers` (R, G, B in 0–255, A in 0–1).
- After both ends are resolved, a tween's ends have the same kind, the same unit and the same `Numbers` length, and no operator.
- A tween's kind and unit are its to value's.

## Edge cases

- Any character before `=` in the second position is taken as an operator (`"a=5"` multiplies, since every operator other than `+` and `-` multiplies); kept from upstream.
- `"+="` with nothing after it is the relative number 0.
- Strings that .NET cannot parse but JavaScript can (`"0x10"`) are not numbers here; they decompose as complex values.
- A `"rgb(...)"` or `"hsl(...)"` string that `IsColor` accepts but `PrestoColor` cannot parse throws `FormatException` when decomposed (CORE-COLORS).
- Only lower-case `rgb`/`hsl` prefixes count as colors; `"RGB(1,2,3)"` is a complex value.
- A unit string containing a space (`"42px "`) is not a unit value; it is a complex value.
- A unit value written to a numeric member writes the number alone, dropping the unit (CORE-TARGETS).

## Threading and lifetime

- `ValueParser`, `UnitConverter` and the recomposition helpers are stateless and may run on any thread. A `DecomposedValue` is a mutable scratch object and is not thread-safe; the animation's scratch values are thread-static, so animations on different engine threads (CORE-THREADING) parse independently.

## Reduced motion

- Not affected: parsing and recomposition do not depend on duration.

## Allocation

- Decomposition allocates (arrays, regex matches) and runs when tweens are built or refreshed, not per frame.
- Per frame, a number or unit tween into a numeric member allocates nothing (`AllocationTests.NumberTweenTicksWithoutAllocating`); a frame written as a string (unit, complex, untyped targets) allocates that string, and an untyped target boxes the number.

## Provenance

- A complex string's exponents keep every digit, where upstream's pattern reads one (see "Complex exponents keep every digit").
- Units other than `deg`, `rad` and `turn` are adopted 1:1 rather than measured against a DOM clone; there is no layout to measure.
- Upstream converts from any angle unit (yielding NaN for a non-angle target) and leaves the value's type unchanged through a no-op comparison; here only angle-to-angle pairs are scaled and the value always becomes a `Unit` value.
- No CSS variable (`var(--x)`) resolution.
- Additional inputs: `PrestoColor`, `double[]`, `bool`, codec-decoded typed values and discrete values.
- Number formatting switches to exponent notation at 1e15 and 1e-5 (.NET) rather than 1e21 and 1e-7 (JavaScript); not emulated.
- String-to-number conversion uses .NET invariant parsing, so hex (`"0x10"`) and binary literals are not numbers.

## Decisions

- **Complex exponents keep every digit.** The complex-string number pattern reads `e[-+]?\d+`, as the unit pattern already does. Reason: upstream's single digit, with the full pattern commented out beside it, split `"1e10"` into `1e1` and `0`, so a path or shadow written with an exponent animated the wrong numbers and changed shape. Rejected: upstream's single digit.
- **`TweenValue` is a small struct holding the raw value.** It converts implicitly from numbers, strings, `TweenParams`, `FunctionValue` and the other value types, takes any other object through `Of`, and is decoded by the target adapter against the property's current value. Reason: a property value of any accepted shape is then written as a literal in the params initializer, and only the adapter knows the property's type to decode it against.
- **No implicit conversion from `object`.** An arbitrary object, such as a bridge-typed color or thickness, goes through `TweenValue.Of`. Reason: C# forbids a user-defined conversion from `object`, and `Of` also normalizes the numeric types. Rejected: the original design's implicit conversion from any object.
- **A refresh keeps the tween's unit for a unitless end.** A refreshed to value without a unit lands in the unit the tween was built with, and the from value is converted into it before a relative to value applies. Reason: kept from upstream `refresh()` and pinned by the ported `ControlsTests.RefreshAnAnimationWithFromToUnitConversionAndRelativeOperator`; the build would instead give the end the from value's unit. Rejected: reconciling a refresh exactly as a fresh build.
- **`FunctionValue` takes three arguments.** It is `(target, index, total)`; upstream's fourth argument, the timeline being resolved in, lives on `Stagger`, and its `prevTween` argument is not ported. Reason: only stagger positions use the timeline argument, and `prevTween` belongs to upstream's WAAPI and draggable code.
- **Non-numeric strings keep upstream's complex-value path.** A string that is not a number stays a complex value and writes its end string from the first frame; strings are never routed to `DiscreteCodec`. Reason: a string with embedded numbers (`"blur(4px)"`, `"10px 20px"`) interpolates them on that path, and one rule for every string keeps its handling from depending on whether it holds a number.
- **No CSS-specific value handling.** There is no `px`/`em`/`rem` conversion, no CSS variables and no DOM attribute animation; numbers, angle units and percentages pass through for the target adapters to interpret. Reason: the targets are UI object properties, not the DOM.
- **Codecs can reconcile ends of different shapes.** See CORE-TARGETS.
- **Inertia is a to-value, shaped like Spring is an ease.** See CORE-INERTIA.

## Non-goals

- Parsing CSS units against layout, `calc()` evaluation, or named colors.
- Locale-aware number parsing.

## Open items

- CORE-VALUES-030 (unit inheritance of a plain-number from), CORE-VALUES-033 (number to color), CORE-VALUES-035 (padding), CORE-VALUES-041 (4-decimal rounding of in-between frames) and CORE-VALUES-052 (function returning `TweenParams`) have no test.
