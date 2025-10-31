using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// Coordinates world generation by executing a deterministic set of passes
    /// across the canonical phases: layout, theming, population, interactions, validation.
    /// </summary>
    public sealed class WorldGenerationOrchestrator
    {
        private readonly MapGeneratorRegistry _registry;
        private readonly List<IWorldGenerationPass> _passes;

        public WorldGenerationOrchestrator(MapGeneratorRegistry registry, IEnumerable<IWorldGenerationPass>? passes = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _passes = passes != null
                ? new List<IWorldGenerationPass>(passes)
                : new List<IWorldGenerationPass>();

            _passes.Sort((a, b) => a.Phase.CompareTo(b.Phase));
        }

        public IReadOnlyList<IWorldGenerationPass> Passes => _passes;

        public void AddPass(IWorldGenerationPass pass)
        {
            if (pass == null)
                throw new ArgumentNullException(nameof(pass));

            _passes.Add(pass);
            _passes.Sort((a, b) => a.Phase.CompareTo(b.Phase));
        }

        public WorldGenerationResult Generate(WorldGenerationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.LayoutGenerator))
            {
                request.LayoutGenerator = request.Template == WorldGenerationTemplate.Dungeon
                    ? "AdvancedDungeon"
                    : "PerlinTerrain";
            }

            var generatorParams = new Dictionary<string, string>(request.Parameters, StringComparer.OrdinalIgnoreCase);
            var generatorContext = new GeneratorContext(request.Width, request.Height, request.Seed)
            {
                Levels = Math.Max(1, request.Levels),
                GeneratorParams = generatorParams,
                NarrativeId = request.Narrative.NarrativeId,
                NarrativeConstraints = request.Narrative,
                GeneratorVersion = request.GeneratorVersion,
                FeatureRegistry = _registry
            };

            var pipelineContext = new WorldGenerationContext(request, generatorContext);

            foreach (var pass in _passes)
            {
                if (!pass.SupportsTemplate(request.Template))
                    continue;

                var sw = Stopwatch.StartNew();
                pass.Execute(pipelineContext);
                sw.Stop();

                if (request.EnableMetrics)
                {
                    generatorContext.Metrics.RecordPhaseDuration(pass.Phase.ToString(), sw.Elapsed.TotalMilliseconds);
                }

                if (pipelineContext.Errors.Count > 0)
                {
                    break;
                }
            }

            if (pipelineContext.ValidationResult != null)
            {
                if (!pipelineContext.ValidationResult.Success)
                {
                    foreach (var err in pipelineContext.ValidationResult.Errors)
                    {
                        generatorContext.Metrics.AddValidationFailure(err);
                    }
                }
                else
                {
                    generatorContext.Metrics.ValidationPassed = true;
                }
            }

            return new WorldGenerationResult(
                pipelineContext.World,
                generatorContext.Metrics,
                pipelineContext.ValidationResult,
                pipelineContext.Errors);
        }
    }
}



