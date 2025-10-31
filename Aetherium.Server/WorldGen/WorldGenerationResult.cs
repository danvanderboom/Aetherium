using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.WorldGen
{
    public sealed class WorldGenerationResult
    {
        public WorldGenerationResult(World? world, GenerationMetrics metrics, GenerationValidationResult? validation, IReadOnlyList<string> errors)
        {
            World = world;
            Metrics = metrics;
            Validation = validation;
            Errors = errors;
        }

        public World? World { get; }
        public GenerationMetrics Metrics { get; }
        public GenerationValidationResult? Validation { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Success => World != null && (Validation?.Success ?? true) && Errors.Count == 0;
    }
}



