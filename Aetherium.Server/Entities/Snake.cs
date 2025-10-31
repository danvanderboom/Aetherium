using System;
using System.Collections.Generic;
using System.Text;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Entities
{
    public class Snake : Character
    {
        Random rand;

        public Snake() : base()
        {
            rand = new Random();

            Set(new Perception());
            
            // Snakes are cold-blooded, emit very low heat
            Set(new HeatSignature(0.4, TimeSpan.FromSeconds(6)));
        }
    }
}

