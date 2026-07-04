using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// Marks a carriable item as a weapon that adds to its holder's effective attack
    /// power while carried. <see cref="Aetherium.Server.CombatSystem"/> applies only
    /// the single best (highest-bonus) weapon in the attacker's inventory — weapon
    /// bonuses do not stack (P3-7 slice 2).
    /// </summary>
    public class Weapon : Component
    {
        public string Name { get; set; } = "Weapon";

        public int AttackBonus { get; set; }

        public Weapon() { }

        public Weapon(string name, int attackBonus)
        {
            Name = name;
            AttackBonus = attackBonus;
        }
    }
}
