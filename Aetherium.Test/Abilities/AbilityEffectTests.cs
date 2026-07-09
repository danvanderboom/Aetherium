using NUnit.Framework;
using Aetherium;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server.Combat;
using Aetherium.Server.Abilities;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Abilities
{
    /// <summary>
    /// Verifies "Composable Ability Effects" (openspec/changes/add-abilities/specs/abilities/spec.md).
    /// Confirms ability effects reuse the already-shipped deepen-combat-model pipeline rather than a
    /// parallel one.
    /// </summary>
    [TestFixture]
    public class AbilityEffectTests
    {
        private static World NewWorld()
        {
            var world = new World();
            var builder = new TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        [Test]
        public void DealDamageEffect_RoutesThrough_ExistingDamagePipeline()
        {
            var world = NewWorld();
            var caster = new Character();
            caster.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(caster);
            var target = new Character();
            target.Set(new WorldLocation(0, 0, 0));
            target.Set(new Health(30, 30));
            world.AddEntity(target);

            var effect = new DealDamageEffect(DamagePacket.Single("arcane", 12), new AlwaysHitResolver());
            effect.Apply(new AbilityEffectContext(world, caster, target));

            Assert.That(target.Get<Health>().Level, Is.EqualTo(18));
        }

        [Test]
        public void DealDamageEffect_NoTarget_IsNoOp()
        {
            var world = NewWorld();
            var caster = new Character();
            caster.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(caster);

            var effect = new DealDamageEffect(DamagePacket.Single("arcane", 12), new AlwaysHitResolver());
            Assert.DoesNotThrow(() => effect.Apply(new AbilityEffectContext(world, caster, target: null)));
        }

        [Test]
        public void ApplyStatusEffect_AddsStatusToTarget()
        {
            var world = NewWorld();
            var caster = new Character();
            caster.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(caster);
            var target = new Character();
            target.Set(new WorldLocation(0, 0, 0));
            target.Set(new StatusEffects());
            world.AddEntity(target);

            var effect = new ApplyStatusEffect(() => new SlowedEffect(durationTicks: 3, speedMultiplier: 0.5));
            effect.Apply(new AbilityEffectContext(world, caster, target));

            Assert.That(target.Get<StatusEffects>().Has("slowed"), Is.True);
        }

        [Test]
        public void ApplyStatusEffect_TargetWithoutStatusEffectsComponent_IsNoOp()
        {
            var world = NewWorld();
            var caster = new Character();
            caster.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(caster);
            var target = new Character(); // Deliberately no StatusEffects component.
            target.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(target);

            var effect = new ApplyStatusEffect(() => new ProneEffect(durationTicks: 1));
            Assert.DoesNotThrow(() => effect.Apply(new AbilityEffectContext(world, caster, target)));
        }

        [Test]
        public void ModifyResourceEffect_OnCaster_AdjustsCastersOwnPool()
        {
            var world = NewWorld();
            var caster = new Character();
            caster.Set(new WorldLocation(0, 0, 0));
            var pools = new ResourcePools();
            pools.Add(new ResourcePool("stamina", max: 100, current: 50));
            caster.Set(pools);
            world.AddEntity(caster);

            var effect = new ModifyResourceEffect("stamina", delta: -20, onCaster: true);
            effect.Apply(new AbilityEffectContext(world, caster, target: null));

            pools.TryGet("stamina", out var stamina);
            Assert.That(stamina!.Current, Is.EqualTo(30));
        }

        [Test]
        public void ModifyResourceEffect_OnTarget_AdjustsTargetsPool_NotCasters()
        {
            var world = NewWorld();
            var caster = new Character();
            caster.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(caster);
            var target = new Character();
            target.Set(new WorldLocation(0, 0, 0));
            var targetPools = new ResourcePools();
            targetPools.Add(new ResourcePool("mana", max: 100, current: 50));
            target.Set(targetPools);
            world.AddEntity(target);

            var effect = new ModifyResourceEffect("mana", delta: -30, onCaster: false);
            effect.Apply(new AbilityEffectContext(world, caster, target));

            targetPools.TryGet("mana", out var mana);
            Assert.That(mana!.Current, Is.EqualTo(20));
        }
    }
}
