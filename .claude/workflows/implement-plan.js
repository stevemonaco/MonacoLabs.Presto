export const meta = {
  name: 'implement-plan',
  description: 'Implement a plan item by item (implement → concision review → tighten), then whole-diff review, full test run with fix rounds, and PLAN.md checklist update',
  whenToUse: 'The implementation phase once a plan exists. args: "Phase 1" (a docs/PLAN.md phase), a plan file path, or the plan text; or { plan, reviewEach: true, maxTestRounds: 3 }',
  phases: [
    { title: 'Decompose', detail: 'read the plan and split it into ordered work items' },
    { title: 'Implement', detail: 'per item, sequentially: implement → concision review → tighten' },
    { title: 'Review', detail: 'whole-diff concision + plan-coverage review, then fix' },
    { title: 'Test', detail: 'build + full test run, fix rounds until green' },
    { title: 'Report', detail: 'tick the PLAN.md checklist when green' },
  ],
}

const opts = typeof args === 'string' ? { plan: args } : (args ?? {})
if (!opts.plan) throw new Error('args must be the plan: "Phase N" of docs/PLAN.md, a plan file path, or plan text — or { plan, reviewEach, maxTestRounds }')
const reviewEach = opts.reviewEach ?? true
const maxTestRounds = opts.maxTestRounds ?? 3

// ---------------------------------------------------------------- schemas

const STRINGS = { type: 'array', items: { type: 'string' } }

const PLAN_SCHEMA = {
  type: 'object',
  properties: {
    title: { type: 'string' },
    source: { type: 'string', description: 'Where the plan was read from: the file path, "docs/PLAN.md Phase N", or "inline"' },
    planPhase: { type: 'string', description: 'The phase label exactly as written in the docs/PLAN.md §5 checklist (e.g. "Phase 1"), or "" when the plan is not a PLAN.md phase' },
    summary: { type: 'string', description: '3-6 sentences another agent can use as context for the whole plan' },
    items: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string', description: 'short slug, e.g. "clock"' },
          title: { type: 'string' },
          area: { type: 'string', enum: ['core', 'bridge', 'sample', 'tests', 'docs'] },
          brief: { type: 'string', description: 'Self-contained instructions: what to build, existing files it extends, upstream files and test suites to port, acceptance criteria' },
          files: STRINGS,
          upstream: { type: 'array', items: { type: 'string' }, description: 'anime.js 4.5.0 paths to read first, e.g. src/timer/timer.js, tests/suites/timings.test.js' },
          visual: { type: 'boolean', description: 'true only when the result must be seen in the Desktop sample' },
        },
        required: ['id', 'title', 'area', 'brief', 'files', 'upstream', 'visual'],
      },
    },
  },
  required: ['title', 'source', 'planPhase', 'summary', 'items'],
}

const IMPLEMENT_SCHEMA = {
  type: 'object',
  properties: {
    summary: { type: 'string', description: '1-3 sentences: what now exists' },
    filesCreated: STRINGS,
    filesModified: STRINGS,
    testsAdded: STRINGS,
    testsPassed: { type: 'boolean' },
    verified: { type: 'string', description: 'Which tests ran and their result; for visual items, what you observed in the sample' },
    blockers: { type: 'array', items: { type: 'string' }, description: 'Parts of the item not done and why; empty when complete' },
  },
  required: ['summary', 'filesCreated', 'filesModified', 'testsAdded', 'testsPassed', 'verified', 'blockers'],
}

const FINDINGS_SCHEMA = {
  type: 'object',
  properties: {
    findings: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          file: { type: 'string' },
          line: { type: 'integer' },
          summary: { type: 'string' },
          fix: { type: 'string', description: 'The concrete change to make' },
          confidence: { type: 'string', enum: ['high', 'medium', 'low'] },
        },
        required: ['file', 'summary', 'fix', 'confidence'],
      },
    },
  },
  required: ['findings'],
}

