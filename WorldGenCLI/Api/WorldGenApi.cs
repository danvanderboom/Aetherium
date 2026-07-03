using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Hybrid;
using Aetherium.WorldGen.Passes;
using WorldGenCLI.Models;
using WorldGenCLI.Rendering;
using WorldGenCLI.Services;

namespace WorldGenCLI.Api
{
    /// <summary>
    /// Minimal API endpoints for PCG editor.
    /// </summary>
    public static class WorldGenApi
    {
        public static void MapEndpoints(this WebApplication app)
        {
            var api = app.MapGroup("/api");

            // Generator endpoints
            api.MapGet("/generators", GetGenerators);
            api.MapGet("/generators/{id}/constraints-schema", GetConstraintsSchema);

            // Template endpoints
            api.MapGet("/templates", GetTemplates);
            api.MapGet("/templates/{name}", GetTemplate);
            api.MapPost("/templates", SaveTemplate);
            api.MapDelete("/templates/{name}", DeleteTemplate);

            // Generation endpoints
            api.MapPost("/generate", GenerateWorld);
            api.MapPost("/generate/abtest", GenerateAbTest);
        }

        private static IResult GetGenerators(HttpContext context)
        {
            var registry = context.RequestServices.GetRequiredService<MapGeneratorRegistry>();
            var generators = registry.ListGenerators()
                .Select(id => new GeneratorInfo
                {
                    Id = id,
                    Name = id, // TODO: Extract friendly name from generator if available
                    Version = "1.0.0" // TODO: Extract version from generator
                })
                .ToList();

            return Results.Ok(generators);
        }

        private static IResult GetConstraintsSchema(string id, HttpContext context)
        {
            var registry = context.RequestServices.GetRequiredService<MapGeneratorRegistry>();
            var generator = registry.GetGenerator(id);
            
            if (generator == null)
                return Results.NotFound($"Generator '{id}' not found");

            var schemaBuilder = new WorldGenCLI.Services.ConstraintSchemaBuilder(registry);
            var descriptor = schemaBuilder.BuildSchema(id);

            return Results.Ok(descriptor);
        }

        private static IResult GetTemplates(HttpContext context)
        {
            var library = context.RequestServices.GetRequiredService<TemplateLibrary>();
            var names = library.ListTemplateNames();
            return Results.Ok(names);
        }

        private static IResult GetTemplate(string name, HttpContext context)
        {
            var library = context.RequestServices.GetRequiredService<TemplateLibrary>();
            var template = library.LoadTemplate(name);
            
            if (template == null)
                return Results.NotFound($"Template '{name}' not found");

            return Results.Ok(template);
        }

        private static async Task<IResult> SaveTemplate(TemplateDto template, HttpContext context)
        {
            if (string.IsNullOrWhiteSpace(template.Name))
                return Results.BadRequest("Template name is required");

            var library = context.RequestServices.GetRequiredService<TemplateLibrary>();
            
            if (template.Modified == null)
                template.Modified = DateTime.UtcNow;
            
            if (template.Created == default)
                template.Created = DateTime.UtcNow;

            var success = library.SaveTemplate(template);
            
            if (!success)
                return Results.Problem("Failed to save template");

            return Results.Ok(template);
        }

        private static IResult DeleteTemplate(string name, HttpContext context)
        {
            var library = context.RequestServices.GetRequiredService<TemplateLibrary>();
            var success = library.DeleteTemplate(name);
            
            if (!success)
                return Results.NotFound($"Template '{name}' not found");

            return Results.NoContent();
        }

