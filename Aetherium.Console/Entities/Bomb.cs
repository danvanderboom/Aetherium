using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Entities
{
    public class Bomb : Entity
    {
        public Bomb() : base()
        {
            Set(new DelayedExplosion());
        }
    }

    
}

