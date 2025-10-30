using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Orleans;

namespace ConsoleGame.WorldGen.Prefabs
{
    /// <summary>
    /// In-memory registry of prefab templates.
    /// Supports file-based loading (dev) and grain-based storage (production).
    /// </summary>
    public class PrefabLibrary
    {
        private readonly ConcurrentDictionary<string, PrefabTemplate> _prefabs = new();
        private readonly IGrainFactory? _grainFactory;
        private readonly bool _useGrainStorage;

        /// <summary>
        /// Creates a file-based prefab library.
        /// </summary>
        public PrefabLibrary()
        {
            _useGrainStorage = false;
        }

        /// <summary>
        /// Creates a grain-based prefab library (for distributed storage).
        /// </summary>
        public PrefabLibrary(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory ?? throw new ArgumentNullException(nameof(grainFactory));
            _useGrainStorage = true;
        }

        /// <summary>
        /// Loads all prefabs from a directory (file mode only).
        /// </summary>
        public void LoadFromDirectory(string directoryPath)
        {
            if (_useGrainStorage)
                throw new InvalidOperationException("Cannot load from directory when using grain storage");

            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"[PrefabLibrary] Directory not found: {directoryPath}");
                return;
            }

            var jsonFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);
            Console.WriteLine($"[PrefabLibrary] Loading {jsonFiles.Length} prefab(s) from {directoryPath}");

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var prefab = JsonSerializer.Deserialize<PrefabTemplate>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (prefab != null)
                    {
                        _prefabs[prefab.PrefabId] = prefab;
                        Console.WriteLine($"[PrefabLibrary] Loaded prefab: {prefab.PrefabId} ({prefab.Name})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PrefabLibrary] Error loading {filePath}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Registers a prefab in the library.
        /// </summary>
        public async Task RegisterPrefabAsync(PrefabTemplate prefab)
        {
            if (string.IsNullOrEmpty(prefab.PrefabId))
                throw new ArgumentException("PrefabId cannot be null or empty", nameof(prefab));

            _prefabs[prefab.PrefabId] = prefab;

            if (_useGrainStorage && _grainFactory != null)
            {
                // TODO: Store in IPrefabLibraryGrain when implemented
                await Task.CompletedTask;
            }
        }

        /// <summary>
        /// Gets a prefab by ID.
        /// </summary>
        public Task<PrefabTemplate?> GetPrefabAsync(string prefabId)
        {
            _prefabs.TryGetValue(prefabId, out var prefab);
            return Task.FromResult(prefab);
        }

        /// <summary>
        /// Searches for prefabs by category and/or tags.
        /// </summary>
        public List<PrefabTemplate> SearchPrefabs(string? category = null, List<string>? tags = null)
        {
            var results = _prefabs.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(category))
            {
                results = results.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            }

            if (tags != null && tags.Count > 0)
            {
                results = results.Where(p =>
                    tags.Any(tag => p.Metadata.ContainsKey(tag) ||
                                   p.Metadata.Values.Any(v => v.Contains(tag, StringComparison.OrdinalIgnoreCase))));
            }

            return results.ToList();
        }

        /// <summary>
        /// Lists all prefab IDs.
        /// </summary>
        public List<string> ListPrefabIds()
        {
            return _prefabs.Keys.ToList();
        }

        /// <summary>
        /// Lists all prefabs in a category.
        /// </summary>
        public List<PrefabTemplate> GetByCategory(string category)
        {
            return _prefabs.Values
                .Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Gets count of loaded prefabs.
        /// </summary>
        public int Count => _prefabs.Count;
    }
}

