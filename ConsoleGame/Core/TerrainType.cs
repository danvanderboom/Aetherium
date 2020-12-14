using System.Collections.Generic;

namespace ConsoleGame.Core
{
    public class TerrainType
    {
        public string Name { get; set; } = string.Empty;

        public TileType? TileType { get; set; }

        public Dictionary<string, string> Settings { get; set; }

        public TerrainType() : base()
        {
            Settings = new Dictionary<string, string>();
        }
    }
}
