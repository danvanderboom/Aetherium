## 1. Implementation
- [x] 1.1 Create audio profile models (BiomeAudioProfile, AmbientEmitter)
- [x] 1.2 Implement JsonAudioProfileRepository with SQL/Cosmos stubs
- [x] 1.3 Create Data/Audio/BiomeAudioProfiles.json with default profiles
- [x] 1.4 Implement AudioGenerationPass for PCG pipeline
- [x] 1.5 Register AudioGenerationPass in all BuildPasses arrays (GameMapGrain, WorldGenCLI)
- [x] 1.6 Create AudioPerceptionDto and add to PerceptionDto
- [x] 1.7 Populate audio fields in PerceptionService from terrain and heat tracking
- [x] 1.8 Extend IAudioSystem with spatial/DSP methods (SetListener, PlayPositionalEffect, PlayAmbientLoop, SetReverbPreset, SetOcclusion)
- [x] 1.9 Implement spatial audio in NAudioSystem (distance attenuation, occlusion)
- [x] 1.10 Add fallback implementations in MauiAudioSystem and NullAudioSystem
- [x] 1.11 Create AudioDirector to translate perception to audio actions
- [x] 1.12 Integrate AudioDirector into ClientConsoleDungeonGameNew
- [x] 1.13 Replace direct footstep calls with AudioDirector.PlayFootstep()

## 2. Testing
- [x] 2.1 Add unit tests for JsonAudioProfileRepository
- [x] 2.2 Add unit tests for AudioGenerationPass
- [x] 2.3 Add unit tests for PerceptionService audio population
- [x] 2.4 Add unit tests for AudioDirector logic and hysteresis

## 3. Documentation
- [x] 3.1 Create OpenSpec change proposal
- [x] 3.2 Create OpenSpec audio spec with requirements
- [x] 3.3 Update pcg-core spec with audio pass delta
- [x] 3.4 Update perception-vision spec with audio perception delta

