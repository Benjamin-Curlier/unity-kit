---
description: Diagnose the Unity editor / MCP bridge connection
---

Diagnose the Unity ↔ Claude connection for the current project, per the unity-launch skill's checklist, in this order:

1. **Project exists?** `ProjectSettings/ProjectVersion.txt` present in the working directory? (An empty folder means unity-init never ran — report that as the root cause and stop the editor checks.)
2. **Unity process running?** Which editor version, and is it the project's version?
3. **Bridge up?** Check `~/.unity-mcp/unity-mcp-status-*.json` for a file matching this project's path: fresh heartbeat, `"reason":"ready"`, and its `unity_port` (typically 6400) answering TCP. Treat stale files or other projects' files as evidence of nothing.
4. **Server registered for this project?** Does the session have `mcp__unityMCP__*` tools; does `claude mcp list` (or `~/.claude.json`) show a UnityMCP entry for this project path? If the bridge is ready but tools are absent, the diagnosis is client-side registration, not the editor.
5. **Editor state readable and idle?** Read `mcpforunity://editor/state` (via the tools, or `scripts/mcp-stdio-call.py` when the session has no unityMCP tools) — `ready_for_tools` true, not compiling.
6. **Multiple instances?** `mcpforunity://instances` + `set_active_instance` if more than one editor is open.
7. **Tool groups**: which are active (`mcpforunity://tool-groups` or `manage_tools` `list_groups`)?

Fix what is fixable (launch the editor via the launch script, register the MCP server, suggest Restart Server), state exactly what you checked, and list any remaining manual steps for the user. When reading the console, ignore `MCP-FOR-UNITY` client-handler noise — it is not an error condition.

Also report the blender and elevenlabs MCP servers' reachability if audio/3D work is expected (for elevenlabs, an API probe that returns 401 means the `ELEVENLABS_API_KEY` is invalid — call that out explicitly).
