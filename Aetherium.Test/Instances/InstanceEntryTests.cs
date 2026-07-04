using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Orleans.TestingHost;
using Orleans.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Aetherium.Server.Instances;
using Aetherium.Server.Groups;
using Aetherium.Server.MultiWorld;
using Aetherium.Model.Instances;
using Aetherium.Model.Groups;
using Aetherium.Model.Worlds;

namespace Aetherium.Test.Instances
{
    /// <summary>
    /// End-to-end coverage of the P3-4 entry path the hub/tool/CLI now expose: form a party, enter a
    /// dungeon (allocate), re-enter (reuse the same instance), then leave and sweep. This is the
    /// cross-grain flow the audit flagged as entirely untested.
    /// </summary>
    [TestFixture]
    public class InstanceEntryTests
    {
        private TestCluster _cluster = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                siloBuilder.AddMemoryGrainStorage("worldStore");
                siloBuilder.AddMemoryGrainStorage("mapStore");

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

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            _cluster = builder.Build();
            _cluster.Deploy();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => _cluster.StopAllSilos();

        private async Task<string> CreateActiveWorldAsync()
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var world = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            await world.InitializeAsync(new WorldConfig
            {
                WorldId = worldId,
                Name = "Entry Test World",
                GeneratorType = "maze",
                Size = new WorldSize { Width = 30, Height = 30, Depth = 1 }
            });
            return worldId;
        }

        [Test]
        public async Task Party_Enters_Dungeon_And_ReEntry_Reuses_Same_Instance()
        {
            var worldId = await CreateActiveWorldAsync();

            // Form a party of two.
            var partyId = $"party-{Guid.NewGuid()}";
            var party = _cluster.GrainFactory.GetGrain<IPartyGrain>(partyId);
            await party.CreateAsync(new PlayerId("leader"), "Leader");
            Assert.That(await party.AddMemberAsync(new PlayerId("member"), "Member"), Is.True);
            var members = await party.GetMemberIdsAsync();

            var allocator = _cluster.GrainFactory.GetGrain<IInstanceAllocatorGrain>(worldId);
            EnterInstanceRequest Request() => new()
            {
                WorldId = new WorldId(worldId),
                DungeonId = new DungeonId("dungeon-1"),
                PartyId = new PartyId(partyId),
                PlayerIds = members
            };

            // First entry allocates a fresh instance with the party's players.
            var first = await allocator.EnterAsync(Request());
            Assert.That(first.Success, Is.True, first.ErrorMessage);
            Assert.That(first.InstanceId.HasValue, Is.True);

            var instanceGrain = _cluster.GrainFactory.GetGrain<IDungeonInstanceGrain>(first.InstanceId!.Value.Value);
            var info = await instanceGrain.GetInfoAsync();
            Assert.That(info!.PlayerCount, Is.EqualTo(2), "Both party members should be in the instance.");
            var mapId = await instanceGrain.GetMapIdAsync();
            Assert.That(await _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId).GetMapIdsAsync(), Does.Contain(mapId));

            // Re-entry reuses the same instance rather than allocating a new one.
            var second = await allocator.EnterAsync(Request());
            Assert.That(second.Success, Is.True, second.ErrorMessage);
            Assert.That(second.InstanceId!.Value.Value, Is.EqualTo(first.InstanceId!.Value.Value),
                "Re-entering the same dungeon with the same party must reuse the instance.");
        }

        [Test]
        public async Task Solo_Enter_Then_Leave_And_Sweep_Frees_Instance()
        {
            var worldId = await CreateActiveWorldAsync();
            var allocator = _cluster.GrainFactory.GetGrain<IInstanceAllocatorGrain>(worldId);

            var result = await allocator.EnterAsync(new EnterInstanceRequest
            {
                WorldId = new WorldId(worldId),
                DungeonId = new DungeonId("dungeon-solo"),
                PartyId = null,
                PlayerIds = new List<PlayerId> { new PlayerId("solo") }
            });
            Assert.That(result.Success, Is.True, result.ErrorMessage);

            var instanceGrain = _cluster.GrainFactory.GetGrain<IDungeonInstanceGrain>(result.InstanceId!.Value.Value);
            var mapId = await instanceGrain.GetMapIdAsync();

            // Leaving empties the instance → Abandoned; the sweeper then reaps it and frees its map.
            await instanceGrain.RemovePlayerAsync(new PlayerId("solo"));
            var reaped = await allocator.SweepAbandonedInstancesAsync();

            Assert.That(reaped, Is.EqualTo(1));
            Assert.That(await _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId).GetMapIdsAsync(),
                Does.Not.Contain(mapId), "Swept instance's map should be freed.");
        }

        [Test]
        public async Task Party_Leader_Leaving_Reassigns_Leadership()
        {
            var partyId = $"party-{Guid.NewGuid()}";
            var party = _cluster.GrainFactory.GetGrain<IPartyGrain>(partyId);
            await party.CreateAsync(new PlayerId("leader"), "Leader");
            await party.AddMemberAsync(new PlayerId("member"), "Member");

            await party.RemoveMemberAsync(new PlayerId("leader"));

            var info = await party.GetInfoAsync();
            Assert.That(info!.Members, Has.Count.EqualTo(1));
            Assert.That(info.Members[0].PlayerId.Value, Is.EqualTo("member"));
            Assert.That(info.Members[0].Role, Is.EqualTo(PartyRole.Leader),
                "The remaining member should be promoted to leader.");
        }
    }
}
