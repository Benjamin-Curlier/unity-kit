---
description: Playtest the game — verify input reaches gameplay, drive a play session, screenshot proof
---

Playtest the current project's game per the unity-playtest skill, all three tiers:

1. **Input path**: ensure InputTestFixture-based PlayMode tests exist for the game's core inputs (create them if missing — asmdef needs `Unity.InputSystem` + `Unity.InputSystem.TestFramework`), then run them.
2. **Live session**: enter play mode, probe game state via `execute_code` (freeze/slow the game's tick for deterministic stepping), drive at intent level, check the console for exceptions.
3. **Visual**: screenshot the Game view at boot and after scripted actions; inspect the images.

If the user reports input doing nothing, run the skill's diagnosis order first (half-switched Active Input Handling → zero `InputSystem.devices` is the classic cause; it needs a verified editor restart via unity-launch).

Stop play mode when done. Report exactly what was pressed, asserted, and seen — including test counts and what the screenshots show. $ARGUMENTS
