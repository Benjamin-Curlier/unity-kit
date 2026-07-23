---
name: unity-netcode-entities
description: Netcode for Entities — client/server ECS multiplayer: worlds, tick rates, ghosts, prediction, input commands, dedicated servers, and the local test loop (thin clients, latency simulation). Use when a DOTS project needs multiplayer, when the user says netcode/dedicated server/client-server/ghosts/prediction/replication, or when an existing ECS sim must become networked. Pair with unity-dots (ECS code), unity-dots-migration (converting a sim), unity-playtest (multiplayer smoke).
---

# Netcode for Entities (client/server ECS multiplayer)

## Package & product boundary
`com.unity.netcode` — on Unity 6000.4+ an **editor-embedded core package versioned with the editor** (6.5 on 6000.5; registry 1.x only for pre-6000.4 editors; `com.unity.transport` is also editor-embedded there). Add by name via `manage_packages`, never pin. It is NOT `com.unity.netcode.gameobjects` (NGO) — that's the GameObject-world product with different APIs and test tools; an ECS sim replicates with Netcode for Entities, and the two never mix. Companions: `com.unity.dedicated-server` (Multiplayer Roles), `com.unity.multiplayer.playmode` (virtual players). Fresh 6000.5 empty projects **preinstall `com.unity.multiplayer.center`** — a stack-selection wizard, not an authority; remove or ignore it once the stack is chosen. `com.unity.logging` is **deprecated on 6000.5** ("no longer supported" Package Manager error) — server logging is `-logFile` + `Debug.Log`.

**Preflight**: dedicated-server builds need the *Dedicated Server Build Support* editor module. Check `<editor>\Editor\Data\PlaybackEngines\WindowsStandaloneSupport\Variations` for `*server*` entries (`find-unity.ps1 -Modules` in this kit); if missing, the Hub installs it headlessly in ~1 min, no elevation:
`& "C:\Program Files\Unity Hub\Unity Hub.exe" -- --headless install-modules --version <ver> -m windows-server` (module ids: `windows-server`, `linux-server`).

## The architecture you're committing to
Server-authoritative snapshots + client prediction — **not lockstep**; never promise cross-machine bit-determinism. Worlds come from a `ClientServerBootstrap` subclass (`CreateDefaultClientServerWorlds()`); a dedicated server build gets the server world only. Sim systems move to `PredictedSimulationSystemGroup` (fixed-step; rolls back and resimulates). Presentation exists only client-side — a dedicated server has no view, so hybrid bridges (unity-dots-migration) become client-only (`#if UNITY_SERVER` destroy, or world filters).

## The bootstrap: three contexts, three world sets (field-tested traps)
One `ClientServerBootstrap` subclass serves editor, dedicated server, and client builds — but **`base.Initialize` in a plain player build creates client AND server worlds ("client-hosted")**: the embedded server loses the port race against your real dedicated server ("Failed to bind UDP socket… address already in use") and the client ends up dialing itself — the DGS sits at zero connections while everything "runs". The working split:

```csharp
public override bool Initialize(string defaultWorldName)
{
    if (SceneManager.GetActiveScene().name.StartsWith("InitTestScene")) return false; // yield to tests
    AutoConnectPort = ParseUShort("-port", DefaultPort);
    var ip = ParseString("-connect", null);
    if (ip != null && NetworkEndpoint.TryParse(ip, AutoConnectPort, out var ep)) DefaultConnectAddress = ep;
    var thin = ParseUShort("-thinclients", 0);
    if (thin > 0) AutomaticThinClientWorldsUtility.NumThinClientsRequested = thin; // guarded — see below
#if !UNITY_SERVER
    if (!Application.isEditor)
    {   // player build = PURE client — never self-host
        var clientWorld = CreateClientWorld("ClientWorld");
        AutomaticThinClientWorldsUtility.ReferenceWorld = clientWorld;
        AutomaticThinClientWorldsUtility.BootstrapThinClientWorlds(); // base would do this; manual must too
        return true;
    }
#endif
    return base.Initialize(defaultWorldName); // editor: PlayMode Tools decides; DGS: server world + auto-listen
}
```
Three traps this encodes: (1) **`CreateClientWorld` already honors `AutoConnectPort`/`DefaultConnectAddress`** — also injecting a `NetworkStreamRequestConnect` double-dials (phantom extra connections on the server). (2) **CLI defaults must not overwrite editor-tooling state**: an unconditional `NumThinClientsRequested = ParseUShort(...)` writes 0 over the PlayMode Tools request and editor thin clients silently never spawn — only assign when the flag is present. (3) A manual client branch must call **`BootstrapThinClientWorlds()`** or `-thinclients N` creates nothing in builds.

