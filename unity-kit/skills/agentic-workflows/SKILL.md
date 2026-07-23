---
name: agentic-workflows
description: Use when orchestrating Unity work with subagents, agent teams, or Workflow-tool runs — parallelizing gamedev tasks, planning long or unattended autonomous sessions, or deciding whether to fan out at all. Also when an orchestrated run stalls on permission prompts, agents contend for the editor, play mode is left running between agents, or a workflow script needs Unity-specific structure.
---

# Agentic workflows for Unity (solo or team)

Generic orchestration advice says "fan out everything". Unity breaks that: **the editor is one exclusive, mutable resource.** Play mode, scene edits, test runs, imports, and domain reloads all contend for it, and a recompile severs every MCP client at once. Everything below follows from that.

## Ladder — pick the lightest rung that holds

1. **Main loop, no agents** — one bounded change. Orchestration overhead beats no work done, but only when there's real fan-out.
2. **One agent** — verbose work with a short conclusion (verification, docs research, a playtest scenario, Blender session). This is what the plugin's agents are for.
3. **Parallel agents** — independent work that is **parallel-safe** (table below). Authoring in disjoint files is parallel-safe; verifying it is not.
4. **Workflow tool** — repeatable fan-out with deterministic control flow. The plugin ships gamedev workflows (below). Requires a paid plan + recent CLI; if the tool is missing, run the same phases sequentially with the Agent tool — the phase structure, not the runtime, is what matters.
5. **Agent team** — multiple peer sessions on a shared task list. Only for genuinely long parallel lanes (systems lane + content lane); one session must own integration.

Roster discipline (why the plugin has no "2D agent"/"workflow master"): narrow single-purpose agents delegate reliably; persona zoos don't. Knowledge lives in skills loaded into whoever needs it. Default topology is hub-and-spoke — subagents report to the orchestrating session and never to each other; peer messaging is what rung 5 (teams) is for.

## The editor rule

| Parallel-safe (no editor) | One serial lane only (editor) |
|---|---|
| Reading/reviewing files, git, docs research | `manage_editor` (play/stop), `run_tests` |
| Authoring `.cs`/tests in **disjoint** files | `execute_code`, `manage_scene/gameobject/asset` |
| Blender / ElevenLabs / gen-sfx (external processes) | Screenshots, profiling, imports, `refresh_unity` |
| Analyzing captured evidence (logs, screenshots, XML) | Anything that triggers or waits on a recompile |

**Serialize structurally, not by convention.** An "editor lock" stated in agent prompts is a wish — an agent that retries after a dropped call, or a health-check hook, will grab the editor mid-session anyway. Enforce it with topology: a serial `for` loop in a workflow script, phase barriers, or "the lead holds the editor" in an agent team. The playtest-sweep workflow is the reference shape: plan (parallel-safe) → play (serial loop) → analyze (parallel, overlapped) → synthesize.

While parallel file-authoring runs alongside an editor session, stop Unity from noticing half-written code: `execute_code` → `AssetDatabase.DisallowAutoRefresh()` before, `AllowAutoRefresh()` + `refresh_unity` after — **paired in the same controlling context**, so a crashed agent can't leave auto-refresh off (if the editor later seems to ignore edits, a leaked Disallow is the first suspect).

## Unattended-run preflight (do this BEFORE launching, not after the first stall)

Subagents inherit your allowlist; on a long run, **every non-allowlisted MCP call is a stall until a human returns** — this, not agent quality, is what kills 4-hour runs. Preflight:

1. **Allowlist the run's tools** for the session: `manage_editor`, `execute_code`, `manage_camera`, `refresh_unity`, plus whatever the plan touches (`manage_scene`, `manage_gameobject`, `manage_asset`, …). **Derive the exact rule prefix from this session's actual tool names first** — MCP rules are exact-match and the bridge's server name varies by registration: `mcp__unityMCP__*` (project `.mcp.json`), `mcp__UnityMCP__*` (a `claude mcp add` registration), or `mcp__plugin_unity-kit_unityMCP__*` (registered by this plugin). A rule with the wrong prefix matches nothing and the run stalls on prompts anyway. Prefer `.claude/settings.local.json` (git-ignored) for per-run grants; the scaffold's settings already allow the read-only tools plus `run_tests`/`manage_tools` under the common prefixes. Remove the grants afterwards if they were for the run, not the project.
2. **Editor state**: bridge answers (`mcpforunity://editor/state`), console clean, `manage_tools` groups the run needs activated once.
3. **Git checkpoint** before, commits at phase boundaries — an unattended run you can't roll back is a gamble, not a workflow.
4. **Report contract**: every agent returns the same shape (status, files touched, tests run/passed, console errors, artifacts, and — for any agent that entered play mode — `playModeStopped`). Every play-mode enter is paired with a stop before the agent returns; a handoff with play mode still running is itself a reportable failure, because it poisons the next editor session. Structured output beats prose for the synthesis step.
5. **Budgets in actions, not wall-clock**: the workflow runtime blocks wall-clock calls (`Date.now` throws — resume safety) — cap by action counts, agent counts, or the `budget` API, and put per-issue fix caps (3, per unity-verify) in agent prompts.

## Domain reloads will happen mid-run

Every recompile and play-mode enter/exit reloads the C# domain and drops MCP clients for ~seconds (occasionally ~20s). Orchestration rules: retry a dropped call **once** before declaring the bridge down; after script edits, poll `is_compiling` until false (unity-verify has the loop); never schedule two agents so one's recompile lands inside the other's play session — that's the editor rule again.

## Report evidence, not verdicts

A controlled study (arXiv:2501.11782, preprint): AI assistance lifts human defect-detection markedly — and when the AI's conclusion is wrong, assisted humans do *worse* than unassisted. So agents report **claims with evidence** (probe values, console lines, screenshots described, repro steps) for the human to adjudicate — never bare "works"/"broken". The unity-verify/unity-playtest report formats already comply; hold orchestrated agents to them.

## Plugin workflows

`${CLAUDE_PLUGIN_ROOT}/workflows/` ships ready-to-run scripts — invoke via `Workflow({scriptPath: "<plugin>/workflows/<name>.js", args: {...}})`, or copy into the project's `.claude/workflows/` to customize (they become repo-shared named workflows):

- **gamedev-review.js** — four review lenses, adversarial 3-vote verification per finding. Read-only: editor not required, fully parallel-safe. `/unity-kit:review`
- **playtest-sweep.js** — N planned scenarios, serial play sessions (TITAN shape: state probes → pre-filtered intent actions → stall-triggered reflection → bug oracles), overlapped analysis. Editor required + preflight above. `/unity-kit:qa-sweep`

## Team mode (agent teams / multiple sessions)

- **Exactly one teammate owns the editor.** Others work parallel-safe: code + tests headless (unity-ci skill), review, research, asset generation. Trading ownership is fine; sharing it is not.
- A second editor instance needs its own worktree (full `Library` import — minutes — plus disk) and MCP routing via `set_active_instance` per the multi-instance rules. Worth it for a long content lane; not for a quick fix.
- Keep authoring and review in separate lanes — the reviewer must not be the author's context.
- Coordinate through the shared task list with explicit blockers; integration merges happen in the lead session, phase-by-phase, verified (unity-verify) at each merge.
