using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.WorldGen
{
    public sealed class WorldGenerationResult
    {
        public WorldGenerationResult(
            World? world,
            GenerationMetrics metrics,
            GenerationValidationResult? validation,
            IReadOnlyList<string> errors,
            string? abortedByPass = null,
            int effectiveSeed = 0)
        {
            World = world;
            Metrics = metrics;
            Validation = validation;
            Errors = errors;
            AbortedByPass = abortedByPass;
            EffectiveSeed = effectiveSeed;
        }

        public World? World { get; }
        public GenerationMetrics Metrics { get; }
        public GenerationValidationResult? Validation { get; }
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// Name of the pass that recorded the first hard error and aborted the pipeline, if any.
        /// Null when the pipeline ran to completion (regardless of validation outcome).
        /// </summary>
        public string? AbortedByPass { get; }

        /// <summary>
        /// The effective seed used for this generation run. Store this value to replay the same world.
        /// Mirrors <see cref="GenerationMetrics.EffectiveSeed"/> for convenient top-level access.
        /// </summary>
        public int EffectiveSeed { get; }

        public bool Success => World != null && (Validation?.Success ?? true) && Errors.Count == 0;
    }
}



