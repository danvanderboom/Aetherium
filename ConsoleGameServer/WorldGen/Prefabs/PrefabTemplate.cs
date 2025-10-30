using System.Collections.Generic;

namespace ConsoleGame.WorldGen.Prefabs
{
    /// <summary>
    /// Template for a reusable map prefab (building, tree cluster, feature, etc.).
    /// </summary>
    public class PrefabTemplate
    {
        public string PrefabId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // "building", "tree-cluster", "lake", etc.
        public int Width { get; set; }
        public int Height { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        public PrefabTile[,] Tiles { get; set; } = new PrefabTile[0, 0];
    }

    /// <summary>
    /// Individual tile within a prefab template.
    /// </summary>
    public class PrefabTile
    {
        public string TerrainType { get; set; } = string.Empty;
        public string? EntityType { get; set; } // Optional entity to spawn (Door, Window, NPC, etc.)
        public Dictionary<string, object>? EntityConfig { get; set; } // Configuration for entity
    }
}

