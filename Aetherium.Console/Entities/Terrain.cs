using System;
using System.Linq;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Entities
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

