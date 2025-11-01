using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aetherium.Server.HubWorld
{
    /// <summary>
    /// Loads hub world definitions from JSON files in the Data/Hubs/ directory.
    /// </summary>
    public class HubWorldLoader
    {
        private readonly Dictionary<string, HubDefinition> _hubs = new Dictionary<string, HubDefinition>();
        private readonly string _basePath;
        private bool _loaded = false;

        /// <summary>
        /// Creates a new hub world loader.
        /// </summary>
        /// <param name="basePath">Base path to search for hub JSON files. Defaults to "Data/Hubs/"</param>
        public HubWorldLoader(string basePath = "Data/Hubs")
        {
            _basePath = basePath;
        }

        /// <summary>
        /// Loads all hub definitions from JSON files in the hub directory.
        /// </summary>
        public async Task LoadHubsAsync()
        {
            if (_loaded)
                return;

            if (!Directory.Exists(_basePath))
            {
                Console.WriteLine($"[HubWorldLoader] Hub directory not found: {_basePath}");
                return;
            }

            var jsonFiles = Directory.GetFiles(_basePath, "*.json", SearchOption.AllDirectories);
            Console.WriteLine($"[HubWorldLoader] Loading {jsonFiles.Length} hub definition(s) from {_basePath}");

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    var hub = JsonSerializer.Deserialize<HubDefinition>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    });

                    if (hub != null && !string.IsNullOrEmpty(hub.HubId))
                    {
                        // Ensure "hub" tag is present
                        if (!hub.Tags.Contains("hub", StringComparer.OrdinalIgnoreCase))
                        {
                            hub.Tags.Add("hub");
                        }

                        _hubs[hub.HubId] = hub;
                        Console.WriteLine($"[HubWorldLoader] Loaded hub: {hub.HubId} ({hub.Name})");
                    }
                    else
                    {
                        Console.WriteLine($"[HubWorldLoader] Skipping {filePath}: missing HubId");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HubWorldLoader] Error loading {filePath}: {ex.Message}");
                }
            }

            _loaded = true;
        }

        /// <summary>
        /// Gets a hub definition by ID.
        /// </summary>
        public Task<HubDefinition?> GetHubAsync(string hubId)
        {
            _hubs.TryGetValue(hubId, out var hub);
            return Task.FromResult<HubDefinition?>(hub);
        }

        /// <summary>
        /// Gets all loaded hub definitions.
        /// </summary>
        public Task<List<HubDefinition>> GetAllHubsAsync()
        {
            return Task.FromResult(_hubs.Values.ToList());
        }

        /// <summary>
        /// Gets hub definitions by tag.
        /// </summary>
        public Task<List<HubDefinition>> GetHubsByTagAsync(string tag)
        {
            var hubs = _hubs.Values
                .Where(h => h.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            return Task.FromResult(hubs);
        }

        /// <summary>
        /// Checks if a hub definition exists.
        /// </summary>
        public bool HasHub(string hubId)
        {
            return _hubs.ContainsKey(hubId);
        }
    }
}

