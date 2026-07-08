using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Server.Combat;
using Aetherium.Server.Abilities;
using Aetherium.Model.Abilities;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Abilities
{
    /// <summary>
    /// Verifies "Per-World Ability Config" (openspec/changes/wire-abilities-live/specs/abilities/spec.md):
    /// the <see cref="AbilityCompiler"/> turns pure-data <see cref="AbilityDefinition"/>/
    /// <see cref="ResourcePoolDefinition"/> into the runtime tier the cast path consumes, binding the
    /// map's damage pipeline into the resulting effects.
    /// </summary>
    [TestFixture]
    public class AbilityCompilerTests
    {
        private static World NewWorld()
        {
            var world = new World();
            var builder = new TorusWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            return world;
        }

        private static AbilityCompiler NewCompiler()
            => new AbilityCompiler(new DamagePipeline(), new AlwaysHitResolver());

        [Test]
        public void CompileCatalog_DealDamageDescriptor_ProducesDamageDealingAbility()
        {
            var def = new AbilityDefinition
            {
                Id = "bolt",
                Range = 1,
                Effects = new List<AbilityEffectDescriptor>
                {
                    new() { Kind = AbilityEffectKind.DealDamage, DamageType = "arcane", Amount = 12 },
                },
            };

            var catalog = NewCompiler().CompileCatalog(new[] { def });
            Assert.That(catalog.TryGet("bolt", out var ability), Is.True);
            Assert.That(ability, Is.Not.Null);

            // Cast the compiled ability's effects at a target and confirm they route through the pipeline.
            var world = NewWorld();
            var caster = new Character(); caster.Set(new WorldLocation(0, 0, 0)); world.AddEntity(caster);
            var target = new Character(); target.Set(new WorldLocation(0, 0, 0)); target.Set(new Health(30, 30)); world.AddEntity(target);

            foreach (var effect in ability!.Effects)
                effect.Apply(new AbilityEffectContext(world, caster, target));

            Assert.That(target.Get<Health>().Level, Is.EqualTo(18), "Compiled DealDamage effect must reduce target health via the pipeline.");
        }

        [Test]
        public void CompileCatalog_ModifyResourceDescriptor_ProducesResourceModifyingAbility()
        {
            var def = new AbilityDefinition
            {
                Id = "focus",
                Effects = new List<AbilityEffectDescriptor>
                {
                    new() { Kind = AbilityEffectKind.ModifyResource, PoolTag = "energy", Delta = -15, ResourceTarget = AbilityEffectTarget.Caster },
                },
            };

            var catalog = NewCompiler().CompileCatalog(new[] { def });
            Assert.That(catalog.TryGet("focus", out var ability), Is.True);

            var world = NewWorld();
            var caster = new Character(); caster.Set(new WorldLocation(0, 0, 0));
            var pools = new ResourcePools();
            pools.Add(new ResourcePool("energy", max: 100, current: 40));
            caster.Set(pools);
            world.AddEntity(caster);

            foreach (var effect in ability!.Effects)
                effect.Apply(new AbilityEffectContext(world, caster, target: null));

            pools.TryGet("energy", out var energy);
            Assert.That(energy!.Current, Is.EqualTo(25), "Compiled ModifyResource effect must adjust the caster's own pool.");
        }

        [Test]
        public void CompileCatalog_ApplyStatusDescriptor_ProducesStatusApplyingAbility()
        {
            var def = new AbilityDefinition
            {
                Id = "immolate",
                Effects = new List<AbilityEffectDescriptor>
                {
                    new() { Kind = AbilityEffectKind.ApplyStatus, StatusId = "burning", DurationTicks = 3, Magnitude = 4 },
                },
            };

            var catalog = NewCompiler().CompileCatalog(new[] { def });
            Assert.That(catalog.TryGet("immolate", out var ability), Is.True);

            var world = NewWorld();
            var caster = new Character(); caster.Set(new WorldLocation(0, 0, 0)); world.AddEntity(caster);
            var target = new Character(); target.Set(new WorldLocation(0, 0, 0)); target.Set(new StatusEffects()); world.AddEntity(target);

            foreach (var effect in ability!.Effects)
                effect.Apply(new AbilityEffectContext(world, caster, target));

            Assert.That(target.Get<StatusEffects>().Has("burning"), Is.True, "Compiled ApplyStatus effect must add the named status.");
        }

        [Test]
        public void CompileCatalog_CarriesTimingAndCostFields_OntoTheRuntimeAbility()
        {
            var def = new AbilityDefinition
            {
                Id = "gated", ResourcePoolTag = "energy", ResourceCost = 30,
                ChargeTime = 1, CastTime = 2, RecoverTime = 1, Cooldown = 5, Range = 3, TargetShape = "beam",
            };

            NewCompiler().CompileCatalog(new[] { def }).TryGet("gated", out var ability);

            Assert.That(ability!.ResourcePoolTag, Is.EqualTo("energy"));
            Assert.That(ability.ResourceCost, Is.EqualTo(30));
            Assert.That(ability.Cooldown, Is.EqualTo(5));
            Assert.That(ability.Range, Is.EqualTo(3));
            Assert.That(ability.TargetShape, Is.EqualTo("beam"));
        }

        [Test]
        public void BuildResourcePools_ProducesWorkingPoolsFromDefinitions()
        {
            var defs = new List<ResourcePoolDefinition>
            {
                new() { Tag = "energy", Max = 100, RegenPerTick = 5, RegenPolicy = ResourceRegenPolicyKind.Continuous, StartingValue = 40 },
                new() { Tag = "heat", Max = 100, RegenPerTick = 10, RegenPolicy = ResourceRegenPolicyKind.Continuous, IsInverse = true, OverheatThreshold = 80 },
            };

            var pools = NewCompiler().BuildResourcePools(defs);

            Assert.That(pools.TryGet("energy", out var energy), Is.True);
            Assert.That(energy!.Current, Is.EqualTo(40));
            Assert.That(energy.Max, Is.EqualTo(100));

            Assert.That(pools.TryGet("heat", out var heat), Is.True);
            Assert.That(heat!.IsInverse, Is.True);
            Assert.That(heat.Current, Is.EqualTo(0), "Inverse pool with no StartingValue begins empty.");
        }

        [Test]
        public void BuildResourcePools_ReturnsFreshInstancesPerCall_NoSharedMutableState()
        {
            var defs = new List<ResourcePoolDefinition>
            {
                new() { Tag = "energy", Max = 100, StartingValue = 50 },
            };
            var compiler = NewCompiler();

            var poolsA = compiler.BuildResourcePools(defs);
            var poolsB = compiler.BuildResourcePools(defs);

            poolsA.TryGet("energy", out var a);
            a!.TrySpend(20); // drain A only

            poolsB.TryGet("energy", out var b);
            Assert.That(b!.Current, Is.EqualTo(50), "Each character must get its own pool instances, not a shared one.");
        }

        [Test]
        public void CompileCatalog_NullDefinitions_ProducesEmptyCatalog()
        {
            var catalog = NewCompiler().CompileCatalog(null);
            Assert.That(catalog.TryGet("anything", out _), Is.False);
        }
    }
}
