---
name: unity-ci
description: Use when running Unity Test Framework tests without the editor GUI — from a terminal, in GitHub Actions or other CI, in pre-push checks, or when the editor is closed and in-editor run_tests is unavailable. Covers headless -batchmode -runTests invocation, exit codes, license and UnityLockfile pitfalls, and CI pipeline wiring.
---

# unity-ci — tests without the editor GUI

Two test paths exist; use both, they don't compete:

| Path | When | How |
|---|---|---|
| **In-editor** (MCP `run_tests`) | Editor already open — the normal dev loop | unity-verify skill |
| **Headless CLI** | Editor closed, CI, pre-push, unattended parallel lanes (agentic-workflows) | `scripts/run-tests-headless.ps1` / `.sh` |

## Local headless runs

```
scripts/run-tests-headless.ps1 -ProjectPath . -Platform Both [-TestFilter <regex>] [-NoGraphics]
```

macOS/Linux: `bash scripts/run-tests-headless.sh --project-path . --platform Both [--test-filter <regex>] [--no-graphics]` (invoking via `bash` works even on checkouts that lost the exec bit).

The script encodes the flag lore so nobody re-derives it: locates the right editor via `ProjectVersion.txt` + find-unity, refuses when `Temp/UnityLockfile` shows the editor has the project open, runs `-batchmode -runTests -testPlatform <mode> -testResults <xml> -logFile <log> -accept-apiupdate -forgetProjectPath`, parses the NUnit 3 XML, prints failures, and exits 0/2/3.

What bites (the script guards these, remember them when hand-rolling):

- **Editor open = instant failure.** One editor instance per project (`Temp/UnityLockfile`). Close it, or use in-editor `run_tests` instead — with the MCP editor typically open, this is the #1 surprise.
- **Never pass `-quit` with `-runTests`** — it can kill the runner mid-run; the runner exits by itself. The most common broken snippet in old docs.
- **Exit codes**: 0 = green, 2 = tests failed, 3 = run didn't complete (compile error, license, lock, crash). On 3 the results XML may not exist — read the `-logFile` instead.
- **Unity.exe is a GUI-subsystem binary** on Windows: `& $unity ...` returns immediately and `$LASTEXITCODE` lies. `Start-Process -Wait -PassThru` → `.ExitCode` (the script does).
- **`-nographics`**: fine for EditMode and pure-logic PlayMode; PlayMode tests touching rendering/cameras/URP need a real (hidden) window — drop the switch if headless PlayMode gets flaky.
- **First run on a clean checkout imports `Library`** — minutes, not seconds. Don't wrap the script in tight timeouts.
- **CLI finds 0 tests** → the test asmdef is wrong (missing `UnityEngine.TestRunner`/`UnityEditor.TestRunner` references), not the flags.
- **License must be activated** on the machine. A box where the Hub runs interactively is fine; fresh CI runners are not (below).

## CI (GitHub Actions)

Standard path: **GameCI** `game-ci/unity-test-runner@v4` on `ubuntu-latest` — it supplies the dockerized editor, licensing, and PR check annotations. Essentials: `unityVersion: auto` (reads `ProjectVersion.txt`), matrix over `editmode`/`playmode`, cache `Library` keyed on `Packages/packages-lock.json` + `ProjectVersion.txt`, license via repo secrets (`UNITY_LICENSE` = the `.ulf` contents for Personal; email/password/serial for Pro), `lfs: true` if assets use LFS. Upload the NUnit XML as an artifact.

Self-hosted Windows runner alternative: pre-install the editor via Hub, pre-activate the license once interactively, then call `run-tests-headless.ps1` — no docker, no secrets, but you maintain the machine.

Timing expectations: cold `Library` import dominates the first CI run; with a warm cache a small project's EditMode suite is ~1–3 min. PlayMode roughly doubles it.
