---
name: unity-playtest
description: Playtest a Unity game through MCP — verify input actually reaches gameplay, drive play sessions with simulated keys, probe game state, and screenshot checkpoints. Use when a game "should be playable now", when the user reports input doing nothing, or as the final gate after wiring input/gameplay.
---

# unity-playtest — prove the game actually plays

Unit tests prove logic; this skill proves the **game**: input reaches gameplay, the loop advances, the screen shows it. Requires the editor running with the MCP bridge ready (`unity-launch`).

## Tier 1 — input-path tests with InputTestFixture (deterministic, do this first)

The **only reliable way to simulate device input** is a PlayMode test inheriting `InputTestFixture` — it swaps in a deterministic input runtime, so simulated keys work headless and with an unfocused editor.

Test asmdef references: `UnityEngine.TestRunner`, the game asmdef, `Unity.InputSystem`, `Unity.InputSystem.TestFramework` (ships inside the Input System package).

```csharp
public class GameInputTests : InputTestFixture
{
    [UnityTest]
    public IEnumerator PressingW_TurnsPlayer()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var go = new GameObject().AddComponent<PlayerController>();
        yield return null;                    // let Start() run
        Press(keyboard.wKey);
        yield return null;                    // input update + game Update
        Assert.AreEqual(expected, go.SomeReadOnlyStateProperty);
    }
}
```

Run via `run_tests` (`mode: "PlayMode"`, generous `init_timeout`). Cover at minimum: one key changes state, one illegal input is rejected, one end-state transition (death/goal/restart).

**Do NOT try to live-simulate keys OR pointer events with `InputSystem.QueueStateEvent` from `execute_code`** while the editor is in normal play mode: `editorInputBehaviorInPlayMode` (default `PointersAndKeyboardsRespectGameViewFocus`) and `backgroundBehavior` (default `ResetAndDisableNonBackgroundDevices`) gate real-device event processing on Game-view/application focus — and an unfocused editor drops the events even with both overridden (mouse state keeps reflecting the physical OS mouse; synthetic right-clicks vanish — re-field-tested on a netcode RTS). Don't burn cycles rediscovering it. For click-driven games the live-session seam is one level up: inject the exact request entity (or call the exact method) the input handler produces — e.g. an RTS order test creates the `MoveOrderRequest` + `SendRpcCommandRequest` entity the right-click would have; Tier 1 separately proves clicks produce it.

## Tier 2 — live play session probing (state, not keys)

For everything that isn't the input path itself: `manage_editor` `play`, then `execute_code` to probe.

- **Read state through public read-only properties** on gameplay components (`public bool IsDead => dead;`). If the game doesn't expose them, add them — a read-only state surface is a testability pattern worth keeping (see gamedev-patterns). Reflection on private fields works as a fallback but breaks silently on rename.
- **MCP call latency is seconds** — a real-time game runs far between two `execute_code` calls (a snake crosses the whole board). For stepwise probing, freeze or slow the game first: set the game's tick/speed field huge via reflection, or `Time.timeScale = 0`. Restore before leaving.
- Drive gameplay at **intent level**, not key level: call the same public method your input handler calls (e.g. `TryQueueDirection(Vector2Int.up)`), or set the queued-intent field. This tests the loop; Tier 1 already proved keys reach it.
- `read_console` after: no exceptions during the session (ignore `MCP-FOR-UNITY` client-handler noise).

## Tier 3 — see it

`manage_camera` `screenshot` (`capture_source: "game_view"`) at checkpoints: after boot, after a scripted action, at the end state. Read the image and actually look at it — assert with your eyes that the thing the state claims is on screen. Screenshots catch a class of bug state probing structurally cannot: state says one thing, pixels show another (field example: a UI label whose `text` was never assigned — every state probe passed, the screenshot showed it blank). When a screenshot surprises you, probe the specific object (`FindObjectsByType`, dump the relevant properties) to localize the mismatch.

Always `manage_editor` `stop` when done. Never leave play mode running.

## Tier 4 — multiplayer smoke (Netcode for Entities projects)

Single-player smokes prove nothing about a client/server game — every smoke runs with the network shaped:

