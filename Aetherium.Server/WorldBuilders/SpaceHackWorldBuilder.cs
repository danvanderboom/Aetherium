using System;
using System.Linq;
using System.Collections.Generic;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;

namespace Aetherium.WorldBuilders
{
    public class SpaceHackWorldBuilder : WorldBuilder
    {
        Random rand = new Random();

        public SpaceHackWorldBuilder() : base()
        {
        }

        public override World Build()
        {
            return new World();
        }
    }
}

