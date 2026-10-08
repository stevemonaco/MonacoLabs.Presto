# anime-fixtures

Runs anime.js 4.5.0 itself and dumps easing samples and spring numbers to `tests/Monaco.Presto.Tests/Fixtures/*.json`,
which `EaseFixtureTests` asserts the C# port against (see docs/ARCHITECTURE.md §4).

It also writes `colors.json` from [culori](https://culorijs.org/) 4.0.2 (a dev dependency): conversions, mixes in every
color space and hue mode, CSS Color 4 gamut mapping and OKLCH relative colors, which `ColorConversionTests`
asserts against within 1e-6 (see docs/specs/core/colors.md).

    npm install && npm run generate

Regenerate only when bumping the upstream reference version; then bump the version in `package.json` and this README.
