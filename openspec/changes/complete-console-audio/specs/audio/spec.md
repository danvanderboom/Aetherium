## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: Acoustic Simulation
The system SHALL convey room acoustics through occlusion applied as volume attenuation and through distance attenuation. Reverb DSP, stereo panning, and frequency-domain occlusion filtering are NOT supported by the console audio backend (a single mixed output with no per-source stereo balance); a requested reverb preset SHALL be recorded for introspection but SHALL NOT alter the signal.

#### Scenario: Reverb preset recorded but not applied
- **WHEN** a reverb preset is set
- **THEN** the preset is recorded (readable for introspection) and the audio signal is unchanged (reverb DSP is unsupported)

#### Scenario: Occlusion affects volume only
- **WHEN** an occlusion value is set
- **THEN** audio volume reflects the occlusion amount; no frequency filtering is applied (unsupported)
