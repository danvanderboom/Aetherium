namespace Aetherium.WorldGen
{
    /// <summary>
    /// Represents a single pipeline phase that can mutate the world, collect metrics,
    /// or enforce validation rules during procedural content generation.
    /// </summary>
    public interface IWorldGenerationPass
    {
        string Name { get; }
        GenerationPhase Phase { get; }
        bool SupportsTemplate(WorldGenerationTemplate template);
        void Execute(WorldGenerationContext context);
    }
}