const FIX_SCHEMA = {
  type: 'object',
  properties: {
    applied: STRINGS,
    skipped: {
      type: 'array',
      items: {
        type: 'object',
        properties: { finding: { type: 'string' }, reason: { type: 'string' } },
        required: ['finding', 'reason'],
      },
    },
    buildOk: { type: 'boolean' },
    testsOk: { type: 'boolean' },
  },
  required: ['applied', 'skipped', 'buildOk', 'testsOk'],
}

const TEST_SCHEMA = {
  type: 'object',
  properties: {
    buildOk: { type: 'boolean' },
    buildErrors: STRINGS,
    warnings: STRINGS,
    passed: { type: 'integer' },
    failed: { type: 'integer' },
    skipped: { type: 'integer' },
    failures: {
      type: 'array',
      items: {
        type: 'object',
        properties: { test: { type: 'string' }, message: { type: 'string' } },
        required: ['test', 'message'],
      },
    },
    changedFiles: STRINGS,
  },
  required: ['buildOk', 'buildErrors', 'warnings', 'passed', 'failed', 'skipped', 'failures', 'changedFiles'],
}

// ---------------------------------------------------------------- prompts

const EDIT_RULES = `

Rules: you are the only agent editing this working tree, so never run two builds at once. Use PowerShell. Do not commit, stash, checkout, reset or otherwise change git state — the user reviews and commits. Never send real OS mouse or keyboard input (only the avalonia_devtools tools may drive a Presto app), and never capture the screen or inspect any window that is not a Monaco.Presto app's own. When porting, read the anime.js 4.5.0 source first: a local checkout if one exists, otherwise fetch the files from github.com/juliangarnier/anime at tag v4.5.0 with WebFetch.`

const READ_ONLY_RULES = ` Do not edit files, build, or run tests.`

const HOT_RELOAD = `
Visual verification (this item changes what the sample shows):
- Load the avalonia_devtools tools with ToolSearch("+avalonia").
- If a PrestoSample.Desktop process is already running, attach-to-app and reuse it: hot reload applies your edits to the live instance.
- Otherwise run \`dotnet watch --project samples/PrestoSample.Desktop\` in the background, wait for the window, then attach-to-app. .axaml edits are patched live; C# edits hot-reload through dotnet watch except rude edits (new types, changed signatures), which need the watch process restarted.
- Drive the page with input, then confirm end states with screenshot and props. Record what you saw in \`verified\`.
- Send input only through the avalonia_devtools \`input\`, \`action\` and \`set-prop\` tools. Never use real OS input (SendInput, mouse_event, keybd_event, SetCursorPos, UI Automation, AutoHotkey, or anything else that moves the real cursor or types on the user's desktop). If DevTools cannot drive something (drags, wheel, multi-touch), say so in \`blockers\` and leave it for the user.
- Look only at Monaco.Presto's own apps: DevTools screenshots of the attached app, or a window capture targeted at a Presto process's window handle. Never capture the whole screen or desktop, and never capture, inspect or read other applications' windows.
- Stop any watch process you started before finishing.
`

const decomposePrompt = `You are decomposing an implementation plan for Monaco.Presto into ordered work items that other agents will implement one at a time, each in a fresh context.

PLAN INPUT (verbatim from the user):
${opts.plan}

Resolve the input first:
- a file path → read that file;
- a phase reference like "Phase 1" → take that phase's checklist entry from docs/PLAN.md §5 plus the matching rows of §2 and the relevant notes in §3, §4 and §6;
- anything else → the input is the plan text itself.
Read docs/PLAN.md in full either way for architecture and API conventions, then read the parts of the codebase the plan touches (existing types, tests, samples) so items refer to what actually exists.

Produce 3–10 work items in dependency order. Each must be implementable by one agent in one sitting: merge tiny items, split anything touching more than ~8 files. Each \`brief\` is self-contained — what to build, which existing files it extends or must stay consistent with, which upstream anime.js 4.5.0 files (src/...) and test suites (tests/suites/...) to read and port, and concrete acceptance criteria. Tests belong inside the item that implements the behaviour. Mark \`visual: true\` only when the result must be seen in the Desktop sample (bridge/sample work); core work is verified by tests.${READ_ONLY_RULES}`

