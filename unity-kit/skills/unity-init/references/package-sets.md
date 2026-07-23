# Package sets for unity-init

Install in-editor via `manage_packages` (Unity resolves versions). Order matters loosely: render pipeline first, input second, then the rest.

Community packages (OpenUPM scoped registry) are a per-need decision **after** init, not part of the bootstrap — see the `unity-packages` skill.

## Always
- `com.unity.inputsystem` — new Input System (requires switching Active Input Handling + editor restart)
- `com.unity.test-framework` — **NOT preinstalled** in fresh Unity 6000.5+ empty projects (it may resolve transitively via 2D packages, but install it explicitly as a direct dependency so the Test Runner is stable)
- `com.unity.ugui` — also **NOT preinstalled** in empty projects; required the moment a Canvas/Text HUD appears (`UnityEngine.UI` won't resolve without it)

## Per concept (not Always)
- `com.unity.cinemachine` — virtual cameras (2D and 3D). Skip for fixed-camera games and dedicated-server targets — it's dead weight there.

## 2D set
- `com.unity.render-pipelines.universal` — URP (use the **2D Renderer**)
- `com.unity.2d.animation`
- `com.unity.2d.aseprite` — only if the user works with Aseprite
- `com.unity.2d.psdimporter` — only if the user works with PSDs
- `com.unity.2d.tilemap.extras` — tile-based games
- `com.unity.2d.spriteshape` — organic terrain outlines

## 3D set
- `com.unity.render-pipelines.universal` — URP (standard renderer)
- `com.unity.probuilder` — greybox/prototype geometry
- `com.unity.ai.navigation` — NavMesh if the concept needs agents

## Multiplayer set (decide at the Phase-1 checkpoint — see unity-netcode-entities)
Pick ONE netcode, never both:
- `com.unity.netcode` — **Netcode for Entities**, for ECS/DOTS simulations (editor-embedded core package on 6000.4+; add by name, no version pin)
- `com.unity.netcode.gameobjects` — NGO, for GameObject/MonoBehaviour games only

Alongside either:
- `com.unity.multiplayer.playmode` — virtual players for in-editor multiplayer testing
- `com.unity.dedicated-server` — Multiplayer Roles + content stripping for dedicated-server hosting (also install the Dedicated Server Build Support editor module — Hub headless: `-m windows-server`)

Notes for fresh 6000.5 projects: the empty-project manifest **preinstalls `com.unity.multiplayer.center`** (stack wizard) — remove it once the netcode choice is made; `com.unity.logging` is **deprecated** on this editor line (use `-logFile`); `com.unity.transport` arrives editor-embedded via netcode, no pin.

## Optional (ask, don't assume)
- `com.unity.addressables` — content-heavy games, later is fine
- `com.unity.postprocessing` is NOT needed with URP (built in via Volume)

## Do not install
- Unity AI Assistant (`com.unity.ai.assistant`) — known `System.Collections.Immutable` DLL conflict with MCP for Unity on Unity 6.3+.
