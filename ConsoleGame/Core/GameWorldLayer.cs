using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConsoleGame.Components;

namespace ConsoleGame.Core
{
    public class GameWorldLayer
    {
        public Size3d size { get; protected set; } = Size3d.Empty;

        public TerrainType[,,] Terrain { get; protected set; }

        public TerrainType GetTerrain(Location location) => Terrain[location.Z, location.Y, location.X];

        public GameWorldLayer()
        {
        }

        public GameWorldLayer(Size3d size)
        {
            Initialize(size);
        }

        private void Initialize(Size3d size)
        {
            this.size = size;

            Terrain = new TerrainType[size.Depth, size.Length, size.Width];
        }
    }
}