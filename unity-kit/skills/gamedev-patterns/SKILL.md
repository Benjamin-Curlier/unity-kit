---
name: gamedev-patterns
description: Battle-tested architecture patterns for Unity games — state machines, object pooling, ScriptableObject event channels, save systems, scene architecture, singletons done safely. Use when structuring gameplay code, when a script grows past one responsibility, or when spawning/saving/scene-flow comes up.
---

# Gamedev patterns (Unity-shaped)

Reach for a pattern when the pain exists, not preemptively — a game jam prototype needs none of these; a growing project needs them one at a time.

## State machines
- ≤5 simple states: an enum + `switch` in one MonoBehaviour is correct — don't over-engineer.
- Beyond that: class-per-state (`IState { Enter(); Tick(); Exit(); }`) with a plain C# `StateMachine` holding current state. States get context via constructor, not statics.
- The **Animator is not a gameplay FSM** — use it for animation states only; gameplay logic in Animator transitions becomes undebuggable.

## Object pooling
- Unity ships `UnityEngine.Pool.ObjectPool<T>` — use it, don't hand-roll. Pool anything Instantiated more than ~once per second: projectiles, impact VFX, damage numbers, audio sources.
- Pooled objects need explicit reset in `OnGet`/`OnRelease` (velocity, trail renderers, coroutines) — the #1 pooling bug is stale state from the previous life.

## Decoupling: events over references
- Same-object or parent-child: plain C# `event Action<...>` (unsubscribe in `OnDisable`).
- Cross-scene / cross-prefab (score changed, player died, wave started): **ScriptableObject event channels** — an SO asset with `Raise()`/`Subscribe()`; emitters and listeners reference the asset, never each other. Designer-wireable, testable, no scene coupling.
- ScriptableObjects also hold shared data (enemy stats, item defs, level configs): edit without touching scenes, reference from prefabs safely.
- **Juice/presentation as event subscribers**: gameplay logic raises domain events (`AteFood`, `Died(newBest)`, `Restarted`); FX, audio, and HUD live in separate sibling components that subscribe in `OnEnable`. Logic stays pure and unit-testable (tests instantiate it with zero subscribers — `?.Invoke` makes that free), each juice layer can be added/removed without touching gameplay, and the read-only state surface (`public int Score => score;`) doubles as the HUD's and the playtest skill's probe API.

## Save system
- Serialize plain C# data classes (`[Serializable]`, no UnityEngine.Object fields) to JSON at `Application.persistentDataPath`. Never serialize MonoBehaviours/GameObjects directly.
- **Version the format from day one**: an `int version` field + migration on load. Retrofitting versioning after players have saves is misery.
- Autosave at safe points (room transitions, not mid-physics); write to a temp file then atomically replace, so a crash mid-write can't corrupt the only save.

## Scene architecture
- A tiny **bootstrap scene** (managers, audio, save) that loads content scenes **additively** (`SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive)`); gameplay scenes stay free of persistent-manager copies.
- `DontDestroyOnLoad` sparingly — the bootstrap-additive pattern mostly removes the need. Loading screens only when a load actually exceeds ~0.5s.

## Singletons, if you must
Acceptable for genuine one-of-a-kinds wired in bootstrap (AudioManager, SaveManager): instance set in `Awake` with a duplicate-destroy guard, no lazy `FindObjectOfType` resurrection. Gameplay objects (player, enemies, spawners) are never singletons — inject references via serialized fields or event channels instead.
