#if MAUI_AUDIO
using System;
using System.Collections.Generic;
using System.IO;
using ConsoleGame.Audio;
using Plugin.SimpleAudioPlayer;

namespace ConsoleGame.Audio
{
	/// <summary>
	/// MAUI-compatible audio system using Plugin.SimpleAudioPlayer.
	/// Compile with MAUI_AUDIO and add the NuGet package Plugin.SimpleAudioPlayer in the MAUI frontend project.
	/// </summary>
	public class MauiAudioSystem : IAudioSystem
	{
		private readonly AudioConfig config;
		private ISimpleAudioPlayer? musicPlayer;
		private readonly List<ISimpleAudioPlayer> effectPlayers = new List<ISimpleAudioPlayer>();
		private float musicVolume;
		private float effectsVolume;

		public bool IsEnabled { get; set; }
		public float MusicVolume => musicVolume;
		public float EffectsVolume => effectsVolume;
		public string? CurrentTrack { get; private set; }

		public MauiAudioSystem(AudioConfig? cfg = null)
		{
			config = cfg ?? new AudioConfig();
			IsEnabled = config.Enabled;
			musicVolume = config.MusicVolume;
			effectsVolume = config.EffectsVolume;
		}

		public void PlayBackgroundMusic(string trackName, bool loop = true)
		{
			if (!IsEnabled) return;
			StopBackgroundMusic();

			var path = FindAudioFile(trackName, "music", config.MusicExtensions);
			if (path == null) return;

			var player = CrossSimpleAudioPlayer.CreateSimpleAudioPlayer();
			if (TryLoad(player, path))
			{
				player.Loop = loop;
				player.Volume = Clamp01(musicVolume);
				player.Play();
				musicPlayer = player;
				CurrentTrack = trackName;
			}
		}

		public void StopBackgroundMusic()
		{
			if (musicPlayer != null)
			{
				musicPlayer.Stop();
				musicPlayer.Dispose();
				musicPlayer = null;
				CurrentTrack = null;
			}
		}

		public void PlaySoundEffect(string effectName)
		{
			if (!IsEnabled) return;
			var path = FindAudioFile(effectName, "effects", config.EffectExtensions);
			if (path == null) return;

			var player = CrossSimpleAudioPlayer.CreateSimpleAudioPlayer();
			if (TryLoad(player, path))
			{
				player.Loop = false;
				player.Volume = Clamp01(effectsVolume);
				player.PlaybackEnded += (s, e) =>
				{
					player.Dispose();
					effectPlayers.Remove(player);
				};
				effectPlayers.Add(player);
				player.Play();
			}
		}

		public void SetMusicVolume(float volume)
		{
			musicVolume = Clamp01(volume);
			if (musicPlayer != null) musicPlayer.Volume = musicVolume;
		}

		public void SetEffectsVolume(float volume)
		{
			effectsVolume = Clamp01(volume);
		}

		public void NextMusicTrack()
		{
			// No built-in playlist; caller should pass specific track names for MAUI builds
		}

		private string? FindAudioFile(string name, string subfolder, string[] extensions)
		{
			foreach (var ext in extensions)
			{
				var path = Path.Combine(config.AssetPath, subfolder, name + ext);
				if (File.Exists(path)) return path;
			}
			return null;
		}

		private static bool TryLoad(ISimpleAudioPlayer player, string filePath)
		{
			try
			{
				using var fs = File.OpenRead(filePath);
				return player.Load(fs);
			}
			catch
			{
				return false;
			}
		}

		private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

		public void Dispose()
		{
			StopBackgroundMusic();
			foreach (var p in effectPlayers.ToArray())
			{
				try { p.Stop(); p.Dispose(); } catch { }
			}
			effectPlayers.Clear();
		}
	}
}
#endif
