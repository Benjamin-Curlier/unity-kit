---
description: Verify the project, then build a player
argument-hint: "[target platform — default: Windows standalone]"
---

Produce a shippable build. Target: $ARGUMENTS (default: Windows standalone).

First run the complete unity-verify loop — console clean and tests green are hard gates; report and stop if they fail. Then build per the unity-build skill. Report: what was verified, build output path, file size, build duration, and any build-log warnings that matter.
