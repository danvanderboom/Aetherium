using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Server.Progression
{
    /// <summary>An actor's set of independent <see cref="ProgressPool"/>s, keyed by pool id.</summary>
    public class ProgressPools : Component
    {
        private readonly Dictionary<string, ProgressPool> _pools = new();

        public IReadOnlyDictionary<string, ProgressPool> Pools => _pools;

        /// <summary>Seeds a pre-built pool (used by <c>ProgressionCompiler</c> to stamp a character's
        /// starting pools with their configured starting xp/level).</summary>
        public void Add(ProgressPool pool) => _pools[pool.Id] = pool;

        public ProgressPool GetOrCreate(string poolId)
        {
            if (!_pools.TryGetValue(poolId, out var pool))
            {
                pool = new ProgressPool(poolId);
                _pools[poolId] = pool;
            }
            return pool;
        }

        /// <summary>Adds <paramref name="amount"/> XP to the named pool (creating it if absent) and
        /// recomputes its level via <paramref name="curve"/>.</summary>
        public ProgressPool AddXp(string poolId, double amount, ILevelCurve curve)
        {
            var pool = GetOrCreate(poolId);
            pool.Xp += amount;
            pool.Level = curve.LevelForXp(pool.Xp);
            return pool;
        }
    }
}
