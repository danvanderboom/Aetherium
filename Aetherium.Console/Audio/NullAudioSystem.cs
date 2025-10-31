using System;

namespace Aetherium.Audio
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

        public void SetListener(AudioListenerState state)
        {
            // No-op
        }

        public void PlayPositionalEffect(string effectName, AudioVector3 position, AudioPlaybackOptions? options = null)
        {
            // No-op
        }

        public void PlayAmbientLoop(string id, string trackName, AudioPlaybackOptions? options = null)
        {
            // No-op
        }

        public void StopAmbientLoop(string id)
        {
            // No-op
        }

        public void SetReverbPreset(string preset)
        {
            // No-op
        }

        public void SetOcclusion(float amount)
        {
            // No-op
        }

        public void Dispose()
        {
            // No-op
        }
    }
}


