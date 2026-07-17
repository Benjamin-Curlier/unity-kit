---
description: Fix a Unity bug test-first (failing repro test before the fix)
argument-hint: <bug description>
---

Fix this bug in the current Unity project: $ARGUMENTS

Work test-first per the unity-verify skill: reproduce and understand the bug, write the **failing** Edit/Play Mode repro test *before* touching any fix, watch it fail, implement the fix, then run the full verify loop (console clean, all tests green including the new one). Bounded fixing applies: max 3 fix→recheck cycles, then report the state honestly instead of thrashing. If no bug description was given above, ask what's broken and how to reproduce it.
