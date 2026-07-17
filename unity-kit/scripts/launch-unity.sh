#!/usr/bin/env bash
# Launches the Unity editor for a project and waits until the MCP for Unity server answers.
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

PORT_WAS_OPEN=0
if port_open "$PORT"; then
  PORT_WAS_OPEN=1
  echo "NOTE: port $PORT already answers — another editor is serving MCP; the new instance joins the shared hub (use set_active_instance)."
fi

"$UNITY_EXE" -projectPath "$PROJECT_PATH" &
PID=$!
echo "Launched Unity (PID $PID) for $PROJECT_PATH — waiting for MCP on port $PORT (timeout ${TIMEOUT_SEC}s)..."

elapsed=0
while (( elapsed < TIMEOUT_SEC )); do
  if ! kill -0 "$PID" 2>/dev/null; then
    echo "Unity exited early — check ~/.config/unity3d/Editor.log (Linux) or ~/Library/Logs/Unity/Editor.log (macOS)" >&2
    exit 1
  fi
  if port_open "$PORT"; then
    if (( PORT_WAS_OPEN )); then
      echo "MCP port $PORT answering (was already up — the new instance may still be importing; poll mcpforunity://editor/state and mcpforunity://instances)."
    else
      echo "MCP server answering on port $PORT. The editor may still be importing — poll mcpforunity://editor/state until ready."
    fi
    exit 0
  fi
  sleep 5; elapsed=$((elapsed + 5))
done

echo "Timed out after ${TIMEOUT_SEC}s waiting for MCP port $PORT (Unity PID $PID still running — first imports are slow; check the editor window or Window > MCP for Unity)." >&2
exit 1
