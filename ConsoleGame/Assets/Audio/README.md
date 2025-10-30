# Audio Assets

This directory contains audio files for the game including background music and sound effects.

## Directory Structure

```
Assets/Audio/
  music/
    mellow-guitar-loop.mp3     # Calm ambient guitar
    techno-synth-loop.mp3      # Upbeat electronic
    dungeon-ambience-loop.mp3  # Dark atmospheric
  effects/
    door-unlock.wav
    door-close.wav
    footstep.wav
    item-pickup.wav
    item-drop.wav
    teleport.wav
```

## Audio File Requirements

- **Music**: MP3, WAV, or OGG format
- **Effects**: WAV or MP3 format
- All files should be royalty-free or properly licensed

## Where to Find Open-Source Audio

### Music
1. **Freesound.org** - CC0 and CC-BY licensed audio
   - Search for "ambient loop", "guitar loop", "techno loop"
   - Download royalty-free tracks

2. **OpenGameArt.org** - Game-focused audio assets
   - Browse music section for loops
   - Many CC0 options available

3. **Incompetech.com** (Kevin MacLeod)
   - High-quality music tracks
   - Free with attribution (CC-BY 3.0)
   - Search for "ambient", "electronic", "dark"

4. **ZapSplat.com** - Free sound effects
   - Requires free account
   - Large library of game sounds

### Sound Effects
1. **Freesound.org** - Search for specific effects:
   - "door unlock"
   - "door close"
   - "footstep"
   - "item pickup"
   - "teleport"

2. **OpenGameArt.org** - Game sound effects
   - Pre-made game SFX packs

## Adding Audio Files

1. Download audio files from the sources above
2. Place music files in `Assets/Audio/music/`
3. Place sound effect files in `Assets/Audio/effects/`
4. Ensure filenames match exactly:
   - Music: `mellow-guitar-loop.mp3`, `techno-synth-loop.mp3`, `dungeon-ambience-loop.mp3`
   - Effects: `door-unlock.wav`, `door-close.wav`, `footstep.wav`, etc.

## Creating Your Own

If you create your own audio:
1. Music loops should be seamless (fade in/out at loop points)
2. Keep music files under 5MB if possible
3. Sound effects should be short (< 2 seconds)
4. Normalize audio levels for consistency

## Current Status

⚠️ **Audio files are not included in the repository by default.**

To enable audio:
1. Follow the instructions above to acquire audio files
2. Place them in the correct directories
3. The game will automatically detect and use them
4. If files are missing, the game will run silently (no errors)

## License Compliance

When adding audio files, ensure you:
1. Check the license requirements
2. Add attribution if required
3. Document licenses in `AUDIO_LICENSES.txt`
4. Never commit copyrighted material

## Example Attribution Format

Create an `AUDIO_LICENSES.txt` file with:

```
mellow-guitar-loop.mp3
- Source: Freesound.org
- Author: [Author Name]
- License: CC0 1.0
- URL: [link to original]

door-unlock.wav
- Source: OpenGameArt.org
- Author: [Author Name]
- License: CC-BY 3.0
- Attribution: [Required attribution text]
- URL: [link to original]
```

