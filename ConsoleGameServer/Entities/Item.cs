using ConsoleGame.Core;
using ConsoleGame.Components;

namespace ConsoleGame.Entities
{
    public class Item : Entity
    {
        public Item() : base()
        {
            Set(new Carriable());
        }
    }
}


