using System.Collections.Generic;

namespace ConsoleGame.Core
{
    public class TerrainType
    {
        public string Name { get; set; }

        public TileType TileType { get; set; }

        public Dictionary<string, string> Settings { get; set; }

        public TerrainType() : base()
        {
        }
    }
}
