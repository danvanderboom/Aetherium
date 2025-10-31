namespace Aetherium.WorldGen.Passes
{
    public sealed class DungeonValidationPass : IWorldGenerationPass
    {
        private readonly GenerationValidationService _service = new GenerationValidationService();

        public string Name => "dungeon-validation";
        public GenerationPhase Phase => GenerationPhase.Validation;

        public bool SupportsTemplate(WorldGenerationTemplate template) => template == WorldGenerationTemplate.Dungeon;

        public void Execute(WorldGenerationContext context)
        {
            var validation = _service.Validate(context);
            context.ValidationResult = validation;
        }
    }
}



