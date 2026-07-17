---
name: game-design
description: Game design method for small teams — core-loop-first thinking, scope discipline, first-playable-slice planning, juice, playtesting. Use when starting a project, when the user proposes features, when fun is in question, or when maintaining Docs/DESIGN.md.
---

# Game design (solo/small-team method)

`Docs/DESIGN.md` is the living source of truth (unity-init stamps it). Every design decision made in conversation gets written back there — a decision that only lives in chat history is lost.

## Core loop first
Before building anything: what is the player's **verb** (jump, trade, sneak, grow)? What happens in a **30-second loop**, and what makes the player want the 31st second? If the loop can't be stated in two sentences, the project isn't ready for features — help the user find it, prototype-cheap.

## First playable slice
The smallest build that proves the loop is fun: **one scene, one mechanic, placeholder art, no menus**. Resist building systems (inventory, save, settings, meta-progression) before the loop is validated — a fun-less game with a great save system is still fun-less. The slice is also the correct scope for unity-init's first bootstrap.

## Scope discipline
- Every feature costs ~3× the estimate once art, UI, edge cases, and saves are counted. Say this out loud when features are proposed.
- **Content scales linearly** (another level ≈ same cost each); **systems scale combinatorially** (each new system must interact with every existing one). When cutting, cut systems, not polish.
- When the user proposes a mid-project feature: check it against DESIGN.md's core loop. If it serves the loop, size it honestly; if it doesn't, surface the trade-off ("this delays the slice by ~X; it doesn't touch the core loop — park it in DESIGN.md's ideas list?") instead of silently building it.

## Juice (after the loop works, not before)
Cheap polish that multiplies perceived quality — apply as a checklist to the working loop: screen shake (small!), hit-stop (2–4 frames), squash & stretch, particles on every impact, a sound on **every** interaction (see unity-audio), tweened UI (nothing snaps), controller rumble if applicable. An afternoon of juice beats a week of features for how good a game feels.

## Playtesting
- Watch, don't explain. Where the player stalls or asks "what do I do?" is a design bug, not a player bug.
- Treat DESIGN.md as falsifiable: playtest findings update it — especially the *Open questions* section.
- After each playtest round, the next milestone is whatever confused or bored players most — not the next feature on the list.