## Tick rates
`ClientServerTickRate` singleton: `SimulationTickRate` (default 60; 10–30 typical for RTS/large-scale), `NetworkTickRate` (≤ sim rate — the bandwidth knob), `MaxSimulationStepBatchSize` (server batches ticks under load — write tick-gated loops as *while not caught up*, never `if`). **It's auto-created on clients but never on the server: set it server-side before any connection** (bootstrap or server system), or use the no-code `NetCodeConfig` asset marked Global (Project Settings > NetCode). Post-connect changes don't replicate.

## Ghosts — the only things that replicate
Replication happens only for **ghost prefabs**: authored GameObjects with ghost settings baked in a SubScene, or built at runtime with `GhostPrefabCreation.ConvertToGhostPrefab` (must run **byte-identically in client and server worlds**; `[CreateAfter(typeof(DefaultVariantSystemGroup))]` when done in OnCreate; the Config has **no owner flags — add `GhostOwner`/`AutoCommandTarget` components yourself**). Live `CreateEntity` entities never replicate, and **only the server instantiates ghost instances**. Modes: interpolated (cheap), predicted (client resimulates — ~22 frames of resim at 300 ms RTT), owner-predicted. Default to interpolated; predict only what the local player must feel instantly. Scale with per-ghost `Importance`, `OptimizationMode.Static` for immobile ghosts, then `GhostRelevancy`/`GhostImportance` at hundreds+ per connection.

**[GhostField] traps:** `int2`/`int3`/`NetworkTick` are not default-supported field types — replicate raw `int`s / `NetworkTick.SerializedData` (or register templates). And any sim-mutated state that outlives a tick (queued input, cooldowns, counters) MUST be a `[GhostField]`, or prediction rollback silently loses it.

## Input & the sim clock
Continuous input = a struct implementing `IInputComponentData` on the player's **owned ghost** (`InputEvent` fields give exactly-once press semantics over the lossy command stream), gathered in `GhostInputSystemGroup` — client worlds only, managed code fine (Input System APIs never enter Burst/jobs). This **replaces** any single input-singleton pattern: netcode input is per-player, tick-stamped, and replayed during rollback. Sporadic commands (RTS orders, purchases): reliable `IRpcCommand` — commands cap at **1024 bytes**, and don't put `Entity` fields in RPCs (ghost-entity translation there is unconfirmed in current docs) — carry ghost ids (e.g. a `FixedList`) and resolve server-side. In predicted systems: never accumulate `DeltaTime` (rollback re-accumulates it — schedule with absolute `NetworkTick` comparisons), no-op on partial ticks for tick-gated sims, iterate deterministically (e.g. sort by `GhostOwner.NetworkId`) when entities contest a shared resource, and query `.WithAll<Simulate>()`.