        private static async Task<IResult> GenerateWorld(GenerateRequest request, HttpContext context)
        {
            try
            {
                var registry = context.RequestServices.GetRequiredService<MapGeneratorRegistry>();
                var templateEnum = request.Template.Equals("outdoor", StringComparison.OrdinalIgnoreCase)
                    ? WorldGenerationTemplate.Outdoor
                    : WorldGenerationTemplate.Dungeon;

                var seedValue = request.Seed ?? Environment.TickCount;

                var worldRequest = new WorldGenerationRequest
                {
                    LayoutGenerator = request.LayoutGenerator,
                    Template = templateEnum,
                    Width = request.Width,
                    Height = request.Height,
                    Levels = request.Levels,
                    Seed = seedValue,
                    GeneratorVersion = request.GeneratorVersion,
                    Parameters = request.Parameters,
                    HybridAnchors = ConvertHybridLayout(request.HybridAnchors)
                };
                // For now, create a basic context
                var genContext = new GeneratorContext(request.Width, request.Height, seedValue)
                {
                    Levels = request.Levels,
                    GeneratorParams = request.Parameters,
                    GeneratorVersion = request.GeneratorVersion,
                    FeatureRegistry = registry
                };

                var passes = BuildPasses(templateEnum);
                var orchestrator = new WorldGenerationOrchestrator(registry, passes);
                var result = orchestrator.Generate(worldRequest);

                if (!result.Success || result.World == null)
                {
                    return Results.Ok(new GenerateResponse
                    {
                        Success = false,
                        Errors = result.Errors.ToList(),
                        ValidationErrors = result.Validation?.Errors.ToList() ?? new List<string>(),
                        Seed = seedValue
                    });
                }

                // Convert World to MapRenderDto
                var mapRender = RenderMapper.MapWorld(
                    result.World,
                    request.Width,
                    request.Height,
                    zLevel: 0,
                    worldRequest.HybridAnchors);

                var metricsDto = new GenerationMetricsDto
                {
                    BranchingFactor = result.Metrics.BranchingFactor,
                    LoopRatio = result.Metrics.LoopRatio,
                    DeadEndCount = result.Metrics.DeadEndCount,
                    Rooms = result.Metrics.Rooms,
                    Corridors = result.Metrics.Corridors,
                    SecretsPlaced = result.Metrics.SecretsPlaced,
                    TrapsPlaced = result.Metrics.TrapsPlaced,
                    BiomeCoverage = result.Metrics.BiomeCoverage != null ? new Dictionary<string, double>(result.Metrics.BiomeCoverage) : new Dictionary<string, double>(),
                    PhaseDurationsMs = result.Metrics.PhaseDurationsMs != null ? new Dictionary<string, double>(result.Metrics.PhaseDurationsMs) : new Dictionary<string, double>(),
                    ValidationPassed = result.Validation?.Success ?? false
                };

                return Results.Ok(new GenerateResponse
                {
                    Success = true,
                    Map = mapRender,
                    Metrics = metricsDto,
                    Errors = new List<string>(),
                    ValidationErrors = result.Validation?.Errors.ToList() ?? new List<string>(),
                    Seed = seedValue
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Generation failed: {ex.Message}");
            }
        }

        private static async Task<IResult> GenerateAbTest(AbTestRequest request, HttpContext context)
        {
            try
            {
                var registry = context.RequestServices.GetRequiredService<MapGeneratorRegistry>();
                var candidates = new List<CandidateResult>();

                // Generate candidates
                var count = Math.Min(request.Count, request.Limit);
                // Vary the auto-seed by the range index. The previous `Environment.TickCount +
                // candidates.Count` produced the SAME seed for every candidate (candidates is
                // empty here, and TickCount is effectively constant across this tight loop), so
                // the A/B comparison compared N identical maps.
                var seeds = request.Seeds ?? Enumerable.Range(0, count)
                    .Select(i => Environment.TickCount + i)
                    .ToList();

                foreach (var seed in seeds.Take(count))
                {
                    var genRequest = request.BaseRequest;
                    genRequest.Seed = seed;

                    // Reuse GenerateWorld logic
                    var templateEnum = genRequest.Template.Equals("outdoor", StringComparison.OrdinalIgnoreCase)
                        ? WorldGenerationTemplate.Outdoor
                        : WorldGenerationTemplate.Dungeon;

                    var worldRequest = new WorldGenerationRequest
                    {
                        LayoutGenerator = genRequest.LayoutGenerator,
                        Template = templateEnum,
                        Width = genRequest.Width,
                        Height = genRequest.Height,
                        Levels = genRequest.Levels,
                        Seed = seed,
                        GeneratorVersion = genRequest.GeneratorVersion,
                        Parameters = genRequest.Parameters,
                        HybridAnchors = ConvertHybridLayout(genRequest.HybridAnchors)
                    };

                    var passes = BuildPasses(templateEnum);
                    var orchestrator = new WorldGenerationOrchestrator(registry, passes);
                    var result = orchestrator.Generate(worldRequest);

                    var mapRender = result.World != null
                        ? RenderMapper.MapWorld(
                            result.World,
                            genRequest.Width,
                            genRequest.Height,
                            zLevel: 0,
                            worldRequest.HybridAnchors)
                        : null;

                    var metricsDto = result.World != null
                        ? new GenerationMetricsDto
                        {
                            BranchingFactor = result.Metrics.BranchingFactor,
                            LoopRatio = result.Metrics.LoopRatio,
                            DeadEndCount = result.Metrics.DeadEndCount,
                            Rooms = result.Metrics.Rooms,
                            Corridors = result.Metrics.Corridors,
                            SecretsPlaced = result.Metrics.SecretsPlaced,
                            TrapsPlaced = result.Metrics.TrapsPlaced,
                            BiomeCoverage = result.Metrics.BiomeCoverage != null ? new Dictionary<string, double>(result.Metrics.BiomeCoverage) : new Dictionary<string, double>(),
                            PhaseDurationsMs = result.Metrics.PhaseDurationsMs != null ? new Dictionary<string, double>(result.Metrics.PhaseDurationsMs) : new Dictionary<string, double>(),
                            ValidationPassed = result.Validation?.Success ?? false
                        }
                        : null;

                    candidates.Add(new CandidateResult
                    {
                        Seed = seed,
                        Map = mapRender,
                        Metrics = metricsDto,
                        Errors = result.Errors.ToList(),
                        ValidationErrors = result.Validation?.Errors.ToList() ?? new List<string>(),
                        Success = result.Success && result.World != null
                    });
                }

                // Sort by metric if specified
                if (!string.IsNullOrEmpty(request.TopByMetric))
                {
                    candidates = request.TopByMetric.ToLowerInvariant() switch
                    {
                        "branchingfactor" or "branching_factor" => candidates
                            .OrderByDescending(c => c.Metrics?.BranchingFactor ?? 0)
                            .ToList(),
                        "loopratio" or "loop_ratio" => candidates
                            .OrderByDescending(c => c.Metrics?.LoopRatio ?? 0)
                            .ToList(),
                        "rooms" => candidates
                            .OrderByDescending(c => c.Metrics?.Rooms ?? 0)
                            .ToList(),
                        "deadendcount" or "dead_end_count" => candidates
                            .OrderByDescending(c => c.Metrics?.DeadEndCount ?? 0)
                            .ToList(),
                        _ => candidates
                    };
                }

                return Results.Ok(new AbTestResponse
                {
                    Candidates = candidates,
                    SortMetric = request.TopByMetric
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"A/B test generation failed: {ex.Message}");
            }
        }

        private static IWorldGenerationPass[] BuildPasses(WorldGenerationTemplate template)
        {
            var hybridPass = new HybridLayoutPass();

            return template switch
            {
                WorldGenerationTemplate.Outdoor => new IWorldGenerationPass[]
                {
                    hybridPass,
                    new OutdoorLayoutPass(),
                    new OutdoorThemingPass(),
                    new OutdoorPopulationPass(),
                    new EnvironmentalStoryPass(),
                    new AudioGenerationPass(),
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
                    new AudioGenerationPass(),
                    new DungeonInteractionsPass(),
                    new DungeonValidationPass()
                }
            };
        }

        private static Aetherium.WorldGen.Hybrid.HybridLayout? ConvertHybridLayout(Models.HybridLayout? layout)
        {
            if (layout == null || layout.Anchors.Count == 0)
                return null;

            var serverLayout = new Aetherium.WorldGen.Hybrid.HybridLayout();

            foreach (var anchor in layout.Anchors)
            {
                var serverAnchor = new Aetherium.WorldGen.Hybrid.HybridAnchor
                {
                    Type = (Aetherium.WorldGen.Hybrid.AnchorType)anchor.Type,
                    X = anchor.X,
                    Y = anchor.Y,
                    Width = anchor.Width,
                    Height = anchor.Height,
                    IsBlocking = anchor.IsBlocking,
                    ZLevel = anchor.ZLevel,
                    Priority = anchor.Priority
                };

                serverAnchor.Tags.AddRange(anchor.Tags);

                if (anchor.Vertices != null)
                {
                    serverAnchor.Vertices = anchor.Vertices
                        .Select(v => new WorldLocation(v.X, v.Y, anchor.ZLevel))
                        .ToList();
                }

                serverLayout.Anchors.Add(serverAnchor);
            }

            return serverLayout;
        }
    }
}

