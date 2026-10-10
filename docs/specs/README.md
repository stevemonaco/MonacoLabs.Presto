# Feature specs

One spec per library feature, describing how it behaves **now**. The specs are the source of truth for behavior: the plan documents in `docs/` are the history of how a feature got here, and where a plan and a spec disagree, the spec wins. Specs are written for AI agents first. They are terse, numbered and checkable, so an agent can read the spec instead of reconstructing a feature from its code, tests, plan and decisions.

## Folders

One folder per shipped package, so a spec never crosses a package boundary:

| Folder | Package | Id prefix |
|---|---|---|
| [`core/`](core/index.md) | `MonacoLabs.Presto` | `CORE-` |
| [`avalonia-bridge/`](avalonia-bridge/index.md) | `MonacoLabs.Presto.Avalonia` | `BRIDGE-` |
| [`lottie/`](lottie/index.md) | `MonacoLabs.Presto.Avalonia.Lottie` | `LOTTIE-` |
| [`devtools/`](devtools/index.md) | `MonacoLabs.Presto.Avalonia.DevTools` | `DEVTOOLS-` |

Core specs follow the architecture rule that the core knows nothing about Avalonia: they never contain the word, and they depend only on other `CORE-` specs. A bridge-side spec builds on core specs by id (`depends`) instead of restating them.

## Using the specs

- **Before changing behavior**, read the feature's spec (find it in the folder's `index.md`, or by the type in its `types` list) and the specs in its `depends`.
- **With the change**, update the spec in the same commit: change or add requirements, update their `Tests:` lines, and strike requirements that no longer hold. A change whose behavior diverges from its spec without updating it is incomplete.
- **New work starts as a change proposal** in [`docs/changes/`](../changes/README.md); a new feature also gets a draft spec (`status: draft` in the front matter), requirements before code. The draft becomes current, and the proposal is deleted, when the change lands.
- **When code and spec disagree** and the spec was not changed on purpose, one of them is a bug. Decide which, and log it in the spec's Open items until it is fixed.

## Format

Copy [`_template.md`](_template.md). `SpecTests` (core test project) and `SpecCoverageTests` (bridge test project) enforce it.

**Front matter** (between `---` lines; a scalar is `key: value`, a list is `key:` followed by `  - item` lines, or `key: []` when empty):

| Key | Content |
|---|---|
| `id` | `CORE-TIMER`, `BRIDGE-PRESENCE`: the prefix for its folder, then an upper-case name. Unique. |
| `title` | The feature's name. |
| `status` | Optional. `draft` for a spec written ahead of its feature; omitted when current. |
| `package` | The package the folder is for. |
| `namespaces` | Namespaces the feature's public types live in. |
| `sources` | Files or folders, relative to the repo root. Each must exist. |
| `types` | The public top-level types this spec owns (generic types without type parameters). Every public type in the package is owned by exactly one spec. A static class with members for several features (`Presto`, `PrestoAvalonia`, `Recipes`) is owned by one spec; the others name the members they specify in their Purpose. |
| `tests` | Test classes that cover the feature. |
| `upstream` | anime.js 4.5.0 files (`src/…`, `tests/suites/…`), or `[]`. |
| `depends` | Ids of specs this one builds on. |

**Sections**, as `##` headings in this order: Purpose, Requirements, Invariants, Edge cases, Threading and lifetime, Reduced motion, Allocation, Deviations from anime.js, Decisions, Non-goals, Open items. A section that does not apply says why in one line ("Not on the tick path.", "No upstream counterpart."). Specs carry no API listings or code samples: signatures belong to the code, and API breaks are checked by tooling.

**Requirements** may be grouped under `###` headings. Each is one bullet, one behavior:

```markdown
- **CORE-TIMER-014** — When `Alternate` is true, the timer shall run every odd iteration backwards.
  - Tests: `DirectionsTests.AlternateRunsOddIterationsBackwards`, `TimingsTests.LoopDelayIsAppliedBetweenIterations`
```

- Ids are `<spec id>-NNN`, never reused or renumbered. A requirement that no longer holds is struck through and kept: `- ~~**CORE-TIMER-014**~~ — Removed in Phase 80: reason.`
- Wording is EARS-style: "When …, the X shall …", "While …, the X shall …", or "The X shall …" for unconditional behavior. State observable behavior, not implementation.
- `Tests:` lists `Class.Method` for each test that pins the requirement (theory methods by name only). Every cited test must exist. Otherwise it says `untested`, or `manual — <what has to be checked by hand, and where>`.

**Decisions** record why the feature behaves as it does: one bullet per decision, led by a bold title, then the decision, its reason and, when known, the alternatives rejected. Requirements and deviations refer to a decision by its title, never by a number. A decision governing several features lives in the spec it most constrains; the others name that spec. Project-wide decisions are in [docs/ARCHITECTURE.md](../ARCHITECTURE.md) §5, and those about the test setup and the samples in its §4 and §6.

```markdown
- **Colors interpolate with premultiplied alpha.** Each channel is weighted by its alpha before mixing, as CSS Color 4 does. Reason: with straight alpha, a fade from a transparent color shows that color's RGB mid-way (a transparent teal fading to gray flashes green). Rejected: anime.js's straight-alpha mix.
```

**Inherited requirements** are marked `(inherited)` after the id:

```markdown
- **BRIDGE-IMAGE-057** (inherited) — When `SourceTransition` sets no duration, the transition shall last 600 ms.
  - Tests: untested
```

An inherited requirement records behavior the code has that may not have been human-guided: a default value, a threshold, or a choice made while implementing that no decision or upstream behavior asked for. It is specified so changes to it are visible, but it carries no design intent. Before relying on it as a contract, or when changing it, a human confirms it; once confirmed, the marker is removed. So far only default values are marked; the other requirements extracted in Phase 72 have not yet been classified, so an unmarked requirement is not yet proof of intent.

## Where the specs came from

The specs were extracted from the code, then the tests, the upstream suite and the per-phase plan documents that preceded them; where those disagreed the code won and the disagreement went to Open items. The plans' decisions were then moved into each spec's Decisions section, their open work into [docs/BACKLOG.md](../BACKLOG.md), and the plan documents deleted (git history keeps them). Specs describe; they do not fix: a behavior that looks wrong is specified as it is and logged.
