#!/usr/bin/env bash
# Launches the Unity editor for a project and waits until the MCP for Unity bridge is ready.
# Readiness signal (current MCP for Unity): ~/.unity-mcp/unity-mcp-status-<hash>.json for this
# project reports reason "ready" with a post-launch mtime, and its unity_port (typically 6400)
# answers TCP. Older builds served HTTP on the port argument (default 8080); kept as a fallback.
# Usage: launch-unity.sh <project-path> [unity-exe] [port] [timeout-sec]
set -euo pipefail

PROJECT_PATH="${1:?usage: launch-unity.sh <project-path> [unity-exe] [port] [timeout-sec]}"
UNITY_EXE="${2:-}"
PORT="${3:-8080}"
TIMEOUT_SEC="${4:-900}"

VERSION_FILE="$PROJECT_PATH/ProjectSettings/ProjectVersion.txt"
[[ -f "$VERSION_FILE" ]] || { echo "Not a Unity project (missing $VERSION_FILE)" >&2; exit 1; }

port_open() { (exec 3<>"/dev/tcp/127.0.0.1/$1") 2>/dev/null && { exec 3>&- 3<&-; return 0; } || return 1; }

if [[ -z "$UNITY_EXE" ]]; then
  VERSION="$(grep 'm_EditorVersion:' "$VERSION_FILE" | head -1 | awk '{print $2}')"
  if [[ "$(uname -s)" == "Darwin" ]]; then
    UNITY_EXE="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
  else
    UNITY_EXE="$HOME/Unity/Hub/Editor/$VERSION/Editor/Unity"
  fi
fi
[[ -x "$UNITY_EXE" ]] || { echo "Editor not found: $UNITY_EXE — install via Unity Hub or pass it explicitly." >&2; exit 1; }

LAUNCH_STAMP="$(mktemp)"
NORMALIZED_ASSETS="${PROJECT_PATH%/}/Assets"

bridge_ready() {
  local f port
  for f in "$HOME"/.unity-mcp/unity-mcp-status-*.json; do
    [[ -f "$f" && "$f" -nt "$LAUNCH_STAMP" ]] || continue
    grep -q "\"project_path\":\"$NORMALIZED_ASSETS\"" "$f" || continue
    grep -q '"reason":"ready"' "$f" || continue
    grep -q '"reloading":false' "$f" || continue
    port="$(sed -n 's/.*"unity_port":\([0-9][0-9]*\).*/\1/p' "$f")"
    [[ -n "$port" ]] && port_open "$port" && { echo "$port"; return 0; }
  done
  return 1
}

"$UNITY_EXE" -projectPath "$PROJECT_PATH" &
PID=$!
echo "Launched Unity (PID $PID) for $PROJECT_PATH — waiting for the MCP bridge (status file + TCP probe, timeout ${TIMEOUT_SEC}s)..."

elapsed=0
while (( elapsed < TIMEOUT_SEC )); do
  if ! kill -0 "$PID" 2>/dev/null; then
    echo "Unity exited early — check ~/.config/unity3d/Editor.log (Linux) or ~/Library/Logs/Unity/Editor.log (macOS)" >&2
    rm -f "$LAUNCH_STAMP"; exit 1
  fi
  if bridge_port="$(bridge_ready)"; then
    echo "MCP bridge ready on TCP port $bridge_port."
    echo "NOTE: MCP tools also require the UnityMCP server to be registered for this project (claude mcp add UnityMCP -- uvx --from mcpforunityserver mcp-for-unity). A Claude session started before that registration has no mcp__unityMCP__* tools — restart the session, or drive the bridge with scripts/mcp-stdio-call.py."
    rm -f "$LAUNCH_STAMP"; exit 0
  fi
  if port_open "$PORT"; then
    echo "MCP answering on legacy HTTP port $PORT. The editor may still be importing — poll mcpforunity://editor/state until ready."
    rm -f "$LAUNCH_STAMP"; exit 0
  fi
  sleep 5; elapsed=$((elapsed + 5))
done

echo "Timed out after ${TIMEOUT_SEC}s waiting for the MCP bridge (Unity PID $PID still running — first imports are slow; check the editor window or Window > MCP for Unity)." >&2
rm -f "$LAUNCH_STAMP"; exit 1
