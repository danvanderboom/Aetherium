using System;

namespace ConsoleGame.Audio
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
    }
}

