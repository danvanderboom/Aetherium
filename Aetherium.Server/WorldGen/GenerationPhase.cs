namespace Aetherium.WorldGen
{
    /// <summary>
    /// Defines the ordered phases used by the procedural generation pipeline.
    /// </summary>
    public enum GenerationPhase
    {
        Layout = 0,
        Theming = 1,
        Population = 2,
        Interactions = 3,
        Adaptation = 4, // Post-generation adaptation based on agent behavior
        Validation = 5
    }
}



