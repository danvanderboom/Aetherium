using System;
using System.Collections.Generic;
using Aetherium.Model;

namespace Aetherium.Audio
{
    /// <summary>
    /// Translates perception data into audio system calls
    /// Handles ambient soundscapes, adaptive music, terrain-aware footsteps, and spatial audio
    /// </summary>
    public class AudioDirector
    {
        private readonly IAudioSystem audioSystem;
        private string? currentBiome;
        private string? currentMusicTrack;
        private bool isInDanger;
        private readonly Dictionary<string, string> activeAmbientLoops = new Dictionary<string, string>();
        private string? currentFootstepMaterial;
        private AudioVector3 lastListenerPosition = new AudioVector3(0, 0, 0);

        // Hysteresis thresholds to avoid thrashing
        private const float DangerThreshold = 0.3f;
        private const float SafeThreshold = 0.2f;

        public AudioDirector(IAudioSystem audioSystem)
        {
            this.audioSystem = audioSystem ?? throw new ArgumentNullException(nameof(audioSystem));
        }

        /// <summary>
        /// Update audio based on new perception data
        /// </summary>
        public void OnPerception(PerceptionDto perception)
        {
            if (!audioSystem.IsEnabled || perception.Audio == null)
                return;

            var audio = perception.Audio;

            // Update listener position (assume player at 0,0,0 relative)
            UpdateListener(perception);

            // Update biome and ambient loops
            if (audio.Biome != currentBiome)
            {
                UpdateBiomeAudio(audio);
                currentBiome = audio.Biome;
            }

            // Update adaptive music based on danger level
            UpdateAdaptiveMusic(audio);

            // Update reverb and occlusion
            UpdateAcoustic(audio);

            // Store current footstep material
            currentFootstepMaterial = audio.FootstepMaterial;
        }

        /// <summary>
        /// Play a footstep sound using current material
        /// </summary>
        public void PlayFootstep()
        {
            if (!audioSystem.IsEnabled)
                return;

            // Choose footstep effect based on material
            var effectName = currentFootstepMaterial?.ToLowerInvariant() switch
            {
                "grass" => "footstep-grass",
                "water" => "footstep-water",
                "dirt" => "footstep-dirt",
                _ => "footstep" // Default/stone
            };

            // Play at listener position (0,0,0) as positional effect
            audioSystem.PlayPositionalEffect(effectName, new AudioVector3(0, 0, 0), new AudioPlaybackOptions
            {
                Volume = 0.6f,
                MaxDistance = 5.0f,
                MinDistance = 0.5f
            });
        }

        private void UpdateListener(PerceptionDto perception)
        {
            // Update listener position (player is always at 0,0,0 relative)
            var listenerState = new AudioListenerState
            {
                Position = new AudioVector3(0, 0, 0),
                Forward = HeadingToVector(perception.HeadingDegrees),
                Up = new AudioVector3(0, 0, 1)
            };

            audioSystem.SetListener(listenerState);
            lastListenerPosition = listenerState.Position;
        }

        private void UpdateBiomeAudio(AudioPerceptionDto audio)
        {
            // Stop old ambient loops
            foreach (var loopId in activeAmbientLoops.Keys)
            {
                audioSystem.StopAmbientLoop(loopId);
            }
            activeAmbientLoops.Clear();

            // Note: Ambient loops are configured in biome profiles, not in perception
            // The AudioDirector will handle biome-based ambient loops if needed via profiles
            // For now, we rely on suggested music track and ambient emitters

            // Start ambient emitters from perception
            if (audio.AmbientEmitters != null)
            {
                foreach (var emitter in audio.AmbientEmitters)
                {
                    var position = new AudioVector3(emitter.Value.X, emitter.Value.Y, emitter.Value.Z);
                    audioSystem.PlayAmbientLoop(emitter.Key, emitter.Value.TrackName, new AudioPlaybackOptions
                    {
                        Volume = emitter.Value.Volume,
                        Loop = emitter.Value.Loop,
                        Position = position,
                        MaxDistance = 30.0f,
                        MinDistance = 1.0f
                    });
                    activeAmbientLoops[emitter.Key] = emitter.Value.TrackName;
                }
            }
        }

        private void UpdateAdaptiveMusic(AudioPerceptionDto audio)
        {
            // Use hysteresis to avoid thrashing between danger and safe music
            var wasInDanger = isInDanger;
            if (audio.DangerLevel >= DangerThreshold)
                isInDanger = true;
            else if (audio.DangerLevel <= SafeThreshold)
                isInDanger = false;

            // Only change music if state changed
            if (wasInDanger != isInDanger || currentMusicTrack == null)
            {
                var suggestedTrack = audio.SuggestedMusicTrack;
                if (string.IsNullOrEmpty(suggestedTrack))
                {
                    // Fall back to biome-based music
                    suggestedTrack = isInDanger && !string.IsNullOrEmpty(audio.Biome)
                        ? GetDangerTrackForBiome(audio.Biome)
                        : GetExplorationTrackForBiome(audio.Biome);
                }

                if (!string.IsNullOrEmpty(suggestedTrack) && suggestedTrack != currentMusicTrack)
                {
                    audioSystem.PlayBackgroundMusic(suggestedTrack, loop: true);
                    currentMusicTrack = suggestedTrack;
                }
            }
        }

        private void UpdateAcoustic(AudioPerceptionDto audio)
        {
            // Update reverb preset
            if (!string.IsNullOrEmpty(audio.ReverbPreset))
            {
                audioSystem.SetReverbPreset(audio.ReverbPreset);
            }

            // Update occlusion
            audioSystem.SetOcclusion(audio.Occlusion);
        }

        private string? GetDangerTrackForBiome(string? biome)
        {
            // Default danger music
            return "techno-synth-loop";
        }

        private string? GetExplorationTrackForBiome(string? biome)
        {
            return biome?.ToLowerInvariant() switch
            {
                "dungeon" => "dungeon-ambience-loop",
                "cave" => "dungeon-ambience-loop",
                _ => "mellow-guitar-loop"
            };
        }

        private AudioVector3 HeadingToVector(int degrees)
        {
            var radians = degrees * Math.PI / 180.0;
            var x = (float)Math.Sin(radians);
            var y = -(float)Math.Cos(radians); // Negate Y because 0° is North (up = negative Y)
            return new AudioVector3(x, y, 0);
        }
    }
}

