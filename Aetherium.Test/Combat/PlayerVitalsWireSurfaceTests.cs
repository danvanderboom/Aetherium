using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.TestingHost;
using global::Orleans.Configuration;
using Aetherium.Model;
using Aetherium.Model.Combat;
using Aetherium.Server;
using Aetherium.Server.MultiWorld;
using Aetherium.WorldBuilders;

namespace Aetherium.Test.Combat
{
    /// <summary>
    /// Verifies the player-death/respawn client surface actually reaches the wire (engine
    /// gap-analysis §4.11, Phase 2 — see openspec/changes/wire-death-respawn-live Slice B), using
    /// the same real-<see cref="GameSessionManager"/>-plus-capturing-<see cref="IHubContext{THub}"/>
    /// pattern as <c>EndToEndSharedMutationTests</c>. Verifies "Player Vitals Wire Surface" in
    /// specs/death-respawn-policy/spec.md.
    /// </summary>
    [TestFixture]
    public class PlayerVitalsWireSurfaceTests
    {
        private TestCluster _cluster = null!;
        private static Aetherium.Test.MultiWorld.CapturingHubContext _hubContext = null!;
        private static GameSessionManager _sessionManager = null!;

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

                    services.AddSingleton<IHubContext<GameHub>>(_hubContext);
                    services.AddSingleton<GameSessionManager>(sp => _sessionManager);
                });
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _hubContext = new Aetherium.Test.MultiWorld.CapturingHubContext();
            _sessionManager = new GameSessionManager(perceptionService: null, hubContext: _hubContext);

            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            _cluster = builder.Build();
            _cluster.Deploy();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => _cluster.StopAllSilos();

        [SetUp]
        public void SetUp() => _hubContext.Reset();

        private async Task<(IGameMapGrain map, string player)> InitMapWithSessionAndAdjacentMonsterAsync(DeathPolicy deathPolicy)
        {
            var worldId = $"world-{Guid.NewGuid()}";
            var mapId = $"{worldId}-map-1";

            var map = _cluster.GrainFactory.GetGrain<IGameMapGrain>(mapId);
            await map.InitializeAsync(worldId, "floor-1",
                new WorldSize { Width = 40, Height = 40, Depth = 1 },
                "maze", new Dictionary<string, object>(), deathPolicy);

            var player = $"player-{Guid.NewGuid()}";
            var join = await map.JoinPlayerAsync(player);
            Assert.That(join.Success, Is.True);
            var spawn = join.SpawnLocation();

            string? monsterId = null;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var spawnResult = await map.SpawnEntityAsync(new SpawnEntityRequest
                {
                    CreatureType = "monster",
                    X = spawn.X + dx, Y = spawn.Y + dy, Z = spawn.Z
                });
                if (spawnResult.Success) { monsterId = spawnResult.EntityId; break; }
            }
            Assert.That(monsterId, Is.Not.Null, "Expected at least one passable neighbour to place the monster.");

            // Register a live session for the player so GameSessionManager.NotifyPlayerEventAsync
            // has somewhere to dispatch to — mirrors EndToEndSharedMutationTests' reflection-based
            // session injection (CreateSession(...) builds a fresh GameSession; here we need the
            // SessionId/ConnectionId/MapId already set to match this specific player/map).
            var session = new GameSession($"conn-{player}", new FovDiagnosticWorldBuilder("open_space"))
            {
                SessionId = player,
                ConnectionId = $"conn-{player}",
                MapId = mapId,
            };
            var sessionsField = typeof(GameSessionManager).GetField("sessions",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var sessionsDict = (System.Collections.Concurrent.ConcurrentDictionary<string, GameSession>)
                sessionsField.GetValue(_sessionManager)!;
            sessionsDict[session.ConnectionId] = session;

            return (map, player);
        }

        [Test]
        public async Task DownedTransition_DispatchesReceiveDowned_WithMatchingVitals_ToOnlyThatPlayersConnection()
        {
            var (map, player) = await InitMapWithSessionAndAdjacentMonsterAsync(DeathPolicy.Default); // DownStateEnabled=true

            for (int i = 0; i < 20; i++)
            {
                await map.TickAsync(TimeSpan.FromSeconds(1));
                if (_hubContext.GetDispatches().Any(d => d.Method == "ReceiveDowned"))
                    break;
            }

            var downedDispatches = _hubContext.GetDispatches().Where(d => d.Method == "ReceiveDowned").ToList();
            Assert.That(downedDispatches, Is.Not.Empty, "Expected a ReceiveDowned dispatch once the player was downed.");

            var dispatch = downedDispatches[0];
            Assert.That(dispatch.ConnectionId, Is.EqualTo($"conn-{player}"), "The signal must target only the affected player's connection.");

            var vitals = dispatch.Args.OfType<PlayerVitalsDto>().FirstOrDefault();
            Assert.That(vitals, Is.Not.Null, "ReceiveDowned's payload must be a PlayerVitalsDto.");
            Assert.That(vitals!.IsDowned, Is.True);
            Assert.That(vitals.Health, Is.EqualTo(0));
            Assert.That(vitals.DownedTicksRemaining, Is.GreaterThan(0));
        }

        [Test]
        public async Task RespawnTransition_DispatchesReceiveRespawn_WithFullHealthVitals()
        {
            var policy = new DeathPolicy { Permadeath = false, DownStateEnabled = false, RespawnInvulnerabilityTicks = 0 };
            var (map, player) = await InitMapWithSessionAndAdjacentMonsterAsync(policy);

            for (int i = 0; i < 20; i++)
            {
                await map.TickAsync(TimeSpan.FromSeconds(1));
                if (_hubContext.GetDispatches().Any(d => d.Method == "ReceiveRespawn"))
                    break;
            }

            var respawnDispatches = _hubContext.GetDispatches().Where(d => d.Method == "ReceiveRespawn").ToList();
            Assert.That(respawnDispatches, Is.Not.Empty, "Expected a ReceiveRespawn dispatch once the instant-respawn policy resolved.");

            var vitals = respawnDispatches[0].Args.OfType<PlayerVitalsDto>().FirstOrDefault();
            Assert.That(vitals, Is.Not.Null);
            Assert.That(vitals!.IsDowned, Is.False);
            Assert.That(vitals.Health, Is.EqualTo(vitals.MaxHealth));
        }
    }
}
