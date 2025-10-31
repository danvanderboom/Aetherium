using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;

namespace Aetherium.Audio
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
        private readonly Dictionary<string, AmbientLoopPlayer> ambientLoops = new Dictionary<string, AmbientLoopPlayer>();
        private readonly object ambientLock = new object();

        private bool isDisposed;
        private float musicVolume;
        private float effectsVolume;
        private AudioListenerState? listenerState;
        private string currentReverbPreset = "outdoor";
        private float currentOcclusion = 0.0f;

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

        public void SetListener(AudioListenerState state)
        {
            listenerState = state;
        }

        public void PlayPositionalEffect(string effectName, AudioVector3 position, AudioPlaybackOptions? options = null)
        {
            if (!IsEnabled)
                return;

            try
            {
                var effectPath = FindAudioFile(effectName, "effects", config.EffectExtensions);
                if (effectPath == null)
                    return;

                var reader = new AudioFileReader(effectPath);
                var volume = options?.Volume ?? effectsVolume;

                // Apply distance attenuation if listener is set
                if (listenerState != null)
                {
                    var distance = CalculateDistance(listenerState.Position, position);
                    var attenuation = CalculateAttenuation(distance, options?.MinDistance ?? 1.0f, options?.MaxDistance ?? 50.0f);
                    volume *= attenuation;

                    // Apply panning (simple left/right based on X offset)
                    var pan = CalculatePan(listenerState.Position, position);
                    // NAudio doesn't have built-in panning in WaveOutEvent, so we approximate with volume balance
                    // For a simple approximation, we can skip this for now
                }

                // Apply occlusion
                volume *= (1.0f - currentOcclusion);

                reader.Volume = volume;

                var player = new WaveOutEvent();
                player.Init(reader);

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
                Console.WriteLine($"[Audio] Error playing positional effect: {ex.Message}");
            }
        }

        public void PlayAmbientLoop(string id, string trackName, AudioPlaybackOptions? options = null)
        {
            if (!IsEnabled)
                return;

            try
            {
                // Stop existing loop with same ID
                StopAmbientLoop(id);

                var trackPath = FindAudioFile(trackName, "music", config.MusicExtensions) 
                    ?? FindAudioFile(trackName, "effects", config.EffectExtensions);
                if (trackPath == null)
                {
                    Console.WriteLine($"[Audio] Ambient loop track not found: {trackName}");
                    return;
                }

                var reader = new AudioFileReader(trackPath);
                var volume = (options?.Volume ?? 0.5f) * (1.0f - currentOcclusion);

                // Apply distance attenuation if positional
                if (options?.Position != null && listenerState != null)
                {
                    var distance = CalculateDistance(listenerState.Position, options.Position.Value);
                    var attenuation = CalculateAttenuation(distance, options.MinDistance, options.MaxDistance);
                    volume *= attenuation;
                }

                reader.Volume = volume;

                var player = new WaveOutEvent();
                player.Init(reader);

                // Setup loop if requested
                if (options?.Loop ?? true)
                {
                    player.PlaybackStopped += (s, e) =>
                    {
                        if (!isDisposed && reader != null)
                        {
                            reader.Position = 0;
                            player.Play();
                        }
                    };
                }

                player.Play();

                lock (ambientLock)
                {
                    ambientLoops[id] = new AmbientLoopPlayer
                    {
                        Player = player,
                        Reader = reader,
                        Options = options
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio] Error playing ambient loop: {ex.Message}");
            }
        }

        public void StopAmbientLoop(string id)
        {
            lock (ambientLock)
            {
                if (ambientLoops.TryGetValue(id, out var loop))
                {
                    try
                    {
                        loop.Player.Stop();
                        loop.Player.Dispose();
                        loop.Reader.Dispose();
                    }
                    catch { /* ignore errors */ }

                    ambientLoops.Remove(id);
                }
            }
        }

        public void SetReverbPreset(string preset)
        {
            currentReverbPreset = preset ?? "outdoor";
            // TODO: Implement actual reverb DSP using NAudio effects
            // For now, this is a stub - volume/low-pass approximation can be added later
        }

        public void SetOcclusion(float amount)
        {
            currentOcclusion = Math.Clamp(amount, 0.0f, 1.0f);
            // Update existing ambient loops with new occlusion
            lock (ambientLock)
            {
                foreach (var loop in ambientLoops.Values)
                {
                    if (loop.Reader != null)
                    {
                        var baseVolume = loop.Options?.Volume ?? 0.5f;
                        loop.Reader.Volume = baseVolume * (1.0f - currentOcclusion);
                    }
                }
            }
        }

        private float CalculateDistance(AudioVector3 a, AudioVector3 b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var dz = b.Z - a.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private float CalculateAttenuation(float distance, float minDistance, float maxDistance)
        {
            if (distance <= minDistance)
                return 1.0f;
            if (distance >= maxDistance)
                return 0.0f;

            // Inverse distance falloff
            var attenuation = minDistance / distance;
            // Smooth falloff to zero at max distance
            var t = (distance - minDistance) / (maxDistance - minDistance);
            return attenuation * (1.0f - t);
        }

        private float CalculatePan(AudioVector3 listenerPos, AudioVector3 sourcePos)
        {
            // Simple panning: left/right based on X offset
            var offsetX = sourcePos.X - listenerPos.X;
            // Normalize to -1 (left) to 1 (right)
            return Math.Clamp(offsetX / 10.0f, -1.0f, 1.0f);
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

        private class AmbientLoopPlayer
        {
            public IWavePlayer Player { get; set; } = null!;
            public AudioFileReader Reader { get; set; } = null!;
            public AudioPlaybackOptions? Options { get; set; }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            StopBackgroundMusic();

            lock (ambientLock)
            {
                foreach (var loop in ambientLoops.Values)
                {
                    try
                    {
                        loop.Player.Stop();
                        loop.Player.Dispose();
                        loop.Reader.Dispose();
                    }
                    catch { /* ignore disposal errors */ }
                }
                ambientLoops.Clear();
            }

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


