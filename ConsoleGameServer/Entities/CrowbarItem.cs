using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Entities
{
    public class CrowbarItem : Item
    {
        public CrowbarItem() : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Crowbar";
            carriable.Icon = "[";

            // Crowbar can force open doors
            Set(new ForcesDoor
            {
                Strength = 3,
                Durability = 20
            });
        }
    }
}
