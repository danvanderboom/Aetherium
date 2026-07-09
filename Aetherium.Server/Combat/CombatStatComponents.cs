using Aetherium.Core;

namespace Aetherium.Server.Combat
{
    /// <summary>Base hit chance (0..1) for <see cref="RollHitResolver"/>. Absent = <see cref="RollHitResolver.DefaultAccuracy"/>.</summary>
    public class Accuracy : Component
    {
        public double Chance { get; set; }
        public Accuracy() { }
        public Accuracy(double chance) { Chance = chance; }
    }

    /// <summary>Chance (0..1) to avoid an incoming attack. Absent = <see cref="RollHitResolver.DefaultEvasion"/>.</summary>
    public class Evasion : Component
    {
        public double Chance { get; set; }
        public Evasion() { }
        public Evasion(double chance) { Chance = chance; }
    }

    /// <summary>Chance (0..1), given a hit, that it's a critical. Absent = <see cref="RollHitResolver.DefaultCritChance"/>.</summary>
    public class CritChance : Component
    {
        public double Chance { get; set; }
        public CritChance() { }
        public CritChance(double chance) { Chance = chance; }
    }
}
