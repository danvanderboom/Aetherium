using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using ConsoleGame.Core;
using ConsoleGame.Components;

namespace ConsoleGame.Entities
{
    public class Bomb : Entity
    {
        public Bomb() : base()
        {
            Set(new DelayedExplosion());
        }
    }

    
}
