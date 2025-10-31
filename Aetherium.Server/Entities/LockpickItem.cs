using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Entities
{
    public class LockpickItem : Item
    {
        public LockpickItem(int skillLevel = 3) : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Lockpick";
            carriable.Icon = ",";

            // Lockpick for picking locks
            Set(new Lockpick
            {
                SkillLevel = skillLevel,
                Durability = 15
            });
        }
    }
}

