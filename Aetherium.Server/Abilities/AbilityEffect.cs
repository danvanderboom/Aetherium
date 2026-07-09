using Aetherium.Core;
using Aetherium.Server.Combat;

namespace Aetherium.Server.Abilities
{
    /// <summary>What an <see cref="Ability"/>'s effect needs to resolve: the world, the caster, and
    /// an optional target (absent for self/AoE-ground effects with no single target).</summary>
    public class AbilityEffectContext
    {
        public World World { get; }
        public Entity Caster { get; }
        public Entity? Target { get; }

        public AbilityEffectContext(World world, Entity caster, Entity? target = null)
        {
            World = world;
            Caster = caster;
            Target = target;
        }
    }

    /// <summary>
    /// One composable effect an <see cref="Ability"/> applies (engine gap-analysis §4.3: "Effects
    /// are composable"). New effect kinds are how a mod/content pack extends what abilities can do,
    /// without engine changes — matching the modding-SDK vision (§4.15).
    /// </summary>
    public interface IAbilityEffect
    {
        void Apply(AbilityEffectContext context);
    }

    /// <summary>Deals a <see cref="DamagePacket"/> to the target via the existing, shipped
    /// <see cref="DamagePipeline"/> — abilities reuse the same damage/mitigation/threat/death-state
    /// pipeline combat does, rather than a parallel one.</summary>
    public class DealDamageEffect : IAbilityEffect
    {
        private readonly DamagePacket _packet;
        private readonly IHitResolver _hitResolver;
        private readonly DamagePipeline _pipeline;

        public DealDamageEffect(DamagePacket packet, IHitResolver hitResolver, DamagePipeline? pipeline = null)
        {
            _packet = packet;
            _hitResolver = hitResolver;
            _pipeline = pipeline ?? new DamagePipeline();
        }

        public void Apply(AbilityEffectContext context)
        {
            if (context.Target is null)
                return;

            _pipeline.Resolve(context.Caster, context.Target, _packet, _hitResolver);
        }
    }

    /// <summary>Applies a <see cref="StatusEffect"/> to the target's <see cref="StatusEffects"/>.
    /// The target must already carry a <see cref="StatusEffects"/> component; if it doesn't, the
    /// effect is a no-op (an ability cannot retrofit components onto an entity that opted out).</summary>
    public class ApplyStatusEffect : IAbilityEffect
    {
        private readonly System.Func<StatusEffect> _effectFactory;

        public ApplyStatusEffect(System.Func<StatusEffect> effectFactory)
        {
            _effectFactory = effectFactory;
        }

        public void Apply(AbilityEffectContext context)
        {
            if (context.Target is null || !context.Target.Has<StatusEffects>())
                return;

            context.Target.Get<StatusEffects>().Apply(_effectFactory());
        }
    }

    /// <summary>Adjusts a resource pool on the caster or the target by <see cref="Delta"/> (positive
    /// or negative) — e.g. an ability that grants stamina, drains a target's mana, or vents heat.</summary>
    public class ModifyResourceEffect : IAbilityEffect
    {
        private readonly string _poolTag;
        private readonly double _delta;
        private readonly bool _onCaster;

        public ModifyResourceEffect(string poolTag, double delta, bool onCaster)
        {
            _poolTag = poolTag;
            _delta = delta;
            _onCaster = onCaster;
        }

        public void Apply(AbilityEffectContext context)
        {
            Entity? subject = _onCaster ? context.Caster : context.Target;
            if (subject is null || !subject.Has<ResourcePools>())
                return;

            if (subject.Get<ResourcePools>().TryGet(_poolTag, out var pool) && pool is not null)
                pool.GainOnHit(_delta);
        }
    }
}