function implementPrompt(plan, item, done, remaining) {
  const doneText = done.length
    ? done.map(d => `- ${d.item.id} ${d.item.title}: ${d.result.summary} (files: ${d.result.filesCreated.concat(d.result.filesModified).join(', ') || 'none'})`).join('\n')
    : '- nothing yet, this is the first item'
  const remainingText = remaining.length ? remaining.map(r => `- ${r.id} ${r.title}`).join('\n') : '- none'
  return `You are implementing one work item of a plan for Monaco.Presto. A reviewer reads your diff next and flags anything that could be smaller, so build exactly what the item asks for and nothing speculative.

PLAN: ${plan.title}
${plan.summary}

ALREADY IMPLEMENTED by earlier items (build on it, do not redo it):
${doneText}

LATER ITEMS (do not start them, but do not paint them into a corner):
${remainingText}

YOUR ITEM: ${item.id} — ${item.title} [${item.area}]
${item.brief}
Files likely involved: ${item.files.join(', ') || 'see brief'}
Upstream to read first: ${item.upstream.join(', ') || 'see brief'}

How to work:
1. Read the existing code you are extending before writing anything.
2. Implement only this item, minimally: no abstractions with one implementation, no options nothing uses, no helper types for one call site, no comments that restate code. Match the surrounding style.
3. \`dotnet build Monaco.Presto.slnx\` must succeed with no new warnings.
4. Port/write the tests the brief names and run the affected test project (\`dotnet test --project tests/Monaco.Presto.Tests\`, or \`dotnet test --solution Monaco.Presto.slnx\` when more than one is affected); they must pass. If the item has no testable behaviour, say so in \`verified\`.
5. If part of the item is impossible or conflicts with what exists, do the rest and list the gap in \`blockers\` — never silently narrow it.
${item.visual ? HOT_RELOAD : ''}${EDIT_RULES}`
}

function concisionPrompt(scope, purpose) {
  return `You are a read-only reviewer with one job: make this change smaller without losing required behaviour.${READ_ONLY_RULES}

CHANGE UNDER REVIEW: ${scope}
WHAT IT WAS MEANT TO DO:
${purpose}

Sprawl to look for:
- abstractions with one implementation or call site: interfaces, base classes, generics, delegates, options or parameters nothing needs
- new files or types that belong in an existing one; helpers duplicating something already in the repo or the BCL
- speculative features, public surface the plan did not ask for, dead or unused members, guards against impossible states
- comments that restate code, label sections, narrate steps or address the reviewer; XML docs on private members (the CLAUDE.md comment rules)
- LINQ or allocations in tick/render hot paths (the repo keeps upstream's linked lists)
- tests that duplicate each other or test the language instead of behaviour
Do NOT flag: structure ported faithfully from anime.js (parity is deliberate even when it looks redundant), naming or formatting taste, or missing functionality.

Report only findings a maintainer would act on, each with a concrete \`fix\` that deletes or merges something. confidence: high = certain it is removable, medium = probably, low = a suggestion. Fewer, sharper findings beat a long list.`
}

function coveragePrompt(plan, done) {
  const reported = done.map(d => `- ${d.item.id} ${d.item.title}: ${d.result.summary}${d.result.blockers.length ? ` — BLOCKERS: ${d.result.blockers.join('; ')}` : ''}`).join('\n')
  return `You are a read-only reviewer checking that the working-tree changes actually implement the plan.${READ_ONLY_RULES}

PLAN: ${plan.title} (source: ${plan.source})
${plan.summary}

What each implementer reported:
${reported}

Read the plan source and the change (\`git status --porcelain\`, \`git diff\`, and every untracked file in full). For every requirement in the plan confirm it is implemented and has a test where the plan or the upstream anime.js suite calls for one. Report each gap as a finding: file = where it should live, summary = what is missing, fix = the smallest change that closes it. Ignore style and concision — another reviewer covers those. confidence: high = the plan explicitly requires it and it is absent, medium = implied by the plan, low = nice to have.`
}

