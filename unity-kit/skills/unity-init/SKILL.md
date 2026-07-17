---
name: unity-init
description: Create and bootstrap a new Unity project from a game concept prompt, optionally with a design doc and existing asset files. Creates the project headlessly, stamps the Claude scaffold, sets up git, launches the editor, and finishes setup in-editor via MCP. Use when the user wants to start a new Unity project or game.
---

# unity-init — new Unity project from a prompt

Pipeline: concept → plan → **[user checkpoint]** → create → stamp files → git → launch editor → in-editor bootstrap via MCP → verify → report.

Plugin scripts referenced below live in `${CLAUDE_PLUGIN_ROOT}/scripts/`; file templates in `${CLAUDE_PLUGIN_ROOT}/templates/`.

## Phase 0 — Gather inputs & preflight

Collect (ask only for what's missing; the concept prompt is required):
- **Concept**: what game/app is this? Derive: 2D or 3D, art direction, first playable slice.
- **Project name + parent directory** (default: sibling of the user's other Unity projects).
- **Optional design doc** (any local file) and **optional asset folder** (sprites, aseprite/PSD, models, audio).

Preflight (fail early with a clear message): `git --version`, `uv --version`, and `scripts/find-unity.ps1` (needs ≥1 installed editor).

## Phase 1 — Plan, then checkpoint with the user

1. Run `scripts/find-unity.ps1` → JSON list of editors with channels.
2. Pick the package set from `references/package-sets.md` (2D or 3D) based on the concept.
3. Draft the plan: editor version, 2D/3D, package list, folder layout, what the first scene will contain.
4. **AskUserQuestion checkpoint** — confirm before creating anything:
   - Editor version: propose the **newest stable**. Never silently pick an alpha/beta; if only pre-release editors are installed, say so and make the user choose explicitly.
   - Show the package set and let them add/remove.

## Phase 2 — Create the project

Run `scripts/new-project.ps1 -UnityExe <exe> -ProjectPath <path>`. This is headless and takes 1–2 minutes. Do not pre-write anything into the folder before this succeeds — the script refuses to overwrite an existing path.

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

1. `scripts/launch-unity.ps1 -ProjectPath <path>` — waits for the MCP port; a fresh project's first import is slow (script default timeout 15 min).
2. Poll `mcpforunity://editor/state` until ready and not compiling. `read_console` must be clean before proceeding.
3. **Install packages via MCP** `manage_packages`, one at a time from the chosen set (each install can trigger recompile/domain reload — poll `editor/state` between installs; the bridge auto-reconnects). Unity resolves the right versions for the editor.
4. Render pipeline (URP sets): create + assign the URP asset (2D Renderer for 2D) via `manage_graphics` if it supports it, else `execute_code` (activate `scripting_ext` via `manage_tools` first). Set Active Input Handling to the new Input System if the input package was installed (needs editor restart — relaunch via `unity-launch` when prompted).
5. Create the first scene per the concept (camera, light if 3D, a placeholder player/board), an `InputActions` asset for the input scheme, and `Assets/Tests/EditMode` + `PlayMode` asmdefs. Save everything (`manage_scene` / `execute_menu_item` File/Save Project).
6. Run the **unity-verify** loop: console clean, a trivial EditMode test passes, screenshot of the Game view.

## Phase 6 — Final commit & report

Commit ("feat: bootstrap <concept> — packages, URP, input, first scene, tests"). Report: project path, editor version, packages installed, what's in the first scene, screenshot, and any step that was skipped or needs a manual follow-up (e.g., input-handling restart).

## Failure notes
- `new-project.ps1` timeout → check the log path it prints; the editor version may be broken or licensing may need a Hub sign-in (interactive — hand to the user).
- MCP never answers after launch → editor is importing (wait), auto-start disabled (Window → MCP for Unity), or port conflict (another instance: use `mcpforunity://instances` + `set_active_instance`).
- Never delete a half-created project to retry without telling the user what and why.
