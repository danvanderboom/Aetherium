using System;
using System.Linq;
using System.Drawing;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium
{
    public class Character : Entity
    {
        public Character() : base()
        {
            // Characters spawn at full health. Previously this was Health(0,0), which left every
            // character "dead" the moment combat could read it; combat (P3-7) now depends on this.
            Set(new Health(100, 100));
            Set(new HasHeading());
            Set(new Perception());
            Set(new Memory());
            
            // Characters emit high heat (visible in infrared)
            Set(new HeatSignature(0.9, TimeSpan.FromSeconds(10)));
        }
    }
}
