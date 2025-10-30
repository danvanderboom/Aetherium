using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;

namespace ConsoleGame.Audio
{
    /// <summary>
    /// NAudio-based implementation of the audio system
    /// </summary>
    public class NAudioSystem : IAudioSystem
    {
        private readonly AudioConfig config;
        private readonly string[] musicPlaylist;
        private int currentTrackIndex = 0;

        private IWavePlayer? musicPlayer;
        private AudioFileReader? musicReader;
        
        private readonly List<IWavePlayer> effectPlayers = new List<IWavePlayer>();
        private readonly object effectLock = new object();

        private bool isDisposed;
        private float musicVolume;
        private float effectsVolume;

        public bool IsEnabled { get; set; }
        public float MusicVolume => musicVolume;
        public float EffectsVolume => effectsVolume;
        public string? CurrentTrack { get; private set; }

        public NAudioSystem(AudioConfig config)
        {
            this.config = config ?? new AudioConfig();
            IsEnabled = this.config.Enabled;
            musicVolume = this.config.MusicVolume;
            effectsVolume = this.config.EffectsVolume;

            // Build music playlist
            musicPlaylist = new[]
            {
                "mellow-guitar-loop",
                "techno-synth-loop",
                "dungeon-ambience-loop"
            };

            // Find index of default track
            currentTrackIndex = Array.IndexOf(musicPlaylist, this.config.DefaultMusicTrack);
            if (currentTrackIndex < 0)
                currentTrackIndex = 0;
        }

        public void PlayBackgroundMusic(string trackName, bool loop = true)
        {
            if (!IsEnabled)
                return;

            try
            {
                // Stop current music
                StopBackgroundMusic();

                // Find the music file
                var musicPath = FindAudioFile(trackName, "music", config.MusicExtensions);
                if (musicPath == null)
                {
                    Console.WriteLine($"[Audio] Music track not found: {trackName}");
                    return;
                }

                // Create music player
                musicReader = new AudioFileReader(musicPath);
                musicReader.Volume = musicVolume;

                musicPlayer = new WaveOutEvent();
                musicPlayer.Init(musicReader);

                if (loop)
                {
                    musicPlayer.PlaybackStopped += (s, e) =>
                    {
                        if (!isDisposed && musicReader != null)
                        {
                            musicReader.Position = 0;
                            musicPlayer?.Play();
                        }
                    };
                }

                musicPlayer.Play();
                CurrentTrack = trackName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio] Error playing music: {ex.Message}");
            }
        }

        public void StopBackgroundMusic()
        {
            if (musicPlayer != null)
            {
                musicPlayer.Stop();
                musicPlayer.Dispose();
                musicPlayer = null;
            }

            if (musicReader != null)
            {
                musicReader.Dispose();
                musicReader = null;
            }

            CurrentTrack = null;
        }

        public void PlaySoundEffect(string effectName)
        {
            if (!IsEnabled)
                return;

            try
            {
                // Find the sound effect file
                var effectPath = FindAudioFile(effectName, "effects", config.EffectExtensions);
                if (effectPath == null)
                {
                    // Silently fail for missing effects (don't clutter console)
                    return;
                }

                // Play effect on a separate channel
                var reader = new AudioFileReader(effectPath);
                reader.Volume = effectsVolume;

                var player = new WaveOutEvent();
                player.Init(reader);

                // Cleanup when done
                player.PlaybackStopped += (s, e) =>
                {
                    player.Dispose();
                    reader.Dispose();
                    
                    lock (effectLock)
                    {
                        effectPlayers.Remove(player);
                    }
                };

                lock (effectLock)
                {
                    effectPlayers.Add(player);
                }

                player.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio] Error playing sound effect: {ex.Message}");
            }
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Math.Clamp(volume, 0f, 1f);
            
            if (musicReader != null)
            {
                musicReader.Volume = musicVolume;
            }
        }

        public void SetEffectsVolume(float volume)
        {
            effectsVolume = Math.Clamp(volume, 0f, 1f);
        }

        public void NextMusicTrack()
        {
            if (musicPlaylist.Length == 0)
                return;

            currentTrackIndex = (currentTrackIndex + 1) % musicPlaylist.Length;
            var nextTrack = musicPlaylist[currentTrackIndex];
            PlayBackgroundMusic(nextTrack, loop: true);
        }

        private string? FindAudioFile(string name, string subfolder, string[] extensions)
        {
            foreach (var ext in extensions)
            {
                var path = Path.Combine(config.AssetPath, subfolder, name + ext);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            StopBackgroundMusic();

            lock (effectLock)
            {
                foreach (var player in effectPlayers.ToList())
                {
                    try
                    {
                        player.Stop();
                        player.Dispose();
                    }
                    catch
                    {
                        // Ignore disposal errors
                    }
                }
                effectPlayers.Clear();
            }
        }
    }
}