- **PlayMode Tools** (Window > Multiplayer > PlayMode Tools): set Num Thin Clients ≥ 1 and the network simulator to a realistic profile (e.g. 100–150 ms RTT + a few % packet drop) for every play session, not just release week. Thin-client systems (`WorldSystemFilterFlags.ThinClientSimulation`) emit scripted legal inputs — the cheap N-player soak. MPPM virtual players (up to 3) when full editor clones are needed.
- **Probe per-world**: with client/server worlds in one editor, `execute_code` must pick the right `World` (server vs client) before reading state — assert the same ghost converges to the same state in both after an action, and that a predicted action rolls back cleanly under the simulator's latency.
- **Automated**: no public netcode test fixture exists — create server+client worlds via `ClientServerBootstrap` statics, connect on loopback, pump `world.Update()`, assert convergence (see unity-netcode-entities).
- Screenshots: capture a client's Game view at checkpoints as usual; state-vs-pixels mismatches (Tier 3) are twice as common under prediction.
- **Real-build smoke**: run the dedicated server + N client exes on one machine (Run In Background makes unfocused clients keep pumping); give one client `-thinclients 1` so an in-process bot army drives gameplay with zero human input. A server-side cadence log line (ticks/s + unit/connection counts) turns the server log into the assertion surface: connections appear, gameplay counts move, killing a client drops exactly its entities (connection-cleanup proof). Capture *per-window* screenshots of the client exes (`PrintWindow` by PID — never full-desktop, which grabs the user's personal screen); two clients screenshotting the same fight also proves per-viewer presentation (e.g. team colors inverted per perspective).

## Structured sessions — the loop shape that finds bugs

For anything beyond a quick smoke, run Tier 2/3 as a **structured session** (the loop shape from 2025 game-QA agent research — TITAN, arXiv:2509.22170, preprint — state abstraction, filtered actions, reflection, oracles):

- **Plan before play**: a falsifiable goal, probe expressions with expected **buckets** (score: 0 / 1–5 / >5 — buckets make "no progress" detectable), an action list of ≤6 intent-level moves, and an action budget.
- **Act only from the planned list** — improvised actions turn a playtest into a wander.
- **Reflection trigger**: 3 consecutive actions with no measurable probe change → stop acting, write the stall hypothesis down (it's evidence — often the bug), optionally one alternative action, end the session.
- **Oracles always armed**: console exceptions (every 2–3 actions), the action budget as runaway cutoff, and screenshot-vs-state mismatch (Tier 3).

The `playtest-qa` agent runs one such session and returns the evidence bundle; `/unity-kit:qa-sweep` plans and runs N of them serially (one editor, one driver — see agentic-workflows).

## When input "does nothing" for the user — diagnosis order

1. `InputSystem.devices.Count == 0` at runtime (with `Keyboard.current == null`) while legacy `Input.*` **throws** → **half-switched editor**: Active Input Handling was changed but the editor never truly restarted. Do a verified restart (unity-launch: old PID must die first). This state arises whenever the post-switch "restart" silently failed.
2. Devices present but game code bails on `Keyboard.current == null` somewhere → make that branch log a warning once instead of failing silently.
3. Devices present, input works in Tier 1 tests, user still sees nothing live → Game view focus (default editor behavior routes keyboard only to a focused Game view) — have them click the Game view.
4. API note for `execute_code` probing: the enum is `InputSettings.EditorInputBehaviorInPlayMode` (not `EditorInputBehavior`); set via reflection when the CodeDom (C# 6) compiler chokes on nested-type literals.

## Report

State what was pressed, what was asserted, what the screenshot shows: "InputTestFixture: W turns, reversal rejected, restart-on-key — 3/3 green; live session: 12 ticks probed, ate 2 food, score 2, no exceptions; screenshot shows snake+HUD" — not "playtested, works".

**Evidence, not verdicts.** A controlled study (arXiv:2501.11782, preprint) shows a wrong AI verdict makes the human reviewer *worse* than no AI at all — so report claims with their supporting evidence (probe values, console lines, what the screenshot shows) and let the human adjudicate. Where evidence is thin or contradictory, say so instead of rounding to pass/fail.
