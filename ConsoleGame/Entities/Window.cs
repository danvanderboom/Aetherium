using ConsoleGame.Core;
using ConsoleGame.Components;

namespace ConsoleGame.Entities
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
