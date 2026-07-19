using System.Linq;

namespace Aetherium.WorldGen.Passes
{
    public sealed class OutdoorValidationPass : IWorldGenerationPass
    {
        private readonly GenerationValidationService _service = new GenerationValidationService();

        public string Name => "outdoor-validation";
        public GenerationPhase Phase => GenerationPhase.Validation;

        public bool SupportsTemplate(WorldGenerationTemplate template) => template == WorldGenerationTemplate.Outdoor;

        // Validates a single start→objective PrimaryPath and square-neighbour connectivity — neither
        // is meaningful on an open H3 sphere (no border, no primary route). Sphere-native validation
        // (great-circle reachability over topology.Neighbors) is the phased follow-up.
        public bool SupportsTopology(string? topology)
            => !string.Equals(topology, "h3", System.StringComparison.OrdinalIgnoreCase);

        public void Execute(WorldGenerationContext context)
        {
            var validation = _service.Validate(context);

            if (context.GeneratorContext.Metrics.BiomeCoverage.Count < 3)
            {
                validation.AddError("Outdoor biome coverage insufficient variety");
            }

            if (!context.PrimaryPath.Any())
            {
                validation.AddError("Primary route missing for outdoor generation");
            }

            context.ValidationResult = validation;
        }
    }
}



