export const meta = {
  name: 'implement-changes',
  description: 'Implement several change proposals one after another with implement-plan, then publish and run the NativeAOT probe over the combined tree',
  whenToUse: 'A long unattended run over several agreed change proposals. args: an ordered array of docs/changes/<name>.md paths, or { changes: [...], reviewEach: true, maxTestRounds: 3, stopWhenRed: true, aotRounds: 2 }',
  phases: [
    { title: 'Changes', detail: 'one implement-plan run per proposal, in order' },
    { title: 'AOT', detail: 'publish and run the NativeAOT probe, fix rounds until it passes' },
  ],
}

const opts = Array.isArray(args) ? { changes: args } : (args ?? {})
if (!opts.changes?.length) throw new Error('args must be an ordered array of docs/changes/<name>.md paths, or { changes, reviewEach, maxTestRounds, stopWhenRed, aotRounds }')
const stopWhenRed = opts.stopWhenRed ?? true
const aotRounds = opts.aotRounds ?? 2

const AOT_SCHEMA = {
  type: 'object',
  properties: {
    publishOk: { type: 'boolean' },
    ilWarnings: { type: 'array', items: { type: 'string' } },
    failures: { type: 'array', items: { type: 'string' }, description: 'Each FAIL line, or the publish error' },
    exitCode: { type: 'integer' },
  },
  required: ['publishOk', 'ilWarnings', 'failures', 'exitCode'],
}

const RULES = `Use PowerShell. Do not commit, stash, checkout, reset or otherwise change git state. Never run two builds at once.`

const AOT_RUN = `Verify that MonacoLabs.Presto still publishes and runs under NativeAOT, and report exactly what you observe. ${RULES} Do not edit files.
1. \`dotnet publish tests/MonacoLabs.Presto.AotProbe -c Release -r win-x64 -o artifacts/aot-probe -p:TrimmerSingleWarn=false\`. If ILC finds no platform linker, follow CLAUDE.md (PATH/LIB at an installed MSVC toolset and Windows SDK, \`-p:IlcUseEnvironmentalTools=true\`). Record every IL2xxx/IL3xxx warning and any error.
2. If it published, run \`artifacts/aot-probe/MonacoLabs.Presto.AotProbe.exe\` and record its exit code and every FAIL line.`

function aotFixPrompt(run, changed) {
  const problems = run.failures.concat(run.ilWarnings).map(p => `- ${p}`).join('\n')
  return `The NativeAOT probe for MonacoLabs.Presto is red after this run's changes. Fix the cause with the trim/AOT-safe pattern the codebase already uses (CLAUDE.md, Code conventions; CORE-TARGETS; ARCHITECTURE §3-4): an \`IMemberTarget\` table for a library type animated by name, \`[UnsafeAccessor]\` for Avalonia internals, compiled bindings, \`Presto.RegisterTarget<T>()\` for a sample's plain object. Never suppress a warning to make it pass. If the new code is a hazard the probe should cover and does not, add a probe case.

Problems:
${problems}

Files changed in this run: ${changed.join(', ') || 'see git status'}

Finish with the probe publishing with no IL warnings and exiting 0, and \`dotnet build MonacoLabs.Presto.slnx\` and \`dotnet test --solution MonacoLabs.Presto.slnx\` green. ${RULES}`
}

phase('Changes')
const results = []
let stoppedAfter = null
for (let i = 0; i < opts.changes.length; i++) {
  const change = opts.changes[i]
  log(`[${i + 1}/${opts.changes.length}] ${change}`)
  const priorWork = results.filter(r => r.result).map(r => `- ${r.change} (${r.result.closed}): ${r.result.implemented.flatMap(x => x.files).join(', ')}`).join('\n')
  let result = null
  try {
    result = await workflow('implement-plan', { plan: change, reviewEach: opts.reviewEach, maxTestRounds: opts.maxTestRounds, priorWork })
  } catch (e) {
    log(`${change} threw: ${e.message}`)
  }
  results.push({ change, result })
  if (!result?.green) {
    log(`${change} ended ${result ? 'red' : 'without a result'}`)
    if (stopWhenRed) { stoppedAfter = change; break }
  } else {
    log(`${change}: green, ${result.closed}`)
  }
}

phase('AOT')
let aot = null
const aotHistory = []
const changedFiles = [...new Set(results.flatMap(r => r.result?.changedFiles ?? []))]
if (stoppedAfter) {
  log('skipping the AOT probe: the tree is red')
} else {
  for (let round = 1; round <= aotRounds + 1; round++) {
    aot = await agent(AOT_RUN, { label: `aot:run-${round}`, phase: 'AOT', schema: AOT_SCHEMA, effort: 'low' })
    if (!aot) break
    aotHistory.push({ round, publishOk: aot.publishOk, exitCode: aot.exitCode, warnings: aot.ilWarnings.length })
    if (aot.publishOk && aot.exitCode === 0 && !aot.ilWarnings.length) { log('AOT probe green'); break }
    if (round > aotRounds) { log(`AOT probe still red after ${aotRounds} fix round(s); leaving it for the user`); break }
    await agent(aotFixPrompt(aot, changedFiles), { label: `aot:fix-${round}`, phase: 'AOT' })
  }
}

return {
  changes: results.map(r => ({
    change: r.change,
    green: r.result?.green ?? false,
    closed: r.result?.closed ?? 'no result',
    blockers: r.result?.blockers ?? [],
    unresolved: r.result?.finalReview?.unresolved ?? [],
    tests: r.result?.tests ?? 'not run',
    implemented: (r.result?.implemented ?? []).map(x => `${x.id}: ${x.summary}`),
  })),
  notRun: opts.changes.slice(results.length),
  stoppedAfter,
  aot: aot ? { green: aot.publishOk && aot.exitCode === 0 && !aot.ilWarnings.length, failures: aot.failures, ilWarnings: aot.ilWarnings, rounds: aotHistory } : 'not run',
  changedFiles,
}
