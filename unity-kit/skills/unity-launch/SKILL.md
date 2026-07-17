---
name: unity-launch
description: Launch the Unity editor for a project and wait until the MCP for Unity bridge answers, or diagnose a dead unityMCP connection. Use when Unity isn't running, when unityMCP tools fail to connect, or before any editor work in a fresh session.
---

# unity-launch

## Launch

```powershell
& "${CLAUDE_PLUGIN_ROOT}/scripts/launch-unity.ps1" -ProjectPath "<absolute project path>"
```

- Resolves the correct editor from `ProjectSettings/ProjectVersion.txt` (pass `-UnityExe` to override).
- Blocks until the MCP bridge is ready or times out (default 900s — first imports are slow; don't shorten it for new projects). Readiness = the project's status file `~/.unity-mcp/unity-mcp-status-<hash>.json` reports `"reason":"ready"` with a post-launch heartbeat **and** its `unity_port` (typically **6400**) answers TCP. Legacy HTTP on `-Port` (default 8080) is only a fallback for old MCP for Unity builds.
- **Run it in the background** (`run_in_background`) — the script's timeout exceeds the harness's foreground tool cap.
- After it succeeds, poll `mcpforunity://editor/state` until the editor is idle (not compiling/updating) before issuing editor commands.

To list installed editors first: `& "${CLAUDE_PLUGIN_ROOT}/scripts/find-unity.ps1"` (JSON: version/channel/exe, newest first).

On macOS/Linux use the `.sh` counterparts of all three scripts (same arguments, positional: `launch-unity.sh <project-path> [unity-exe] [port] [timeout]`).

## Session has no unityMCP tools

The `unityMCP` server is registered **per project** in the Claude client config (`claude mcp list` to check). If the session started before the registration existed — always the case right after unity-init creates a project — `mcp__unityMCP__*` tools don't exist in the session even with the editor up and the bridge ready. Fix both ends:

1. Register for future sessions (from the project directory): `claude mcp add UnityMCP -- uvx --from mcpforunityserver mcp-for-unity`. The in-editor auto-setup does not do this for Claude Code by itself.
2. For the **current** session, drive the bridge through `${CLAUDE_PLUGIN_ROOT}/scripts/mcp-stdio-call.py`: put a JSON array of calls in a file (`{"type":"tool"|"resource"|"list_tools"|"list_resources", ...}`) and run `python mcp-stdio-call.py calls.json`. It speaks the full MCP protocol (tools, resources) over stdio to the same server.

## Multiple editor instances

All instances share one MCP server. If more than one project is open, calls error until routed: read `mcpforunity://instances`, then `set_active_instance` (instances are named `Name@hash`). Each open project also writes its own status file — match on `project_path`.

## Diagnosing a dead connection

In order:
1. **Does the project exist?** `ProjectSettings/ProjectVersion.txt` present? An empty or missing folder means there is nothing to launch — run unity-init first.
2. Is a Unity process running at all? (`tasklist /FI "IMAGENAME eq Unity.exe"`) — if not, launch as above.
3. Editor running — is the bridge up? Check `~/.unity-mcp/unity-mcp-status-*.json` for this project: fresh heartbeat + `"reason":"ready"` + its `unity_port` accepting TCP. A stale file (old heartbeat, or another project's path) is not evidence. If the file is stale/absent: in Unity, `Window → MCP for Unity` — check Connected / use **Restart Server**; verify auto-start is enabled.
4. Bridge ready but no `mcp__unityMCP__*` tools in the session → registration problem, not an editor problem — see "Session has no unityMCP tools".
5. Tools exist but error → domain reload in progress (bridge reconnects with backoff — wait a few seconds and retry once), or multi-instance routing (see above).
6. Editor version is an alpha/beta → mention that as a suspect when behavior is odd.
