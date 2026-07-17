#!/usr/bin/env bash
# Creates an empty Unity project headlessly. Usage: new-project.sh <unity-exe> <project-path> [timeout-sec]
set -euo pipefail

UNITY_EXE="${1:?usage: new-project.sh <unity-exe> <project-path> [timeout-sec]}"
PROJECT_PATH="${2:?usage: new-project.sh <unity-exe> <project-path> [timeout-sec]}"
TIMEOUT_SEC="${3:-600}"

[[ -x "$UNITY_EXE" ]] || { echo "Unity editor not found/executable: $UNITY_EXE" >&2; exit 1; }
# An existing EMPTY directory is fine — Unity's -createProject accepts it. This matters when the
# Claude session's cwd IS the target folder (it can't be deleted while the shell holds it open).
if [[ -e "$PROJECT_PATH" ]]; then
  if [[ ! -d "$PROJECT_PATH" || -n "$(ls -A "$PROJECT_PATH" 2>/dev/null)" ]]; then
    echo "Path already exists and is not empty: $PROJECT_PATH — refusing to overwrite." >&2; exit 1
  fi
fi

LOG="${TMPDIR:-/tmp}/unity-create-$(basename "$PROJECT_PATH").log"
"$UNITY_EXE" -batchmode -quit -createProject "$PROJECT_PATH" -logFile "$LOG" &
PID=$!

elapsed=0
while kill -0 "$PID" 2>/dev/null; do
  if (( elapsed >= TIMEOUT_SEC )); then
    kill "$PID" 2>/dev/null || true
    echo "Timed out after ${TIMEOUT_SEC}s creating the project. Log: $LOG" >&2
    exit 1
  fi
  sleep 5; elapsed=$((elapsed + 5))
done
wait "$PID" || true

if [[ ! -f "$PROJECT_PATH/ProjectSettings/ProjectVersion.txt" ]]; then
  echo "Unity exited but the project was not created. Log: $LOG" >&2
  exit 1
fi
echo "Created Unity project at $PROJECT_PATH (log: $LOG)"
