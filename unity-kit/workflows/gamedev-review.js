// unity-kit: gamedev-review — multi-lens review of a Unity project with adversarial verification.
// Read-only (no editor required). Run via:
//   Workflow({ scriptPath: "<plugin>/workflows/gamedev-review.js", args: { scope: "optional focus hint" } })
// or copy into your project's .claude/workflows/ to customize (then it's a named workflow).
export const meta = {
  name: 'gamedev-review',
  description: 'Review a Unity project through four lenses (correctness, Unity pitfalls, performance, design alignment); every finding is adversarially verified before it reaches the report',
  whenToUse: 'After a feature lands or before a merge. Read-only file analysis — the Unity editor does not need to be open.',
  phases: [
    { title: 'Scope', detail: 'map the change surface and project facts' },
    { title: 'Review', detail: 'one finder per lens' },
    { title: 'Verify', detail: 'three adversarial votes per finding' },
    { title: 'Synthesize', detail: 'merge survivors into a claims-with-evidence report' },
  ],
}

const scopeHint = (args && args.scope) ? String(args.scope) : ''
const MAX_PER_LENS = 8

phase('Scope')
const SCOPE_SCHEMA = {
  type: 'object',
  properties: {
    summary: { type: 'string', description: 'What this project is and what changed recently' },
    unityVersion: { type: 'string' },
    designDocPresent: { type: 'boolean' },
    focusFiles: { type: 'array', items: { type: 'string' }, description: 'Repo-relative paths most worth reviewing' },
  },
  required: ['summary', 'designDocPresent', 'focusFiles'],
}
const scope = await agent(`Map the review surface of this Unity project (read-only; do NOT edit anything, do NOT use editor/MCP tools).
1. Read ProjectSettings/ProjectVersion.txt for the Unity version; note the render pipeline and input system from Packages/manifest.json if quick.
2. Check whether Docs/DESIGN.md exists.
3. Find what changed recently: git log --oneline -15 and git diff vs the default branch if on a feature branch; otherwise the most recently modified scripts under Assets/.
4. List the 5-20 files most worth reviewing (gameplay scripts, new systems, tests).
${scopeHint ? `The user narrowed the scope to: ${scopeHint} — honor it.` : ''}
Return structured output.`, { label: 'scope', schema: SCOPE_SCHEMA })

if (!scope) throw new Error('scope agent returned nothing')
log(`Scope: ${scope.focusFiles.length} focus files, DESIGN.md ${scope.designDocPresent ? 'present' : 'MISSING'}`)

phase('Review')
const FINDINGS_SCHEMA = {
  type: 'object',
  properties: {
    findings: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          title: { type: 'string', description: 'Short label for the defect/risk' },
          file: { type: 'string' },
          line: { type: 'number' },
          claim: { type: 'string', description: 'One-sentence statement of the problem' },
          evidence: { type: 'string', description: 'The code/doc excerpt or reasoning that supports the claim' },
          severity: { type: 'string', enum: ['high', 'medium', 'low'] },
        },
        required: ['title', 'file', 'claim', 'evidence', 'severity'],
      },
    },
  },
  required: ['findings'],
}
const LENSES = [
  { key: 'correctness', prompt: 'logic bugs, null/lifecycle hazards, race conditions between Update/coroutines/events, broken edge cases, tests that assert nothing' },
  { key: 'unity-pitfalls', prompt: 'Unity-specific traps: legacy Input API usage, per-frame allocations from string/LINQ/boxing, GetComponent in Update, non-serialized state that resets on domain reload, physics in Update instead of FixedUpdate, misuse of coroutines vs async, missing null-check on destroyed UnityEngine.Object' },
  { key: 'performance', prompt: 'GC allocation spikes, per-frame work that should be cached or event-driven, expensive Find/Camera.main calls, unbatched UI rebuilds, physics/collider misconfiguration, asset settings that bloat memory' },
  { key: 'design-alignment', prompt: 'divergence between Docs/DESIGN.md and the implementation: features that contradict the documented core loop, scope creep systems the design does not call for, missing items the design marks as required, stale design decisions the code has moved past' },
]

