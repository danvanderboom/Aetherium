## ADDED Requirements

### Requirement: Biome Audio Profiles
The system SHALL define audio profiles for each biome/room type with ambient soundscapes, music tracks, footstep materials, reverb presets, and occlusion values.

#### Scenario: Profile loaded for biome
- WHEN a biome is detected during world generation
- THEN the corresponding audio profile is loaded from repository

#### Scenario: Profile stored in JSON
- WHEN audio profiles are defined
- THEN they are stored in Data/Audio/BiomeAudioProfiles.json by default

#### Scenario: Repository adapters support SQL/Cosmos
- WHEN SQL Server or Cosmos DB adapters are configured
- THEN profiles can be loaded from database instead of JSON

### Requirement: PCG Audio Zone Generation
The system SHALL generate audio zones during procedural generation by analyzing terrain types and computing acoustic properties.

#### Scenario: Audio zones created from terrain
- WHEN world generation completes
- THEN audio zones are created for each terrain location with biome mapping

#### Scenario: Reverb computed from room shape
- WHEN a room is analyzed
- THEN reverb preset is computed based on connected open spaces

#### Scenario: Occlusion computed from walls
- WHEN a location is analyzed
- THEN occlusion value is computed based on blocking neighbors

### Requirement: Audio Perception Data
The system SHALL include audio perception data in PerceptionDto with biome, danger level, reverb preset, occlusion, footstep material, and suggested music track.

#### Scenario: Audio perception populated
- WHEN perception is computed
- THEN AudioPerceptionDto is populated from terrain and heat tracking

#### Scenario: Danger level from heat tracking
- WHEN heat signatures exist near player
- THEN danger level is computed and included in audio perception

#### Scenario: Footstep material from terrain
- WHEN player is on terrain
- THEN footstep material type is determined from terrain type

### Requirement: Spatial Audio Support
The system SHALL support 3D spatial audio with listener positioning, distance attenuation, and occlusion filtering.

#### Scenario: Positional effects play at distance
- WHEN a sound effect is played at a position
- THEN volume attenuates based on distance from listener

#### Scenario: Listener position updated
- WHEN player moves or rotates
- THEN listener position and orientation are updated

#### Scenario: Occlusion affects volume
- WHEN occlusion value changes
- THEN audio volume is reduced proportionally

### Requirement: Ambient Soundscapes
The system SHALL play ambient loops based on biome with automatic transitions when biome changes.

#### Scenario: Ambient loop plays for biome
- WHEN player enters a biome
- THEN the biome's ambient loop starts playing

#### Scenario: Loop transitions smoothly
- WHEN player moves from one biome to another
- THEN old ambient loop stops and new one starts

#### Scenario: Ambient emitters at fixed positions
- WHEN ambient emitters are defined in profile
- THEN they play at their specified positions with distance attenuation

### Requirement: Adaptive Music
The system SHALL play different music tracks based on danger level (exploration vs danger/combat) with hysteresis to prevent thrashing.

#### Scenario: Exploration music in safe areas
- WHEN danger level is below threshold
- THEN exploration music track plays

#### Scenario: Danger music in combat areas
- WHEN danger level exceeds threshold
- THEN danger music track plays

#### Scenario: Hysteresis prevents thrashing
- WHEN danger level oscillates near threshold
- THEN music does not change until crossing safe/danger thresholds

### Requirement: Terrain-Aware Footsteps
The system SHALL play footstep sounds appropriate to the current terrain material type.

#### Scenario: Grass footsteps on grass terrain
- WHEN player moves on grass terrain
- THEN grass footstep sound plays

#### Scenario: Stone footsteps on stone terrain
- WHEN player moves on stone terrain
- THEN stone footstep sound plays

#### Scenario: Water footsteps on water terrain
- WHEN player moves on water terrain
- THEN water footstep sound plays

### Requirement: Acoustic Simulation
The system SHALL apply reverb presets and occlusion filtering to simulate room acoustics.

#### Scenario: Reverb preset applied
- WHEN reverb preset is set
- THEN audio system applies reverb effect (or approximation)

#### Scenario: Occlusion filtering applied
- WHEN occlusion value is set
- THEN audio volume and frequency filtering reflect occlusion

