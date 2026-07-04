using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// An entity's base melee attack power — the damage it deals per hit before any
    /// weapon bonus. When absent, <see cref="Aetherium.Server.CombatSystem"/> falls
    /// back to <see cref="Aetherium.Server.CombatSystem.DefaultAttackDamage"/>, so
    /// this component makes attack strength vary per entity (P3-7 slice 2).
    /// </summary>
    public class AttackPower : Component
    {
        public int Amount { get; set; }

        public AttackPower() { }

        public AttackPower(int amount)
        {
            Amount = amount;
        }
    }
}
