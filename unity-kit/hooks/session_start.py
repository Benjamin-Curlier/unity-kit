"""SessionStart hook: report whether the Unity editor + MCP server are up.

Silent unless the current working directory is a Unity project
(hooks run with cwd = project root).
"""
import os
import socket
import subprocess
import sys

MCP_PORT = 8080


def is_unity_project():
    return os.path.isfile(os.path.join("ProjectSettings", "ProjectVersion.txt"))


def unity_process_running():
    try:
        out = subprocess.run(
            ["tasklist", "/FI", "IMAGENAME eq Unity.exe", "/FO", "CSV", "/NH"],
            capture_output=True, text=True, timeout=10,
        ).stdout
        return "Unity.exe" in out
    except Exception:
        return None  # unknown


def mcp_port_open(port=MCP_PORT):
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=1):
            return True
    except OSError:
        return False


if not is_unity_project():
    sys.exit(0)

proc = unity_process_running()
port = mcp_port_open()

if port:
    print(f"[unity-kit] MCP for Unity server is answering on localhost:{MCP_PORT} — "
          "unityMCP tools are available. If several editor instances are open, check "
          "mcpforunity://instances and set_active_instance before editor work.")
elif proc:
    print(f"[unity-kit] A Unity editor process is running but nothing answers on "
          f"localhost:{MCP_PORT} — the MCP server may still be starting, or auto-start is "
          "off (Window > MCP for Unity > Restart Server).")
else:
    print("[unity-kit] No Unity editor process detected — unityMCP tools will fail. "
          "Use the unity-launch skill to start the editor for this project before editor work.")

sys.exit(0)
