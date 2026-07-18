using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Orleans.Hosting;
using Orleans.Configuration;
using Orleans;
using Aetherium.Model;
using Aetherium.Server.Management;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Test
{
    /// <summary>
    /// Tests for the "aetherctl headless driving" change. Each test maps to an OpenSpec
    /// requirement under changes/add-aetherctl-headless-driving/specs/game-management-grain.
    /// </summary>
    [TestFixture]
    public class HeadlessDrivingTests
    {
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

                    services.AddSingleton<Aetherium.Server.Persistence.IWorldSnapshotStore, Aetherium.Test.TestStubs.InMemoryWorldSnapshotStore>();

                    services.AddSingleton<Aetherium.WorldGen.MapGeneratorRegistry>(sp =>
                    {
                        var registry = new Aetherium.WorldGen.MapGeneratorRegistry();
                        registry.DiscoverTypes(typeof(Aetherium.WorldGen.IMapGenerator).Assembly);
                        return registry;
                    });

                    services.AddSingleton<Aetherium.Server.GameSessionManager>();

                    // Tool registry (reflection-discovered) so ExecuteToolAsync / batch can run tools.
                    services.AddSingleton<Aetherium.Server.Agents.Tools.AgentToolRegistry>(sp =>
                    {
                        var registry = new Aetherium.Server.Agents.Tools.AgentToolRegistry(sp);
                        registry.DiscoverTools(typeof(Aetherium.Server.Agents.Tools.AgentToolRegistry).Assembly);
                        return registry;
                    });

                    // The in-process bridge under test: GameMapGrain publishes worlds here,
                    // GameManagementGrain reads/drives them for headless sessions + snapshots.
                    services.AddSingleton<Aetherium.Server.Services.WorldRegistry>();

                    services.AddSingleton<Microsoft.AspNetCore.SignalR.IHubContext<Aetherium.Server.GameHub>>(sp => new NullHubContext());
                });
            }

            private sealed class NullHubContext : Microsoft.AspNetCore.SignalR.IHubContext<Aetherium.Server.GameHub>
            {
                public Microsoft.AspNetCore.SignalR.IHubClients Clients { get; } = new NullHubClients();
                public Microsoft.AspNetCore.SignalR.IGroupManager Groups { get; } = new NullGroupManager();

                private sealed class NullHubClients : Microsoft.AspNetCore.SignalR.IHubClients
                {
                    public Microsoft.AspNetCore.SignalR.IClientProxy All => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy AllExcept(System.Collections.Generic.IReadOnlyList<string> excludedConnectionIds) => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy Client(string connectionId) => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy Clients(System.Collections.Generic.IReadOnlyList<string> connectionIds) => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy Group(string groupName) => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy GroupExcept(string groupName, System.Collections.Generic.IReadOnlyList<string> excludedConnectionIds) => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy Groups(System.Collections.Generic.IReadOnlyList<string> groupNames) => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy User(string userId) => new NullClientProxy();
                    public Microsoft.AspNetCore.SignalR.IClientProxy Users(System.Collections.Generic.IReadOnlyList<string> userIds) => new NullClientProxy();
                }

                private sealed class NullGroupManager : Microsoft.AspNetCore.SignalR.IGroupManager
                {
                    public System.Threading.Tasks.Task AddToGroupAsync(string connectionId, string groupName, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.CompletedTask;
                    public System.Threading.Tasks.Task RemoveFromGroupAsync(string connectionId, string groupName, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.CompletedTask;
                }

                private sealed class NullClientProxy : Microsoft.AspNetCore.SignalR.IClientProxy
                {
                    public System.Threading.Tasks.Task SendCoreAsync(string method, object?[] args, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.CompletedTask;
                }
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
        public void OneTimeTearDown()
        {
            _cluster.StopAllSilos();
        }

        private IGameManagementGrain Mgmt() => _cluster.GrainFactory.GetGrain<IGameManagementGrain>("GLOBAL");

        private async Task<string> CreateWorldAsync(string name = "Headless Test World")
        {
            var request = new CreateWorldRequest
            {
                Name = name,
                Description = "world for headless-driving tests",
                GeneratorType = "maze",
                MaxPlayers = 10,
                Size = new WorldSize { Width = 40, Height = 40, Depth = 1 }
            };
            return await Mgmt().CreateWorldAsync(request);
        }

        // Spec: game-management-grain / Headless Session Provisioning
        //       Scenario "Create headless session in an existing world"
        [Test]
        public async Task CreateHeadlessSession_InExistingWorld_PlacesCharacterAndRegistersSession()
        {
            var mgmt = Mgmt();
            var worldId = await CreateWorldAsync();

            var result = await mgmt.CreateHeadlessSessionAsync(worldId, null, null, null, null);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.SessionId, Is.Not.Null.And.Not.Empty);

            var sessions = await mgmt.ListSessionsAsync();
            Assert.That(sessions.Any(s => s.SessionId == result.SessionId), Is.True);

            // A character was placed, so perception is computable.
            var perception = await mgmt.GetPerceptionAsync(result.SessionId!);
            Assert.That(perception, Is.Not.Null.And.Not.Empty);
        }

        // Spec: game-management-grain / Headless Session Provisioning
        //       Scenario "Create headless session at an explicit start location"
        [Test]
        public async Task CreateHeadlessSession_AtExplicitLocation_PlacesCharacterThere()
        {
            var mgmt = Mgmt();
            var worldId = await CreateWorldAsync();

            // Discover a valid, passable location by placing a character normally and reading its
            // absolute position, then provision a second session pinned to that location.
            var seed = await mgmt.CreateHeadlessSessionAsync(worldId, null, null, null, null);
            Assert.That(seed.Success, Is.True, seed.Message);
            using var seedDoc = JsonDocument.Parse((await mgmt.GetPerceptionAsync(seed.SessionId!, true))!);
            var seedLoc = seedDoc.RootElement.GetProperty("PlayerLocation");
            int lx = seedLoc.GetProperty("X").GetInt32();
            int ly = seedLoc.GetProperty("Y").GetInt32();
            int lz = seedLoc.GetProperty("Z").GetInt32();

            var pinned = await mgmt.CreateHeadlessSessionAsync(worldId, lx, ly, lz, null);
            Assert.That(pinned.Success, Is.True, pinned.Message);

            using var pinnedDoc = JsonDocument.Parse((await mgmt.GetPerceptionAsync(pinned.SessionId!, true))!);
            var pinnedLoc = pinnedDoc.RootElement.GetProperty("PlayerLocation");
            Assert.That(pinnedLoc.GetProperty("X").GetInt32(), Is.EqualTo(lx));
            Assert.That(pinnedLoc.GetProperty("Y").GetInt32(), Is.EqualTo(ly));
            Assert.That(pinnedLoc.GetProperty("Z").GetInt32(), Is.EqualTo(lz));
        }

        // Spec: game-management-grain / Headless Session Provisioning
        //       Scenario "Create headless session in a non-existent world"
        [Test]
        public async Task CreateHeadlessSession_UnknownWorld_Fails()
        {
            var result = await Mgmt().CreateHeadlessSessionAsync($"nonexistent-{Guid.NewGuid()}", null, null, null, null);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message.ToLowerInvariant(), Does.Contain("not found").Or.Contain("not initialized"));
        }

        // Spec: game-management-grain / Headless Session Provisioning
        //       Scenario "Drive a headless session with existing verbs"
        [Test]
        public async Task HeadlessSession_CanBeDriven_WithExistingVerbs()
        {
            var mgmt = Mgmt();
            var worldId = await CreateWorldAsync();
            var session = await mgmt.CreateHeadlessSessionAsync(worldId, null, null, null, null);
            Assert.That(session.Success, Is.True, session.Message);

            var op = await mgmt.SetDirectionalVisionAsync(session.SessionId!, true);
            Assert.That(op.Success, Is.True, op.Message);

            var json = await mgmt.GetPerceptionAsync(session.SessionId!);
            Assert.That(json, Is.Not.Null);
            using var doc = JsonDocument.Parse(json!);
            Assert.That(doc.RootElement.GetProperty("IsDirectionalVision").GetBoolean(), Is.True);
        }

        // Spec: game-management-grain / Headless Session Provisioning
        //       Scenario "Drive a headless session with existing verbs" (agent runner attaches with no client)
        [Test]
        public async Task AgentRunner_CanAttachToHeadlessSession()
        {
            var mgmt = Mgmt();
            var worldId = await CreateWorldAsync();
            var session = await mgmt.CreateHeadlessSessionAsync(worldId, null, null, null, null);
            Assert.That(session.Success, Is.True, session.Message);

            var runner = _cluster.GrainFactory.GetGrain<Aetherium.Server.Agents.IAgentRunnerGrain>($"runner-{Guid.NewGuid()}");
            var attached = await runner.AttachAsync(session.SessionId!, "agent-headless");
            Assert.That(attached, Is.True, "agent runner should attach to a headless session with no client connected");

            var status = await runner.GetStatusAsync();
            Assert.That(status.SessionId, Is.EqualTo(session.SessionId));
        }

        // Spec: game-management-grain / Operator Perception Retrieval
        //       Scenarios "Retrieve perception with absolute coordinates" + default relativized
        [Test]
        public async Task Perception_AbsoluteReturnsTrueCoordinates_DefaultRelativized()
        {
            var mgmt = Mgmt();
            var worldId = await CreateWorldAsync();
            var session = await mgmt.CreateHeadlessSessionAsync(worldId, null, null, null, null);
            Assert.That(session.Success, Is.True, session.Message);

            // Default (relative) perception is always (0,0,0).
            var relativeJson = await mgmt.GetPerceptionAsync(session.SessionId!);
            using var relDoc = JsonDocument.Parse(relativeJson!);
            var relLoc = relDoc.RootElement.GetProperty("PlayerLocation");
            Assert.That(relLoc.GetProperty("X").GetInt32(), Is.EqualTo(0));
            Assert.That(relLoc.GetProperty("Y").GetInt32(), Is.EqualTo(0));
            Assert.That(relLoc.GetProperty("Z").GetInt32(), Is.EqualTo(0));

            // Absolute perception carries the character's true location, matching the snapshot.
            var absoluteJson = await mgmt.GetPerceptionAsync(session.SessionId!, true);
            using var absDoc = JsonDocument.Parse(absoluteJson!);
            var absLoc = absDoc.RootElement.GetProperty("PlayerLocation");
            int ax = absLoc.GetProperty("X").GetInt32();
            int ay = absLoc.GetProperty("Y").GetInt32();
            int az = absLoc.GetProperty("Z").GetInt32();

            var snapshotJson = await mgmt.GetWorldSnapshotAsync(worldId);
            using var snapDoc = JsonDocument.Parse(snapshotJson!);
            var characters = snapDoc.RootElement.GetProperty("Entities").EnumerateArray()
                .Where(e => e.GetProperty("Type").GetString() == "Character")
                .ToList();
            Assert.That(characters.Count, Is.GreaterThanOrEqualTo(1), "snapshot should contain the placed character");
            bool matches = characters.Any(c =>
            {
                var l = c.GetProperty("Location");
                return l.GetProperty("X").GetInt32() == ax
                    && l.GetProperty("Y").GetInt32() == ay
                    && l.GetProperty("Z").GetInt32() == az;
            });
            Assert.That(matches, Is.True, $"absolute perception ({ax},{ay},{az}) should match a character in the snapshot");
        }

        // Spec: game-management-grain / World State Snapshot
        //       Scenario "Retrieve a world snapshot"
        [Test]
        public async Task WorldSnapshot_ReturnsEntitiesAndTiles()
        {
            var mgmt = Mgmt();
            var worldId = await CreateWorldAsync();
            // Place a character so the snapshot contains at least one Character entity.
            await mgmt.CreateHeadlessSessionAsync(worldId, null, null, null, null);

            var json = await mgmt.GetWorldSnapshotAsync(worldId);
            Assert.That(json, Is.Not.Null.And.Not.Empty);

            using var doc = JsonDocument.Parse(json!);
            var root = doc.RootElement;
            Assert.That(root.GetProperty("EntityCount").GetInt32(), Is.GreaterThan(0));
            Assert.That(root.GetProperty("Tiles").GetArrayLength(), Is.GreaterThan(0));
            var hasCharacter = root.GetProperty("Entities").EnumerateArray()
                .Any(e => e.GetProperty("Type").GetString() == "Character");
            Assert.That(hasCharacter, Is.True);
        }

        // Spec: game-management-grain / World State Snapshot
        //       Scenario "Snapshot for a non-existent world"
        [Test]
        public async Task WorldSnapshot_UnknownWorld_ReturnsNull()
        {
            var json = await Mgmt().GetWorldSnapshotAsync($"nonexistent-{Guid.NewGuid()}");
            Assert.That(json, Is.Null);
        }

        // Spec: game-management-grain / Operator Authorization for God-View Operations
        //       Scenarios "Player profile denied god-view operations" + "Operator caller permitted"
        [Test]
        public async Task OperatorGate_Disabled_DeniesGodViewOperations_ButNotRelativePerception()
        {
            var mgmt = Mgmt();
            var worldId = await CreateWorldAsync();
            // Provision a session while operator access is enabled so we have a sessionId to probe.
            var session = await mgmt.CreateHeadlessSessionAsync(worldId, null, null, null, null);
            Assert.That(session.Success, Is.True, session.Message);

            try
            {
                Environment.SetEnvironmentVariable(OperatorAccess.DisableEnvVar, "1");

                // Denied: headless creation, absolute perception, world snapshot.
                var denied = await mgmt.CreateHeadlessSessionAsync(worldId, null, null, null, null);
                Assert.That(denied.Success, Is.False);
                Assert.That(denied.Message.ToLowerInvariant(), Does.Contain("operator access is disabled"));

                Assert.That(await mgmt.GetWorldSnapshotAsync(worldId), Is.Null);
                Assert.That(await mgmt.GetPerceptionAsync(session.SessionId!, true), Is.Null);

                // Permitted regardless of the gate: ordinary relative perception.
                Assert.That(await mgmt.GetPerceptionAsync(session.SessionId!), Is.Not.Null);
            }
            finally
            {
                Environment.SetEnvironmentVariable(OperatorAccess.DisableEnvVar, null);
            }

            // Operator caller permitted once the gate is re-enabled.
            var reenabled = await mgmt.CreateHeadlessSessionAsync(worldId, null, null, null, null);
            Assert.That(reenabled.Success, Is.True, reenabled.Message);
        }

        // Spec: game-management-grain / Headless Session Provisioning
        //       Scenario "Terminate and reap headless sessions" (reaper targets only headless sessions)
        [Test]
        public async Task ReapIdleHeadlessSessions_RemovesIdleHeadlessSession()
        {
            var mgmt = Mgmt();
            var worldId = await CreateWorldAsync();
            var session = await mgmt.CreateHeadlessSessionAsync(worldId, null, null, null, null);
            Assert.That(session.Success, Is.True, session.Message);

            // Not idle yet: a large threshold reaps nothing and the session survives.
            var reapedNone = await mgmt.ReapIdleHeadlessSessionsAsync(100000);
            Assert.That(reapedNone, Is.EqualTo(0));
            var stillThere = await mgmt.ListSessionsAsync();
            Assert.That(stillThere.Any(s => s.SessionId == session.SessionId), Is.True);

            // Zero idle threshold reaps the headless session.
            var reaped = await mgmt.ReapIdleHeadlessSessionsAsync(0);
            Assert.That(reaped, Is.GreaterThanOrEqualTo(1));
            var afterReap = await mgmt.ListSessionsAsync();
            Assert.That(afterReap.Any(s => s.SessionId == session.SessionId), Is.False);
        }

        // ---- Scripted / batch action execution (change: add-aetherctl-scripted-actions) ----

        private static ScriptedActionDto Move(string dir = "forward") =>
            new ScriptedActionDto { Tool = "move", Args = new Dictionary<string, object> { ["direction"] = dir } };

        private async Task<string> HeadlessSessionInNewWorldAsync()
        {
            var worldId = await CreateWorldAsync();
            var session = await Mgmt().CreateHeadlessSessionAsync(worldId, null, null, null, null);
            Assert.That(session.Success, Is.True, session.Message);
            return session.SessionId!;
        }

        // Spec: game-management-grain / Batch Action Execution — Scenario "Execute an ordered batch"
        [Test]
        public async Task Batch_RunsInOrder_OneResultPerStep()
        {
            var sessionId = await HeadlessSessionInNewWorldAsync();
            var actions = new List<ScriptedActionDto> { Move(), Move(), Move() };

            var results = await Mgmt().ExecuteToolBatchAsync(sessionId, actions, false);

            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results.Select(r => r.Index), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(results.All(r => r.Tool == "move"), Is.True);
            Assert.That(results.All(r => r.Success), Is.True);
        }

        // Spec: game-management-grain / Batch Action Execution — Scenario "Stop on first error"
        [Test]
        public async Task Batch_StopOnError_HaltsAtFirstFailure()
        {
            var sessionId = await HeadlessSessionInNewWorldAsync();
            var actions = new List<ScriptedActionDto>
            {
                Move(),
                new ScriptedActionDto { Tool = "no-such-tool", Args = new Dictionary<string, object>() },
                Move()
            };

            var results = await Mgmt().ExecuteToolBatchAsync(sessionId, actions, stopOnError: true);

            Assert.That(results.Count, Is.EqualTo(2), "should stop after the failing step");
            Assert.That(results[0].Success, Is.True);
            Assert.That(results[1].Success, Is.False);
        }

        // Spec: game-management-grain / Batch Action Execution — Scenario "Continue past errors"
        [Test]
        public async Task Batch_ContinuePastErrors_ReportsEachStep()
        {
            var sessionId = await HeadlessSessionInNewWorldAsync();
            var actions = new List<ScriptedActionDto>
            {
                Move(),
                new ScriptedActionDto { Tool = "no-such-tool", Args = new Dictionary<string, object>() },
                Move()
            };

            var results = await Mgmt().ExecuteToolBatchAsync(sessionId, actions, stopOnError: false);

            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results[0].Success, Is.True);
            Assert.That(results[1].Success, Is.False);
            Assert.That(results[2].Success, Is.True);
        }

        // Spec: game-management-grain / Batch Action Execution — Scenario "Unknown session"
        [Test]
        public async Task Batch_UnknownSession_SingleFailure()
        {
            var results = await Mgmt().ExecuteToolBatchAsync($"nope-{Guid.NewGuid()}", new List<ScriptedActionDto> { Move() }, false);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Success, Is.False);
            Assert.That(results[0].Message.ToLowerInvariant(), Does.Contain("not found"));
        }

        // Spec: game-management-grain / Batch Action Execution — Scenario "Empty and oversized batches"
        [Test]
        public async Task Batch_EmptyAndOversized()
        {
            var sessionId = await HeadlessSessionInNewWorldAsync();

            var empty = await Mgmt().ExecuteToolBatchAsync(sessionId, new List<ScriptedActionDto>(), false);
            Assert.That(empty, Is.Empty);

            var oversized = Enumerable.Range(0, 1001).Select(_ => Move()).ToList();
            var overResult = await Mgmt().ExecuteToolBatchAsync(sessionId, oversized, false);
            Assert.That(overResult.Count, Is.EqualTo(1));
            Assert.That(overResult[0].Success, Is.False);
            Assert.That(overResult[0].Message.ToLowerInvariant(), Does.Contain("too large"));
        }

        // Spec: game-management-grain / Batch Action Execution — ordered batch changes observable state
        [Test]
        public async Task Batch_ChangesObservableState()
        {
            var sessionId = await HeadlessSessionInNewWorldAsync();
            var actions = new List<ScriptedActionDto>
            {
                new ScriptedActionDto { Tool = "toggledirectionalvision", Args = new Dictionary<string, object> { ["enabled"] = true } }
            };

            var results = await Mgmt().ExecuteToolBatchAsync(sessionId, actions, false);
            Assert.That(results.Single().Success, Is.True, results.Single().Message);

            using var doc = JsonDocument.Parse((await Mgmt().GetPerceptionAsync(sessionId))!);
            Assert.That(doc.RootElement.GetProperty("IsDirectionalVision").GetBoolean(), Is.True);
        }

        // Spec: aetherctl / Multi-Character Scenario Command — core capability the CLI orchestrates:
        //       two characters in one world, each driven by its own batch, both reporting results.
        [Test]
        public async Task MultipleCharacters_InOneWorld_EachDrivenAndReport()
        {
            var mgmt = Mgmt();
            var worldId = await CreateWorldAsync();
            var a = await mgmt.CreateHeadlessSessionAsync(worldId, null, null, null, null);
            var b = await mgmt.CreateHeadlessSessionAsync(worldId, null, null, null, null);
            Assert.That(a.Success && b.Success, Is.True);
            Assert.That(a.SessionId, Is.Not.EqualTo(b.SessionId));

            var ra = await mgmt.ExecuteToolBatchAsync(a.SessionId!, new List<ScriptedActionDto> { Move(), Move() }, false);
            var rb = await mgmt.ExecuteToolBatchAsync(b.SessionId!, new List<ScriptedActionDto> { Move() }, false);

            Assert.That(ra.Count, Is.EqualTo(2));
            Assert.That(rb.Count, Is.EqualTo(1));
            Assert.That(ra.All(r => r.Success) && rb.All(r => r.Success), Is.True);
        }
    }
}
