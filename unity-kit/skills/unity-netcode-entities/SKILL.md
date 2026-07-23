---
name: unity-netcode-entities
description: Netcode for Entities — client/server ECS multiplayer: worlds, tick rates, ghosts, prediction, input commands, dedicated servers, and the local test loop (thin clients, latency simulation). Use when a DOTS project needs multiplayer, when the user says netcode/dedicated server/client-server/ghosts/prediction/replication, or when an existing ECS sim must become networked. Pair with unity-dots (ECS code), unity-dots-migration (converting a sim), unity-playtest (multiplayer smoke).
---

# Netcode for Entities (client/server ECS multiplayer)

## Package & product boundary
`com.unity.netcode` — on Unity 6000.4+ an **editor-embedded core package versioned with the editor** (6.5 on 6000.5; registry 1.x only for pre-6000.4 editors). Add by name via `manage_packages`, never pin. It is NOT `com.unity.netcode.gameobjects` (NGO) — that's the GameObject-world product with different APIs and test tools; an ECS sim replicates with Netcode for Entities, and the two never mix. Companions: `com.unity.dedicated-server` (Multiplayer Roles), `com.unity.multiplayer.playmode` (virtual players). The Multiplayer Center window is a quickstart entry point, not an authority on package choice.

## The architecture you're committing to
Server-authoritative snapshots + client prediction — **not lockstep**; never promise cross-machine bit-determinism. Worlds come from a `ClientServerBootstrap` subclass (`CreateDefaultClientServerWorlds()`); a dedicated server build gets the server world only. Sim systems move to `PredictedSimulationSystemGroup` (fixed-step; rolls back and resimulates). Presentation exists only client-side — a dedicated server has no view, so hybrid bridges (unity-dots-migration) become client-only (`#if UNITY_SERVER` destroy, or world filters).

## Tick rates
`ClientServerTickRate` singleton: `SimulationTickRate` (default 60; 10–30 typical for RTS/large-scale), `NetworkTickRate` (≤ sim rate — the bandwidth knob), `MaxSimulationStepBatchSize` (server batches ticks under load — write tick-gated loops as *while not caught up*, never `if`). **It's auto-created on clients but never on the server: set it server-side before any connection** (bootstrap or server system), or use the no-code `NetCodeConfig` asset marked Global (Project Settings > NetCode). Post-connect changes don't replicate.

## Ghosts — the only things that replicate
Replication happens only for **ghost prefabs**: authored GameObjects with ghost settings baked in a SubScene, or built at runtime with `GhostPrefabCreation.ConvertToGhostPrefab` (must run **byte-identically in client and server worlds**; `[CreateAfter(typeof(DefaultVariantSystemGroup))]` when done in OnCreate; the Config has **no owner flags — add `GhostOwner`/`AutoCommandTarget` components yourself**). Live `CreateEntity` entities never replicate, and **only the server instantiates ghost instances**. Modes: interpolated (cheap), predicted (client resimulates — ~22 frames of resim at 300 ms RTT), owner-predicted. Default to interpolated; predict only what the local player must feel instantly. Scale with per-ghost `Importance`, `OptimizationMode.Static` for immobile ghosts, then `GhostRelevancy`/`GhostImportance` at hundreds+ per connection.

**[GhostField] traps:** `int2`/`int3`/`NetworkTick` are not default-supported field types — replicate raw `int`s / `NetworkTick.SerializedData` (or register templates). And any sim-mutated state that outlives a tick (queued input, cooldowns, counters) MUST be a `[GhostField]`, or prediction rollback silently loses it.

## Input & the sim clock
Continuous input = a struct implementing `IInputComponentData` on the player's **owned ghost** (`InputEvent` fields give exactly-once press semantics over the lossy command stream), gathered in `GhostInputSystemGroup` — client worlds only, managed code fine (Input System APIs never enter Burst/jobs). This **replaces** any single input-singleton pattern: netcode input is per-player, tick-stamped, and replayed during rollback. Sporadic commands (RTS orders, purchases): reliable `IRpcCommand` — commands cap at **1024 bytes**, and don't put `Entity` fields in RPCs (ghost-entity translation there is unconfirmed in current docs) — carry ghost ids (e.g. a `FixedList`) and resolve server-side. In predicted systems: never accumulate `DeltaTime` (rollback re-accumulates it — schedule with absolute `NetworkTick` comparisons), no-op on partial ticks for tick-gated sims, iterate deterministically (e.g. sort by `GhostOwner.NetworkId`) when entities contest a shared resource, and query `.WithAll<Simulate>()`.

## Presentation under prediction
Sim→view events as monotonic counters (unity-dots-migration) survive with one change: fire on **increase only** — predicted counters can regress on rollback. Authoritative randomness (loot, spawns) stays server-side: clients predict the effect, never the roll.

## Dedicated server
Dedicated Server build target (needs the DS Build Support module) strips audio/textures/meshes/shaders and runs headless; `com.unity.dedicated-server` adds Multiplayer Roles + content stripping — but **roles don't touch baked SubScene entities**: use ghost PrefabType overrides for server-/client-only components. Burst works in server builds.

## Test loop from day one
Prerequisite: **Run In Background ON** (Player settings) — unfocused extra clients and editors silently stop pumping connections without it.
- **PlayMode Tools** (Window > Multiplayer > PlayMode Tools): PlayMode Type, Num Thin Clients, network simulator (RTT delay/jitter, packet drop/fuzz %, presets), Simulate Dedicated Server. Latency simulation belongs in every play smoke, not release week.
- **Thin clients**: `AutomaticThinClientWorldsUtility`; systems opt in via `WorldSystemFilterFlags.ThinClientSimulation` and emit scripted legal inputs — the cheap N-player soak.
- **MPPM** (`com.unity.multiplayer.playmode`): up to 3 virtual players + 4 built instances — full editor clones, orthogonal to thin clients.
- **Automated**: there is no public netcode test fixture (`NetCodeTestWorld` is internal; NGO's `NetcodeIntegrationTest` is a different product) — create worlds via `ClientServerBootstrap.CreateServerWorld/CreateClientWorld`, connect on loopback, pump `world.Update()`, assert ghost convergence. A custom bootstrap must **yield in test scenes** (e.g. return false when the active scene starts with `InitTestScene`) or its auto-created worlds fight the test's.
- `unity-verify` as usual; inspect bandwidth/ghosts with the **Netcode Profiler** (the browser Network Debugger is deprecated in 6.x).
