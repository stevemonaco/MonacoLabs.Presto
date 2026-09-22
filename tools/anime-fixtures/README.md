# anime-fixtures

Runs anime.js 4.5.0 itself and dumps easing samples and spring numbers to `tests/Monaco.Presto.Tests/Fixtures/*.json`,
which `EaseFixtureTests` asserts the C# port against (see docs/PLAN.md section 6).

    npm install && npm run generate

Regenerate only when bumping the upstream reference version; then bump the version in `package.json` and this README.
