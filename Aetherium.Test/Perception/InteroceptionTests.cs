using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.TestingHost;
using global::Orleans.Configuration;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Model;
using Aetherium.Server;
using Aetherium.Server.Abilities;
using Aetherium.Server.Combat;
using Aetherium.Server.MultiWorld;
using World = Aetherium.Core.World;
using TileType = Aetherium.Core.TileType;
using TerrainType = Aetherium.Core.TerrainType;
using WorldLocation = Aetherium.Components.WorldLocation;

namespace Aetherium.Test.Perception
{
    /// <summary>
    /// Verifies the interoception channel (openspec/changes/add-interoception-channel): the
    /// perceiver's own body state — health, felt statuses, resource pools, ability readiness —
    /// as an optional, additive, self-only block on the perception frame. Each test is named on
    /// a `Verified by:` line of specs/perception/spec.md's three ADDED requirements.
    /// </summary>
    [TestFixture]
    public class InteroceptionTests
    {
        private World _world = null!;
        private WorldLocation _playerLocation = null!;
        private PerceptionService _perception = null!;

        [SetUp]
        public void SetUp()
        {
            _world = new World();
            _playerLocation = new WorldLocation(0, 0, 0);

            var tileTypes = new List<TileType>
            {
                new TileType { Name = "Plains", Settings = new Dictionary<string, string>() },
            };
            _world.AddTileTypes(tileTypes);
            _world.AddTerrainTypes(new List<TerrainType>
            {
                new TerrainType { Name = "Plains", TileType = tileTypes[0] },
            });
            _world.SetTerrain("Plains", _playerLocation);

            _perception = new PerceptionService();
        }

        private PerceptionDto Compute(Entity? self) => _perception.ComputePerception(
            _world, _playerLocation, Aetherium.WorldDirection.North, new System.Drawing.Size(20, 20), self);

        private Character MakeSelf()
        {
            var character = new Character();
            character.Set(_playerLocation);
            _world.AddEntity(character);
            return character;
        }

        // ---- Requirement: Interoception Data Model ----

        [Test]
        public void InteroceptionDto_SerializesAndRoundTrips_PascalCaseJson()
        {
            var original = new InteroceptionDto
            {
                Health = 12,
                MaxHealth = 40,
                Statuses = { new SelfStatusDto { Id = "burning", RemainingTicks = 3 } },
                Pools = { new ResourcePoolStateDto { Tag = "heat", Current = 10, Max = 50, IsInverse = true } },
                Cooldowns = { new AbilityReadinessDto { AbilityId = "breach", RemainingTicks = 2 } },
            };

            // Same serializer + defaults the grain's perception path uses (PascalCase property names).
            var json = System.Text.Json.JsonSerializer.Serialize(original);
            Assert.That(json, Does.Contain("\"Health\":12").And.Contain("\"IsInverse\":true"),
                "the wire carries PascalCase property names");

            var roundTripped = System.Text.Json.JsonSerializer.Deserialize<InteroceptionDto>(json)!;
            Assert.That(roundTripped.Health, Is.EqualTo(12));
            Assert.That(roundTripped.MaxHealth, Is.EqualTo(40));
            Assert.That(roundTripped.Statuses.Single().Id, Is.EqualTo("burning"));
            Assert.That(roundTripped.Statuses.Single().RemainingTicks, Is.EqualTo(3));
            Assert.That(roundTripped.Pools.Single().Tag, Is.EqualTo("heat"));
            Assert.That(roundTripped.Pools.Single().Current, Is.EqualTo(10));
            Assert.That(roundTripped.Pools.Single().Max, Is.EqualTo(50));
            Assert.That(roundTripped.Pools.Single().IsInverse, Is.True);
            Assert.That(roundTripped.Cooldowns.Single().AbilityId, Is.EqualTo("breach"));
            Assert.That(roundTripped.Cooldowns.Single().RemainingTicks, Is.EqualTo(2));
        }

        [Test]
        public void PerceptionDto_Interoception_DefaultsToNull()
        {
            Assert.That(new PerceptionDto().Interoception, Is.Null,
                "a frame without a self-sense is byte-identical to a pre-change frame");
        }

        // ---- Requirement: Interoception Channel in Perception ----

