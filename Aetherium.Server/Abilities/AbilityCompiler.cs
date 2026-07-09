using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Model.Abilities;
using Aetherium.Server.Combat;
using ModelRegen = Aetherium.Model.Abilities.ResourceRegenPolicyKind;

namespace Aetherium.Server.Abilities
{
    /// <summary>
    /// Compiles pure-data ability content (<see cref="AbilityDefinition"/>/<see cref="ResourcePoolDefinition"/>,
    /// from a world's <see cref="AbilityConfig"/>) into the runtime tier the cast path consumes:
    /// an <see cref="AbilityCatalog"/> of <see cref="Ability"/>s (with their effects bound to the
    /// map's <see cref="DamagePipeline"/>/<see cref="IHitResolver"/>) and fresh <see cref="ResourcePools"/>
    /// components. This is the data→behavior seam that lets abilities be per-world content rather than
    /// engine-hardcoded — mirrors ContentAtlas's data(Model)/seeding(Server) split.
    /// </summary>
    public class AbilityCompiler
    {
        private readonly DamagePipeline _pipeline;
        private readonly IHitResolver _hitResolver;

        public AbilityCompiler(DamagePipeline pipeline, IHitResolver hitResolver)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _hitResolver = hitResolver ?? throw new ArgumentNullException(nameof(hitResolver));
        }

        /// <summary>Compiles a set of definitions into an <see cref="AbilityCatalog"/> (empty if null).</summary>
        public AbilityCatalog CompileCatalog(IEnumerable<AbilityDefinition>? definitions)
        {
            var catalog = new AbilityCatalog();
            if (definitions is null)
                return catalog;

            foreach (var def in definitions)
                catalog.Add(CompileAbility(def));

            return catalog;
        }

        public Ability CompileAbility(AbilityDefinition def)
        {
            var effects = def.Effects.Select(CompileEffect).ToList();
            return new Ability(
                def.Id,
                effects,
                resourcePoolTag: def.ResourcePoolTag,
                resourceCost: def.ResourceCost,
                chargeTime: def.ChargeTime,
                castTime: def.CastTime,
                recoverTime: def.RecoverTime,
                cooldown: def.Cooldown,
                range: def.Range,
                targetShape: def.TargetShape,
                tags: def.Tags);
        }

        private IAbilityEffect CompileEffect(AbilityEffectDescriptor d) => d.Kind switch
        {
            AbilityEffectKind.DealDamage => new DealDamageEffect(
                DamagePacket.Single(d.DamageType ?? "physical", d.Amount, delivery: DamageDelivery.Ranged),
                _hitResolver,
                _pipeline),

            AbilityEffectKind.ModifyResource => new ModifyResourceEffect(
                d.PoolTag ?? throw new InvalidOperationException("ModifyResource effect requires a PoolTag"),
                d.Delta,
                onCaster: d.ResourceTarget == AbilityEffectTarget.Caster),

            AbilityEffectKind.ApplyStatus => new ApplyStatusEffect(() => BuildStatus(d)),

            _ => throw new InvalidOperationException($"Unknown ability effect kind: {d.Kind}"),
        };

        private static StatusEffect BuildStatus(AbilityEffectDescriptor d) => (d.StatusId ?? string.Empty) switch
        {
            "burning" => new BurningEffect(d.DurationTicks, d.Magnitude),
            "slowed" => new SlowedEffect(d.DurationTicks, d.Magnitude),
            "prone" => new ProneEffect(d.DurationTicks),
            _ => throw new InvalidOperationException($"Unknown status id for ApplyStatus effect: '{d.StatusId}'"),
        };

        /// <summary>Builds a fresh <see cref="ResourcePools"/> component from definitions — called once
        /// per joining character so each carries its own mutable pool state (empty if null).</summary>
        public ResourcePools BuildResourcePools(IEnumerable<ResourcePoolDefinition>? definitions)
        {
            var pools = new ResourcePools();
            if (definitions is null)
                return pools;

            foreach (var def in definitions)
            {
                pools.Add(new ResourcePool(
                    def.Tag,
                    def.Max,
                    regenPerTick: def.RegenPerTick,
                    regenPolicy: MapRegen(def.RegenPolicy),
                    isInverse: def.IsInverse,
                    overheatThreshold: def.OverheatThreshold,
                    current: def.StartingValue));
            }

            return pools;
        }

        private static ResourceRegenPolicy MapRegen(ModelRegen kind) => kind switch
        {
            ModelRegen.OutOfCombat => ResourceRegenPolicy.OutOfCombat,
            ModelRegen.OnHit => ResourceRegenPolicy.OnHit,
            ModelRegen.Continuous => ResourceRegenPolicy.Continuous,
            _ => ResourceRegenPolicy.Continuous,
        };
    }
}
