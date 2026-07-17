---
name: unity-animation
description: Animator controllers, animation clips, 2D sprite/rig animation, and animation-driven gameplay in Unity. Use when creating or wiring animations, Animator state machines, blend trees, animation events, or importing animated art (Aseprite/rigged sprites/FBX).
---

# Unity animation

MCP tooling: activate the `animation` group (`manage_tools`) for `manage_animation` (Animator control + AnimationClip creation). Assets live under `Assets/Animations/<Feature>/`.

## Animator state machines
- Keep controllers small: states for genuinely distinct behaviors, **blend trees** for continuous variation (speed, direction) — a controller with 20 hand-wired transitions is a bug generator.
- Drive parameters from code via cached hashes: `static readonly int Speed = Animator.StringToHash("Speed");` — never scatter string literals.
- Transition settings that bite: *Has Exit Time* on a transition that should react instantly (turn it off for input-driven transitions); missing *Any State* loops causing re-entry every frame.
- Query state before assuming: `GetCurrentAnimatorStateInfo`, or via `manage_animation` when inspecting in-editor.

## 2D pipelines (pick per asset)
- **Frame animation**: sprite-swap clips. The **Aseprite importer generates clips from tags automatically** — prefer tagging in Aseprite over hand-building clips; re-import updates them.
- **Rigged (skeletal)**: 2D Animation package — bones + Sprite Skin on a PSD/PSB via the PSD importer; heavier but smooth deformation and retargeting.
- Pivot/PPU consistency matters more than anything: mismatched pivots make every clip look broken.

## 3D / imported clips
- Humanoid rig for anything that should retarget between characters, Generic otherwise. Configure on the FBX importer, not per-clip.
- **Root motion**: decide explicitly — root motion moves the transform via the clip (physics conflicts ahead), code-driven movement wants it off and in-place clips.

## Animation events & gameplay
- Animation events call **public** methods on the same GameObject; renaming the method silently breaks the event — grep clips (or re-wire via `manage_animation`) whenever renaming such a method.
- Hit frames/footsteps: animation events beat timers. Gameplay-critical timing (attack windows) is better in code or StateMachineBehaviours than eyeballed event placement.

## Tweening (UI, pickups, juice)
- Simple one-off motion: a coroutine/`Mathf.Lerp` or `Animator` is fine. For pervasive juice, a tween library (e.g. DOTween via OpenUPM/Asset Store) is worth it — but it's a dependency: **ask the user before adding it** (see unity-packages skill).

## Verify
Animations only prove themselves moving: play mode + `read_console` (animation events throwing?), and a `manage_camera` screenshot mid-motion or a short play-mode observation. State-machine wiring can be inspected via `manage_animation` without entering play mode.