        [Test]
        public void Interoception_Health_ReflectsSelfLevelAndMax()
        {
            var self = MakeSelf();
            self.Set(new Health(12, 40));

            var frame = Compute(self);

            Assert.That(frame.Interoception, Is.Not.Null);
            Assert.That(frame.Interoception!.Health, Is.EqualTo(12));
            Assert.That(frame.Interoception.MaxHealth, Is.EqualTo(40));
        }

        [Test]
        public void Interoception_Statuses_ListSelfActiveStatuses_WithRemainingTicks()
        {
            var self = MakeSelf();
            var statuses = new StatusEffects();
            statuses.Apply(new BurningEffect(durationTicks: 3, damagePerTick: 1.0));
            statuses.Apply(new SlowedEffect(durationTicks: 5, speedMultiplier: 0.5));
            self.Set(statuses);

            var frame = Compute(self);

            var felt = frame.Interoception!.Statuses;
            Assert.That(felt.Select(s => s.Id), Is.EquivalentTo(new[] { "burning", "slowed" }));
            Assert.That(felt.Single(s => s.Id == "burning").RemainingTicks, Is.EqualTo(3));
            Assert.That(felt.Single(s => s.Id == "slowed").RemainingTicks, Is.EqualTo(5));
        }

        [Test]
        public void Interoception_Pools_CarryTagCurrentMaxAndInverseFlag()
        {
            var self = MakeSelf();
            var pools = new ResourcePools();
            pools.Add(new ResourcePool("charge", max: 100, current: 40));
            pools.Add(new ResourcePool("heat", max: 50, isInverse: true, overheatThreshold: 45));
            self.Set(pools);

            var frame = Compute(self);

            var felt = frame.Interoception!.Pools;
            Assert.That(felt.Select(p => p.Tag), Is.EquivalentTo(new[] { "charge", "heat" }));

            var charge = felt.Single(p => p.Tag == "charge");
            Assert.That(charge.Current, Is.EqualTo(40));
            Assert.That(charge.Max, Is.EqualTo(100));
            Assert.That(charge.IsInverse, Is.False, "a normal pool renders as a draining battery");

            var heat = felt.Single(p => p.Tag == "heat");
            Assert.That(heat.Current, Is.EqualTo(0), "an inverse pool starts empty and fills with use");
            Assert.That(heat.Max, Is.EqualTo(50));
            Assert.That(heat.IsInverse, Is.True, "the heat gauge renders as filling, not draining");
        }

        [Test]
        public void Interoception_Cooldowns_ListOnlyAbilitiesStillOnCooldown_WithRemainingTicks()
        {
            var self = MakeSelf();
            var cooldowns = new AbilityCooldowns();
            cooldowns.SetCooldown("breach", 2);
            // "jab" was never put on cooldown — it is ready, so it must be absent.
            self.Set(cooldowns);

            var frame = Compute(self);

            var unready = frame.Interoception!.Cooldowns;
            Assert.That(unready.Select(c => c.AbilityId), Is.EqualTo(new[] { "breach" }),
                "a ready ability is simply absent from the cooldown read");
            Assert.That(unready.Single().RemainingTicks, Is.EqualTo(2));
        }

        // ---- Requirement: Interoception Is Self-Only and Fail-Safe ----

        [Test]
        public void Interoception_SelfOnly_DoesNotReflectAnotherEntitysState()
        {
            var self = MakeSelf();
            self.Set(new Health(30, 30));

            // A badly wounded, burning bystander in the same frame.
            var other = new Character();
            other.Set(new WorldLocation(1, 0, 0));
            other.Set(new Health(1, 50));
            var otherStatuses = new StatusEffects();
            otherStatuses.Apply(new BurningEffect(durationTicks: 9, damagePerTick: 2.0));
            other.Set(otherStatuses);
            _world.AddEntity(other);

            var frame = Compute(self);

            Assert.That(frame.Interoception!.Health, Is.EqualTo(30), "my self-sense is my health, not theirs");
            Assert.That(frame.Interoception.MaxHealth, Is.EqualTo(30));
            Assert.That(frame.Interoception.Statuses, Is.Empty, "their burning is not my burning");
        }

        [Test]
        public void Interoception_NullWhenNoSelfProvided_LegacyCallersUnaffected()
        {
            // The perceiver exists in the world, but the caller never identifies it — the
            // pre-change calling convention.
            MakeSelf().Set(new Health(12, 40));

            var frame = Compute(self: null);

            Assert.That(frame.Interoception, Is.Null);
            Assert.That(frame.Visuals, Is.Not.Empty, "the rest of the frame is computed as before");
        }

