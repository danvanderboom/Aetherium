using System;

namespace ConsoleGame.Audio
{
    /// <summary>
    /// Null object pattern implementation of IAudioSystem for when audio is disabled
    /// or unavailable
    /// </summary>
    public class NullAudioSystem : IAudioSystem
    {
        public bool IsEnabled { get; set; }
        public float MusicVolume => 0f;
        public float EffectsVolume => 0f;
        public string? CurrentTrack => null;

        public void PlayBackgroundMusic(string trackName, bool loop = true)
        {
            // No-op
        }

        public void StopBackgroundMusic()
        {
            // No-op
        }

        public void PlaySoundEffect(string effectName)
        {
            // No-op
        }

        public void SetMusicVolume(float volume)
        {
            // No-op
        }

        public void SetEffectsVolume(float volume)
        {
            // No-op
        }

        public void NextMusicTrack()
        {
            // No-op
        }

        public void Dispose()
        {
            // No-op
        }
    }
}

