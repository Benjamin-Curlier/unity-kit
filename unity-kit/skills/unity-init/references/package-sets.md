# Package sets for unity-init

Install in-editor via `manage_packages` (Unity resolves versions). Order matters loosely: render pipeline first, input second, then the rest.

## Always
- `com.unity.inputsystem` — new Input System (requires switching Active Input Handling + editor restart)
- `com.unity.test-framework` — usually preinstalled; verify
- `com.unity.cinemachine` — virtual cameras (2D and 3D)

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

## Optional (ask, don't assume)
- `com.unity.addressables` — content-heavy games, later is fine
- `com.unity.netcode.gameobjects` — multiplayer (big commitment; confirm explicitly)
- `com.unity.postprocessing` is NOT needed with URP (built in via Volume)

## Do not install
- Unity AI Assistant (`com.unity.ai.assistant`) — known `System.Collections.Immutable` DLL conflict with MCP for Unity on Unity 6.3+.
