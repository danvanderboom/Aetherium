namespace Aetherium.Server.Progression
{
    /// <summary>One named XP/level track (engine gap-analysis §4.4) — a campaign may define several
    /// independent pools (combat XP, exploration XP, crafting XP, faction rep).</summary>
    public class ProgressPool
    {
        public string Id { get; }
        public double Xp { get; internal set; }
        public int Level { get; internal set; }

        public ProgressPool(string id, double xp = 0, int level = 1)
        {
            Id = id;
            Xp = xp;
            Level = level;
        }
    }

    /// <summary>Converts a pool's cumulative XP into a level. A campaign supplies its own —
    /// the engine ships only <see cref="LinearLevelCurve"/> as a boring default.</summary>
    public interface ILevelCurve
    {
        int LevelForXp(double cumulativeXp);
    }

    /// <summary>Level = floor(xp / xpPerLevel) + 1 — the simplest possible curve, not a design
    /// recommendation.</summary>
    public class LinearLevelCurve : ILevelCurve
    {
        private readonly double _xpPerLevel;

        public LinearLevelCurve(double xpPerLevel)
        {
            _xpPerLevel = xpPerLevel;
        }

        public int LevelForXp(double cumulativeXp)
            => 1 + (int)System.Math.Floor(System.Math.Max(0, cumulativeXp) / _xpPerLevel);
    }
}
