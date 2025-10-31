using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text.Json;
using Aetherium.Core;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Passes;
using Aetherium.WorldGen.Training;

namespace WorldGenCLI
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var root = new RootCommand("World generation tooling for deterministic map repro and metrics export");

            var generatorOpt = new Option<string>("--generator", () => "AdvancedDungeon", "Layout generator identifier");
            var templateOpt = new Option<string>("--template", () => "dungeon", "Template: dungeon|outdoor");
            var widthOpt = new Option<int>("--width", () => 80, "Map width");
            var heightOpt = new Option<int>("--height", () => 80, "Map height");
            var levelsOpt = new Option<int>("--levels", () => 1, "Vertical levels");
            var seedOpt = new Option<int?>("--seed", description: "Deterministic seed");
            var versionOpt = new Option<string>("--generator-version", () => "1.0.0", "Generator version tag");
            var paramOpt = new Option<string[]>("--param", () => Array.Empty<string>(), "Additional generator parameters key=value");
            var outputOpt = new Option<string?>("--output", "Optional metrics export path (JSON)");
            var benchmarkOpt = new Option<string?>("--benchmark", "Generate a specific benchmark scenario by ID");

            root.AddOption(generatorOpt);
            root.AddOption(templateOpt);
            root.AddOption(widthOpt);
            root.AddOption(heightOpt);
            root.AddOption(levelsOpt);
            root.AddOption(seedOpt);
            root.AddOption(versionOpt);
            root.AddOption(paramOpt);
            root.AddOption(outputOpt);
            root.AddOption(benchmarkOpt);

            root.SetHandler((InvocationContext ctx) =>
            {
                var generator = ctx.ParseResult.GetValueForOption(generatorOpt)!;
                var template = ctx.ParseResult.GetValueForOption(templateOpt)!;
                var width = ctx.ParseResult.GetValueForOption(widthOpt);
                var height = ctx.ParseResult.GetValueForOption(heightOpt);
                var levels = ctx.ParseResult.GetValueForOption(levelsOpt);
                var seedOption = ctx.ParseResult.GetValueForOption(seedOpt);
                var version = ctx.ParseResult.GetValueForOption(versionOpt)!;
                var parameters = ctx.ParseResult.GetValueForOption(paramOpt) ?? Array.Empty<string>();
                var output = ctx.ParseResult.GetValueForOption(outputOpt);
                var benchmarkId = ctx.ParseResult.GetValueForOption(benchmarkOpt);

                var registry = new MapGeneratorRegistry();
                registry.DiscoverTypes(typeof(IMapGenerator).Assembly);

                WorldGenerationRequest request;

                // If benchmark specified, load benchmark and use its recipe
                if (!string.IsNullOrWhiteSpace(benchmarkId))
                {
                    BenchmarkLibrary.LoadBenchmarks();
                    var benchmark = BenchmarkLibrary.GetBenchmark(benchmarkId);
                    
                    if (benchmark == null)
                    {
                        Console.Error.WriteLine($"Benchmark not found: {benchmarkId}");
                        ctx.ExitCode = 1;
                        return;
                    }

                    Console.WriteLine($"Loading benchmark: {benchmark.Name}");
                    request = BenchmarkGenerator.GenerateRequest(benchmark.Recipe);
                }
                else
                {
                    var templateEnum = template.Equals("outdoor", StringComparison.OrdinalIgnoreCase)
                        ? WorldGenerationTemplate.Outdoor
                        : WorldGenerationTemplate.Dungeon;

                    var seedValue = seedOption ?? Environment.TickCount;

                    request = new WorldGenerationRequest
                    {
                        LayoutGenerator = generator,
                        Template = templateEnum,
                        Width = width,
                        Height = height,
                        Levels = levels,
                        Seed = seedValue,
                        GeneratorVersion = version,
                        Parameters = ParseParameters(parameters)
                    };
                }

                var context = new GeneratorContext(width, height, seedValue)
                {
                    Levels = levels
                };

                var passes = BuildPasses(templateEnum);
                var orchestrator = new WorldGenerationOrchestrator(registry, passes);
                var result = orchestrator.Generate(request);

                if (!result.Success || result.World == null)
                {
                    Console.Error.WriteLine("Generation failed:");
                    foreach (var err in result.Errors)
                    {
                        Console.Error.WriteLine(" - " + err);
                    }
                    if (result.Validation != null)
                    {
                        foreach (var err in result.Validation.Errors)
                        {
                            Console.Error.WriteLine(" - " + err);
                        }
                    }
                    ctx.ExitCode = 1;
                    return;
                }

                Console.WriteLine($"World generated with seed {seedValue} (version {version})");
                Console.WriteLine($" Branching factor: {result.Metrics.BranchingFactor:F2}");
                Console.WriteLine($" Loop ratio: {result.Metrics.LoopRatio:F2}");
                Console.WriteLine($" Dead ends: {result.Metrics.DeadEndCount}");
                Console.WriteLine($" Rooms: {result.Metrics.Rooms}");
                Console.WriteLine($" Corridors: {result.Metrics.Corridors}");

                if (!string.IsNullOrWhiteSpace(output))
                {
                    var report = new
                    {
                        request.LayoutGenerator,
                        request.Template,
                        request.Width,
                        request.Height,
                        request.Levels,
                        request.Seed,
                        request.GeneratorVersion,
                        Metrics = new
                        {
                            result.Metrics.BranchingFactor,
                            result.Metrics.LoopRatio,
                            result.Metrics.DeadEndCount,
                            result.Metrics.Rooms,
                            result.Metrics.Corridors,
                            result.Metrics.SecretsPlaced,
                            result.Metrics.TrapsPlaced,
                            result.Metrics.BiomeCoverage,
                            result.Metrics.PhaseDurationsMs
                        },
                        Validation = result.Validation?.Errors ?? Enumerable.Empty<string>(),
                        Errors = result.Errors
                    };

                    if (!string.IsNullOrEmpty(output))
                    {
                        var directory = Path.GetDirectoryName(output);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions
                        {
                            WriteIndented = true
                        }));
                        Console.WriteLine($"Metrics exported to {output}");
                    }
                }
            });

            return root.Invoke(args);
        }

        private static Dictionary<string, string> ParseParameters(IEnumerable<string> values)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                var parts = value.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2)
                {
                    dict[parts[0]] = parts[1];
                }
            }
            return dict;
        }

        private static IWorldGenerationPass[] BuildPasses(WorldGenerationTemplate template)
        {
            return template switch
            {
                WorldGenerationTemplate.Outdoor => new IWorldGenerationPass[]
                {
                    new OutdoorLayoutPass(),
                    new OutdoorThemingPass(),
                    new OutdoorPopulationPass(),
                    new OutdoorInteractionsPass(),
                    new OutdoorValidationPass()
                },
                _ => new IWorldGenerationPass[]
                {
                    new DungeonLayoutPass(),
                    new DungeonThemingPass(),
                    new DungeonPopulationPass(),
                    new DungeonInteractionsPass(),
                    new DungeonValidationPass()
                }
            };
        }
    }
}