const reviewOf = (lens) => agent(`You are a ${lens.key} reviewer for a Unity project. Read-only — no edits, no editor/MCP tools.
Project context: ${scope.summary}
Unity version: ${scope.unityVersion || 'unknown'}. Design doc present: ${scope.designDocPresent}.
Focus files (read them, plus anything they pull you toward): ${scope.focusFiles.join(', ')}
Hunt ONLY for: ${lens.prompt}.
${lens.key === 'design-alignment' && !scope.designDocPresent ? 'Docs/DESIGN.md is missing — your single finding should note that the project has no design source of truth; do not invent alignment issues.' : ''}
Report at most ${MAX_PER_LENS} findings, each a precise claim with the evidence that supports it (file, line, excerpt). No style nits. An empty findings list is a valid result.`, { label: `review:${lens.key}`, phase: 'Review', schema: FINDINGS_SCHEMA })

// Barrier on purpose: dedup across ALL lenses before paying for verification.
const reviews = (await parallel(LENSES.map(l => () => reviewOf(l).then(r => ({ lens: l.key, findings: (r && r.findings) || [] }))))).filter(Boolean)

const all = []
const seen = new Set()
let dupes = 0
for (const r of reviews) {
  if (r.findings.length === MAX_PER_LENS) log(`review:${r.lens} hit the ${MAX_PER_LENS}-finding cap — deeper issues in that lens may be unreported`)
  for (const f of r.findings) {
    const key = `${f.file}|${f.title.toLowerCase().replace(/[^a-z0-9]+/g, ' ').trim()}`
    if (seen.has(key)) { dupes++; continue }
    seen.add(key)
    all.push({ ...f, lens: r.lens })
  }
}
log(`Review: ${all.length} unique findings (${dupes} cross-lens duplicates merged)`)

phase('Verify')
const VOTE_SCHEMA = {
  type: 'object',
  properties: {
    refuted: { type: 'boolean', description: 'true if the finding does NOT hold up' },
    reason: { type: 'string' },
  },
  required: ['refuted', 'reason'],
}
const VERIFY_LENSES = [
  { key: 'exists', angle: 'Re-read the cited file yourself. Does the code actually say what the finding claims? If the excerpt is misread, out of date, or the claimed behavior cannot occur from this code, refute.' },
  { key: 'version-truth', angle: 'Is the claim technically true for THIS Unity version and package set (check ProjectVersion.txt / Packages/manifest.json)? Folk knowledge from old Unity versions that no longer applies means refute.' },
  { key: 'matters', angle: 'Would fixing this change anything a player or developer can observe (bug, frame time, GC spike, design drift)? Purely theoretical or taste-level findings are refuted.' },
]
const verified = await parallel(all.map(f => () =>
  parallel(VERIFY_LENSES.map(v => () =>
    agent(`Adversarially verify a code-review finding in this Unity project. Default to refuted=true unless the evidence convinces you. Read-only.
Finding [${f.severity}] (${f.lens} lens): ${f.title}
File: ${f.file}${f.line ? `:${f.line}` : ''}
Claim: ${f.claim}
Evidence given: ${f.evidence}
Your angle: ${v.angle}`, { label: `verify:${v.key}:${f.title.slice(0, 30)}`, phase: 'Verify', schema: VOTE_SCHEMA, effort: 'low' })
  )).then(votes => {
    const good = votes.filter(Boolean)
    const refutes = good.filter(x => x.refuted).length
    return { ...f, refutes, votes: good.map(x => `${x.refuted ? 'REFUTE' : 'confirm'}: ${x.reason}`), survives: good.length > 0 && refutes <= 1 }
  })
))
const survivors = verified.filter(Boolean).filter(f => f.survives)
const refuted = verified.filter(Boolean).filter(f => !f.survives)
log(`Verify: ${survivors.length} confirmed, ${refuted.length} refuted`)

phase('Synthesize')
const report = await agent(`Write the final review report for this Unity project from these adversarially-verified findings (JSON below). Rules:
- Present findings as claims with their evidence, ordered high→low severity; the human decides what to act on.
- Group by lens; merge findings that describe the same root cause even across lenses.
- End with a short "Refuted along the way" list (title + one-line reason) so the human sees what was checked and dismissed.
- Plain markdown, no preamble.
CONFIRMED: ${JSON.stringify(survivors)}
REFUTED: ${JSON.stringify(refuted.map(f => ({ title: f.title, file: f.file, votes: f.votes })))}`, { label: 'synthesize', effort: 'medium' })

return { report, confirmed: survivors.length, refuted: refuted.length }
