---
description: Diagnose the Unity editor / MCP bridge connection
---

Diagnose the Unity ↔ Claude connection for the current project, per the unity-launch skill's checklist: Unity process running? MCP port answering? `mcpforunity://editor/state` readable and idle? Multiple instances needing `set_active_instance`? Which tool groups are active (`mcpforunity://tool-groups`)?

Fix what is fixable (launch the editor via the launch script, suggest Restart Server), state exactly what you checked, and list any remaining manual steps for the user. Also report the blender and elevenlabs MCP servers' reachability if audio/3D work is expected.
