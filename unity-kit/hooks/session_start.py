"""SessionStart hook: report whether the Unity editor + MCP bridge are up.

Silent unless the current working directory is a Unity project
(hooks run with cwd = project root).

Readiness signal is the MCP for Unity status file (~/.unity-mcp/unity-mcp-status-*.json)
matching this project: fresh mtime, reason "ready", and its unity_port answering TCP.
Current MCP for Unity does not serve HTTP on 8080 — do not probe that.
"""
import glob
import json
import os
import socket
import subprocess
import sys
import time

STALE_AFTER_SEC = 120  # a live editor heartbeats the status file far more often than this


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
        return None  # unknown (e.g. non-Windows)


def project_status():
    """This project's MCP for Unity status file content (+ _age_sec), or None."""
    assets = os.getcwd().replace("\\", "/").rstrip("/") + "/Assets"
    for path in glob.glob(os.path.expanduser("~/.unity-mcp/unity-mcp-status-*.json")):
        try:
            with open(path, encoding="utf-8") as f:
                data = json.load(f)
        except (OSError, json.JSONDecodeError):
            continue
        if data.get("project_path") == assets:
            data["_age_sec"] = time.time() - os.path.getmtime(path)
            return data
    return None


def port_open(port):
    try:
        with socket.create_connection(("127.0.0.1", int(port)), timeout=1):
            return True
    except (OSError, ValueError, TypeError):
        return False


def server_registered():
    """Is a UnityMCP server registered for this project in ~/.claude.json?"""
    try:
        with open(os.path.expanduser("~/.claude.json"), encoding="utf-8") as f:
            cfg = json.load(f)
    except (OSError, json.JSONDecodeError):
        return None
    cwd_fwd = os.getcwd().replace("\\", "/")
    for proj, entry in (cfg.get("projects") or {}).items():
        if proj.replace("\\", "/") == cwd_fwd:
            return "UnityMCP" in (entry.get("mcpServers") or {})
    return False


if not is_unity_project():
    sys.exit(0)

status = project_status()
fresh = status is not None and status["_age_sec"] < STALE_AFTER_SEC
ready = (fresh and status.get("reason") == "ready" and not status.get("reloading")
         and port_open(status.get("unity_port", 6400)))

if ready:
    msg = (f"[unity-kit] MCP for Unity bridge is ready for {status.get('project_name')} "
           f"(TCP port {status.get('unity_port')}, Unity {status.get('unity_version')}).")
    if server_registered() is False:
        msg += (" WARNING: no UnityMCP server is registered for this project, so this session has "
                "no mcp__UnityMCP__* tools. Register: claude mcp add UnityMCP -- uvx --from "
                "mcpforunityserver mcp-for-unity  — then restart the session, or use the "
                "unity-launch skill's mcp-stdio-call.py shim meanwhile.")
    print(msg)
elif fresh:
    print(f"[unity-kit] Unity is up but the bridge reports '{status.get('reason')}' "
          "(starting or reloading) — poll mcpforunity://editor/state before editor work.")
elif unity_process_running():
    print("[unity-kit] A Unity editor process is running but this project's MCP status file "
          "(~/.unity-mcp/unity-mcp-status-*.json) is stale or missing — likely another project's "
          "editor, or the bridge is still starting. Check Window > MCP for Unity (Restart Server) "
          "or follow the unity-launch skill's diagnosis checklist.")
else:
    print("[unity-kit] No Unity editor process detected — unityMCP tools will fail. "
          "Use the unity-launch skill to start the editor for this project before editor work.")

sys.exit(0)
