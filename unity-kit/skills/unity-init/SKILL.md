---
name: unity-init
description: Create and bootstrap a new Unity project from a game concept prompt, optionally with a design doc and existing asset files. Creates the project headlessly, stamps the Claude scaffold, sets up git, launches the editor, and finishes setup in-editor via MCP. Use when the user wants to start a new Unity project or game.
---

# unity-init — new Unity project from a prompt

Pipeline: concept → plan → **[user checkpoint]** → create → stamp files → git → launch editor → in-editor bootstrap via MCP → verify → report.

Plugin scripts referenced below live in `${CLAUDE_PLUGIN_ROOT}/scripts/`; file templates in `${CLAUDE_PLUGIN_ROOT}/templates/`.

## Phase 0 — Gather inputs & preflight

Collect (ask only for what's missing; the concept prompt is required):
- **Concept**: what game/app is this? Derive: 2D or 3D, art direction, first playable slice — and whether it's **multiplayer** (netcode model and hosting are day-one architecture, not a bolt-on).
- **Project name + parent directory** (default: sibling of the user's other Unity projects).
- **Optional design doc** (any local file) and **optional asset folder** (sprites, aseprite/PSD, models, audio).

Preflight (fail early with a clear message): `git --version`, `uv --version`, and `scripts/find-unity.ps1` (needs ≥1 installed editor).

## Phase 1 — Plan, then checkpoint with the user

1. Run `scripts/find-unity.ps1` → JSON list of editors with channels.
2. Pick the package set from `references/package-sets.md` (2D or 3D) based on the concept.
3. Draft the plan: editor version, 2D/3D, package list, folder layout, what the first scene will contain.
4. **AskUserQuestion checkpoint** — confirm before creating anything. *Fast path:* if the user has explicitly pre-decided every checkpoint item (editor version, package set, netcode model — e.g. an existing design doc names them, or the invocation pins them), state the choices in one line and proceed without asking; in an autonomous run a blocked question is worse than a logged decision.
   - Editor version: propose the **newest stable**. Never silently pick an alpha/beta; if only pre-release editors are installed, say so and make the user choose explicitly.
   - Show the package set and let them add/remove.
   - **Multiplayer**: if the concept is (or may become) multiplayer, settle it here — ECS/DOTS sim → `com.unity.netcode` (Netcode for Entities); GameObject sim → `com.unity.netcode.gameobjects`; never both. Add the multiplayer set from `references/package-sets.md` and load **unity-netcode-entities** before designing the first scene — client/server shapes the architecture from the first script.

## Phase 2 — Create the project

Run `scripts/new-project.ps1 -UnityExe <exe> -ProjectPath <path>`. This is headless and takes 1–2 minutes. Do not pre-write anything into the folder before this succeeds — the script refuses a **non-empty** existing path. (An existing *empty* directory is fine — the common case where the Claude session's working directory is the target folder and can't be deleted.)

## Phase 3 — Stamp the scaffold (before first real editor launch)

All from `templates/` (fill `{{PLACEHOLDERS}}`):
1. `CLAUDE.project.md` → `<project>/CLAUDE.md` — project facts (name, editor version, 2D/3D, pipeline, input system) + concept summary.
2. `settings.project.json` → `<project>/.claude/settings.json` — permission guard rails.
3. `project.gitignore` → `.gitignore`; `project.gitattributes` → `.gitattributes`.
4. `DESIGN.template.md` → `Docs/DESIGN.md` — expand the user's concept (and design doc, if given) into it. This becomes the source of truth future sessions read.
5. Edit `Packages/manifest.json`: add **only** `"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main"`. Do NOT hand-pin other package versions — they are editor-version-dependent and get installed in-editor in Phase 5.
6. Create plain `Assets/` folders: `Scripts`, `Scenes`, `Prefabs`, `Art`, `Audio`, `Tests` (Unity generates `.meta` files on import). Copy user-provided asset files into `Assets/Art/Source/` (or `Audio/`).
7. **Do not touch `ProjectSettings/*.asset` YAML** — settings changes happen in-editor via MCP.

## Phase 4 — Git

`git init` + `git add -A` + first commit ("chore: empty Unity <version> project + scaffold"). If `user.email` isn't configured, set it repo-locally after asking the user. Optionally register Unity's YAML merge driver:
`git config merge.unityyamlmerge.driver '"<editor>/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p %O %B %A %A'`

## Phase 5 — Launch & in-editor bootstrap

1. `scripts/launch-unity.ps1 -ProjectPath <path>` — waits for the MCP bridge (status file `~/.unity-mcp/unity-mcp-status-*.json` reporting `ready` + its TCP port answering); a fresh project's first import is slow (script default timeout 15 min — run it in the background, the harness's foreground tool timeout is shorter).
2. **Register the MCP server for the project** so sessions get native `mcp__unityMCP__*` tools: `claude mcp add UnityMCP -- uvx --from mcpforunityserver mcp-for-unity` (run from the project directory; the in-editor auto-setup does not do this for Claude Code by itself). The **current** session started before that registration, so it has no unityMCP tools — drive the bridge with `scripts/mcp-stdio-call.py` (batch JSON-RPC calls from a JSON file) for the rest of this phase.
3. Poll `mcpforunity://editor/state` until ready and not compiling. `read_console` must be clean before proceeding (ignore `MCP-FOR-UNITY` client-handler noise — see unity-verify).
4. **Install packages via MCP** `manage_packages` (`add_package`), one at a time from the chosen set (each install is an async job and can trigger recompile/domain reload — poll `editor/state` between installs; the bridge auto-reconnects). Unity resolves the right versions for the editor. Include `com.unity.test-framework` explicitly — it is not preinstalled in fresh 6000.5+ projects.
5. Render pipeline (URP sets): `manage_graphics` cannot *create* pipeline assets — use `execute_code` (activate `scripting_ext` via `manage_tools` first; the CodeDom fallback compiler is C# 6, write accordingly) to create the URP asset + 2D/3D renderer data, assign `GraphicsSettings.defaultRenderPipeline` and every quality level, then verify with `manage_graphics` `pipeline_get_info`. Set Active Input Handling to the new Input System if the input package was installed (`activeInputHandler = 1` on ProjectSettings via SerializedObject). This needs a **verified** editor restart: save via MCP, close the Unity process from the OS side (`EditorApplication.Exit` via `execute_code` is unreliable), **wait until the old PID is actually gone**, then relaunch via `unity-launch`. A second instance launched while the old one lives dies with "project already open" while the old instance's status-file heartbeats make it look like the relaunch worked. Afterwards verify the switch took: at runtime `UnityEngine.InputSystem.InputSystem.devices.Count > 0`. A half-switched editor (legacy `Input` throws, but zero devices / `Keyboard.current == null`) means the restart never happened — keyboard input will silently do nothing.
6. Assert **Api Compatibility Level = .NET Standard** for `NamedBuildTarget.Standalone` (and `NamedBuildTarget.Server` when the dedicated-server package is in) — standing user rule, all projects; 6000.5 empty projects already default to it, so this is a stamp, not a change: `PlayerSettings.SetApiCompatibilityLevel(t, ApiCompatibilityLevel.NET_Standard)`.
7. Create the first scene per the concept (camera, light if 3D, a placeholder player/board), an `InputActions` asset for the input scheme, and `Assets/Tests/EditMode` + `PlayMode` asmdefs. Save everything (`manage_scene` / `execute_menu_item` File/Save Project).
8. Run the **unity-verify** loop: console clean, a trivial EditMode test passes, screenshot of the Game view.

## Phase 6 — Final commit & report

Commit ("feat: bootstrap <concept> — packages, URP, input, first scene, tests"). Report: project path, editor version, packages installed, what's in the first scene, screenshot, and any step that was skipped or needs a manual follow-up (e.g., input-handling restart).

## Failure notes
- Missing build-support module (e.g. Dedicated Server for netcode projects — check `find-unity.ps1 -Modules`): the Hub installs it headlessly in ~1 min, no elevation, editor closed or open: `& "C:\Program Files\Unity Hub\Unity Hub.exe" -- --headless install-modules --version <ver> -m windows-server` (ids: `windows-server`, `linux-server`, `android`, `webgl`, …).
- `new-project.ps1` timeout → check the log path it prints; the editor version may be broken or licensing may need a Hub sign-in (interactive — hand to the user).
- MCP never answers after launch → editor is importing (wait), auto-start disabled (Window → MCP for Unity), or another instance needs routing (`mcpforunity://instances` + `set_active_instance`). Check the status file first: `~/.unity-mcp/unity-mcp-status-*.json` with a fresh heartbeat and `"reason":"ready"` means the bridge is fine and the problem is client-side (server not registered for the session — see Phase 5 step 2).
- Never delete a half-created project to retry without telling the user what and why.
