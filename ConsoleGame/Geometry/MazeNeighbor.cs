using System.Collections.Generic;
using ConsoleGame.Components;

namespace ConsoleGame
{
    public class MazeNeighbor
    {
        public WorldDirection Direction { get; set; } = WorldDirection.North;

        public IList<WorldLocation> Walls { get; set; } = new List<WorldLocation>();

        public override string ToString() => $"{Direction}, {Walls.Count} walls";
    }
}
