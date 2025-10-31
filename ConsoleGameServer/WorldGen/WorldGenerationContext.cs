using System;
using System.Collections.Generic;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.WorldGen
{
    /// <summary>
    /// Shared context object passed to each generation pass.
    /// </summary>
    public sealed class WorldGenerationContext
    {
        public WorldGenerationRequest Request { get; }
        public GeneratorContext GeneratorContext { get; }
        public GenerationMetrics Metrics => GeneratorContext.Metrics;
        public World? World { get; set; }
        public List<string> Errors { get; } = new();
        public GenerationValidationResult? ValidationResult { get; set; }
        public Dictionary<string, object> SharedData => GeneratorContext.PhaseArtifacts;
        public WorldLocation? StartLocation => GeneratorContext.StartLocation;
        public WorldLocation? ObjectiveLocation => GeneratorContext.ObjectiveLocation;
        public IList<WorldLocation> PrimaryPath => GeneratorContext.PrimaryPath;

        public WorldGenerationContext(WorldGenerationRequest request, GeneratorContext generatorContext)
        {
            Request = request;
            GeneratorContext = generatorContext;
        }

        public void AddError(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                return;

            Errors.Add(error);
        }
    }
}


