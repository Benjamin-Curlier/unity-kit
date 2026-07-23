// unity-kit: playtest-sweep — multi-scenario playtest of the running game, TITAN-shaped
// (state abstraction → pre-filtered intent actions → stall-triggered reflection → parallel bug oracles).
// REQUIRES the Unity editor open with the MCP for Unity bridge connected (unity-launch skill first),
// and the long-run allowlist from the agentic-workflows skill (otherwise every play session stalls on prompts).
// Run via:
//   Workflow({ scriptPath: "<plugin>/workflows/playtest-sweep.js", args: { count: 5, focus: "optional hint" } })
// Play sessions are SERIAL by construction — one editor, one driver. Analysis overlaps the next session.
export const meta = {
  name: 'playtest-sweep',
  description: 'Playtest the game across N planned scenarios: serial play sessions with state probes, intent-level actions, and bug oracles; parallel evidence analysis; claims-with-evidence report',
  whenToUse: 'When a feature set "should be playable", before a build, or as a regression sweep. Editor must be open with MCP connected.',
  phases: [
    { title: 'Plan', detail: 'read DESIGN.md + code, propose scenarios' },
    { title: 'Play', detail: 'one serial editor session per scenario' },
    { title: 'Analyze', detail: 'evidence vs oracles, per scenario in parallel' },
    { title: 'Synthesize', detail: 'claims-with-evidence report' },
  ],
}

const count = Math.max(1, Math.min(10, (args && args.count) || 5))
const focus = (args && args.focus) ? String(args.focus) : ''

phase('Plan')
const PLAN_SCHEMA = {
  type: 'object',
  properties: {
    gameSummary: { type: 'string' },
    stateSurface: { type: 'string', description: 'Which public read-only properties/methods exist for probing, and the tick/speed field to freeze or slow the game' },
    scenarios: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          name: { type: 'string' },
          goal: { type: 'string', description: 'What this scenario proves, phrased falsifiably' },
          entryScene: { type: 'string' },
          probes: { type: 'array', items: { type: 'string' }, description: 'Concrete C# read expressions/snippets for execute_code, with expected value buckets (e.g. score: 0 / 1-5 / >5)' },
          actions: { type: 'array', items: { type: 'string' }, description: 'At most 6 intent-level moves (public method calls / queued intents the input handler produces) — NOT key simulation' },
          successCriteria: { type: 'string' },
          maxActions: { type: 'number', description: 'Action budget before the session is cut off (execution-time oracle)' },
        },
        required: ['name', 'goal', 'probes', 'actions', 'successCriteria', 'maxActions'],
      },
    },
  },
  required: ['gameSummary', 'stateSurface', 'scenarios'],
}
const plan = await agent(`Plan ${count} playtest scenarios for this Unity project. READ-ONLY (files only — do not touch the editor).
1. Read Docs/DESIGN.md (core loop, open questions) and the gameplay scripts under Assets/.
2. Map the state surface: public read-only properties suitable for probing via execute_code, and the field that freezes/slows the game tick for deterministic stepping (see unity-playtest skill: MCP latency is seconds — a live game outruns probes).
3. Propose exactly ${count} scenarios, mixing: the happy-path core loop, boundary/illegal input, an end-state transition (death/win/restart), and — per the design doc's open questions — whatever the design is least sure about. ${focus ? `The user asked to focus on: ${focus}.` : ''}
Each scenario's actions must be INTENT-LEVEL (the method/field the input handler drives), at most 6 distinct moves. Probes must be pasteable C# expressions with expected buckets, not vague wishes.`,
  { label: 'plan-scenarios', schema: PLAN_SCHEMA })

if (!plan || !plan.scenarios || plan.scenarios.length === 0) throw new Error('scenario planning returned nothing')
log(`Planned ${plan.scenarios.length} scenarios: ${plan.scenarios.map(s => s.name).join(' · ')}`)

const EVIDENCE_SCHEMA = {
  type: 'object',
  properties: {
    scenarioName: { type: 'string' },
    sessionRan: { type: 'boolean' },
    actionsTaken: { type: 'array', items: { type: 'string' } },
    probeLog: { type: 'array', items: { type: 'string' }, description: 'timestamped-by-order probe → observed value lines' },
    consoleTail: { type: 'array', items: { type: 'string' }, description: 'Errors/exceptions seen (MCP-FOR-UNITY client-handler noise excluded)' },
    screenshots: { type: 'array', items: { type: 'string' }, description: 'What each captured screenshot actually shows, described from looking at it' },
    goalEvidence: { type: 'string', description: 'Claim about the goal with the evidence for/against — NOT a bare verdict' },
    anomalies: { type: 'array', items: { type: 'string' } },
    playModeStopped: { type: 'boolean' },
  },
  required: ['scenarioName', 'sessionRan', 'actionsTaken', 'probeLog', 'consoleTail', 'goalEvidence', 'playModeStopped'],
}

