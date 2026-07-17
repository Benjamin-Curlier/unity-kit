---
name: unity-assets
description: Generate or import game assets — images/sprites, 3D models, audio — using MCP for Unity's asset_gen tools or the Blender MCP pipeline (PolyHaven, Hyper3D, Sketchfab). Use when the user wants art, models, textures, HDRIs, or audio created or brought into a Unity project.
---

# unity-assets — generation & import pipelines

## API keys — hard boundary
Both pipelines use bring-your-own keys. **The user enters keys themselves**:
- Unity asset-gen: `Window → MCP for Unity → Asset Gen` tab (fal.ai / OpenRouter for images, Tripo / Meshy for models, Sketchfab for library import).
- Blender addon: `Edit → Preferences → Add-ons → Blender MCP` (Sketchfab / Hyper3D / Hunyuan3D), or env vars like `BLENDERMCP_SKETCHFAB_API_KEY`.

Never ask the user to paste a key into chat, never write keys into config files, never enter keys into fields for them. If a generation tool fails with an auth error, tell the user which tab needs a key and stop there. PolyHaven (textures/models/HDRIs) is free and needs no key.

## Pipeline A — inside Unity (asset_gen group)

1. Activate once per session: `manage_tools` `action: "activate"`, group `asset_gen`.
2. Tools: `generate_image` (sprites, textures, concept art), `generate_model` (text/image → 3D), `generate_audio` (SFX/music; newer servers only), `import_model` (Sketchfab search+import), `import_model_file` (local FBX/OBJ/glTF).
3. Generated assets land in the project — then fix import settings via `manage_asset`: sprites need PPU consistent with the project, point filtering for pixel art; models need scale/materials checked.
4. Generation is slow and costs the user money per call — batch thoughtfully, show the result (screenshot or image) before generating variants.

## Pipeline B — Blender (blender MCP server)

Requires: Blender 3.0+ running, the blender-mcp addon installed and **Connect to Claude** clicked in the 3D View sidebar (N-panel → BlenderMCP tab). If blender tools fail, that connection is the first suspect — tell the user exactly those steps.

- Good for: real modeling work (parametric edits, modifiers, UV, materials), free PolyHaven assets, Hyper3D/Hunyuan3D generation, Sketchfab downloads, then export to Unity.
- The server exposes scene inspection, object/material creation, viewport screenshots, and `execute_blender_code` (arbitrary Python). Keep code snippets small and incremental; screenshot the viewport to verify each significant step visually.
- **Export to Unity**: FBX or glTF into the project's `Assets/Art/Models/` (export via Blender code: `bpy.ops.export_scene.fbx(filepath=..., use_selection=True)`), then let Unity import and check settings via `manage_asset`. Unity applies a 90° X-rotation fix for Blender FBX automatically; verify scale (Blender meters vs Unity units) on first import.
- Blender-mcp and Unity-mcp don't talk to each other — the filesystem (the Assets folder) is the handoff point.

## Choosing
- 2D sprite/texture → Pipeline A `generate_image`.
- Stock/realistic props, HDRIs → Blender PolyHaven (free) or Sketchfab.
- Custom 3D that needs editing after generation → Blender (generate, refine, export).
- Quick one-shot 3D placeholder → Pipeline A `generate_model`.
