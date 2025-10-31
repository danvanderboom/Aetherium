using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Aetherium.Components;

namespace Aetherium.Core
{
    public abstract class Entity : Component
    {
        public string EntityId { get; set; } = Guid.NewGuid().ToString();

        public Entity() : base() 
        {
            Set(new WorldLocation());
            Set(new Tile());
        }
    }
}

