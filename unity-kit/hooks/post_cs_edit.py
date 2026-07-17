"""PostToolUse hook: after a .cs file is edited in a Unity project, remind Claude to verify.

Silent unless the current working directory is a Unity project
(hooks run with cwd = project root).
"""
import json
import os
import sys

if not os.path.isfile(os.path.join("ProjectSettings", "ProjectVersion.txt")):
    sys.exit(0)

try:
    data = json.load(sys.stdin)
except Exception:
    sys.exit(0)

tool_input = data.get("tool_input") or {}
path = tool_input.get("file_path", "")

if path.endswith(".cs"):
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PostToolUse",
            "additionalContext": (
                "A .cs file was just changed. Before reporting this work as done, "
                "run the unity-verify loop: poll the mcpforunity://editor/state resource until "
                "is_compiling is false (no manual refresh), then read_console for compile errors "
                "and fix any that appear. If the Unity editor is not running, state that "
                "verification was skipped."
            )
        }
    }))

sys.exit(0)