function tightenPrompt(findings, context) {
  const list = findings.map((f, i) => `${i + 1}. ${f.file}${f.line ? ':' + f.line : ''} — ${f.summary}\n   Fix: ${f.fix}`).join('\n')
  return `Apply these review findings to the Monaco.Presto working tree.${context}

${list}

Apply each one unless it would remove behaviour the plan requires or break a test; then skip it and say why. Make no unrelated changes. When done, \`dotnet build Monaco.Presto.slnx\` and \`dotnet test --solution Monaco.Presto.slnx\` must pass — if a fix breaks them and cannot be repaired within the finding's scope, undo that fix and mark it skipped.${EDIT_RULES}`
}

const TEST_PROMPT = `Run the full verification for Monaco.Presto and report the results exactly as observed.${READ_ONLY_RULES}
1. \`dotnet build Monaco.Presto.slnx\` — record every error and every warning.
2. \`dotnet test --solution Monaco.Presto.slnx\` — record passed/failed/skipped counts and, for each failure, the test name and its assertion message.
3. \`git status --porcelain\` — record the changed files.
If the build fails, skip step 2 but still do step 3.`

function testFixPrompt(tests, plan) {
  const problems = tests.buildOk
    ? tests.failures.map(f => `- ${f.test}: ${f.message}`).join('\n')
    : tests.buildErrors.map(e => `- ${e}`).join('\n')
  return `The Monaco.Presto build/test run is red after implementing "${plan.title}". Make it green with the smallest correct change.

${tests.buildOk ? 'Failing tests' : 'Build errors'}:
${problems}

For each failure decide whether the implementation or the test is wrong by checking the plan and the upstream anime.js behaviour, then fix that side. Never delete, skip or loosen a test to make it pass unless it asserts something the plan or upstream contradicts, and say so in \`skipped\` if you do. Finish with \`dotnet build Monaco.Presto.slnx\` and \`dotnet test --solution Monaco.Presto.slnx\` green.${EDIT_RULES}`
}

function checklistPrompt(plan) {
  return `All tests pass after implementing ${plan.planPhase} of docs/PLAN.md. Tick that phase's checkbox in §5 and correct anything in that same entry that is now stale. Change nothing else in the file and no other files.${EDIT_RULES}`
}

function actionable(review, label) {
  const all = review?.findings ?? []
  const kept = all.filter(f => f.confidence !== 'low')
  if (all.length !== kept.length) log(`${label}: ${all.length - kept.length} low-confidence finding(s) not applied`)
  return kept
}

// ---------------------------------------------------------------- run

phase('Decompose')
const plan = await agent(decomposePrompt, { label: 'decompose', phase: 'Decompose', schema: PLAN_SCHEMA })
if (!plan) throw new Error('Decomposition did not complete; nothing was changed.')
log(`${plan.title}: ${plan.items.length} item(s) from ${plan.source} — up to ${plan.items.length * (reviewEach ? 3 : 1) + 6} agents, items run one at a time`)

phase('Implement')
const done = []
const tightened = []
let stoppedAt = null
for (let i = 0; i < plan.items.length; i++) {
  const item = plan.items[i]
  log(`[${i + 1}/${plan.items.length}] ${item.id} — ${item.title}`)
  const result = await agent(implementPrompt(plan, item, done, plan.items.slice(i + 1)), { label: `implement:${item.id}`, phase: 'Implement', schema: IMPLEMENT_SCHEMA })
  if (!result) {
    stoppedAt = item.id
    log(`${item.id} did not complete; later items depend on it, so stopping implementation and reviewing what exists`)
    break
  }
  done.push({ item, result })
  if (result.blockers.length) log(`${item.id} blockers: ${result.blockers.join('; ')}`)
  if (!reviewEach) continue

  const files = result.filesCreated.concat(result.filesModified)
  if (!files.length) continue
  const scope = `these files: ${files.join(', ')} (read created files in full; for modified ones use \`git diff -- <file>\`)`
  const review = await agent(concisionPrompt(scope, item.brief), { label: `review:${item.id}`, phase: 'Implement', schema: FINDINGS_SCHEMA })
  const findings = actionable(review, item.id)
  if (!findings.length) continue
  const fix = await agent(tightenPrompt(findings, ` They concern item "${item.title}" of plan "${plan.title}".`), { label: `tighten:${item.id}`, phase: 'Implement', schema: FIX_SCHEMA })
  tightened.push({ item: item.id, findings: findings.length, applied: fix?.applied ?? [], skipped: fix?.skipped ?? [] })
}

