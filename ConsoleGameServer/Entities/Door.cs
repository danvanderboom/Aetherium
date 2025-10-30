using System;
using System.Collections.Generic;
using System.Text;
using ConsoleGame.Core;
using ConsoleGame.Components;

namespace ConsoleGame.Entities
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
