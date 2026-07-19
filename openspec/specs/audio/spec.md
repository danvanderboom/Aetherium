# audio Specification

## Purpose
TBD - created by archiving change add-procedural-audio-ambiance. Update Purpose after archive.
## Requirements
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
The system SHALL convey room acoustics through occlusion applied as volume attenuation and through distance attenuation. Reverb DSP, stereo panning, and frequency-domain occlusion filtering are NOT supported by the console audio backend (a single mixed output with no per-source stereo balance); a requested reverb preset SHALL be recorded for introspection but SHALL NOT alter the signal.

#### Scenario: Reverb preset recorded but not applied
- **WHEN** a reverb preset is set
- **THEN** the preset is recorded (readable for introspection) and the audio signal is unchanged (reverb DSP is unsupported)

#### Scenario: Occlusion affects volume only
- **WHEN** an occlusion value is set
- **THEN** audio volume reflects the occlusion amount; no frequency filtering is applied (unsupported)

### Requirement: Audio Runtime Availability & Fallback
The client SHALL select a working audio implementation at startup and SHALL degrade to silence without disrupting the terminal UI when audio output is unavailable. Playback failures SHALL mute audio for the session and be recorded for diagnostics; they SHALL NOT be written to the console (which the TUI owns). A missing audio asset SHALL be a silent soft failure that does not disable audio.

#### Scenario: No output device selects the null implementation
- **WHEN** the client starts on a host without audio output (e.g. a non-Windows or headless machine)
- **THEN** the null (no-op) audio system is selected up front, so no play call throws into the terminal UI

#### Scenario: A playback error mutes silently
- **WHEN** an audio device or playback error occurs at runtime
- **THEN** audio is disabled for the rest of the session and the error is recorded (not printed to the console)

#### Scenario: A missing asset is silent and non-disabling
- **WHEN** a requested music track or sound effect file does not exist
- **THEN** the call returns without error, without console output, and audio remains enabled

### Requirement: Audio Asset Resolution
The audio system SHALL resolve its asset directory relative to the application base directory when the configured path is not absolute, and the build SHALL copy the audio asset tree to the output directory, so audio files are found when the client runs from its build output.

#### Scenario: Relative asset path resolves under the base directory
- **WHEN** the configured asset path is relative (the default `Assets/Audio`)
- **THEN** the resolved asset root is an absolute path under the application base directory

#### Scenario: Absolute asset path is preserved
- **WHEN** the configured asset path is absolute
- **THEN** the resolved asset root equals the configured path unchanged

