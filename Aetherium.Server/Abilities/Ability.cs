using System.Collections.Generic;

namespace Aetherium.Server.Abilities
{
    /// <summary>
    /// A generic active/triggered capability data asset (engine gap-analysis §4.3) — the
    /// genre-agnostic replacement for "spells". A swing, a spell, a hack, a tech power, a prayer
    /// are all instances of this one shape; what a campaign names its <see cref="ResourcePoolTag"/>
    /// or <see cref="TargetShape"/> string is entirely up to it.
    /// </summary>
    public class Ability
    {
        public string Id { get; }
        public string? ResourcePoolTag { get; }
        public double ResourceCost { get; }

        /// <summary>Ticks before the ability activates (charge-up).</summary>
        public double ChargeTime { get; }
        /// <summary>Ticks the cast itself takes.</summary>
        public double CastTime { get; }
        /// <summary>Ticks of recovery after the effect resolves before the actor can act again.</summary>
        public double RecoverTime { get; }
        /// <summary>Ticks before this ability can be used again after its last use.</summary>
        public double Cooldown { get; }

        public double Range { get; }

        /// <summary>A renderer-facing visual tag (e.g. "beam", "melee_arc", "projectile", "aoe_ground") —
        /// clients bind their own effect to it, matching the render-agnostic contract.</summary>
        public string TargetShape { get; }

        public IReadOnlyList<IAbilityEffect> Effects { get; }
        public IReadOnlyList<string> Tags { get; }

        public Ability(string id, IReadOnlyList<IAbilityEffect> effects, string? resourcePoolTag = null,
            double resourceCost = 0, double chargeTime = 0, double castTime = 0, double recoverTime = 0,
            double cooldown = 0, double range = 1, string targetShape = "single", IReadOnlyList<string>? tags = null)
        {
            Id = id;
            Effects = effects;
            ResourcePoolTag = resourcePoolTag;
            ResourceCost = resourceCost;
            ChargeTime = chargeTime;
            CastTime = castTime;
            RecoverTime = recoverTime;
            Cooldown = cooldown;
            Range = range;
            TargetShape = targetShape;
            Tags = tags ?? System.Array.Empty<string>();
        }
    }

    /// <summary>Registry of <see cref="Ability"/> definitions by id.</summary>
    public class AbilityCatalog
    {
        private readonly Dictionary<string, Ability> _abilities = new();

        public bool Add(Ability ability) => _abilities.TryAdd(ability.Id, ability);

        public bool TryGet(string id, out Ability? ability) => _abilities.TryGetValue(id, out ability);
    }
}
