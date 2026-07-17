---
name: unity-verify
description: Verify Unity work end-to-end — wait for recompile, read console errors, run tests, optionally play-mode smoke test and screenshot. Use after editing any C# script, scene, prefab, or asset in a Unity project, and before reporting any Unity change as done.
---

# Unity verify loop

Requires the Unity editor running with MCP for Unity connected (`unityMCP` server). If tools fail, use the **unity-launch** skill first — do not claim verification happened.

## 1. Compile check (always, after any .cs change)

1. **Do not manually refresh.** Unity auto-compiles on file changes. Poll the `mcpforunity://editor/state` resource until `is_compiling` is false. (Use `refresh_unity` only if the editor hasn't noticed external changes at all.)
2. Call `read_console` with `types: ["error"]` — any compile error is a hard stop: fix it and re-check before anything else. One error frequently masks the next, so re-read after each fix.
3. Then `read_console` with `types: ["warning"]` — fix new warnings you introduced if trivial, otherwise report them.
4. `validate_script` can lint a single script before waiting on a full compile.

## 2. Tests (when behavior changed)

1. Activate the testing tools once per session: `manage_tools` with `action: "activate"`, group `testing`.
2. `run_tests` with `mode: "EditMode"` for pure-logic changes; `mode: "PlayMode"` for GameObject lifecycle, physics, scenes, or input. It returns a `job_id` — poll `get_test_job` (use `wait_timeout` of 30–60s; PlayMode runs need generous timeouts, and an unfocused editor can stall — the tool nudges focus automatically, just be patient before declaring a hang).
3. If no test covers a bug you're fixing, add one under `Assets/Tests/EditMode` or `Assets/Tests/PlayMode` (create the folders + asmdefs referencing the code under test on first use).
4. A failing test after your change is your problem even if it "looks unrelated" — investigate before dismissing.

## 3. Runtime smoke check (when a scene/feature should visibly work)

1. `manage_editor` with `action: "play"`, let it run a few seconds, `read_console` for exceptions, then `manage_editor` `action: "stop"`. **Never leave play mode running.**
2. For visual changes, verify with your eyes: `manage_camera` with the `screenshot` action (`capture_source: "game_view"`, `include_image: true`) and inspect the image.
3. Play mode and recompiles cause domain reloads — the bridge auto-reconnects with backoff; if a call drops mid-verify, retry once before concluding it's down.

## Reporting

State exactly what was verified: "console clean, 4/4 EditMode tests pass, play-mode smoke OK, screenshot checked" — not just "done". If any step was skipped (editor closed, tests missing), say so explicitly.
