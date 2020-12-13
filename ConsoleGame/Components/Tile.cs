using System;
using System.Collections.Generic;
using System.Text;
using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class Tile : Component
    {
        public TileType Type { get; set; }

        public Tile() { }
    }
}
