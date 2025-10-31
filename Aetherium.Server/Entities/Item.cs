using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Entities
{
    public class Item : Entity
    {
        public Item() : base()
        {
            Set(new Carriable());
        }
    }
}



