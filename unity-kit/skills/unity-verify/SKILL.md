---
name: unity-verify
description: Verify Unity work end-to-end — wait for recompile, read console errors, run tests, optionally play-mode smoke test and screenshot. Use after editing any C# script, scene, prefab, or asset in a Unity project, and before reporting any Unity change as done.
---

# Unity verify loop

Requires the Unity editor running with MCP for Unity connected (`unityMCP` server). If tools fail, use the **unity-launch** skill first — do not claim verification happened.

## 1. Compile check (always, after any .cs change)

1. **Do not manually refresh.** Unity auto-compiles on file changes. Poll the `mcpforunity://editor/state` resource until `is_compiling` is false. (Use `refresh_unity` only if the editor hasn't noticed external changes at all.)
2. Call `read_console` with `types: ["error"]` — any compile error is a hard stop: fix it and re-check before anything else. One error frequently masks the next, so re-read after each fix. **Filter out MCP plumbing noise first**: entries containing `MCP-FOR-UNITY` (e.g. "Client handler exited", "[IO] ✗ write FAIL … ObjectDisposedException") are logged when MCP clients disconnect during domain reloads — they are not compile errors and must not fail the check.
3. Then `read_console` with `types: ["warning"]` — fix new warnings you introduced if trivial, otherwise report them.
4. `validate_script` can lint a single script before waiting on a full compile.

## 2. Tests (when behavior changed)

1. Activate the testing tools once per session: `manage_tools` with `action: "activate"`, group `testing`.
2. `run_tests` with `mode: "EditMode"` for pure-logic changes; `mode: "PlayMode"` for GameObject lifecycle, physics, scenes, or input. It returns a `job_id` — poll `get_test_job` (use `wait_timeout` of 30–60s; PlayMode runs need generous timeouts, and an unfocused editor can stall — the tool nudges focus automatically, just be patient before declaring a hang). **Orphaned-run signature**: a job frozen at `completed=0` with a stale `last_update` while the editor is back in *edit* mode with an `InitTestScene` open means the Test Framework itself faulted (e.g. an internal `PlayModeRunTask` NRE after soured runner state) and the job will never finish — it is not a slow test. Recover: reload the real scene, re-run the tests; if it repeats, restart the editor. Delete any `Assets/InitTestScene*`/`Assets/_Recovery*` leftovers such episodes strand (with their .metas) before committing.
3. Bug fixes are **test-first**: write the failing repro test under `Assets/Tests/EditMode` or `PlayMode` *before* touching the fix, watch it fail, then fix until green (create the folders + asmdefs referencing the code under test on first use).
4. A failing test after your change is your problem even if it "looks unrelated" — investigate before dismissing.

## 3. Runtime smoke check (when a scene/feature should visibly work)

For anything input-driven ("does the keyboard actually steer the player?"), use the **unity-playtest** skill — a smoke check here does not exercise the input path, and live key simulation has editor-focus pitfalls that skill documents.

1. `manage_editor` with `action: "play"`, let it run a few seconds, `read_console` for exceptions, then `manage_editor` `action: "stop"`. **Never leave play mode running.**
2. For visual changes, verify with your eyes: `manage_camera` with the `screenshot` action (`capture_source: "game_view"`, `include_image: true`) and inspect the image.
3. Play mode and recompiles cause domain reloads — the bridge auto-reconnects with backoff; if a call drops mid-verify, retry once before concluding it's down.

## Bounded fixing — no thrashing

Cap fix→recheck cycles at **3 per issue**. If the same error text comes back unchanged twice, the current strategy is wrong — stop iterating, and either change approach deliberately or report the state (error verbatim, what was tried, current hypothesis) and ask. An honest "stuck after 3 attempts" report beats ten silent mutations of the same file.

## Reporting

State exactly what was verified: "console clean, 4/4 EditMode tests pass, play-mode smoke OK, screenshot checked" — not just "done". If any step was skipped (editor closed, tests missing), say so explicitly.
