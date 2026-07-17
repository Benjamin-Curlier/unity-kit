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

**Do NOT try to live-simulate keys with `InputSystem.QueueStateEvent` from `execute_code`** while the editor is in normal play mode: `editorInputBehaviorInPlayMode` (default `PointersAndKeyboardsRespectGameViewFocus`) and `backgroundBehavior` (default `ResetAndDisableNonBackgroundDevices`) gate real-device event processing on Game-view/application focus — and an unfocused editor drops the events even with both overridden. Field-tested; don't burn cycles rediscovering it.

## Tier 2 — live play session probing (state, not keys)

For everything that isn't the input path itself: `manage_editor` `play`, then `execute_code` to probe.

- **Read state through public read-only properties** on gameplay components (`public bool IsDead => dead;`). If the game doesn't expose them, add them — a read-only state surface is a testability pattern worth keeping (see gamedev-patterns). Reflection on private fields works as a fallback but breaks silently on rename.
- **MCP call latency is seconds** — a real-time game runs far between two `execute_code` calls (a snake crosses the whole board). For stepwise probing, freeze or slow the game first: set the game's tick/speed field huge via reflection, or `Time.timeScale = 0`. Restore before leaving.
- Drive gameplay at **intent level**, not key level: call the same public method your input handler calls (e.g. `TryQueueDirection(Vector2Int.up)`), or set the queued-intent field. This tests the loop; Tier 1 already proved keys reach it.
- `read_console` after: no exceptions during the session (ignore `MCP-FOR-UNITY` client-handler noise).

## Tier 3 — see it

`manage_camera` `screenshot` (`capture_source: "game_view"`) at checkpoints: after boot, after a scripted action, at the end state. Read the image and actually look at it — assert with your eyes that the thing the state claims is on screen.

Always `manage_editor` `stop` when done. Never leave play mode running.

## When input "does nothing" for the user — diagnosis order

1. `InputSystem.devices.Count == 0` at runtime (with `Keyboard.current == null`) while legacy `Input.*` **throws** → **half-switched editor**: Active Input Handling was changed but the editor never truly restarted. Do a verified restart (unity-launch: old PID must die first). This state arises whenever the post-switch "restart" silently failed.
2. Devices present but game code bails on `Keyboard.current == null` somewhere → make that branch log a warning once instead of failing silently.
3. Devices present, input works in Tier 1 tests, user still sees nothing live → Game view focus (default editor behavior routes keyboard only to a focused Game view) — have them click the Game view.
4. API note for `execute_code` probing: the enum is `InputSettings.EditorInputBehaviorInPlayMode` (not `EditorInputBehavior`); set via reflection when the CodeDom (C# 6) compiler chokes on nested-type literals.

## Report

State what was pressed, what was asserted, what the screenshot shows: "InputTestFixture: W turns, reversal rejected, restart-on-key — 3/3 green; live session: 12 ticks probed, ate 2 food, score 2, no exceptions; screenshot shows snake+HUD" — not "playtested, works".
