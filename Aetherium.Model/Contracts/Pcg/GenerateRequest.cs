using System.Collections.Generic;
using Aetherium.WorldGen;
using WorldGenCLI.Models;

namespace WorldGenCLI.Models
{
    /// <summary>
    /// Simplified request for API generation endpoint.
    /// </summary>
    public sealed class GenerateRequest
    {
        public string LayoutGenerator { get; set; } = string.Empty;
        public string Template { get; set; } = "dungeon";
        public int Width { get; set; } = 80;
        public int Height { get; set; } = 80;
        public int Levels { get; set; } = 1;
        public int? Seed { get; set; }
        public string GeneratorVersion { get; set; } = "1.0.0";
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public HybridLayout? HybridAnchors { get; set; }
    }
}

