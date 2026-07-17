---
name: unity-csharp
description: Conventions and pitfalls for writing Unity C# — serialization, lifecycle, Input System, URP, editor-vs-runtime code. Use when creating or modifying MonoBehaviours, ScriptableObjects, editor scripts, or any .cs file in a Unity project.
---

# Unity C# rules

Read the project's `CLAUDE.md` for its specifics (Unity version, 2D/3D, render pipeline, input system) — the rules below apply everywhere.

## Structure
- Runtime code: `Assets/Scripts/<Feature>/`. Editor-only code: any folder named `Editor` (or guarded by `#if UNITY_EDITOR`).
- File name = public class name, one MonoBehaviour/ScriptableObject per file. Unity will not find a component whose file name doesn't match.
- After creating a script, Unity must import it before it can be added to a GameObject — wait for the compile (see `unity-verify`), then add the component via `manage_components`.

## Input
- If the project uses the **new Input System** (`com.unity.inputsystem` — most projects scaffolded by unity-init do): legacy `Input.GetKey/GetAxis/GetMouseButton` throws when Active Input Handling is set to the new system — never use it. Prefer an `InputActionAsset` referenced via `[SerializeField] InputActionReference`, or `PlayerInput` with unity events for simple cases. Enable actions in `OnEnable`, disable in `OnDisable`.

## Serialization
- `[SerializeField] private Foo foo;` — not public fields. Properties are not serialized.
- Serializable custom types need `[System.Serializable]`. Unity cannot serialize interfaces, dictionaries, or properties directly.
- Renaming a serialized field silently loses its value in every scene/prefab — use `[FormerlySerializedAs("oldName")]` (`UnityEngine.Serialization`) when renaming.

## Lifecycle & references
- `Awake`: self-wiring (`GetComponent`, caching). `Start`: references to other objects. `OnEnable`/`OnDisable`: event (un)subscription. `OnDestroy`: final cleanup.
- `UnityEngine.Object` has an overloaded `==`; **never** use `?.`, `??`, or `is null` on Unity object references — they bypass the destroyed-object check. Write `if (obj != null)`.
- `GetComponent` in `Update` is a smell — cache in `Awake`. Use `TryGetComponent` to avoid allocation on failure.
- Unity API is main-thread only — anything Unity-related off the main thread throws.

## Physics & rendering
- 2D projects: `Rigidbody2D`/`Collider2D`, `OnCollisionEnter2D`/`OnTriggerEnter2D`; move bodies with `Rigidbody2D.MovePosition` in `FixedUpdate`, not `transform.position` in `Update`. Sprites sort by Sorting Layer + Order in Layer, not Z.
- URP: lights are `Light2D` (2D Renderer) or URP lights — not legacy; post-processing is built in via Volumes (no separate package). Camera stacking uses Universal Additional Camera Data.
- Physics in `FixedUpdate`, input/visuals in `Update`.

## Common failure modes
- `UnityEditor` API referenced from runtime code → builds break even though the editor compiles. Editor folder or `#if UNITY_EDITOR`.
- Coroutines stop when their GameObject deactivates; for logic that must survive, use a persistent runner or async with cancellation on destroy.
- `Instantiate`/`Destroy` per frame → GC pressure; pool.
- `Find`/`FindObjectOfType` at runtime is slow and fragile — inject references via serialized fields instead. (Editor-side, prefer `FindObjectsByType` — `FindObjectsOfType` is obsolete in Unity 2023+.)
