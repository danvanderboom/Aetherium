using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Hosting;
using Orleans.Serialization;
using Orleans.TestingHost;
using global::Orleans.Configuration;
using Aetherium.Components;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Persistence;
using Aetherium.Test.TestStubs;

namespace Aetherium.Test.Persistence
{
    /// <summary>
    /// Verifies the Phase D recovery path: <c>ForceSnapshotAsync</c> captures world state,
    /// the persisted snapshot is self-contained, and a fresh world can be reconstructed from
    /// the snapshot blob via <c>SnapshotWorldBuilder</c> — the same path
    /// <c>GameMapGrain.OnActivateAsync</c> takes after a silo restart.
    /// </summary>
    [TestFixture]
    public class MapGrainRecoveryTests
    {
        private static RecordingWorldSnapshotStore _store = null!;
        private TestCluster _cluster = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                siloBuilder.AddMemoryGrainStorage("worldStore");
                siloBuilder.AddMemoryGrainStorage("mapStore");
                siloBuilder.AddMemoryGrainStorage("management");

                siloBuilder.Configure<SiloMessagingOptions>(opts =>
                {
                    opts.ResponseTimeout = TimeSpan.FromMinutes(3);
                });

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

                    services.AddSingleton<IWorldSnapshotStore>(_store);

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
            _store = new RecordingWorldSnapshotStore();
            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            _cluster = builder.Build();
            _cluster.Deploy();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => _cluster.StopAllSilos();

        [SetUp]
        public void Reset() => _store.Reset();

        private async Task<(IGameMapGrain map, string worldId, string mapId, string playerA)> InitMapAsync(string seed)
        {
            var worldId = $"world-recovery{seed}-{Guid.NewGuid()}";
            var mapId = $"{worldId}-map-1";
            var worldGrain = _cluster.GrainFactory.GetGrain<IWorldGrain>(worldId);
            await worldGrain.InitializeAsync(new WorldConfig
            {
                WorldId = worldId,
                Name = "Recovery Test World",
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 }
            });

            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            await map.InitializeAsync(worldId, "floor-1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 },
                "maze",
                new Dictionary<string, object>());

            var playerA = $"player-A-{Guid.NewGuid()}";
            var join = await map.JoinPlayerAsync(playerA);
            Assert.That(join.Success, Is.True);
            return (map, worldId, mapId, playerA);
        }

        [Test]
        public async Task ForceSnapshotAsync_Persists_Snapshot_With_Captured_Sequence()
        {
            var (map, worldId, mapId, playerA) = await InitMapAsync("-capture");

            // Drive a couple of mutations so the sequence has advanced.
            await map.RotateAsync(playerA, 90);
            await map.MoveAsync(playerA, Aetherium.Model.RelativeDirection.Forward, 1);

            var capturedSeq = await map.ForceSnapshotAsync();

            Assert.That(capturedSeq, Is.GreaterThan(0), "Snapshot should cover at least one emitted delta");

            var snap = await _store.LoadSnapshotAsync(worldId, mapId);
            Assert.That(snap, Is.Not.Null);
            Assert.That(snap!.LastSequence, Is.EqualTo(capturedSeq));
            Assert.That(snap.SerializedEntities, Is.Not.Null.And.Not.Empty);
            Assert.That(snap.MapId, Is.EqualTo(mapId));
        }

        [Test]
        public async Task ForceSnapshotAsync_Compacts_DeltaLog_Through_Captured_Sequence()
        {
            var (map, worldId, mapId, playerA) = await InitMapAsync("-compact");

            await map.RotateAsync(playerA, 45);
            await map.RotateAsync(playerA, 135);

            var beforeCount = (await _store.GetMapDeltasSinceSequenceAsync(worldId, mapId, 0)).Length;
            Assert.That(beforeCount, Is.GreaterThan(0));

            var capturedSeq = await map.ForceSnapshotAsync();

            var afterEqual = await _store.GetMapDeltasSinceSequenceAsync(worldId, mapId, 0);
            // All deltas at or before the captured sequence should be gone.
            Assert.That(afterEqual.All(d => d.Sequence > capturedSeq), Is.True,
                $"After snapshot+compact, no delta should remain with seq<={capturedSeq}");
        }

        [Test]
        public async Task Snapshot_Blob_Reconstructs_World_Via_SnapshotWorldBuilder()
        {
            // Sets the world up, captures, then simulates a cold-start by feeding the
            // persisted blob into the same SnapshotWorldBuilder that OnActivateAsync uses.
            var (map, worldId, mapId, _) = await InitMapAsync("-reconstruct");
            await map.ForceSnapshotAsync();

            var snap = await _store.LoadSnapshotAsync(worldId, mapId);
            Assert.That(snap, Is.Not.Null);
            Assert.That(snap!.SerializedEntities, Is.Not.Null);

            var serializer = _cluster.ServiceProvider.GetRequiredService<Serializer>();

            // The cluster client-side service provider doesn't carry MapGeneratorRegistry
            // (it's a silo-side service). Construct a fresh equivalent — the registry is
            // stateless and discovers types by reflection.
            var generators = new Aetherium.WorldGen.MapGeneratorRegistry();
            generators.DiscoverTypes(typeof(Aetherium.WorldGen.IMapGenerator).Assembly);

            var worldSnapshot = serializer.Deserialize<WorldSnapshot>(snap.SerializedEntities!);
            Assert.That(worldSnapshot.Recipe, Is.Not.Null, "Self-contained snapshot must carry the WorldRecipe");
            Assert.That(worldSnapshot.Entities, Is.Not.Empty, "Snapshot should carry the grain's entities");

            var builder = new Aetherium.WorldBuilders.SnapshotWorldBuilder(worldSnapshot, generators);
            var reconstructed = builder.Build();

            // The reconstructed world should contain the entity ids from the snapshot
            // (Characters, items, doors, etc. — same set the original grain world held).
            var reconstructedIds = reconstructed.Entities.Values
                .Where(e => e is not Aetherium.Entities.Terrain)
                .Select(e => e.EntityId)
                .ToHashSet();
            var snapshotIds = worldSnapshot.Entities.Select(p => p.EntityId).ToHashSet();
            Assert.That(reconstructedIds, Is.SupersetOf(snapshotIds),
                "Every entity in the snapshot must appear in the reconstructed world");
        }

        [Test]
        public async Task Snapshot_Captures_Mid_Game_Mutations_Like_Heading_Changes()
        {
            var (map, worldId, mapId, playerA) = await InitMapAsync("-heading-mutation");
            await map.RotateAsync(playerA, 270);
            await map.ForceSnapshotAsync();

            var snap = await _store.LoadSnapshotAsync(worldId, mapId);
            var serializer = _cluster.ServiceProvider.GetRequiredService<Serializer>();
            var worldSnapshot = serializer.Deserialize<WorldSnapshot>(snap!.SerializedEntities!);

            // The player's Character placement in the snapshot should carry HasHeading=270.
            var playerPlacement = worldSnapshot.Entities.FirstOrDefault(p => p.EntityId == playerA);
            Assert.That(playerPlacement, Is.Not.Null, "Joined player Character should appear in the snapshot");
            Assert.That(playerPlacement!.Properties.TryGetValue("Heading", out var headingStr), Is.True,
                $"Snapshot placement should serialize HasHeading.Heading. Got: {string.Join(",", playerPlacement.Properties.Keys)}");
            Assert.That(headingStr, Is.EqualTo("270"));
        }
    }
}
