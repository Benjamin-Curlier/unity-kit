# unity-kit

A Claude Code plugin for Unity development. Philosophy: **integrate the mature MCPs, don't rebuild them** — this plugin wraps [MCP for Unity](https://github.com/CoplayDev/unity-mcp) (CoplayDev) and [blender-mcp](https://github.com/ahujasid/blender-mcp) (ahujasid) with the workflow layer that makes Claude effective in a Unity project.

## What's inside

| Piece | Purpose |
|---|---|
| `skills/unity-init` | Create + bootstrap a new Unity project from a concept prompt (headless create → scaffold → git → launch → in-editor setup via MCP) |
| `skills/unity-launch` | Launch the right editor for a project and wait for the MCP bridge; connection diagnosis |
| `skills/unity-verify` | The verify loop: compile → console → tests → play-mode smoke → screenshot |
| `skills/unity-csharp` | Unity C# conventions and pitfalls (serialization, lifecycle, Input System, URP) |
| `skills/unity-scene` | Scene/GameObject/prefab/asset work through MCP tools; custom-tool extension path |
| `skills/unity-build` | Player builds via `manage_build`, with headless CLI fallback |
| `skills/unity-assets` | Asset generation: Unity `asset_gen` tools + the Blender pipeline (PolyHaven/Hyper3D/Sketchfab) |
| `agents/unity-runner` | Subagent that runs verification and reports pass/fail concisely |
| `hooks/` | SessionStart: is Unity running? · PostToolUse: verify reminder after `.cs` edits (both silent outside Unity projects) |
| `scripts/` | `find-unity.ps1`, `new-project.ps1`, `launch-unity.ps1` (Windows/PowerShell) |
| `templates/` | Per-project files stamped by unity-init (CLAUDE.md, settings, gitignore/attributes, DESIGN.md) |
| `.mcp.json` | Registers `unityMCP` (HTTP, localhost:8080) and `blender` (uvx blender-mcp, telemetry off) |

## Requirements

- Windows, Unity Hub with at least one editor installed (2021.3 LTS – 6.x; MCP for Unity supports these)
- Python 3.10+ and `uv` on PATH (MCP servers + hooks)
- `git`
- Optional: Blender 3.0+ with the [blender-mcp addon](https://github.com/ahujasid/blender-mcp) installed and connected

## Install

```
claude plugin marketplace add C:\Users\bencu\claude-plugins
claude plugin install unity-kit@bencu-plugins
```

## Per-project setup

- New project: just ask for one — the `unity-init` skill drives the whole pipeline (it checkpoints editor version + packages with you before creating).
- Existing project: run unity-init's Phase 3 only (stamp CLAUDE.md/settings/git files, add the MCP package to `Packages/manifest.json`).

## API keys (asset generation)

All keys are bring-your-own and entered **by you, in the tools' own UIs** — never in chat, never in config files:
- Unity: `Window → MCP for Unity → Asset Gen` tab (fal.ai/OpenRouter, Tripo/Meshy, Sketchfab)
- Blender: addon preferences, or `BLENDERMCP_*` environment variables

## Known caveats

- The Unity editor must be open for `unityMCP` tools; the SessionStart hook tells Claude whether it is.
- Don't install Unity AI Assistant alongside MCP for Unity (DLL conflict on Unity 6.3+).
- Multiple open editors share one MCP server — route with `set_active_instance`.
