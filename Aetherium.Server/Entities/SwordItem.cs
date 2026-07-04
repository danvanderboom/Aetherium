using Aetherium.Components;

namespace Aetherium.Entities
{
    /// <summary>
    /// A basic melee weapon. Carriable loot that boosts its holder's effective attack
    /// power while it sits in inventory (see <see cref="Aetherium.Server.CombatSystem"/>'s
    /// weapon-bonus resolution). Dropped by defeated monsters as loot (P3-7 slice 2),
    /// closing the loop: kill a monster → pick up its sword → hit harder.
    /// </summary>
    public class SwordItem : Item
    {
        public const int DefaultBonus = 5;

        public SwordItem() : base()
        {
            // Replaces the base Item's generic Carriable with a labelled one.
            Set(new Carriable { Label = "Sword", Icon = "/", Weight = 3 });
            Set(new Weapon("Sword", DefaultBonus));
        }
    }
}
