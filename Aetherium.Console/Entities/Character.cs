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
            Set(new Health());
            Set(new HasHeading());
            Set(new Perception());
            Set(new Memory());
        }
    }
}
