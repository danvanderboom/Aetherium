# Game Audio Assets

This directory contains curated background music and sound effects for the Console Game client.

## Status: no assets bundled by default

Only placeholders (`.gitkeep`) and this README ship in the repo — **no audio files are committed**, so the client runs **silently by default**. This is intentional (avoids bundling third-party binaries). The audio path degrades gracefully:

- `NAudioSystem` resolves this folder under the app base directory (`bin/.../Assets/Audio`) and the `.csproj` copies the tree to output, so the moment you drop real files into `music/` and `effects/` (using the filenames below) they are found and played — no code change needed.
- On audio-less or non-Windows hosts, the client selects `NullAudioSystem` up front (`NAudioSystem.IsOutputAvailable()`), and any later device error mutes audio silently rather than writing into the TUI.
- Reverb DSP and stereo panning are **unsupported**; spatial cues come from distance attenuation and occlusion (volume) only.

## Licensing

- Primary source: Kenney Game Assets — CC0 (Public Domain). No attribution required.
  - Website: https://kenney.nl/assets
  - Music pack: https://kenney.nl/assets/music-loops
  - Various SFX packs (UI, footsteps, impacts, doors): browse via https://kenney.nl/assets
- Fallback music (if needed): Kevin MacLeod (Incompetech) — CC-BY 4.0 (Attribution required).
  - Collections: https://incompetech.com/music/royalty-free/collections.php
  - Attribution template: "[Track Title]" by Kevin MacLeod (https://incompetech.com) — Licensed under CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/)

If any non-CC0 assets are used, keep the exact attribution text below.

## File layout

- Music: `Assets/Audio/music/*.wav`
- Effects: `Assets/Audio/effects/*.wav`

## Current filenames and intended usage

Music (loopable):
- `mellow-guitar-loop.wav` — ambient/light exploration
- `techno-synth-loop.wav` — upbeat/combat/test
- `dungeon-ambience-loop.wav` — dark exploration

Effects:
- `footstep.wav` — player movement
- `teleport.wav` — teleport action
- `item-pickup.wav` — pickup success
- `item-drop.wav` — drop success
- `door-unlock.wav` — opening/unlocking a door
- `door-close.wav` — closing a door

These names match the in-code calls in `Aetherium.Core.ClientConsoleDungeonGameNew` and `Aetherium.Audio.NAudioSystem`.

## Source notes

- If these files originate from Kenney packs, they are CC0.
- If any were sourced from CC-BY creators (e.g., Kevin MacLeod), add per-track attributions here:

Attributions (only if CC-BY was used):
- Track: "<title>" by <author> — CC-BY 4.0 — <link>
- SFX: "<title>" by <author> — CC-BY 4.0 — <link>


