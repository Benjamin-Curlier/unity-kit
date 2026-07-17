---
name: unity-launch
description: Launch the Unity editor for a project and wait until the MCP for Unity bridge answers, or diagnose a dead unityMCP connection. Use when Unity isn't running, when unityMCP tools fail to connect, or before any editor work in a fresh session.
---

# unity-launch

## Launch

```powershell
& "${CLAUDE_PLUGIN_ROOT}/scripts/launch-unity.ps1" -ProjectPath "<absolute project path>"
```

- Resolves the correct editor from `ProjectSettings/ProjectVersion.txt` (pass `-UnityExe` to override, `-Port` if the project uses a non-default MCP port).
- Blocks until the MCP server answers on the port (default 8080) or times out (default 900s — first imports are slow; don't shorten it for new projects).
- Run it in the background (`run_in_background`) if you have other work meanwhile.
- After it succeeds, poll `mcpforunity://editor/state` until the editor is idle (not compiling/updating) before issuing editor commands.

To list installed editors first: `& "${CLAUDE_PLUGIN_ROOT}/scripts/find-unity.ps1"` (JSON: version/channel/exe, newest first).

## Multiple editor instances

All instances share one MCP server. If more than one project is open, calls error until routed: read `mcpforunity://instances`, then `set_active_instance` (instances are named `Name@hash`). The launch script warns when the port was already up before launching.

## Diagnosing a dead connection

In order:
1. Is a Unity process running at all? (`tasklist /FI "IMAGENAME eq Unity.exe"`) — if not, launch as above.
2. Editor running but port closed → in Unity: `Window → MCP for Unity` — check Connected / use **Restart Server**; verify auto-start is enabled.
3. Port open but tools error → domain reload in progress (bridge reconnects with backoff — wait a few seconds and retry once), or multi-instance routing (see above).
4. Editor version is an alpha/beta → mention that as a suspect when behavior is odd.
