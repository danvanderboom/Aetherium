using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aetherium.Core;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Passes;
using Aetherium.WorldGen.Training;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using WorldGenCLI.Api;
using WorldGenCLI.Services;
using WorldGenCLI.Rendering;
using WorldGenCLI.Models;
using SkiaSharp;

namespace Aetherctl.Commands
{
    public static class WorldGenCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var worldgenCmd = new Command("worldgen", "World generation tooling");

            var generateCmd = new Command("generate", "Generate a world map");
            var generatorOpt = new Option<string>("--generator", () => "AdvancedDungeon", "Layout generator identifier");
            var templateOpt = new Option<string>("--template", () => "dungeon", "Template: dungeon|outdoor");
            var widthOpt = new Option<int>("--width", () => 80, "Map width");
            var heightOpt = new Option<int>("--height", () => 80, "Map height");
            var levelsOpt = new Option<int>("--levels", () => 1, "Vertical levels");
            var seedOpt = new Option<int?>("--seed", "Deterministic seed");
            var versionOpt = new Option<string>("--generator-version", () => "1.0.0", "Generator version tag");
            var paramOpt = new Option<string[]>("--param", () => Array.Empty<string>(), "Additional generator parameters key=value");
            var outputOpt = new Option<string?>("--output", "Optional metrics export path (JSON)");
            var benchmarkOpt = new Option<string?>("--benchmark", "Generate a specific benchmark scenario by ID");

            generateCmd.AddOption(generatorOpt);
            generateCmd.AddOption(templateOpt);
            generateCmd.AddOption(widthOpt);
            generateCmd.AddOption(heightOpt);
            generateCmd.AddOption(levelsOpt);
            generateCmd.AddOption(seedOpt);
            generateCmd.AddOption(versionOpt);
            generateCmd.AddOption(paramOpt);
            generateCmd.AddOption(outputOpt);
            generateCmd.AddOption(benchmarkOpt);
            generateCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var generator = parseResult.GetValueForOption(generatorOpt)!;
                    var template = parseResult.GetValueForOption(templateOpt)!;
                    var width = parseResult.GetValueForOption(widthOpt);
                    var height = parseResult.GetValueForOption(heightOpt);
                    var levels = parseResult.GetValueForOption(levelsOpt);
                    var seedOption = parseResult.GetValueForOption(seedOpt);
                    var version = parseResult.GetValueForOption(versionOpt)!;
                    var parameters = parseResult.GetValueForOption(paramOpt) ?? Array.Empty<string>();
                    var output = parseResult.GetValueForOption(outputOpt);
                    var benchmarkId = parseResult.GetValueForOption(benchmarkOpt);

                    var registry = new MapGeneratorRegistry();
                    registry.DiscoverTypes(typeof(IMapGenerator).Assembly);

                    WorldGenerationRequest request;
                    WorldGenerationTemplate templateEnum;
                    int seedValue;

