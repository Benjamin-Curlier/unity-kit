---
description: Multi-scenario playtest sweep — planned scenarios, serial play sessions with bug oracles, claims-with-evidence report
argument-hint: "[scenario count (default 5)] [optional focus, e.g. 'the new enemy AI']"
---

Playtest this project across multiple planned scenarios with the plugin's playtest-sweep workflow.

**Trust gate:** the play sessions run the project's own C# with your full OS privileges (`run_tests`/`execute_code` are not sandboxed). Run this only on a project whose contents the user trusts — for unknown or third-party code, tell them to sandbox in a VM/container instead of proceeding.

Preflight (agentic-workflows skill, unattended-run section):
1. Editor open with the MCP bridge answering (`mcpforunity://editor/state`) — use unity-launch if not.
2. This run drives the editor for many minutes: check the project allowlist covers `manage_editor`, `execute_code`, `manage_camera` (and `manage_scene` if scenarios span scenes) — **using the exact server prefix this session's unityMCP tools actually have** (`mcp__unityMCP__*`, `mcp__UnityMCP__*`, or `mcp__plugin_unity-kit_unityMCP__*` depending on how the bridge was registered; a wrong-prefix rule matches nothing). Otherwise the serial play phase stalls on prompts. Tell the user which grants are missing before launching.
3. **Hands off the editor:** warn the user that the run owns the editor — human clicks mid-run taint the evidence, and unsaved scene changes may block sessions (agents refuse to discard them and abort as evidence-tainted). Ask them to save and step away for the duration.
4. **Editor ownership (advisory):** claim it per the agentic-workflows skill — write `~/.unity-mcp/claude-editor-owner-<project>.json` with this session's id, touch it at phase boundaries, delete it when the run ends. If a fresh file from a foreign session already exists, do not launch; surface it to the user.
5. **Multi-instance:** read `mcpforunity://instances`; if more than one editor is connected, pick this project's `Name@hash` and pass it as `args.instance` below — the play agents then pin `unity_instance` on every call and never touch `set_active_instance`.

Then invoke the Workflow tool with `scriptPath: "${CLAUDE_PLUGIN_ROOT}/workflows/playtest-sweep.js"` and `args: {count: <N from $ARGUMENTS, default 5>, focus: "<rest of $ARGUMENTS if any>", instance: "<Name@hash if step 5 found several>"}`. If `${CLAUDE_PLUGIN_ROOT}` arrives unexpanded (you see the literal variable text), the scripts live in the plugin cache — glob for `workflows/playtest-sweep.js` under `~/.claude/plugins/` and use that absolute path. If this Claude Code version doesn't support invoking a script by path, copy the script into the project's `.claude/workflows/` and run it as a named workflow instead. Play sessions are serial by construction (one editor, one driver); analysis overlaps. Run artifacts (plan, raw probe/console JSONL, screenshots, evidence, report) persist under `Docs/playtest-runs/<runId>/`.

If the Workflow tool is unavailable entirely, degrade to a **reduced** run: plan scenarios yourself from Docs/DESIGN.md (mix happy path, illegal input, end-state, and the design's open questions), cap at ~3 scenarios via the `unity-kit:playtest-qa` agent one at a time, then synthesize. State the approximate agent count and get the user's go-ahead before starting, and label the result as a reduced sweep — it has not been validated to the workflow's standard.

Wrap-up — always, including on abort or plan-limit death: check `mcpforunity://editor/state`; if play mode is still running, stop it (`manage_editor` "stop") before anything else. Then relay the report as claims with evidence for the user to adjudicate — including what was NOT covered, any budget-skipped scenarios, and any evidence-tainted sessions first. Verify play mode was stopped; if any session reported otherwise, say so first.
