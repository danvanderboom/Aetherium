using System;
using System.Linq;
using System.Collections.Generic;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGame.Entities;

namespace ConsoleGame.WorldBuilders
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
