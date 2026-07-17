---
name: unity-audio
description: Unity audio — AudioSources, Mixer routing, 2D/3D sound, pooling, music systems — plus the audio generation pipelines (ElevenLabs MCP, in-Unity generate_audio, CC0 stock packs). Use when adding, generating, importing, or wiring any sound or music in a Unity project.
---

# Unity audio

## Fundamentals
- One `AudioListener` per scene (on the camera). `AudioSource` per emitter; `spatialBlend` 0 = 2D (UI, music), 1 = 3D (world SFX, set min/max distance + logarithmic rolloff).
- Import settings by use: short SFX → *Decompress On Load* + PCM/ADPCM; music/ambience → *Streaming* + Vorbis; many mid-length clips → *Compressed In Memory*. Force To Mono for 3D-positioned SFX (halves size, panning comes from spatialization anyway).
- **AudioMixer** from day one: `Master → Music / SFX / UI` groups; route every AudioSource to a group. Volume settings UI drives exposed mixer params — convert linear sliders to dB: `mixer.SetFloat("MusicVol", Mathf.Log10(Mathf.Max(v, 0.0001f)) * 20f)`. Ducking (music dips under dialogue/SFX): mixer snapshots or a duck-volume effect on the Music group.

## Playing patterns
- One-shots: `source.PlayOneShot(clip)` — fine for UI and sparse SFX. Rapid-fire SFX (gunfire, footsteps, impacts): a small pool of AudioSources (see gamedev-patterns pooling), never one AudioSource per spawned object.
- Kill repetition: randomize pitch ±5–10% (`source.pitch = Random.Range(0.95f, 1.05f)`) and rotate 2–3 clip variants.
- Music: two AudioSources crossfading, or mixer snapshot transitions; for intro+loop tracks, schedule with `PlayScheduled(AudioSettings.dspTime + …)` — `Invoke`/coroutine timing drifts.

## Getting audio into the project — three pipelines
1. **In-Unity generation** — `generate_audio` (asset_gen group, **stable in MCP for Unity v10.1.0+**): fal.ai backends (Stable Audio 2.5, CassetteAI SFX, Lyria 2). Async: `generate` returns a job id, poll `status` until it yields the imported AudioClip path. Key: user enters their fal key in Unity's Asset Gen tab (secure store — never in config).
2. **ElevenLabs MCP** (bundled as `elevenlabs`; needs `ELEVENLABS_API_KEY` set in the user's environment — never ask for the key in chat): `text_to_sound_effects` (prompt + duration), `compose_music`, `text_to_speech` for voice. **Always pass an output path inside the project** (`Assets/Audio/...`) — its default output directory is the Desktop. Licensing: free tier is non-commercial + attribution; any paid plan includes commercial use — remind the user before a commercial build ships generated audio.
3. **Stock CC0**: Kenney audio packs (10 packs, CC0, great UI/impact/jingle coverage), OpenGameArt/Freesound via the asset-scout agent (license varies per asset — scout reports it).

Pick 3 for placeholder/prototype speed, 1–2 for tailored content. Generated audio lands under `Assets/Audio/<SFX|Music|Voice>/`; check import settings after (they default wrong for the use case more often than not).

## Verify — honestly
You cannot hear. Verify what's verifiable: clip imported with expected length/channels/sample rate (`manage_asset`), AudioSource wired to the right mixer group with sane spatial settings, no console errors in play mode when the sound triggers. Then **ask the user to listen** for subjective quality — say explicitly that acoustic quality was not machine-verified.
