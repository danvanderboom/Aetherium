using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Components;

namespace Aetherium.WorldBuilders
{
    // var builder = new WorldBuilder().Build().Populate();
    public abstract class WorldBuilder
    {
        public World? World { get; set; }

        public List<WorldFeature> Features { get; set; }

        public WorldBuilder()
        {
            Features = new List<WorldFeature>();
        }

        public abstract World Build();
    }
}

