---
description: Multi-lens gamedev review — correctness, Unity pitfalls, performance, design alignment — every finding adversarially verified
argument-hint: [optional scope hint — a path, "the diff vs main", "the last 3 commits"]
---

Review this Unity project with the plugin's gamedev-review workflow (read-only; the editor does not need to be open):

Invoke the Workflow tool with `scriptPath: "${CLAUDE_PLUGIN_ROOT}/workflows/gamedev-review.js"` and `args: {scope: "$ARGUMENTS"}` (omit scope if no arguments were given). It scopes the change surface, reviews through four lenses in parallel, adversarially verifies every finding with three refutation votes, and synthesizes a claims-with-evidence report.

If the Workflow tool is unavailable in this session (older CLI or plan), run the same four phases sequentially with the Agent tool instead — one Explore-style scope agent, four lens reviewers, three verify votes per surviving finding, then synthesize yourself. The phase structure is what matters, not the runtime (see the agentic-workflows skill).

Relay the final report to the user as-is: findings are claims with evidence for the user to adjudicate, ordered by severity, with the refuted-along-the-way list at the end. Do not start fixing anything unless the user asks.
