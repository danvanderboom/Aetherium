using NUnit.Framework;
using Aetherium.Server.Progression;

namespace Aetherium.Test.Progression
{
    /// <summary>Verifies "Generic Progress Pools"
    /// (openspec/changes/add-character-progression/specs/character-progression/spec.md).</summary>
    [TestFixture]
    public class ProgressPoolsTests
    {
        [Test]
        public void AddXp_CreatesPool_AndAccumulates()
        {
            var pools = new ProgressPools();
            var curve = new LinearLevelCurve(xpPerLevel: 100);

            pools.AddXp("combat_xp", 30, curve);
            pools.AddXp("combat_xp", 40, curve);

            Assert.That(pools.Pools["combat_xp"].Xp, Is.EqualTo(70));
        }

        [Test]
        public void AddXp_RecomputesLevel_ViaInjectedCurve()
        {
            var pools = new ProgressPools();
            var curve = new LinearLevelCurve(xpPerLevel: 100);

            var pool = pools.AddXp("combat_xp", 250, curve);

            Assert.That(pool.Level, Is.EqualTo(3), "250 xp / 100 per level = level 3 (floor(2.5) + 1).");
        }

        [Test]
        public void MultiplePools_AreIndependent()
        {
            var pools = new ProgressPools();
            var curve = new LinearLevelCurve(xpPerLevel: 100);

            pools.AddXp("combat_xp", 500, curve);
            pools.AddXp("exploration_xp", 10, curve);

            Assert.That(pools.Pools["combat_xp"].Level, Is.GreaterThan(pools.Pools["exploration_xp"].Level));
        }

        [Test]
        public void LinearLevelCurve_ZeroXp_IsLevelOne()
        {
            var curve = new LinearLevelCurve(xpPerLevel: 100);
            Assert.That(curve.LevelForXp(0), Is.EqualTo(1));
        }

        [Test]
        public void CustomLevelCurve_IsHonored_NotHardcoded()
        {
            // A campaign-defined curve wildly different from the engine's boring default.
            var steepCurve = new StubLevelCurve(fixedLevel: 42);
            var pools = new ProgressPools();

            var pool = pools.AddXp("faction_rep", 1, steepCurve);

            Assert.That(pool.Level, Is.EqualTo(42), "The engine must not assume LinearLevelCurve — any ILevelCurve must be honored.");
        }

        private sealed class StubLevelCurve : ILevelCurve
        {
            private readonly int _fixedLevel;
            public StubLevelCurve(int fixedLevel) { _fixedLevel = fixedLevel; }
            public int LevelForXp(double cumulativeXp) => _fixedLevel;
        }
    }
}
