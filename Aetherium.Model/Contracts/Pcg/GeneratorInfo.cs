namespace Aetherium.Model.Pcg
{
    /// <summary>
    /// Generator metadata for listing available generators.
    /// </summary>
    public sealed class GeneratorInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}

