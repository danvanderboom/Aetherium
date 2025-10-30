using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public enum ConsumableEffectType
    {
        HealthRestore,
        HungerRestore,
        EnergyRestore
    }

    public class Consumable : Component
    {
        public ConsumableEffectType EffectType { get; set; } = ConsumableEffectType.HealthRestore;
        public int EffectValue { get; set; } = 1;
        public int Uses { get; set; } = 1;

        public Consumable() : base() { }
    }
}
