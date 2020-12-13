using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using ConsoleGame.Components;

namespace ConsoleGame.Core
{
    public abstract class Entity : Component
    {
        public string EntityId { get; set; } = Guid.NewGuid().ToString();

        public Entity() : base() 
        {
            Set(new Location());
            Set(new Tile());
        }
    }
}
