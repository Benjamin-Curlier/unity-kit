---
name: unity-dots
description: ECS (Entities), Burst, and the C# Job System — when to use DOTS, and how to write correct systems, components, bakers, and Burst-compiled jobs. Use when the user asks for ECS/DOTS/Burst/Jobs, or when profiling shows a MonoBehaviour approach can't scale (thousands of active entities).
---

# Unity DOTS (Entities + Burst + Jobs)

## When to use — be honest first
DOTS pays off for **mass simulation**: thousands of projectiles/agents/cells updated uniformly. For a typical indie game it is the wrong default — MonoBehaviours are simpler, tooling-complete, and fast enough. Adopting DOTS is an architectural commitment (different scene workflow, different debugging, smaller ecosystem). **Confirm with the user before introducing it**, and prefer the middle path where it fits: plain **C# Jobs + Burst** for a hot loop inside an otherwise MonoBehaviour game (no ECS required).

## Packages
`com.unity.entities` (pulls Burst/Collections/Mathematics), `com.unity.entities.graphics` (rendering), `com.unity.physics` (only if DOTS-simulated physics is needed). On Unity 6000.4+ these are **editor-embedded core packages versioned with the editor** (6.5 on 6000.5) — add by name via `manage_packages`, never pin a registry version; pre-6000.4 editors use the registry 1.x line. Expect long compile + domain reloads. Converting an existing GameObject project? Use **unity-dots-migration** for the order and the hybrid bridges.

## Core patterns
- Components: `IComponentData` structs, blittable, no managed references, no methods with state. Tags are empty structs.
- Systems: prefer `ISystem` (unmanaged, Burst-compilable) with `[BurstCompile]` on `OnCreate/OnUpdate`; `SystemBase` only when managed interop is unavoidable. Query with `SystemAPI.Query<RefRW<Foo>, RefRO<Bar>>()`.
- Per-entity work: `IJobEntity` with `.ScheduleParallel()`; structural changes (add/remove/destroy) go through an `EntityCommandBuffer` (get from `SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()`), never mid-iteration.
- Authoring: MonoBehaviour + `Baker<T>` converts GameObjects to entities — **baking only runs inside SubScenes**; an authoring object outside a SubScene silently does nothing. This is the #1 "why doesn't my entity exist" bug.

## Burst rules
- No managed types, no exceptions as flow control, no string interpolation in logs (`Debug.Log($"{x}")` breaks Burst — use `UnityEngine.Debug.Log` sparingly outside, or `Unity.Logging`).
- Use `Unity.Mathematics` (`float3`, `math.*`) not `Mathf`/`Vector3` inside jobs.
- Check the **Burst Inspector** (or console Burst warnings) — a job that silently fell back to managed defeats the point. Treat Burst compile warnings as errors.

## Jobs safety
- The safety system's race errors are real races. Fix with proper dependencies or `[ReadOnly]` — reach for `[NativeDisableParallelForRestriction]` only with a written justification.
- `Schedule` returns a `JobHandle`; complete or chain it — a `.Complete()` on the main thread right after scheduling is a smell (you wanted the parallelism).
- `NativeArray`/`NativeList` need explicit `Dispose` (or `Allocator.Temp` scope) — leaks show as console errors on domain reload.

## Verify
`unity-verify` as usual, plus: Burst warnings in `read_console`, and back any performance claim with `manage_profiler` (activate `profiling` group) numbers before/after — "it's ECS now" is not a result.
