using System;
using System.Collections.Generic;
using System.Text;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Entities
{
    public class Door : Entity
    {
        public Door() : base() 
        {
            Set(new OpensAndCloses { IsOpen = false, IsLocked = false, KeyShape = string.Empty });
            Set(new ObstructsView { Opacity = 1 });
            Set(new ObstructsMovement { Obstruction = 1 });
        }
    }
}