## Assemblies & authoring files (6.x line)
Sim asmdef reference set: `Unity.Entities`, `Unity.Entities.Hybrid` (bakers), **`Unity.Transforms`** (a separate assembly — `LocalTransform` lives there; `using Unity.Transforms;` resolves while the type errors CS0103, which misleads), `Unity.Collections`, `Unity.Burst`, `Unity.Mathematics`, `Unity.NetCode`, `Unity.Networking.Transport`; add `Unity.Entities.Graphics` when baking presentation components (e.g. `URPMaterialPropertyBaseColor` — the snapshot-safe way to do per-client tinting, since it never replicates). `Simulate` is in `Unity.Entities`, not `Unity.NetCode`. **One authoring MonoBehaviour per file, filename = class name**: violate it and the live editor silently serializes an embedded-MonoScript fallback that the **bake import worker cannot resolve** — `[Worker] The referenced script is missing on <GO>`, the SubScene bakes empty, GoInGame RPCs sit unconsumed, and everything looked fine in the editor.

## Presentation under prediction
Sim→view events as monotonic counters (unity-dots-migration) survive with one change: fire on **increase only** — predicted counters can regress on rollback. Authoritative randomness (loot, spawns) stays server-side: clients predict the effect, never the roll.

## Dedicated server
Dedicated Server build target (needs the DS Build Support module) strips audio/textures/meshes/shaders and runs headless; `com.unity.dedicated-server` adds Multiplayer Roles + content stripping — but **roles don't touch baked SubScene entities**: use ghost PrefabType overrides for server-/client-only components. Burst works in server builds. With `ClientServerTickRate.TargetFrameRateMode = Auto` the headless server **sleeps between ticks** — field-measured ~2% of one core for a 48-ghost sim at 15 Hz — so cheap that a tick-cadence log line (`ticks/s` + unit/connection counts every ~10 s) is worth shipping day one as the smoke-test instrument. Two build-tooling traps: **after building a Server-subtarget player the editor STAYS on that subtarget**, silently dropping `!UNITY_SERVER` assemblies (in-editor C# eval and anything referencing them breaks with "Metadata file …Client.dll not found") — switch `EditorUserBuildSettings.standaloneBuildSubtarget` back to Player after server builds; and client-only MonoBehaviours living in build scenes log a benign "different serialization layout" warning in server players until you strip them via roles.

## Test loop from day one
Prerequisite: **Run In Background ON** (Player settings) — unfocused extra clients and editors silently stop pumping connections without it.
- **PlayMode Tools** (Window > Multiplayer > PlayMode Tools): PlayMode Type, Num Thin Clients, network simulator (RTT delay/jitter, packet drop/fuzz %, presets), Simulate Dedicated Server. Latency simulation belongs in every play smoke, not release week.
- **Thin clients**: `AutomaticThinClientWorldsUtility`; systems opt in via `WorldSystemFilterFlags.ThinClientSimulation` and emit scripted legal inputs — the cheap N-player soak.
- **MPPM** (`com.unity.multiplayer.playmode`): up to 3 virtual players + 4 built instances — full editor clones, orthogonal to thin clients.
- **Automated**: there is no public netcode test fixture (`NetCodeTestWorld` is internal; NGO's `NetcodeIntegrationTest` is a different product) — create worlds via `ClientServerBootstrap.CreateServerWorld/CreateClientWorld`, connect on loopback, pump `world.Update()`, assert ghost convergence. A custom bootstrap must **yield in test scenes** (e.g. return false when the active scene starts with `InitTestScene`) or its auto-created worlds fight the test's.
- **Domain reload must be ON for PlayMode netcode tests on the 6.5 line.** Enter Play Mode Options with Reload Domain disabled (the classic DOTS iteration-speed tweak) + a test disposing netcode worlds = **hard native editor crash** in `GhostCollectionSystem.OnDestroy` allocator tracking (Unity even warns "if you experience any issues, disable Enter Play Mode Options" at test start). Signature: editor process dies mid-run, crash dump stack ends `World.Dispose → GhostCollectionSystem.OnDestroy → NativeHashMap.Dispose`. Set `EditorSettings.enterPlayModeOptionsEnabled = false` before PlayMode test runs — the seconds per play entry buy back whole editor restarts.
- `unity-verify` as usual; inspect bandwidth/ghosts with the **Netcode Profiler** (the browser Network Debugger is deprecated in 6.x).
