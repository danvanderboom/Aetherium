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
                    _generatorTypes[name] = type;
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
                return null;

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
                return null;

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
    }
}

