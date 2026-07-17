# {{PROJECT_NAME}} — Claude Instructions

## Project facts
- Unity **{{UNITY_VERSION}}** — {{DIMENSION}} project on **{{PIPELINE}}**
- Input: **new Input System** — never use the legacy `Input.*` API
- Concept: {{CONCEPT_ONE_LINER}} — full design in `Docs/DESIGN.md` (read it before feature work)
- Tests: `Assets/Tests/EditMode` and `Assets/Tests/PlayMode`
- **MCP for Unity** is installed; the editor hosts a TCP bridge (port in `~/.unity-mcp/unity-mcp-status-*.json`, typically 6400) and the `unityMCP` stdio server (`uvx --from mcpforunityserver mcp-for-unity`) connects Claude to it. The editor must be **open** for editor tools — use the `unity-launch` skill if not. If `mcp__unityMCP__*` tools are missing from the session, the server wasn't registered when the session started — see unity-launch's "Session has no unityMCP tools" section.

## MCP usage rules
- **Resource-first**: read `mcpforunity://editor/state` before acting; after script edits, poll it until `is_compiling` is false instead of manually refreshing, then `read_console`.
- Only the `core` tool group is on by default — activate `testing`, `scripting_ext`, `asset_gen`, etc. via `manage_tools` when needed.
- Prefer `batch_execute` for multi-step editor operations.

## Hard rules
- **Never** create or edit files under `Library/`, `Temp/`, `obj/`, `Logs/`, or `UserSettings/` (enforced by permission rules).
- **Never** hand-edit `.meta` files unless explicitly asked; never delete a `.meta` without its asset. Move assets via MCP so GUIDs survive.
- Scene/prefab files are YAML — edit through MCP editor tools, not text edits.
- Don't install Unity AI Assistant (DLL conflict with MCP for Unity).

## Verify loop (after any C# or asset change)
Use the `unity-verify` skill: console clean → relevant tests green → play-mode smoke when behavior should visibly work. Only report done after verification, and say exactly what was verified.
