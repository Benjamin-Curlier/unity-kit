#!/usr/bin/env bash
# unity-kit: run Unity Test Framework tests headless (no editor GUI). macOS/Linux port of run-tests-headless.ps1.
# Usage: ./run-tests-headless.sh [--project-path .] [--platform EditMode|PlayMode|Both] [--test-filter <regex>] [--no-graphics]
# Exit code: 0 all green, 2 tests failed, 3 run did not complete (compile error, license, lock, crash).
# Preconditions that WILL bite (see unity-ci skill): the editor GUI must be CLOSED for this project
# (Temp/UnityLockfile), the machine's Unity license must be activated, and the first run on a clean
# checkout imports Library (minutes, not seconds).
set -uo pipefail

PROJECT_PATH="."
PLATFORM="Both"
TEST_FILTER=""
NO_GRAPHICS=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --project-path) PROJECT_PATH="$2"; shift 2 ;;
    --platform)     PLATFORM="$2"; shift 2 ;;
    --test-filter)  TEST_FILTER="$2"; shift 2 ;;
    --no-graphics)  NO_GRAPHICS=1; shift ;;
    *) echo "unknown arg: $1" >&2; exit 3 ;;
  esac
done
PROJECT_PATH="$(cd "$PROJECT_PATH" && pwd)"

VERSION_FILE="$PROJECT_PATH/ProjectSettings/ProjectVersion.txt"
[[ -f "$VERSION_FILE" ]] || { echo "Not a Unity project: $VERSION_FILE missing" >&2; exit 3; }
VERSION="$(sed -n 's/^m_EditorVersion: *//p' "$VERSION_FILE" | tr -d '[:space:]')"

if [[ -f "$PROJECT_PATH/Temp/UnityLockfile" ]]; then
  echo "Temp/UnityLockfile exists - the editor has this project open. Close it (or use in-editor run_tests via MCP instead)." >&2
  exit 3
fi

# Locate the editor via find-unity.sh (JSON list), python3 for parsing.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UNITY="$("$SCRIPT_DIR/find-unity.sh" | python3 -c "
import json, sys
editors = json.load(sys.stdin)
for e in editors:
    if e['version'] == '$VERSION':
        print(e['exe']); break
")"
[[ -n "$UNITY" && -x "$UNITY" ]] || { echo "Unity $VERSION not found via find-unity.sh" >&2; exit 3; }

RESULTS_DIR="$PROJECT_PATH/TestResults"
mkdir -p "$RESULTS_DIR"

PLATFORMS=()
case "$PLATFORM" in
  Both) PLATFORMS=(EditMode PlayMode) ;;
  EditMode|PlayMode) PLATFORMS=("$PLATFORM") ;;
  *) echo "bad --platform: $PLATFORM" >&2; exit 3 ;;
esac

WORST=0
for P in "${PLATFORMS[@]}"; do
  LOWER="$(echo "$P" | tr '[:upper:]' '[:lower:]')"
  XML="$RESULTS_DIR/$LOWER-results.xml"
  LOG="$RESULTS_DIR/$LOWER.log"
  ARGS=(-batchmode -projectPath "$PROJECT_PATH" -runTests -testPlatform "$P"
        -testResults "$XML" -logFile "$LOG" -accept-apiupdate -forgetProjectPath)
  # -nographics is safe for EditMode; PlayMode tests that touch rendering need a real (hidden) window.
  if [[ "$NO_GRAPHICS" == 1 || "$P" == "EditMode" ]]; then ARGS+=(-nographics); fi
  if [[ -n "$TEST_FILTER" ]]; then ARGS+=(-testFilter "$TEST_FILTER"); fi
  # NOTE: no -quit — the test runner exits by itself; -quit can kill it mid-run.
  echo "[$P] Unity $VERSION -> $XML"
  "$UNITY" "${ARGS[@]}"
  CODE=$?

  if [[ -f "$XML" ]]; then
    python3 - "$XML" "$P" <<'EOF'
import sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
print(f"[{sys.argv[2]}] total={root.get('total')} passed={root.get('passed')} failed={root.get('failed')} skipped={root.get('skipped')}")
for tc in root.iter('test-case'):
    if tc.get('result') == 'Failed':
        msg = tc.find('.//failure/message')
        print(f"  FAIL {tc.get('fullname')}: {(msg.text or '').strip()[:200] if msg is not None else ''}")
EOF
  else
    echo "[$P] no results XML written (exit $CODE) - run did not complete; last log lines:"
    tail -n 25 "$LOG" 2>/dev/null | sed 's/^/  /'
    CODE=3
  fi
  [[ $CODE -gt $WORST ]] && WORST=$CODE
done
exit $WORST
