using System;
using System.Linq;
using ConsoleGame.Core;
using ConsoleGame.Components;

namespace ConsoleGame.Entities
{
    public class Terrain : Entity
    {
        public TerrainType Type { get; set; }

        public Terrain(TerrainType type) : base() 
        {
            Type = type;
        }
    }
}
