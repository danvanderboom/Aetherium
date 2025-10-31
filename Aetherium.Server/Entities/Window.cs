using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Entities
{
    public class Window : Entity
    {
        public Window() : base()
        {
            Set(new OpensAndCloses { IsOpen = false, IsLocked = false, KeyShape = string.Empty });
            Set(new ObstructsMovement { Obstruction = 1 });
        }
    }
}

