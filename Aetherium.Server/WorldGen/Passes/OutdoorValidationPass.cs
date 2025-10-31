using System.Linq;

namespace Aetherium.WorldGen.Passes
{
    public sealed class OutdoorValidationPass : IWorldGenerationPass
    {
        private readonly GenerationValidationService _service = new GenerationValidationService();

        public string Name => "outdoor-validation";
        public GenerationPhase Phase => GenerationPhase.Validation;

        public bool SupportsTemplate(WorldGenerationTemplate template) => template == WorldGenerationTemplate.Outdoor;

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



