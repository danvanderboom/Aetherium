using System.Collections.Generic;
using Aetherium.Components;

namespace Aetherium
{
    public class MazeNeighbor
    {
        public WorldDirection Direction { get; set; } = WorldDirection.North;

        public IList<WorldLocation> Walls { get; set; } = new List<WorldLocation>();

        public override string ToString() => $"{Direction}, {Walls.Count} walls";
    }
}

