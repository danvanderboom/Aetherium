using System.Collections.Generic;
using ConsoleGame.Components;

namespace ConsoleGame
{
    public class MazeNeighbor
    {
        public WorldDirection Direction { get; set; }

        public IList<WorldLocation> Walls { get; set; }

        public override string ToString() => $"{Direction}, {Walls.Count} walls";
    }
}
