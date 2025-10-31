using System;
using System.Collections.Generic;
using System.Text;
using Aetherium.Core;

namespace Aetherium.Components
{
    public class Tile : Component
    {
        public TileType Type { get; set; } = TileType.None;

        public Tile() { }
    }
}

