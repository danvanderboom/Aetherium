using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aetherium.Server.Audio
{
    /// <summary>
    /// JSON file-based repository for biome audio profiles
    /// </summary>
    public class JsonAudioProfileRepository : IAudioProfileRepository
    {
        private readonly string _filePath;
        private Dictionary<string, BiomeAudioProfile> _profiles = new Dictionary<string, BiomeAudioProfile>();
        private bool _initialized = false;

        public JsonAudioProfileRepository(string filePath = "Data/Audio/BiomeAudioProfiles.json")
        {
            _filePath = filePath;
        }

        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            try
            {
                if (File.Exists(_filePath))
                {
                    var json = await File.ReadAllTextAsync(_filePath);
                    var profiles = JsonSerializer.Deserialize<List<BiomeAudioProfile>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    });

                    if (profiles != null)
                    {
                        _profiles = profiles.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with empty profiles
                Console.WriteLine($"[Audio] Failed to load audio profiles from {_filePath}: {ex.Message}");
            }

            _initialized = true;
        }

        public async Task<BiomeAudioProfile?> GetProfileAsync(string id)
        {
            await EnsureInitializedAsync();
            return _profiles.TryGetValue(id, out var profile) ? profile : null;
        }

        public async Task<IReadOnlyList<BiomeAudioProfile>> GetAllProfilesAsync()
        {
            await EnsureInitializedAsync();
            return _profiles.Values.ToList();
        }

        public async Task SaveProfileAsync(BiomeAudioProfile profile)
        {
            await EnsureInitializedAsync();
            
            if (profile == null || string.IsNullOrWhiteSpace(profile.Id))
                throw new ArgumentException("Profile must have a valid Id", nameof(profile));

            _profiles[profile.Id] = profile;

            // Save to file
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var profilesList = _profiles.Values.ToList();
            var json = JsonSerializer.Serialize(profilesList, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_filePath, json);
        }

        public async Task DeleteProfileAsync(string id)
        {
            await EnsureInitializedAsync();
            
            if (_profiles.Remove(id))
            {
                // Save to file
                var profilesList = _profiles.Values.ToList();
                var json = JsonSerializer.Serialize(profilesList, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_filePath, json);
            }
        }

        private async Task EnsureInitializedAsync()
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }
        }
    }
}

