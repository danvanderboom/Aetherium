## Why
Currently have visual PCG but audio could enhance immersion. Procedural audio generation based on biome/room types will create more engaging and dynamic game experiences.

## What Changes
- Add `AudioGenerationPass` to PCG pipeline to analyze terrain and create audio zones
- Define `BiomeAudioProfile` system with JSON storage and stubs for SQL Server/Cosmos DB adapters
- Extend `IAudioSystem` with spatial audio, ambient loops, reverb, and occlusion support
- Add `AudioDirector` to translate perception data into audio actions
- Extend `PerceptionDto` with `AudioPerceptionDto` containing biome, danger level, reverb, occlusion, footstep material
- Integrate audio updates into client perception handling

## Impact
- Affected specs: `pcg-core` (new audio pass), `perception-vision` (audio perception fields), new `audio` spec
- Affected code: `Aetherium.Server/Audio/`, `Aetherium.Server/WorldGen/Passes/`, `Aetherium.Server/PerceptionService.cs`, `Aetherium.Console/Audio/`, `Aetherium.Console/Core/ClientConsoleDungeonGameNew.cs`, `Aetherium.Model/`

