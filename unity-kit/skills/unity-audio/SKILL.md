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

## Getting audio into the project — four pipelines
0. **Code-synthesized (free, no keys, instant)** — for retro/arcade SFX and placeholders, *write the sound*: a Python script using only stdlib `wave`/`math` (oscillators + pitch sweeps + envelopes + filtered noise) synthesizes coin/laser/jump/explosion/click WAVs directly into `Assets/Audio/SFX/`. A working starter generator ships with the plugin: `python "${CLAUDE_PLUGIN_ROOT}/scripts/gen-sfx.py" <output-dir>` — copy and tune its per-sound functions for the project's aesthetic. Recipes: coin = two-note rising square arpeggio with fast decay; laser = exponential sweep 1800→300 Hz; explosion = white noise through a widening moving-average (darkening tail) with long decay; click = 6 ms sine blip + noise burst. Normalize to ~0.85 peak, 44.1 kHz 16-bit mono. For runtime/variated SFX inside Unity, **usfxr** (C# sfxr port — sounds are parameter sets, mutate for variation). Local AI models (Stable Audio Open, AudioCraft) are the heavier free tier — GPU + setup required, and verify the model's output-license terms before commercial use.
2. **ElevenLabs MCP** (bundled as `elevenlabs`; needs `ELEVENLABS_API_KEY` set in the user's environment — never ask for the key in chat): `text_to_sound_effects` (prompt + duration), `compose_music`, `text_to_speech` for voice. **Always pass an output path inside the project** (`Assets/Audio/...`) — its default output directory is the Desktop. Licensing: free tier is non-commercial + attribution; any paid plan includes commercial use — remind the user before a commercial build ships generated audio.
3. **Stock CC0**: Kenney audio packs (10 packs, CC0, great UI/impact/jingle coverage), OpenGameArt/Freesound via the asset-scout agent (license varies per asset — scout reports it).

Pick 0 for instant free retro SFX, 3 for stock placeholder/prototype speed, 1–2 for tailored or realistic content. Generated audio lands under `Assets/Audio/<SFX|Music|Voice>/`; check import settings after (they default wrong for the use case more often than not).

## Verify — honestly
You cannot hear. Verify what's verifiable: clip imported with expected length/channels/sample rate (`manage_asset`), AudioSource wired to the right mixer group with sane spatial settings, no console errors in play mode when the sound triggers. Then **ask the user to listen** for subjective quality — say explicitly that acoustic quality was not machine-verified.
