using System;

namespace Aetherium.Audio
{
    /// <summary>
    /// Interface for game audio system supporting music and sound effects
    /// </summary>
    public interface IAudioSystem : IDisposable
    {
        /// <summary>
        /// Whether audio is currently enabled
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// Play background music track
        /// </summary>
        /// <param name="trackName">Name of the music track (without path/extension)</param>
        /// <param name="loop">Whether to loop the track</param>
        void PlayBackgroundMusic(string trackName, bool loop = true);

        /// <summary>
        /// Stop currently playing background music
        /// </summary>
        void StopBackgroundMusic();

        /// <summary>
        /// Play a sound effect
        /// </summary>
        /// <param name="effectName">Name of the sound effect (without path/extension)</param>
        void PlaySoundEffect(string effectName);

        /// <summary>
        /// Set music volume (0.0 to 1.0)
        /// </summary>
        void SetMusicVolume(float volume);

        /// <summary>
        /// Set effects volume (0.0 to 1.0)
        /// </summary>
        void SetEffectsVolume(float volume);

        /// <summary>
        /// Get current music volume
        /// </summary>
        float MusicVolume { get; }

        /// <summary>
        /// Get current effects volume
        /// </summary>
        float EffectsVolume { get; }

        /// <summary>
        /// Cycle to the next music track in the playlist
        /// </summary>
        void NextMusicTrack();

        /// <summary>
        /// Get the currently playing track name
        /// </summary>
        string? CurrentTrack { get; }

        /// <summary>
        /// Set listener position and orientation for 3D audio
        /// </summary>
        void SetListener(AudioListenerState state);

        /// <summary>
        /// Play a sound effect at a specific 3D position
        /// </summary>
        /// <param name="effectName">Name of the sound effect (without path/extension)</param>
        /// <param name="position">3D position for spatial audio</param>
        /// <param name="options">Optional playback options</param>
        void PlayPositionalEffect(string effectName, AudioVector3 position, AudioPlaybackOptions? options = null);

        /// <summary>
        /// Play an ambient loop with an ID (can be stopped by ID)
        /// </summary>
        /// <param name="id">Unique identifier for this loop</param>
        /// <param name="trackName">Name of the track (without path/extension)</param>
        /// <param name="options">Optional playback options</param>
        void PlayAmbientLoop(string id, string trackName, AudioPlaybackOptions? options = null);

        /// <summary>
        /// Stop an ambient loop by ID
        /// </summary>
        /// <param name="id">Unique identifier for the loop to stop</param>
        void StopAmbientLoop(string id);

        /// <summary>
        /// Set reverb preset (e.g., "hall", "cave", "room", "outdoor")
        /// </summary>
        void SetReverbPreset(string preset);

        /// <summary>
        /// Set occlusion amount (0.0 = no occlusion, 1.0 = fully occluded)
        /// Affects volume and frequency filtering
        /// </summary>
        void SetOcclusion(float amount);
    }
}


