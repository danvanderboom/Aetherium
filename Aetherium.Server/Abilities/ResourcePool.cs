using System;
using Aetherium.Core;

namespace Aetherium.Server.Abilities
{
    public enum ResourceRegenPolicy
    {
        OutOfCombat,
        OnHit,
        Continuous,
    }

    /// <summary>
    /// A data-driven resource pool (engine gap-analysis §4.3) — mana, stamina, focus, battery,
    /// oxygen, hack-charges are all instances of this one shape. <see cref="IsInverse"/> supports
    /// "heat"-style pools that fill with use and drain (vent) via regen, rather than draining with
    /// use and refilling via regen.
    /// </summary>
    public class ResourcePool
    {
        public string Tag { get; }
        public double Current { get; private set; }
        public double Max { get; }
        public double RegenPerTick { get; }
        public ResourceRegenPolicy RegenPolicy { get; }
        public bool IsInverse { get; }

        /// <summary>For an inverse pool, spending that would push <see cref="Current"/> above this
        /// threshold is refused (overheated) — ignored for a non-inverse pool.</summary>
        public double? OverheatThreshold { get; }

        public ResourcePool(string tag, double max, double regenPerTick = 0,
            ResourceRegenPolicy regenPolicy = ResourceRegenPolicy.Continuous, bool isInverse = false,
            double? overheatThreshold = null, double? current = null)
        {
            Tag = tag;
            Max = max;
            RegenPerTick = regenPerTick;
            RegenPolicy = regenPolicy;
            IsInverse = isInverse;
            OverheatThreshold = overheatThreshold;
            Current = current ?? (isInverse ? 0 : max);
        }

        public bool CanAfford(double cost)
            => IsInverse ? Current + cost <= (OverheatThreshold ?? Max) : Current >= cost;

        /// <summary>Spends <paramref name="cost"/> if affordable (draining a normal pool, filling an
        /// inverse one). Returns false and leaves <see cref="Current"/> unchanged if not affordable.</summary>
        public bool TrySpend(double cost)
        {
            if (!CanAfford(cost))
                return false;

            Current = Math.Clamp(Current + (IsInverse ? cost : -cost), 0, Max);
            return true;
        }

        /// <summary>Applies one tick of passive regen per <see cref="RegenPolicy"/> (a normal pool
        /// fills toward <see cref="Max"/>; an inverse pool vents toward zero). <paramref name="inCombat"/>
        /// gates <see cref="ResourceRegenPolicy.OutOfCombat"/>; <see cref="ResourceRegenPolicy.OnHit"/>
        /// pools do not regen here — see <see cref="GainOnHit"/>.</summary>
        public void Regen(bool inCombat)
        {
            bool shouldRegen = RegenPolicy switch
            {
                ResourceRegenPolicy.Continuous => true,
                ResourceRegenPolicy.OutOfCombat => !inCombat,
                ResourceRegenPolicy.OnHit => false,
                _ => false,
            };
            if (!shouldRegen)
                return;

            Current = Math.Clamp(Current + (IsInverse ? -RegenPerTick : RegenPerTick), 0, Max);
        }

        /// <summary>Explicit gain for <see cref="ResourceRegenPolicy.OnHit"/> pools — called by
        /// whatever event fires "on hit", not by <see cref="Regen"/>.</summary>
        public void GainOnHit(double amount)
        {
            Current = Math.Clamp(Current + (IsInverse ? -amount : amount), 0, Max);
        }
    }

    /// <summary>An actor's set of resource pools, keyed by tag.</summary>
    public class ResourcePools : Component
    {
        private readonly System.Collections.Generic.Dictionary<string, ResourcePool> _pools = new();

        public void Add(ResourcePool pool) => _pools[pool.Tag] = pool;

        public bool TryGet(string tag, out ResourcePool? pool) => _pools.TryGetValue(tag, out pool);
    }
}
