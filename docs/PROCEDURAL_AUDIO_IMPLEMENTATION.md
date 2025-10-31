# Procedural Audio & Ambiance Implementation

## Overview

This document describes the procedural audio and ambiance system implementation, which adds immersive audio experiences to procedurally generated worlds based on biome types, danger levels, and spatial positioning.

## Features

### 1. Biome Audio Profiles
- **JSON-based profiles** stored in `Data/Audio/BiomeAudioProfiles.json`
- Defines ambient loops, exploration/danger music, footstep materials, reverb presets, and occlusion values per biome
- **Repository pattern** with `JsonAudioProfileRepository` (default), plus stubs for SQL Server and Cosmos DB

### 2. PCG Audio Zone Generation
- **AudioGenerationPass** runs during world generation (Adaptation phase)
- Analyzes terrain to determine biomes and create audio zones
- Computes reverb and occlusion heuristics from room shapes and walls
- Stores audio zones in `WorldGenerationContext.SharedData` for use by perception system

### 3. Audio Perception Integration
- **AudioPerceptionDto** added to `PerceptionDto`
- Contains: biome, danger level, reverb preset, occlusion, footstep material, ambient emitters, suggested music track
- Populated in `PerceptionService` from terrain analysis and heat tracking

### 4. Spatial Audio System
- **Extended IAudioSystem** with:
  - `SetListener()` - Update listener position/orientation
  - `PlayPositionalEffect()` - Play sounds at 3D positions with distance attenuation
  - `PlayAmbientLoop()` / `StopAmbientLoop()` - Manage ambient loops by ID
  - `SetReverbPreset()` / `SetOcclusion()` - Apply acoustic effects
- **NAudioSystem** implements spatial audio with distance attenuation and occlusion
- **MauiAudioSystem** and **NullAudioSystem** provide fallback implementations

### 5. AudioDirector
- **Translates perception → audio actions**
- Handles:
  - **Biome transitions** - Automatically starts/stops ambient loops when biome changes
  - **Adaptive music** - Switches between exploration and danger music based on danger level with hysteresis
  - **Terrain-aware footsteps** - Selects appropriate footstep sound based on material
  - **Ambient emitters** - Plays positioned ambient sounds from profiles
  - **Acoustic updates** - Updates reverb and occlusion based on environment

## Architecture

### Server-Side Components

```
Aetherium.Server/Audio/
├── BiomeAudioProfile.cs          # Profile model
├── IAudioProfileRepository.cs    # Repository interface
├── JsonAudioProfileRepository.cs # JSON implementation
├── SqlServerAudioProfileRepository.cs # SQL stub
└── CosmosAudioProfileRepository.cs   # Cosmos stub

Aetherium.Server/WorldGen/Passes/
└── AudioGenerationPass.cs        # PCG pass for audio zones

Aetherium.Server/PerceptionService.cs
└── ComputeAudioPerception()     # Populates audio fields
```

### Client-Side Components

```
Aetherium.Console/Audio/
├── IAudioSystem.cs              # Extended interface
├── AudioTypes.cs                # AudioVector3, AudioListenerState, AudioPlaybackOptions
├── NAudioSystem.cs              # Spatial audio implementation
├── AudioDirector.cs             # Perception → audio translator
└── [MauiAudioSystem, NullAudioSystem] # Fallbacks

Aetherium.Console/Core/
└── ClientConsoleDungeonGameNew.cs # Integration point
```

### Data Models

```
Aetherium.Model/
├── PerceptionDto.cs             # Contains AudioPerceptionDto
└── AudioPerceptionDto.cs        # Audio perception data
```

## Usage

### World Generation
Audio zones are automatically generated during world generation when `AudioGenerationPass` runs. The pass:
1. Analyzes terrain types to determine biomes
2. Creates audio zones with biome mappings
3. Computes reverb presets from room connectivity
4. Computes occlusion from surrounding walls
5. Stores results in `WorldGenerationContext.SharedData`

### Client Integration
The `AudioDirector` automatically handles audio updates:

```csharp
// In ClientConsoleDungeonGameNew
private readonly AudioDirector audioDirector;

// On perception update
audioDirector.OnPerception(perception);

// For footsteps
audioDirector.PlayFootstep(); // Uses current material
```

### Customization
Modify `Data/Audio/BiomeAudioProfiles.json` to add/change biome profiles:

```json
{
  "id": "my-biome",
  "name": "My Biome",
  "ambientLoop": "my-ambience-loop",
  "explorationMusic": "my-exploration-music",
  "dangerMusic": "my-danger-music",
  "footstepMaterial": "dirt",
  "reverbPreset": "room",
  "baseOcclusion": 0.3
}
```

## Testing

Unit tests cover:
- **JsonAudioProfileRepository** - Profile loading, saving, deletion
- **AudioGenerationPass** - Zone creation, biome mapping, metrics
- **PerceptionService** - Audio perception population from terrain/heat
- **AudioDirector** - Perception translation, hysteresis, footstep selection

Run tests:
```bash
dotnet test Aetherium.Test/Audio/
```

## Future Enhancements

- **Full reverb DSP** - Implement actual reverb effects in NAudioSystem (currently stubbed)
- **Low-pass filtering for occlusion** - Add frequency filtering based on occlusion
- **Dynamic ambient emitters** - Generate ambient emitters procedurally based on world features
- **Audio occlusion rays** - Compute occlusion more accurately using ray casting
- **Music transitions** - Smooth crossfades between music tracks
- **Audio streaming** - Stream large audio files instead of loading fully
- **Multi-layer ambient mixing** - Support multiple overlapping ambient layers

## Technical Notes

### Hysteresis
AudioDirector uses hysteresis thresholds (danger: 0.3, safe: 0.2) to prevent music from thrashing when danger level oscillates near the threshold.

### Distance Attenuation
Uses inverse distance falloff with configurable min/max distances. Volume reduces smoothly from min distance to zero at max distance.

### Occlusion Approximation
Currently uses volume reduction as occlusion approximation. Full implementation would add low-pass filtering to simulate sound muffling through walls.

### Reverb Presets
Reverb preset selection is heuristic-based (counting open neighbors). Full implementation would apply actual reverb DSP effects.

## References

- OpenSpec: `openspec/changes/add-procedural-audio-ambiance/`
- Audio Profiles: `Data/Audio/BiomeAudioProfiles.json`
- Tests: `Aetherium.Test/Audio/`

