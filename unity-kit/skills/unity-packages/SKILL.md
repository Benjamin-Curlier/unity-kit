---
name: unity-packages
description: Install, update, and vet Unity packages — official registry via MCP manage_packages, community packages via OpenUPM scoped registries, and git-URL packages. Use when adding/removing/upgrading any Unity package or resolving package errors.
---

# Unity package management

## Official registry (default path)
Use MCP `manage_packages` (core group) — install by name (e.g. `com.unity.cinemachine`) and let Unity resolve the version compatible with the editor. Every install/remove triggers recompile + domain reload: poll `mcpforunity://editor/state` until idle, then `read_console`, before the next operation.

## Community packages — OpenUPM
Community packages come from the OpenUPM registry via a **scoped registry** in `Packages/manifest.json`:

```json
"scopedRegistries": [
  {
    "name": "package.openupm.com",
    "url": "https://package.openupm.com",
    "scopes": ["com.cysharp.unitask"]
  }
],
"dependencies": {
  "com.cysharp.unitask": "2.5.10"
}
```

- Each package's reverse-domain prefix must be covered by `scopes` (one registry entry, grow the scopes array).
- Community packages need an **explicit pinned version** — check the latest on openupm.com.
- After editing `manifest.json` externally, the editor needs a focus/refresh to resolve; verify via `manage_packages` list + console.

## Git-URL packages
`"com.example.pkg": "https://github.com/owner/repo.git?path=/Subfolder#v1.2.3"` — pin a **tag** for reproducibility once stability matters (`#main` only for tools you deliberately track, like MCP for Unity during development).

## Vetting community packages — before installing, check:
1. Last release date and open-issue activity (abandoned Unity packages rot fast across editor versions).
2. Declared Unity version support vs. the project's version.
3. License (must be compatible with the user's distribution plans).
4. Whether an official package already covers the need (e.g. don't add a tween lib for one lerp).
Flag anything alpha/experimental/preview explicitly and get user confirmation — same for anything that changes project architecture (DOTS, networking, DI frameworks).

## Rules
- Commit `Packages/manifest.json` **and** `Packages/packages-lock.json` together — a manifest change without its lock is a reproducibility bug.
- Never install Unity AI Assistant alongside MCP for Unity (DLL conflict).
- Removal errors ("type not found") usually mean an asmdef still references the removed package — fix references, don't reinstall to silence errors.
- The unity-docs-researcher agent can compare candidate packages when the choice isn't obvious.
