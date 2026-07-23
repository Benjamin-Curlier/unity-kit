# unity-kit

A Claude Code plugin for Unity development. Philosophy: **integrate the mature MCPs, don't rebuild them** — this plugin wraps [MCP for Unity](https://github.com/CoplayDev/unity-mcp) (CoplayDev) and [blender-mcp](https://github.com/ahujasid/blender-mcp) (ahujasid) with the workflow layer that makes Claude effective in a Unity project.

## What's inside

**Skills** (knowledge, loaded on demand into the working context):

| Skill | Purpose |
|---|---|
| `unity-init` | Create + bootstrap a new Unity project from a concept prompt (headless create → scaffold → git → launch → in-editor setup via MCP) |
| `unity-launch` | Launch the right editor for a project and wait for the MCP bridge; connection diagnosis |
| `unity-verify` | The verify loop: compile → console → tests → play-mode smoke → screenshot |
| `unity-playtest` | Prove the game plays: InputTestFixture input-path tests, live state probing, screenshot checkpoints |
| `unity-csharp` | Unity C# conventions and pitfalls (serialization, lifecycle, Input System, URP, 2D/3D) |
| `unity-scene` | Scene/GameObject/prefab/asset work through MCP tools; custom-tool extension path |
| `unity-animation` | Animator state machines, blend trees, 2D frame/rig pipelines, animation events, tweening |
| `unity-dots` | ECS + Burst + Jobs — when DOTS is (and isn't) worth it, and how to write it correctly |
| `unity-dots-migration` | GameObject→ECS conversion: the gate, migration order, hybrid view bridges, and the 2D-rendering / SubScene-stripping traps |
| `unity-netcode-entities` | Netcode for Entities: worlds, tick rates, ghosts, prediction, input commands, dedicated servers, and the thin-client/latency-sim test loop |
| `unity-geo-maps` | Real-world map data as game worlds: provider licensing (Google/Mapbox walls, OSM/ODbL, DEMs), offline bake pipeline, projections, multiplayer map determinism |
| `unity-packages` | Official registry via `manage_packages`, OpenUPM scoped registries, git-URL packages, vetting |
| `unity-build` | Player builds via `manage_build`, with headless CLI fallback |
| `unity-assets` | Asset generation: Unity `asset_gen` tools + the Blender pipeline (PolyHaven/Hyper3D/Sketchfab) |
| `unity-audio` | AudioSources, Mixer routing, pooling, music systems + generation pipelines (ElevenLabs, `generate_audio`, CC0 packs) |
| `game-design` | Core-loop-first method, scope discipline, first playable slice, juice checklist, playtesting |
| `gamedev-patterns` | State machines, pooling, ScriptableObject event channels, save systems, scene architecture |

**Commands** (explicit workflow entry points):

| Command | Does |
|---|---|
| `/unity-kit:new <concept>` | Full unity-init pipeline (with editor-version + package checkpoint) |
| `/unity-kit:fix <bug>` | Test-first bug fix: failing repro test → fix → full verify |
| `/unity-kit:ship [target]` | Verify loop as a hard gate, then player build |
| `/unity-kit:doctor` | Diagnose editor/MCP-bridge connection and tool-group state |
| `/unity-kit:playtest` | Playtest the game: input-path tests, live session probing, screenshots |

**Agents** (separate execution contexts for verbose work with short conclusions):

| Agent | Purpose |
|---|---|
| `unity-runner` | Runs verification (compile/console/tests/play-mode) and reports pass/fail concisely |
| `unity-docs-researcher` | Version-correct Unity API/docs/package research (uses `unity_reflect` when the editor is up) |
| `asset-scout` | License-checked asset shortlists from PolyHaven/Kenney/Sketchfab/OpenGameArt/itch.io |
| `blender-modeler` | Multi-step Blender modeling sessions with screenshot verification, exporting FBX/glTF into Assets |

**Infrastructure**:

| Piece | Purpose |
|---|---|
| `hooks/` | SessionStart: is Unity running? · PostToolUse: verify reminder after `.cs` edits (both silent outside Unity projects) |
| `scripts/` | `find-unity`, `new-project`, `launch-unity` — `.ps1` (Windows) and `.sh` (macOS/Linux) variants |
| `templates/` | Per-project files stamped by unity-init (CLAUDE.md, settings, gitignore/attributes, DESIGN.md) |
| `.mcp.json` | Registers `unityMCP` (HTTP, localhost:8080), `blender` (uvx blender-mcp, telemetry off), and `elevenlabs` (uvx elevenlabs-mcp, key via `${ELEVENLABS_API_KEY}`) |

There is deliberately no "2D agent" / "3D agent" / "workflow master": domain knowledge lives in skills loaded into the main working context, and orchestration is the main conversation's job — agents exist only where verbose work compresses to a short report.

## Requirements

- Windows, Unity Hub with at least one editor installed (2021.3 LTS – 6.x; MCP for Unity supports these)
- Python 3.10+ and `uv` on PATH (MCP servers + hooks)
- `git`
- Optional: Blender 3.0+ with the [blender-mcp addon](https://github.com/ahujasid/blender-mcp) installed and connected

## Install

From GitHub:

```
claude plugin marketplace add Benjamin-Curlier/unity-kit
claude plugin install unity-kit@bencu-plugins
```

For local development of the plugin itself, point the marketplace at your clone instead:

```
claude plugin marketplace add C:\path\to\unity-kit-repo
```

Release zips (see GitHub Releases): `unity-kit-plugin.zip` (the plugin, for manual installs) and `unity-project-scaffold.zip` (drop into an **existing** Unity project folder — contains CLAUDE.md, permission settings, git files, design-doc template, plus a README with the two manual steps).

## Per-project setup

- New project: just ask for one — the `unity-init` skill drives the whole pipeline (it checkpoints editor version + packages with you before creating).
- Existing project: run unity-init's Phase 3 only (stamp CLAUDE.md/settings/git files, add the MCP package to `Packages/manifest.json`).

## API keys (asset & audio generation)

All keys are bring-your-own and entered **by you** — never in chat, never written into config files:
- Unity: `Window → MCP for Unity → Asset Gen` tab (fal.ai for images/models/audio, Tripo/Meshy, Sketchfab) — stored in the OS secure store
- Blender: addon preferences, or `BLENDERMCP_*` environment variables
- ElevenLabs: set `ELEVENLABS_API_KEY` in your OS environment — the plugin's `.mcp.json` only references `${ELEVENLABS_API_KEY}`. Without it, the `elevenlabs` server simply shows as unavailable. **Licensing:** the ElevenLabs free tier is non-commercial and requires attribution; any paid plan includes commercial use — check before shipping generated audio in a commercial game.

## Known caveats

- The Unity editor must be open for `unityMCP` tools; the SessionStart hook tells Claude whether it is.
- Don't install Unity AI Assistant alongside MCP for Unity (DLL conflict on Unity 6.3+).
- Multiple open editors share one MCP server — route with `set_active_instance`.
- The `.sh` scripts are untested on real macOS/Linux machines (authored on Windows, syntax-checked only) — issues welcome.
- If a `uvx`-launched server fails to start on Windows, ensure `uv` is on PATH; as a last resort wrap the command as `cmd /c uvx …` in a project-level `.mcp.json` override.
- Blender server choice (2026-07): ahujasid/blender-mcp (24k★, active, PolyHaven/Sketchfab/Hyper3D built in) over the official Blender Lab MCP server (Blender 5.1+, no asset-library integrations yet) — revisit when the official server gains asset sourcing.
- Audio landscape (2026-07): there is no "blender-mcp of audio" — DAW-control MCPs are immature (best: Audacity-MCP, 52★, Audacity 3.x only). ElevenLabs official MCP + unity-mcp's `generate_audio` (stable since v10.1.0) + CC0 packs cover the pipeline instead. Suno wrappers deliberately skipped (unofficial APIs, litigation, subscription-bound rights).

## Prior art & positioning (surveyed 2026-07)

No maintained, marketplace-installable plugin combines project init + verify loop + conventions + scene/asset work on top of CoplayDev's unity-mcp; the official Anthropic plugin marketplaces contain no gamedev entries. Ideas adopted from the ecosystem: scene/prefab text-edit guards and bounded verify-fix loops (everything-claude-unity), test-first bug fixing (nowsprinting/unity-coding-skills). Roadmap candidates: a router that loads skills based on detected project packages (awesome-gamedev-agent-skills pattern), bundling a version-pinned Unity API docs MCP, and a submission to the community marketplace.

## License

MIT — see [LICENSE](../LICENSE). The wrapped projects have their own licenses: MCP for Unity (MIT, CoplayDev) and blender-mcp (MIT, ahujasid).
