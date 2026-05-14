using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// Registry for discovering and instantiating map generators and features by name.
    /// </summary>
    public sealed class MapGeneratorRegistry
    {
        private readonly Dictionary<string, Type> _generatorTypes = new Dictionary<string, Type>();
        private readonly Dictionary<string, Type> _featureTypes = new Dictionary<string, Type>();
        private readonly List<string> _discoveryWarnings = new List<string>();
        private readonly object _discoveryLock = new object();

        /// <summary>
        /// Diagnostic messages collected during type discovery (load failures, missing ctors, etc.).
        /// </summary>
        public IReadOnlyList<string> DiscoveryWarnings => _discoveryWarnings;

        /// <summary>
        /// Discovers all types implementing IMapGenerator and IGenerationFeature in the provided assemblies.
        /// </summary>
        public void DiscoverTypes(params Assembly[] assemblies)
        {
            lock (_discoveryLock)
            {
                if (assemblies.Length == 0)
                {
                    assemblies = new[] { Assembly.GetExecutingAssembly() };
                }

                var generatorTypes = new List<Type>();
                var featureTypes = new List<Type>();

                foreach (var assembly in assemblies)
                {
                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        // Some types failed to load (missing dependencies, etc.). Use the ones that
                        // did load and record the rest for diagnostics.
                        types = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
                        foreach (var loaderEx in ex.LoaderExceptions ?? Array.Empty<Exception?>())
                        {
                            if (loaderEx != null)
                            {
                                _discoveryWarnings.Add($"Type load failure in {assembly.GetName().Name}: {loaderEx.Message}");
                            }
                        }
                    }

                    foreach (var type in types)
                    {
                        if (type.IsInterface || type.IsAbstract)
                            continue;
                        if (typeof(IMapGenerator).IsAssignableFrom(type))
                            generatorTypes.Add(type);
                        if (typeof(IGenerationFeature).IsAssignableFrom(type))
                            featureTypes.Add(type);
                    }
                }

                // Sort by FullName for deterministic registration order across runtimes/trimming.
                generatorTypes.Sort((a, b) => StringComparer.Ordinal.Compare(a.FullName, b.FullName));
                featureTypes.Sort((a, b) => StringComparer.Ordinal.Compare(a.FullName, b.FullName));

                foreach (var type in generatorTypes)
                {
                    if (!ValidateParameterlessCtor(type))
                        continue;
                    var name = GetTypeName(type);
                    if (!_generatorTypes.ContainsKey(name))
                    {
                        _generatorTypes[name] = type;
                    }
                    else if (string.Equals(name, "Maze", StringComparison.OrdinalIgnoreCase))
                    {
                        // Prefer legacy Aetherium.MazeGenerator over WorldGen variant when both
                        // are present (preserves existing test behavior).
                        var existing = _generatorTypes[name];
                        bool newIsLegacy = type.FullName == "Aetherium.MazeGenerator";
                        bool existingIsWorldGen = existing.FullName?.Contains(".WorldGen.") == true;
                        if (newIsLegacy && existingIsWorldGen)
                        {
                            _generatorTypes[name] = type;
                        }
                    }
                }

                foreach (var type in featureTypes)
                {
                    if (!ValidateParameterlessCtor(type))
                        continue;
                    var name = GetTypeName(type);
                    if (!_featureTypes.ContainsKey(name))
                    {
                        _featureTypes[name] = type;
                    }
                }

                // Legacy Maze passthrough (the type may live outside our IMapGenerator hierarchy).
                foreach (var assembly in assemblies)
                {
                    var legacyMazeType = assembly.GetType("Aetherium.MazeGenerator");
                    if (legacyMazeType != null)
                    {
                        _generatorTypes["Maze"] = legacyMazeType;
                    }
                }

                // Alias resolution — runs ONCE after discovery, not per-assembly, so multi-assembly
                // discovery can't flip results based on which assembly came first.
                ResolveAliases();
            }
        }

        private void ResolveAliases()
        {
            // Maze -> RoomsAndCorridors (only if no explicit Maze generator exists)
            var rac = _generatorTypes.FirstOrDefault(kvp => Normalize(kvp.Key).Contains("roomsandcorridors"));
            if (rac.Value != null && !_generatorTypes.ContainsKey("Maze"))
            {
                _generatorTypes["Maze"] = rac.Value;
            }

            // OutdoorTerrain -> PerlinTerrain
            var perlin = _generatorTypes.FirstOrDefault(kvp => Normalize(kvp.Key).Contains("perlinterrain"));
            if (perlin.Value != null && !_generatorTypes.ContainsKey("OutdoorTerrain"))
            {
                _generatorTypes["OutdoorTerrain"] = perlin.Value;
            }

            // City -> GridCity (prefer); else any *City
            var gridCity = _generatorTypes.FirstOrDefault(kvp => Normalize(kvp.Key) == "gridcity");
            if (gridCity.Value != null && !_generatorTypes.ContainsKey("City"))
            {
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
        }

        private bool ValidateParameterlessCtor(Type type)
        {
            // A parameterless ctor is required because we instantiate via Activator.CreateInstance().
            var ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor == null)
            {
                _discoveryWarnings.Add(
                    $"Type {type.FullName ?? type.Name} has no public parameterless constructor; skipping registration.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Gets a generator instance by name. Returns null if not found.
        /// On instantiation failure (constructor throws), the underlying exception is rethrown
        /// rather than swallowed — silent failures previously masked configuration bugs.
        /// </summary>
        public IMapGenerator? GetGenerator(string name)
        {
            var type = ResolveGeneratorType(name);
            if (type == null)
                return null;

            return (IMapGenerator?)CreateInstance(type);
        }

        /// <summary>
        /// Gets a feature instance by name. Returns null if not found.
        /// On instantiation failure, the underlying exception is rethrown.
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

            return (IGenerationFeature?)CreateInstance(type);
        }

        private Type? ResolveGeneratorType(string name)
        {
            if (_generatorTypes.TryGetValue(name, out var type))
                return type;

            var target = Normalize(name);
            var match = _generatorTypes.FirstOrDefault(kvp => Normalize(kvp.Key) == target);
            if (match.Value == null && target == "maze")
            {
                match = _generatorTypes.FirstOrDefault(kvp => Normalize(kvp.Key).Contains("roomsandcorridors"));
            }
            if (match.Value == null && target == "city")
            {
                match = _generatorTypes.FirstOrDefault(kvp => Normalize(kvp.Key) == "gridcity");
                if (match.Value == null)
                    match = _generatorTypes.FirstOrDefault(kvp => Normalize(kvp.Key).Contains("city"));
            }
            return match.Value;
        }

        private static object? CreateInstance(Type type)
        {
            try
            {
                return Activator.CreateInstance(type);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw new InvalidOperationException(
                    $"Failed to instantiate {type.FullName ?? type.Name}: {ex.InnerException.Message}",
                    ex.InnerException);
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
