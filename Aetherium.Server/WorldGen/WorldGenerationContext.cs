using System;
using System.Collections.Generic;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.WorldGen
{
    /// <summary>
    /// Shared context object passed to each generation pass.
    /// </summary>
    public sealed class WorldGenerationContext
    {
        private readonly List<string> _errors = new();

        public WorldGenerationRequest Request { get; }
        public GeneratorContext GeneratorContext { get; }
        public GenerationMetrics Metrics => GeneratorContext.Metrics;
        public World? World { get; set; }

        /// <summary>
        /// Errors accumulated during pipeline execution. Read-only to callers — use
        /// <see cref="AddError"/> to record an error. The orchestrator aborts the pipeline
        /// on the first new error from any pass.
        /// </summary>
        public IReadOnlyList<string> Errors => _errors;

        public GenerationValidationResult? ValidationResult { get; set; }

        /// <summary>
        /// Shared data bag for inter-pass communication. Backed by
        /// <see cref="GeneratorContext.PhaseArtifacts"/>. Treat keys as a public contract —
        /// passes that consume from this bag should fail loudly if the expected key/type is
        /// missing rather than silently no-op.
        /// </summary>
        public IDictionary<string, object> SharedData => GeneratorContext.PhaseArtifacts;

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

            _errors.Add(error);
        }
    }
}



