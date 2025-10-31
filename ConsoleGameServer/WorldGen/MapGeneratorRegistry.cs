using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ConsoleGame.WorldGen
{
    /// <summary>
    /// Registry for discovering and instantiating map generators and features by name.
    /// </summary>
    public sealed class MapGeneratorRegistry
    {
        private readonly Dictionary<string, Type> _generatorTypes = new Dictionary<string, Type>();
        private readonly Dictionary<string, Type> _featureTypes = new Dictionary<string, Type>();

        /// <summary>
        /// Discovers all types implementing IMapGenerator and IGenerationFeature in the provided assemblies.
        /// </summary>
        public void DiscoverTypes(params Assembly[] assemblies)
        {
            if (assemblies.Length == 0)
            {
                assemblies = new[] { Assembly.GetExecutingAssembly() };
            }

            foreach (var assembly in assemblies)
            {
                // Discover generators
                var generatorTypes = assembly.GetTypes()
                    .Where(t => typeof(IMapGenerator).IsAssignableFrom(t) && 
                               !t.IsInterface && 
                               !t.IsAbstract)
                    .ToList();

                foreach (var type in generatorTypes)
                {
                    var name = GetTypeName(type);
                    if (!_generatorTypes.ContainsKey(name))
                    {
                        _generatorTypes[name] = type;
                    }
                    else
                    {
                        // Prefer legacy ConsoleGame.MazeGenerator over WorldGen variant when both are present
                        if (string.Equals(name, "Maze", System.StringComparison.OrdinalIgnoreCase))
                        {
                            var existing = _generatorTypes[name];
                            var newIsLegacy = type.FullName != null && type.FullName.Equals("ConsoleGame.MazeGenerator");
                            var existingIsWorldGen = existing.FullName != null && existing.FullName.Contains(".WorldGen.");
                            if (newIsLegacy && existingIsWorldGen)
                            {
                                _generatorTypes[name] = type;
                            }
                        }
                    }
                }

                // Also register legacy Maze generator if present in this assembly even if it doesn't implement IMapGenerator
                var legacyMazeType = assembly.GetType("ConsoleGame.MazeGenerator");
                if (legacyMazeType != null)
                {
                    _generatorTypes["Maze"] = legacyMazeType;
                }

                // Register common aliases for backward compatibility with tests
                // Maze -> RoomsAndCorridors (only if no explicit Maze generator exists)
                var rac = _generatorTypes.FirstOrDefault(kvp => Normalize(kvp.Key).Contains("roomsandcorridors"));
                if (rac.Value != null && !_generatorTypes.ContainsKey("Maze"))
                {
                    _generatorTypes["Maze"] = rac.Value;
                }

                // OutdoorTerrain -> PerlinTerrain (terrain-style generator)
                var perlin = _generatorTypes.FirstOrDefault(kvp => Normalize(kvp.Key).Contains("perlinterrain"));
                if (perlin.Value != null && !_generatorTypes.ContainsKey("OutdoorTerrain"))
                {
                    _generatorTypes["OutdoorTerrain"] = perlin.Value;
                }

                // City -> GridCity (prefer GridCity if available; else any *City)
                var gridCity = _generatorTypes.FirstOrDefault(kvp => Normalize(kvp.Key) == "gridcity");
                if (gridCity.Value != null)
                {
                    if (!_generatorTypes.ContainsKey("City"))
                        _generatorTypes["City"] = gridCity.Value;
                }
                else
                {
                    var anyCity = _generatorTypes.FirstOrDefault(kvp => Normalize(kvp.Key).Contains("city"));
                    if (anyCity.Value != null && !_generatorTypes.ContainsKey("City"))
                    {
                        _generatorTypes["City"] = anyCity.Value;
                    }
                }

                // Discover features
                var featureTypes = assembly.GetTypes()
                    .Where(t => typeof(IGenerationFeature).IsAssignableFrom(t) && 
                               !t.IsInterface && 
                               !t.IsAbstract)
                    .ToList();

                foreach (var type in featureTypes)
                {
                    var name = GetTypeName(type);
                    _featureTypes[name] = type;
                }
            }
        }

        /// <summary>
        /// Gets a generator instance by name. Returns null if not found.
        /// </summary>
        public IMapGenerator? GetGenerator(string name)
        {
            if (!_generatorTypes.TryGetValue(name, out var type))
            {
                // Fallback: try normalized name matching (case-insensitive, remove dashes/underscores/spaces)
                var target = Normalize(name);
                var match = _generatorTypes.FirstOrDefault(kvp => Normalize(kvp.Key) == target);
                if (match.Value == null && target == "maze")
                {
                    // Alias: map 'maze' to RoomsAndCorridors
                    match = _generatorTypes.FirstOrDefault(kvp => Normalize(kvp.Key).Contains("roomsandcorridors"));
                }
                if (match.Value == null && target == "city")
                {
                    // Alias: map 'city' to any city generator (prefer GridCity)
                    match = _generatorTypes.FirstOrDefault(kvp => Normalize(kvp.Key) == "gridcity");
                    if (match.Value == null)
                    {
                        match = _generatorTypes.FirstOrDefault(kvp => Normalize(kvp.Key).Contains("city"));
                    }
                }
                if (match.Value == null)
                    return null;
                type = match.Value;
            }

            try
            {
                return (IMapGenerator?)Activator.CreateInstance(type);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a feature instance by name. Returns null if not found.
        /// </summary>
        public IGenerationFeature? GetFeature(string name)
        {
            if (!_featureTypes.TryGetValue(name, out var type))
            {
                var target = Normalize(name);
                var match = _featureTypes.FirstOrDefault(kvp => Normalize(kvp.Key) == target);
                if (match.Value == null)
                    return null;
                type = match.Value;
            }

            try
            {
                return (IGenerationFeature?)Activator.CreateInstance(type);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Lists all registered generator names.
        /// </summary>
        public IEnumerable<string> ListGenerators() => _generatorTypes.Keys;

        /// <summary>
        /// Lists all registered feature names.
        /// </summary>
        public IEnumerable<string> ListFeatures() => _featureTypes.Keys;

        private static string GetTypeName(Type type)
        {
            // Use the type name without namespace, and remove "Generator" or "Feature" suffix if present
            var name = type.Name;
            if (name.EndsWith("Generator"))
                name = name.Substring(0, name.Length - "Generator".Length);
            if (name.EndsWith("Feature"))
                name = name.Substring(0, name.Length - "Feature".Length);
            return name;
        }

        private static string Normalize(string name)
        {
            var chars = name.Where(char.IsLetterOrDigit).ToArray();
            return new string(chars).ToLowerInvariant();
        }
    }
}

