---
name: unity-dots-migration
description: Convert an existing GameObject/MonoBehaviour project or subsystem to ECS/DOTS incrementally — the migration order, the hybrid bridges that keep the game shippable mid-conversion, and the traps (2D rendering, SubScene stripping, stale package advice). Use when the user asks to migrate/convert/port a game to ECS/DOTS/Entities, or when MonoBehaviour code must start interoperating with an entity simulation. Pair with unity-dots (writing the ECS code) and unity-verify (per-slice checks).
---

# GameObject → DOTS migration

## Gate first (see unity-dots)
Migration is an architectural commitment, not a perf patch. "It stutters" is a profiling task (`manage_profiler`), not a conversion trigger — a few hundred entities updated per tick gain nothing from ECS. Legitimate reasons: mass simulation on the roadmap, or the user explicitly wants the architecture/learning value. Confirm which one — it decides how far the conversion goes.

## Package reality (Unity 6000.4+)
Entities, Entities Graphics, Physics, and Collections are **editor-embedded core packages versioned with the editor** (the 6.5 line on Unity 6000.5) — add `com.unity.entities` by name via `manage_packages`, never pin a registry version; upgrading Entities means upgrading the editor. Burst is still 1.8, Mathematics still 1.3. Pre-6000.4 editors use the registry 1.4 line instead. Guidance that says "install Entities 1.3/1.4" on Unity 6000.4+ is stale — as is anything mentioning `com.unity.dots` / `com.unity.ecs` (never existed), `com.unity.2d.entities` (dead 0.x preview), or the removed 0.x APIs (`ConvertToEntity`, `GameObjectConversionSystem`, `IConvertGameObjectToEntity`).

## The one architecture decision: the view layer
The simulation moves to ECS first — rendering is a separate, explicit choice:
- **Hybrid view (default; the only supported option for sprites)**: GameObjects keep rendering (SpriteRenderer, Animator, tilemaps); a presentation bridge syncs them from ECS data each frame. **Entities Graphics cannot render through the URP 2D Renderer and has no SpriteRenderer support** (companion components only) — for a 2D game, hybrid isn't a compromise, it's the supported path.
- **Entities Graphics view**: URP Forward+/HDRP only; meshes with per-instance material-property components (e.g. `URPMaterialPropertyBaseColor`). Worth it for mass rendering. In a 2D project this means unlit quads plus a Forward renderer added alongside the 2D one (per-camera renderer index) — call out that visual parity work (sorting layers → Z, sprite punch → transform/`PostTransformMatrix`) before choosing it.

There is no official Unity migration guide — DOTS adoption is documented as selective/hybrid. The order below is this plugin's; don't present it as Unity doctrine.

## Migration order (verify each step before the next)
1. **Parity baseline**: full test suite green + a played smoke *before* touching anything; keep the old scene/code path working as the safety net until parity is proven.
2. Install packages; compile clean (first Burst compile is slow — info logs are normal, warnings are not).
3. **Extract pure rules** into Burst-safe statics (`int2`/`math.*`, allocation-free); the old MonoBehaviour delegates to them → existing EditMode tests stay green and now cover the future sim core.
4. **Data next**: config and state become `IComponentData`; ordered per-entity collections become `DynamicBuffer` (`[InternalBufferCapacity(0)]` for growing buffers); sim→presentation events become monotonic counters or an event buffer the bridge drains — never managed C# events from Burst code.
5. **Systems**: `ISystem` + `[BurstCompile]` for the sim. Input is a managed, un-Bursted edge system reading the Input System on the main thread into a singleton component (the pattern Unity's own ECS samples use) — input APIs never appear inside jobs or Burst code.
6. **Bridge**: keep the old MonoBehaviour's public surface (events, read-only properties — even the class name, file, and GUID) as a facade over ECS data, so HUD, audio, FX, scene references, and tests survive unchanged. `PlayerPrefs` and all file/platform I/O stay on the managed side.
7. Per slice: `unity-verify` — console, old suite still green, new sim tests, play smoke. Sim systems are headless-testable in EditMode: `new World(...)`, add the components, `world.SetTime(...)`, `system.Update(world.Unmanaged)` — no scene, no SubScene.
8. Only after parity: swap the view layer, add jobs, or take the next subsystem. One slice at a time.

## Hybrid rules — what never migrates
uGUI/UI Toolkit, `AudioSource`, `PlayerPrefs`, cameras, Cinemachine: no ECS equivalents exist — they stay GameObjects, fed by the bridge. **SubScene stripping trap**: baking strips every MonoBehaviour not on the companion-supported list, so UI, audio, and manager objects live in regular scenes, never inside SubScenes. Baking (authoring MonoBehaviour + `Baker<T>`) only runs inside SubScenes at all — but runtime creation (`EntityManager.CreateEntity`, `Instantiate` of a baked entity prefab) is a first-class path, and it's how PlayMode tests and bootstrap code get a sim without any SubScene. 2D physics has no ECS path (Unity Physics is 3D-only; Unity 6.3+'s low-level Box2D API is Burst-friendly but explicitly not ECS) — use grid/custom logic in components, or hybrid colliders on the view objects.

## Verify
Parity is the bar: old tests green, new sim tests green, same feel in a play smoke — and any performance claim needs `manage_profiler` numbers before/after (unity-dots rule: "it's ECS now" is not a result).
