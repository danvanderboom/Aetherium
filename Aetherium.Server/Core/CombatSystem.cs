using System;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Server
{
    /// <summary>
    /// Pure, deterministic melee-combat resolution, mirroring <see cref="InteractionSystem"/>'s
    /// stateless-service shape so both the in-process (<c>LocalMutationGateway</c>) and
    /// grain-authoritative (<c>GameMapGrain</c>) paths can share one implementation.
    /// </summary>
    public class CombatSystem
    {
        /// <summary>Fixed melee damage per hit. Constant (not RNG) so combat is reproducible/testable.</summary>
        public const int DefaultAttackDamage = 10;

        /// <summary>
        /// Resolves an attack by <paramref name="attacker"/> against the entity identified by
        /// <paramref name="targetEntityId"/> in <paramref name="world"/>. On success the target's
        /// <see cref="Health"/> is reduced; if it reaches zero the target is <em>defeated</em>.
        /// When <paramref name="removeOnDeath"/> is true (the default) a defeated target is removed
        /// from the world — the semantics for a player killing a monster. Monster retaliation on the
        /// tick passes <c>false</c> so a downed player's entity survives at 0 HP rather than vanishing
        /// mid-tick (which would strand its session and desync map bookkeeping); death/respawn is a
        /// separate lifecycle concern. <see cref="CombatResult.TargetDefeated"/> reports the 0-HP
        /// state regardless of removal.
        /// </summary>
        public CombatResult TryAttack(World world, Entity attacker, string targetEntityId, bool removeOnDeath = true)
        {
            if (world == null || attacker == null)
                return CombatResult.Fail("No attacker");

            if (string.IsNullOrEmpty(targetEntityId))
                return CombatResult.Fail("No target");

            if (!world.Entities.TryGetValue(targetEntityId, out var target) || target == null)
                return CombatResult.Fail("Target not found");

            if (ReferenceEquals(target, attacker) || target.EntityId == attacker.EntityId)
                return CombatResult.Fail("Cannot attack yourself");

            // Entity.Get<T>() throws when the component is absent, so gate every access on Has<T>().
            if (!attacker.Has<WorldLocation>() || !target.Has<WorldLocation>())
                return CombatResult.Fail("Attacker or target has no location");

            var attackerLoc = attacker.Get<WorldLocation>();
            var targetLoc = target.Get<WorldLocation>();

            int distance = Math.Abs(targetLoc.X - attackerLoc.X)
                         + Math.Abs(targetLoc.Y - attackerLoc.Y)
                         + Math.Abs(targetLoc.Z - attackerLoc.Z);
            if (distance > 1)
                return CombatResult.Fail("Target is not in reach");

            if (!target.Has<Health>())
                return CombatResult.Fail("Target cannot be attacked");

            var health = target.Get<Health>();

            int damage = ComputeAttackDamage(attacker);
            health.Level = Math.Max(0, health.Level - damage);
            bool defeated = health.Level <= 0;

            var targetType = target.GetType().Name;

            if (defeated && removeOnDeath)
                world.TryRemoveEntity(targetEntityId);

            return CombatResult.Hit(damage, health.Level, defeated, targetType, targetEntityId);
        }

        /// <summary>
        /// The damage <paramref name="attacker"/> deals per hit: its base
        /// <see cref="AttackPower"/> (or <see cref="DefaultAttackDamage"/> when the
        /// component is absent) plus the single best weapon bonus among carried items.
        /// Weapon bonuses do not stack — only the strongest <see cref="Weapon"/> in the
        /// attacker's <see cref="Inventory"/> applies. Guards every component read with
        /// <c>Has&lt;T&gt;()</c> because <c>Get&lt;T&gt;()</c> throws when a component is missing.
        /// </summary>
        public static int ComputeAttackDamage(Entity attacker)
        {
            if (attacker is null)
                return 0;

            int baseDamage = attacker.Has<AttackPower>()
                ? attacker.Get<AttackPower>().Amount
                : DefaultAttackDamage;

            int weaponBonus = 0;
            if (attacker.Has<Inventory>())
            {
                foreach (var item in attacker.Get<Inventory>().Items.Values)
                {
                    if (item is not null && item.Has<Weapon>())
                    {
                        var bonus = item.Get<Weapon>().AttackBonus;
                        if (bonus > weaponBonus)
                            weaponBonus = bonus;
                    }
                }
            }

            return Math.Max(0, baseDamage + weaponBonus);
        }
    }

    /// <summary>
    /// Outcome of a <see cref="CombatSystem.TryAttack"/> call.
    /// </summary>
    public class CombatResult
    {
        public bool Success { get; init; }
        public string? Reason { get; init; }
        public int Damage { get; init; }
        public int RemainingHealth { get; init; }
        public bool TargetDefeated { get; init; }
        public string TargetType { get; init; } = string.Empty;
        public string TargetEntityId { get; init; } = string.Empty;

        public static CombatResult Hit(int damage, int remainingHealth, bool defeated, string targetType, string targetEntityId)
            => new()
            {
                Success = true,
                Damage = damage,
                RemainingHealth = remainingHealth,
                TargetDefeated = defeated,
                TargetType = targetType,
                TargetEntityId = targetEntityId,
            };

        public static CombatResult Fail(string reason)
            => new() { Success = false, Reason = reason };
    }
}
