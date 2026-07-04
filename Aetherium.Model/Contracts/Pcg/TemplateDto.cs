using System;
using System.Collections.Generic;
using WorldGenCLI.Models;

namespace WorldGenCLI.Models
{
    /// <summary>
    /// Saved PCG template configuration.
    /// </summary>
    public sealed class TemplateDto
    {
        public string Name { get; set; } = string.Empty;
        public string GeneratorId { get; set; } = string.Empty;
        public string Template { get; set; } = "dungeon";
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public HybridLayout? HybridAnchors { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Modified { get; set; }
        public string? Description { get; set; }
    }
}

