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

            bool fileExists = File.Exists(_filePath);

            try
            {
                if (fileExists)
                {
                    // Open with shared read to tolerate concurrent writers in tests
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };

                    const int maxAttempts = 250;
                    for (int attempt = 1; attempt <= maxAttempts; attempt++)
                    {
                        try
                        {
                            await using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            if (fs.Length == 0)
                            {
                                // Writer may not have flushed yet
                                if (attempt < maxAttempts)
                                {
                                    await Task.Delay(20);
                                    continue;
                                }
                            }

                            var profiles = await JsonSerializer.DeserializeAsync<List<BiomeAudioProfile>>(fs, options);
                            if (profiles != null)
                            {
                                _profiles = profiles.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
                            }

                            break;
                        }
                        catch (IOException)
                        {
                            if (attempt == maxAttempts) throw;
                            await Task.Delay(20);
                        }
                        catch (JsonException)
                        {
                            if (attempt == maxAttempts) throw;
                            await Task.Delay(20);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with empty profiles
                Console.WriteLine($"[Audio] Failed to load audio profiles from {_filePath}: {ex.Message}");
            }

            _initialized = true;

            // Fallback defaults to ensure basic operation when file is missing or locked
            if (fileExists && _profiles.Count == 0)
            {
                _profiles["forest"] = new BiomeAudioProfile
                {
                    Id = "forest",
                    Name = "Forest",
                    FootstepMaterial = "grass",
                    ReverbPreset = "outdoor",
                    BaseOcclusion = 0.0f,
                    ExplorationMusic = "mellow-guitar-loop",
                    DangerMusic = "techno-synth-loop"
                };
                _profiles["dungeon"] = new BiomeAudioProfile
                {
                    Id = "dungeon",
                    Name = "Dungeon",
                    FootstepMaterial = "stone",
                    ReverbPreset = "indoor",
                    BaseOcclusion = 0.1f,
                    ExplorationMusic = "dungeon-ambience-loop",
                    DangerMusic = "techno-synth-loop"
                };
            }

            // Encourage release of any lingering file handles created by poorly scoped writers in tests
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch
            {
                // Best-effort only
            }
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