        [Test]
        public void Interoception_MissingComponents_DegradeToEmpty_WithoutThrowing()
        {
            // A bare character: no StatusEffects, no ResourcePools, no AbilityCooldowns.
            // (Health can never be absent — the Entity base ctor gives every body 100/100 —
            // so the guarded read is exercised on the three genuinely optional components.)
            var self = MakeSelf();

            PerceptionDto frame = null!;
            Assert.DoesNotThrow(() => frame = Compute(self));

            Assert.That(frame.Interoception, Is.Not.Null, "a supplied self always yields a block");
            Assert.That(frame.Interoception!.Health, Is.EqualTo(100), "the Entity default body");
            Assert.That(frame.Interoception.MaxHealth, Is.EqualTo(100));
            Assert.That(frame.Interoception.Statuses, Is.Empty);
            Assert.That(frame.Interoception.Pools, Is.Empty);
            Assert.That(frame.Interoception.Cooldowns, Is.Empty);
        }

        // ---- End-to-end through the grain ----

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                siloBuilder.AddMemoryGrainStorage("worldStore");
                siloBuilder.AddMemoryGrainStorage("mapStore");
                siloBuilder.AddMemoryGrainStorage("management");

                siloBuilder.Configure<SiloMessagingOptions>(opts => opts.ResponseTimeout = TimeSpan.FromMinutes(3));

                siloBuilder.ConfigureServices(services =>
                {
                    services.Configure<Aetherium.Server.Simulation.SimulationOptions>(opts =>
                    {
                        opts.RegionSize = 128;
                        opts.EnableWeather = false;
                        opts.EnableSeasons = false;
                        opts.EnableAgentChanges = false;
                        opts.EnableProceduralEvents = false;
                    });

                    services.AddSingleton<Aetherium.Server.Persistence.IWorldSnapshotStore,
                        Aetherium.Test.TestStubs.InMemoryWorldSnapshotStore>();

                    services.AddSingleton<Aetherium.WorldGen.MapGeneratorRegistry>(sp =>
                    {
                        var registry = new Aetherium.WorldGen.MapGeneratorRegistry();
                        registry.DiscoverTypes(typeof(Aetherium.WorldGen.IMapGenerator).Assembly);
                        return registry;
                    });
                });
            }
        }

        [Test]
        public async Task ComputeAgentPerceptionAsync_IncludesInteroceptionForThePlayer()
        {
            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            var cluster = builder.Build();
            cluster.Deploy();
            try
            {
                var worldId = $"world-{Guid.NewGuid()}";
                var map = cluster.GrainFactory.GetGrain<IGameMapGrain>($"{worldId}-map-1");

                // A per-world resource pool so the live frame carries a non-trivial pools read.
                var abilities = new Aetherium.Model.Abilities.AbilityConfig
                {
                    CharacterResourcePools = new List<Aetherium.Model.Abilities.ResourcePoolDefinition>
                    {
                        new() { Tag = "energy", Max = 100, RegenPerTick = 5, StartingValue = 40 },
                    },
                };
                await map.InitializeAsync(worldId, "floor-1",
                    new WorldSize { Width = 40, Height = 40, Depth = 1 },
                    "maze", new Dictionary<string, object>(), null, abilities);

                var player = $"player-{Guid.NewGuid()}";
                var join = await map.JoinPlayerAsync(player);
                Assert.That(join.Success, Is.True);

                var json = await map.ComputeAgentPerceptionAsync(player);
                Assert.That(json, Is.Not.Null);

                var frame = System.Text.Json.JsonSerializer.Deserialize<PerceptionDto>(json!)!;
                Assert.That(frame.Interoception, Is.Not.Null,
                    "a live player frame carries the player's own interoception");
                Assert.That(frame.Interoception!.MaxHealth, Is.GreaterThan(0), "the player's own body has health");
                Assert.That(frame.Interoception.Health, Is.GreaterThan(0).And.LessThanOrEqualTo(frame.Interoception.MaxHealth));
                Assert.That(frame.Interoception.Pools.Select(p => p.Tag), Does.Contain("energy"),
                    "the per-world pool is felt through interoception");
            }
            finally
            {
                cluster.StopAllSilos();
            }
        }
    }
}
