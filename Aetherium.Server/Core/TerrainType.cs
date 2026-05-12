using System.Collections.Generic;

namespace Aetherium.Core
{
    public class TerrainType
    {
        public string Name { get; set; } = string.Empty;

        public TileType? TileType { get; set; }

        public Dictionary<string, string> Settings { get; set; }

        /// <summary>
        /// Whether characters can walk onto this terrain. When unset (null), the legacy
        /// name-based <c>World.PassableTerrain</c> fallback decides. Set explicitly when
        /// registering a new TerrainType so behaviour doesn't depend on the legacy switch.
        /// </summary>
        public bool? IsPassable { get; set; }

        public TerrainType() : base()
        {
            Settings = new Dictionary<string, string>();
        }
    }
}