if (!done.length) {
  return { plan: { title: plan.title, source: plan.source }, implemented: [], stoppedAt, note: 'No item completed; nothing to review or test.' }
}

phase('Review')
const wholeTree = 'the whole working tree: `git status --porcelain`, `git diff`, and every untracked file read in full'
const [concision, coverage] = await parallel([
  () => agent(concisionPrompt(wholeTree, `${plan.title}\n${plan.summary}\nThis pass looks across items: duplication between files written by different items, helpers that could be merged, surface no item needed.`), { label: 'review:concision', phase: 'Review', schema: FINDINGS_SCHEMA }),
  () => agent(coveragePrompt(plan, done), { label: 'review:coverage', phase: 'Review', schema: FINDINGS_SCHEMA }),
])
const finalFindings = actionable(concision, 'concision').concat(actionable(coverage, 'coverage'))
let finalFix = null
if (finalFindings.length) {
  finalFix = await agent(tightenPrompt(finalFindings, ` They come from a whole-diff review of plan "${plan.title}"; coverage gaps mean implementing the missing piece minimally.`), { label: 'fix:final', phase: 'Review', schema: FIX_SCHEMA })
}

phase('Test')
let tests = null
const testRounds = []
for (let round = 1; round <= maxTestRounds; round++) {
  tests = await agent(TEST_PROMPT, { label: `test:run-${round}`, phase: 'Test', schema: TEST_SCHEMA, effort: 'low' })
  if (!tests) break
  testRounds.push({ round, buildOk: tests.buildOk, passed: tests.passed, failed: tests.failed })
  if (tests.buildOk && tests.failed === 0) { log(`green: ${tests.passed} passed`); break }
  log(`round ${round}: ${tests.buildOk ? `${tests.failed} failing` : 'build failed'}`)
  if (round === maxTestRounds) { log(`still red after ${maxTestRounds} rounds; leaving it for the user`); break }
  await agent(testFixPrompt(tests, plan), { label: `test:fix-${round}`, phase: 'Test', schema: FIX_SCHEMA })
}
const green = !!tests && tests.buildOk && tests.failed === 0

phase('Report')
const blockers = done.flatMap(d => d.result.blockers.map(b => `${d.item.id}: ${b}`))
const unresolved = (finalFix?.skipped ?? []).map(s => `${s.finding}: ${s.reason}`)
let checklist = 'not a PLAN.md phase'
if (plan.planPhase) {
  if (green && !stoppedAt && !blockers.length && !unresolved.length) {
    await agent(checklistPrompt(plan), { label: 'plan-checklist', phase: 'Report', effort: 'low' })
    checklist = `ticked ${plan.planPhase}`
  } else {
    checklist = `left unticked: ${!green ? 'tests not green' : stoppedAt ? 'implementation stopped' : 'open blockers or gaps'}`
  }
}

return {
  plan: { title: plan.title, source: plan.source, items: plan.items.length },
  implemented: done.map(d => ({ id: d.item.id, title: d.item.title, summary: d.result.summary, files: d.result.filesCreated.concat(d.result.filesModified), tests: d.result.testsAdded, verified: d.result.verified })),
  stoppedAt,
  blockers,
  tightened,
  finalReview: { findings: finalFindings.length, applied: finalFix?.applied ?? [], unresolved },
  tests: tests ? { buildOk: tests.buildOk, passed: tests.passed, failed: tests.failed, failures: tests.failures, warnings: tests.warnings, rounds: testRounds } : 'not run',
  green,
  changedFiles: tests?.changedFiles ?? [],
  checklist,
}
