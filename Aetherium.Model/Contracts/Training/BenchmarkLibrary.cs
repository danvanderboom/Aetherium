using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Aetherium.Model.Training
{
    /// <summary>
    /// Loads and manages benchmark scenarios.
    /// </summary>
    public static class BenchmarkLibrary
    {
        private static Dictionary<string, BenchmarkScenario> _benchmarks = new Dictionary<string, BenchmarkScenario>();
        private static readonly object _lock = new object();
        private static bool _initialized = false;

        /// <summary>
        /// Gets a benchmark by its ID.
        /// </summary>
        public static BenchmarkScenario? GetBenchmark(string benchmarkId)
        {
            EnsureInitialized();
            
            lock (_lock)
            {
                return _benchmarks.TryGetValue(benchmarkId, out var benchmark) ? benchmark : null;
            }
        }

        /// <summary>
        /// Gets all benchmarks in a category.
        /// </summary>
        public static List<BenchmarkScenario> GetBenchmarksByCategory(string category)
        {
            EnsureInitialized();
            
            lock (_lock)
            {
                return _benchmarks.Values
                    .Where(b => b.Categories.Contains(category, StringComparer.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all benchmarks.
        /// </summary>
        public static List<BenchmarkScenario> GetAllBenchmarks()
        {
            EnsureInitialized();
            
            lock (_lock)
            {
                return new List<BenchmarkScenario>(_benchmarks.Values);
            }
        }

        /// <summary>
        /// Registers a benchmark scenario.
        /// </summary>
        public static void RegisterBenchmark(BenchmarkScenario benchmark)
        {
            if (benchmark == null || string.IsNullOrWhiteSpace(benchmark.BenchmarkId))
                return;

            lock (_lock)
            {
                _benchmarks[benchmark.BenchmarkId] = benchmark;
            }
        }

        /// <summary>
        /// Loads benchmarks from the Data/Benchmarks directory.
        /// </summary>
        public static void LoadBenchmarks(string? dataDirectory = null)
        {
            dataDirectory = dataDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "Data", "Benchmarks");
            
            if (!Directory.Exists(dataDirectory))
            {
                Console.WriteLine($"[BenchmarkLibrary] Benchmark directory not found: {dataDirectory}");
                return;
            }

            var jsonFiles = Directory.GetFiles(dataDirectory, "*.json");
            var loadedCount = 0;

            foreach (var jsonFile in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(jsonFile);
                    var benchmark = JsonSerializer.Deserialize<BenchmarkScenario>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (benchmark != null && !string.IsNullOrWhiteSpace(benchmark.BenchmarkId))
                    {
                        RegisterBenchmark(benchmark);
                        loadedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BenchmarkLibrary] Error loading benchmark from {jsonFile}: {ex.Message}");
                }
            }

            Console.WriteLine($"[BenchmarkLibrary] Loaded {loadedCount} benchmarks from {dataDirectory}");
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            lock (_lock)
            {
                if (_initialized)
                    return;

                LoadBenchmarks();
                _initialized = true;
            }
        }
    }
}

