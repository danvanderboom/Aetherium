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
        }
    }
}