const playPrompt = (s) => `Run ONE playtest session for the scenario below. The Unity editor is open with MCP for Unity connected; you own the editor for this session. Use the unity-playtest skill's discipline throughout.

GAME: ${plan.gameSummary}
STATE SURFACE: ${plan.stateSurface}
SCENARIO: ${JSON.stringify(s)}

Protocol (TITAN loop):
1. Read the mcpforunity://editor/state resource; if compiling, wait until done. read_console to snapshot pre-existing errors.
2. ${s.entryScene ? `Open scene ${s.entryScene} via manage_scene if not already open. ` : ''}Enter play mode (manage_editor "play"). Freeze or slow the tick per the state surface before probing.
3. Loop, at most ${s.maxActions} actions: probe state (the scenario's probes; record observed vs expected bucket) → pick the next action FROM THE SCENARIO'S ACTION LIST ONLY → apply it at intent level via execute_code → re-probe.
4. Reflection trigger: if 3 consecutive actions produce no measurable probe change, STOP acting; re-read the goal, write down your hypothesis for the stall (this is evidence, not failure), optionally try ONE alternative action from the list, then end the session.
5. Oracles, always on: read_console after every 2-3 actions (exceptions = anomaly; ignore MCP-FOR-UNITY client-handler noise); the ${s.maxActions}-action budget is the runaway oracle; screenshot (manage_camera, capture_source "game_view", include_image true) at boot, once mid-session, and at the end — LOOK at each image and record what it shows, especially where it contradicts probed state.
6. If an MCP call drops mid-session (domain reload / bridge blip), retry it once before treating the bridge as down.
7. ALWAYS exit play mode (manage_editor "stop") before returning, even after errors — report playModeStopped truthfully.

Report evidence, not verdicts: goalEvidence is a claim plus the probes/screenshots that support or undercut it. A human adjudicates.`

phase('Play')
// Serial play (one editor), analysis overlapped: each session's analysis starts
// while the next session plays.
const analyses = []
const sessions = []
for (const s of plan.scenarios) {
  const ev = await agent(playPrompt(s), { label: `play:${s.name.slice(0, 30)}`, phase: 'Play', schema: EVIDENCE_SCHEMA })
  if (!ev) { log(`play:${s.name} returned nothing — skipped in report`); continue }
  sessions.push(ev)
  if (ev.playModeStopped === false) log(`WARNING play:${s.name} reported play mode NOT stopped — next session may misbehave`)
  analyses.push(agent(`Analyze this playtest evidence bundle against its scenario's oracles. Read-only; no editor access.
SCENARIO: ${JSON.stringify(s)}
EVIDENCE: ${JSON.stringify(ev)}
For each oracle (console exceptions, goal/task status, action-budget runaway, screenshot-vs-state mismatch): state what the evidence shows as a claim with the supporting excerpt. Distinguish "bug in the game" from "gap in the scenario/probes". Flag anything a human must adjudicate (ambiguous evidence, screenshot contradicting probes). Severity-tag anomalies high/medium/low. Return concise markdown.`,
    { label: `analyze:${s.name.slice(0, 30)}`, phase: 'Analyze', effort: 'medium' }))
}

phase('Analyze')
const analyzed = (await Promise.all(analyses)).filter(Boolean)
log(`Analyzed ${analyzed.length}/${sessions.length} sessions`)

phase('Synthesize')
const report = await agent(`Write the playtest-sweep report from these per-scenario analyses. Rules:
- Lead with what a human should look at first (high-severity anomalies, contradicted screenshots).
- Per scenario: goal, what happened (claims + evidence), anomalies. Keep probe logs summarized, not dumped.
- Findings are claims for human adjudication, not verdicts — automation bias is real; when the evidence is thin, say so.
- End with scenario-coverage gaps (what was NOT tested).
Plain markdown, no preamble.
ANALYSES:\n${analyzed.map((a, i) => `--- scenario ${i + 1} ---\n${a}`).join('\n')}`,
  { label: 'synthesize', effort: 'medium' })

return { report, scenariosPlanned: plan.scenarios.length, sessionsRun: sessions.length }
