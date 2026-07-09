using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Server.Combat
{
    public class DamagePipelineResult
    {
        public bool Hit { get; init; }
        public bool Critical { get; init; }
        public double Damage { get; init; }
        public bool TargetEnteredDying { get; init; }
        public string? Reason { get; init; }

        public static DamagePipelineResult Miss() => new() { Hit = false };
        public static DamagePipelineResult Fail(string reason) => new() { Hit = false, Reason = reason };
    }

    /// <summary>
    /// Composes hit resolution, damage-packet mitigation, threat, and the death-state transition
    /// (engine gap-analysis §4.2) into the deep combat pipeline. Delivery-agnostic (melee/ranged/
    /// aoe) and deliberately does not perform reach/target-finding checks — those remain the
    /// caller's concern (e.g. the existing melee-reach check in <see cref="CombatSystem.TryAttack"/>,
    /// or a future ability/projectile system for ranged delivery). Not yet called from any live
    /// command path — see openspec/changes/deepen-combat-model for the deferred Phase 2 wiring.
    /// </summary>
    public class DamagePipeline
    {
        /// <summary>Multiplier applied to mitigated damage on a critical hit.</summary>
        public const double CriticalMultiplier = 1.5;

        public DamagePipelineResult Resolve(Entity attacker, Entity target, DamagePacket packet,
            IHitResolver hitResolver, int dyingTicks = 3)
        {
            if (target.Has<Dying>() || target.Has<Corpse>())
                return DamagePipelineResult.Fail("Target is already dying or dead");

            if (!target.Has<Health>())
                return DamagePipelineResult.Fail("Target cannot be attacked");

            var hitResult = hitResolver.ResolveHit(attacker, target);
            if (!hitResult.Hit)
                return DamagePipelineResult.Miss();

            var resistances = target.Has<Resistances>() ? target.Get<Resistances>() : null;
            double amount = DamageResolution.ResolveTotal(packet, resistances);
            if (hitResult.Critical)
                amount *= CriticalMultiplier;

            var health = target.Get<Health>();
            health.Level = System.Math.Max(0, health.Level - (int)System.Math.Round(amount));

            if (!target.Has<ThreatTable>())
                target.Set(new ThreatTable());
            target.Get<ThreatTable>().AddThreat(attacker.EntityId, amount);

            bool enteredDying = false;
            if (health.Level <= 0)
            {
                target.Set(new Dying(dyingTicks));
                enteredDying = true;
            }

            return new DamagePipelineResult
            {
                Hit = true,
                Critical = hitResult.Critical,
                Damage = amount,
                TargetEnteredDying = enteredDying,
            };
        }
    }
}
