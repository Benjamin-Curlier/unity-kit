---
name: unity-scene
description: Work with Unity scenes, GameObjects, components, prefabs, and assets through the MCP for Unity tools. Use when creating or modifying scenes, hierarchies, prefabs, materials, tilemaps, or importing art in a Unity project.
---

# Scene, prefab & asset work via MCP

All scene-graph and asset manipulation goes through the MCP for Unity tools while the editor is running — not through text-editing YAML. The editor is the source of truth.

Main tools: `manage_scene` (scene CRUD), `find_gameobjects` (search, returns instance IDs), `manage_gameobject`, `manage_components` (add/remove components, set properties), `manage_prefabs`, `manage_asset`, `manage_material`, `execute_menu_item`. Read the `mcpforunity://scene/gameobject/{instance_id}` resource for object details. Wrap multi-step editor operations in `batch_execute` — it is 10–100x faster than one call at a time.

## Ground rules
- **Read before you write**: read `mcpforunity://editor/state` and query the hierarchy (`find_gameobjects`) before modifying; don't assume what exists.
- **Save explicitly**: after a batch of scene changes, save via `manage_scene` or `execute_menu_item` (`File/Save Project`). Unsaved scene changes are lost on editor crash or play-mode quirks.
- **Prefabs over loose objects**: anything instantiated more than once, or spawned at runtime, becomes a prefab under `Assets/Prefabs/`. Edit the prefab asset, not each instance; be deliberate about instance overrides.
- Create folders via `manage_asset` so `.meta` files are generated correctly; never delete a `.meta` without its asset.

## Typical flows

**New behavior on a new object**
1. Write the C# script (see `unity-csharp`) → run the `unity-verify` compile check.
2. Create the GameObject (`manage_gameobject`), add the component and set serialized fields (`manage_components`).
3. Save the scene, then play-mode smoke check (`unity-verify` §3).

**Art import**
- Drop source files (aseprite/PSD/PNG/FBX/glTF) under `Assets/Art/`, let the importer run, then check import settings via `manage_asset` (for sprites: PPU, filter mode, sprite mode) rather than hand-creating assets. For generated art, see the `unity-assets` skill.

**Wiring references**
- Set component references via `manage_components` using the target's path/instance ID — never by hand-editing scene YAML GUIDs.
- A `Missing` reference after a rename/move means a GUID broke: re-assign via MCP and check whether a `.meta` got regenerated.

## When MCP can't do it
If a needed operation has no dedicated MCP tool (rare — ~48 tools cover most of the editor), options in order:
1. `execute_menu_item` with the menu path (the `mcpforunity://menu-items` resource lists every available path).
2. `execute_code` — arbitrary editor C# (activate the `scripting_ext` group first via `manage_tools`). Powerful; keep snippets small and idempotent.
3. For a recurring gap, propose a project custom tool: a static Editor-folder class with `[McpForUnityTool("tool_name")]`, invoked via `execute_custom_tool` — this is how we extend the MCP instead of replacing it.
4. Ask the user to do the one manual step in the editor, stating exactly what to click.
Text-editing `.unity`/`.prefab` YAML is the last resort, and only for trivial, additive changes with the scene closed.
