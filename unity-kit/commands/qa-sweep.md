---
description: Multi-scenario playtest sweep — planned scenarios, serial play sessions with bug oracles, claims-with-evidence report
argument-hint: [scenario count (default 5)] [optional focus, e.g. "the new enemy AI"]
---

Playtest this project across multiple planned scenarios with the plugin's playtest-sweep workflow.

Preflight (agentic-workflows skill, unattended-run section):
1. Editor open with the MCP bridge answering (`mcpforunity://editor/state`) — use unity-launch if not.
2. This run drives the editor for many minutes: check the project allowlist covers `manage_editor`, `execute_code`, `run_tests`, `manage_tools`, `manage_camera` (and `manage_scene` if scenarios span scenes); otherwise the serial play phase stalls on prompts. Tell the user which grants are missing before launching.

Then invoke the Workflow tool with `scriptPath: "${CLAUDE_PLUGIN_ROOT}/workflows/playtest-sweep.js"` and `args: {count: <N from $ARGUMENTS, default 5>, focus: "<rest of $ARGUMENTS if any>"}`. Play sessions are serial by construction (one editor, one driver); analysis overlaps.

If the Workflow tool is unavailable, degrade to: plan scenarios yourself from Docs/DESIGN.md (mix happy path, illegal input, end-state, and the design's open questions), then run them one at a time via the `unity-kit:playtest-qa` agent, then synthesize.

Relay the report as claims with evidence for the user to adjudicate — including what was NOT covered. Verify play mode ended stopped; if any session reported otherwise, say so first.
