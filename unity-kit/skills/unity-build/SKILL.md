---
name: unity-build
description: Build a playable Unity player (Windows/WebGL/etc.) from the current project via the MCP manage_build tool, with a headless CLI fallback. Use when the user asks for a build, an executable, or something they can share/playtest.
---

# unity-build

## Preconditions
- Run the `unity-verify` loop first — never build on a dirty console or failing tests.
- Confirm target platform with the user if unstated (default: Windows standalone). Switching build target triggers a full reimport — slow the first time; warn the user.
- Output goes to `Builds/<Target>/` (already gitignored by the scaffold).

## Primary path — in-editor via MCP

Use `manage_build` (core group). Inspect its schema at call time for the exact actions; it handles player build configuration and execution. Typical sequence:
1. Ensure the scenes that matter are in Build Settings (scene list), with the boot scene first.
2. Configure target + output path, then build.
3. A build blocks the editor and can take minutes — poll rather than assuming a hang; check `read_console` afterward for build errors.

## Fallback — headless CLI (editor closed, or CI)

```powershell
& "<Unity.exe>" -batchmode -quit -projectPath "<project>" `
  -executeMethod BuildTools.BuildWindows -logFile "<project>\Builds\build.log"
```
This needs a small editor script (create it once per project under `Assets/Scripts/Editor/BuildTools.cs`) with a static method calling `BuildPipeline.BuildPlayer` with the scene list and output path. Close the editor first — batchmode can't share a project with an open editor.

## Verify & report
- Check the output exists (e.g. `Builds/Windows/<name>.exe`) and report its size and the build duration.
- Report warnings from the build log that matter (missing scenes, stripped code); a "Build succeeded" line in the log is the pass signal — quote failure lines verbatim otherwise.
- Do not launch the built player unless asked (it grabs focus/fullscreen).