                    if (!string.IsNullOrWhiteSpace(benchmarkId))
                    {
                        BenchmarkLibrary.LoadBenchmarks();
                        var benchmark = BenchmarkLibrary.GetBenchmark(benchmarkId);
                        if (benchmark == null)
                        {
                            Common.WriteError(parseResult, $"Benchmark not found: {benchmarkId}");
                            return;
                        }
                        request = BenchmarkGenerator.GenerateRequest(benchmark.Recipe);
                        templateEnum = request.Template;
                        seedValue = request.Seed ?? 0;
                    }
                    else
                    {
                        templateEnum = template.Equals("outdoor", StringComparison.OrdinalIgnoreCase)
                            ? WorldGenerationTemplate.Outdoor
                            : WorldGenerationTemplate.Dungeon;
                        seedValue = seedOption ?? Environment.TickCount;
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

                    var context = new GeneratorContext(width, height, seedValue) { Levels = levels };
                    var passes = BuildPasses(templateEnum);
                    var orchestrator = new WorldGenerationOrchestrator(registry, passes);
                    var result = orchestrator.Generate(request);

                    if (!result.Success || result.World == null)
                    {
                        if (Common.IsJsonOutput(parseResult))
                        {
                            Common.WriteOutput(parseResult, new
                            {
                                success = false,
                                error = "Generation failed",
                                errors = result.Errors,
                                validationErrors = result.Validation?.Errors ?? new List<string>()
                            });
                        }
                        else
                        {
                            Console.Error.WriteLine("Generation failed:");
                            foreach (var err in result.Errors)
                                Console.Error.WriteLine(" - " + err);
                            if (result.Validation != null)
                                foreach (var err in result.Validation.Errors)
                                    Console.Error.WriteLine(" - " + err);
                        }
                        Environment.Exit(1);
                        return;
                    }

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            seed = seedValue,
                            version,
                            metrics = new
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
                            }
                        });
                    }
                    else
                    {
                        Console.WriteLine($"World generated with seed {seedValue} (version {version})");
                        Console.WriteLine($" Branching factor: {result.Metrics.BranchingFactor:F2}");
                        Console.WriteLine($" Loop ratio: {result.Metrics.LoopRatio:F2}");
                        Console.WriteLine($" Dead ends: {result.Metrics.DeadEndCount}");
                        Console.WriteLine($" Rooms: {result.Metrics.Rooms}");
                        Console.WriteLine($" Corridors: {result.Metrics.Corridors}");
                    }

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

                        var directory = Path.GetDirectoryName(output);
                        if (!string.IsNullOrEmpty(directory))
                            Directory.CreateDirectory(directory);

                        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                        if (!Common.IsQuiet(parseResult))
                            Console.WriteLine($"Metrics exported to {output}");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error generating world: {ex.Message}");
                }
            });

            var serveCmd = new Command("serve", "Start HTTP API server");
            var portOpt = new Option<int>("--port", () => 5000, "Port for HTTP API server");
            serveCmd.AddOption(portOpt);
            serveCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var port = parseResult.GetValueForOption(portOpt);
                    await StartApiServer(port);
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error starting API server: {ex.Message}");
                }
            });

            // worldgen render --ascii (Phase 1)
            var renderCmd = new Command("render", "Render a generated world preview");
            var renderAsciiOpt = new Option<bool>("--ascii", "Output ASCII map to stdout");
            var renderPngOpt = new Option<string?>("--png", "Write PNG preview to the specified path");
            renderCmd.AddOption(generatorOpt);
            renderCmd.AddOption(templateOpt);
            renderCmd.AddOption(widthOpt);
            renderCmd.AddOption(heightOpt);
            renderCmd.AddOption(levelsOpt);
            renderCmd.AddOption(seedOpt);
            renderCmd.AddOption(versionOpt);
            renderCmd.AddOption(paramOpt);
            renderCmd.AddOption(benchmarkOpt);
            renderCmd.AddOption(renderAsciiOpt);
            renderCmd.AddOption(renderPngOpt);
            renderCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var generator = parseResult.GetValueForOption(generatorOpt)!;
                    var template = parseResult.GetValueForOption(templateOpt)!;
                    var width = parseResult.GetValueForOption(widthOpt);
                    var height = parseResult.GetValueForOption(heightOpt);
                    var levels = parseResult.GetValueForOption(levelsOpt);
                    var seedOption = parseResult.GetValueForOption(seedOpt);
                    var version = parseResult.GetValueForOption(versionOpt)!;
                    var parameters = parseResult.GetValueForOption(paramOpt) ?? Array.Empty<string>();
                    var benchmarkId = parseResult.GetValueForOption(benchmarkOpt);
                    var ascii = parseResult.GetValueForOption(renderAsciiOpt);
                    var pngPath = parseResult.GetValueForOption(renderPngOpt);

                    var registry = new MapGeneratorRegistry();
                    registry.DiscoverTypes(typeof(IMapGenerator).Assembly);

                    WorldGenerationRequest request;
                    WorldGenerationTemplate templateEnum;
                    int seedValue;

                    if (!string.IsNullOrWhiteSpace(benchmarkId))
                    {
                        BenchmarkLibrary.LoadBenchmarks();
                        var benchmark = BenchmarkLibrary.GetBenchmark(benchmarkId);
                        if (benchmark == null)
                        {
                            Common.WriteError(parseResult, $"Benchmark not found: {benchmarkId}");
                            return;
                        }
                        request = BenchmarkGenerator.GenerateRequest(benchmark.Recipe);
                        templateEnum = request.Template;
                        seedValue = request.Seed ?? 0;
                    }
                    else
                    {
                        templateEnum = template.Equals("outdoor", StringComparison.OrdinalIgnoreCase)
                            ? WorldGenerationTemplate.Outdoor
                            : WorldGenerationTemplate.Dungeon;
                        seedValue = seedOption ?? Environment.TickCount;
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

                    var context = new GeneratorContext(width, height, seedValue) { Levels = levels };
                    var passes = BuildPasses(templateEnum);
                    var orchestrator = new WorldGenerationOrchestrator(registry, passes);
                    var result = orchestrator.Generate(request);

                    if (!result.Success || result.World == null)
                    {
                        Common.WriteError(parseResult, "Generation failed for render");
                        return;
                    }

                    var map = RenderMapper.MapWorld(result.World, request.Width, request.Height, 0, request.HybridAnchors);

                    if (Common.IsJsonOutput(parseResult))
                    {
                        // Emit a simple JSON with rows of ASCII for convenience
                        var rows = new string[map.Height];
                        for (int y = 0; y < map.Height; y++)
                        {
                            var sb = new System.Text.StringBuilder(map.Width);
                            for (int x = 0; x < map.Width; x++)
                            {
                                var tileId = map.Tiles[y * map.Width + x];
                                var symbol = map.Palette.TryGetValue(tileId, out var info) ? info.Symbol : "?";
                                sb.Append(symbol);
                            }
                            rows[y] = sb.ToString();
                        }
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            seed = seedValue,
                            version,
                            width = map.Width,
                            height = map.Height,
                            rows
                        });
                    }
                    else if (!string.IsNullOrWhiteSpace(pngPath))
                    {
                        var directory = Path.GetDirectoryName(pngPath);
                        if (!string.IsNullOrEmpty(directory))
                            Directory.CreateDirectory(directory);

                        RenderPng(map, pngPath!, 8);
                        if (!Common.IsQuiet(parseResult))
                            Console.WriteLine($"PNG written to {pngPath}");
                    }
                    else if (ascii)
                    {
                        for (int y = 0; y < map.Height; y++)
                        {
                            var sb = new System.Text.StringBuilder(map.Width + 2);
                            sb.Append("│");
                            for (int x = 0; x < map.Width; x++)
                            {
                                var tileId = map.Tiles[y * map.Width + x];
                                var symbol = map.Palette.TryGetValue(tileId, out var info) ? info.Symbol : "?";
                                sb.Append(symbol);
                            }
                            sb.Append("│");
                            Console.WriteLine(sb.ToString());
                        }
                    }
                    else
                    {
                        if (!Common.IsQuiet(parseResult))
                            Console.WriteLine("Use --ascii to render the map to stdout or --json for structured output.");
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error rendering world: {ex.Message}");
                }
            });

            worldgenCmd.AddCommand(generateCmd);
            worldgenCmd.AddCommand(serveCmd);
            worldgenCmd.AddCommand(renderCmd);
            root.AddCommand(worldgenCmd);
        }

        private static Dictionary<string, string> ParseParameters(string[] values)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                var parts = value.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2)
                    dict[parts[0]] = parts[1];
            }
            return dict;
        }

        private static IWorldGenerationPass[] BuildPasses(WorldGenerationTemplate template)
        {
            var hybridPass = new Aetherium.WorldGen.Hybrid.HybridLayoutPass();
            return template switch
            {
                WorldGenerationTemplate.Outdoor => new IWorldGenerationPass[]
                {
                    hybridPass,
                    new OutdoorLayoutPass(),
                    new OutdoorThemingPass(),
                    new OutdoorPopulationPass(),
                    new EnvironmentalStoryPass(),
                    new Aetherium.WorldGen.Passes.AudioGenerationPass(),
                    new OutdoorInteractionsPass(),
                    new OutdoorValidationPass()
                },
                _ => new IWorldGenerationPass[]
                {
                    hybridPass,
                    new DungeonLayoutPass(),
                    new DungeonThemingPass(),
                    new DungeonPopulationPass(),
                    new EnvironmentalStoryPass(),
                    new Aetherium.WorldGen.Passes.AudioGenerationPass(),
                    new DungeonInteractionsPass(),
                    new DungeonValidationPass()
                }
            };
        }

        private static void RenderPng(MapRenderDto map, string path, int scale)
        {
            int width = map.Width * scale;
            int height = map.Height * scale;

            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(new SKColor(20, 20, 24));

            using var paint = new SKPaint { IsAntialias = false, FilterQuality = SKFilterQuality.None };
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var tileId = map.Tiles[y * map.Width + x];
                    var color = ResolveColor(map, tileId);
                    paint.Color = color;
                    var rect = new SKRect(x * scale, y * scale, (x + 1) * scale, (y + 1) * scale);
                    canvas.DrawRect(rect, paint);
                }
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
            data.SaveTo(stream);
        }

        private static SKColor ResolveColor(MapRenderDto map, byte tileId)
        {
            if (map.Palette.TryGetValue(tileId, out var info))
            {
                var name = (info.Name ?? "").ToLowerInvariant();
                return name switch
                {
                    "wall" or "stone" => new SKColor(80, 80, 88),
                    "floor" or "ground" => new SKColor(180, 180, 190),
                    "door" => new SKColor(139, 69, 19),
                    "water" => new SKColor(70, 130, 180),
                    "grass" => new SKColor(34, 139, 34),
                    "dirt" => new SKColor(160, 82, 45),
                    "rock" => new SKColor(105, 105, 105),
                    "tree" => new SKColor(0, 100, 0),
                    "lava" => new SKColor(220, 20, 60),
                    _ => new SKColor(120, 120, 128)
                };
            }
            return new SKColor(30, 30, 34);
        }

        private static async Task StartApiServer(int port)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddSingleton<MapGeneratorRegistry>(sp =>
            {
                var registry = new MapGeneratorRegistry();
                registry.DiscoverTypes(typeof(IMapGenerator).Assembly);
                return registry;
            });
            builder.Services.AddSingleton<TemplateLibrary>();
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                });
            });

            var app = builder.Build();
            app.UseCors();
            app.MapEndpoints();

            Console.WriteLine($"WorldGen API server listening on http://localhost:{port}");
            Console.WriteLine("Press Ctrl+C to stop.");
            app.Urls.Add($"http://localhost:{port}");
            await app.RunAsync();
        }
    }
}

