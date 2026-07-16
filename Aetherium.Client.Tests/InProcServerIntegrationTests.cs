using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.AspNetCore.Mvc.Testing;
using ClientContracts = Aetherium.Client.Contracts;

namespace Aetherium.Client.Tests
{
    /// <summary>
    /// The library driven end-to-end against the REAL server, in-process: a
    /// WebApplicationFactory hosts Aetherium.Server (Orleans disabled — the co-hosted silo
    /// hangs under TestServer; local-session mode exercises the same hub, session,
    /// perception, and tool pipeline), and the AetheriumClient connects over real SignalR
    /// through the TestServer transport. This is where the design's live assertions get
    /// pinned: frames flow on connect, interoception rides the hub push, tool calls execute,
    /// and anchoring holds — relative offsets are world-axis-aligned, so remembered terrain
    /// keeps its client-space position across the player's own movement.
    /// </summary>
    [TestFixture]
    public class InProcServerIntegrationTests
    {
        private WebApplicationFactory<Aetherium.Server.Program> _factory = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Environment.SetEnvironmentVariable("DISABLE_ORLEANS", "1");
            _factory = new WebApplicationFactory<Aetherium.Server.Program>();
            // Force server startup so CreateHandler is ready.
            _ = _factory.Server;
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await _factory.DisposeAsync();
            Environment.SetEnvironmentVariable("DISABLE_ORLEANS", null);
        }

        private AetheriumClient NewClient() => new AetheriumClient(
            _factory.Server.BaseAddress.ToString().TrimEnd('/'),
            configureHttpConnection: options => options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler());

        private static async Task<T> WaitFor<T>(TaskCompletionSource<T> source, string what, int seconds = 10)
        {
            var completed = await Task.WhenAny(source.Task, Task.Delay(TimeSpan.FromSeconds(seconds)));
            Assert.That(completed, Is.SameAs(source.Task), $"timed out waiting for {what}");
            return await source.Task;
        }

        [Test]
        public async Task Connect_ReceivesGameState_AndFirstPerceptionFrame()
        {
            await using var client = NewClient();
            var stateReceived = new TaskCompletionSource<ClientContracts.GameStateDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            var frameReceived = new TaskCompletionSource<ClientContracts.PerceptionDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.Connection.GameStateReceived += s => stateReceived.TrySetResult(s);
            client.Connection.PerceptionReceived += f => frameReceived.TrySetResult(f);

            await client.ConnectAsync();

            var state = await WaitFor(stateReceived, "ReceiveGameState");
            var frame = await WaitFor(frameReceived, "ReceivePerceptionUpdate");

            Assert.That(client.Connection.State, Is.EqualTo(AetheriumConnectionState.Connected));
            Assert.That(state.PlayerId, Is.Not.Empty, "the server assigns a session id on connect");
            Assert.That(client.Connection.PlayerId, Is.EqualTo(state.PlayerId));
            Assert.That(frame.Visuals, Is.Not.Empty, "the first frame carries the visible world");
            Assert.That(client.Store.LatestFrame, Is.Not.Null, "the store consumed the frame");
            Assert.That(client.Store.Memory, Is.Not.Empty, "revealed cells entered memory");
        }

        [Test]
        public async Task LiveFrame_CarriesInteroception_ThroughTheHubPush()
        {
            // Pins the session-path interoception wiring (add-interoception-channel): the
            // frames the HUB pushes — not just the grain's agent JSON — feel the body.
            await using var client = NewClient();
            var frameReceived = new TaskCompletionSource<ClientContracts.PerceptionDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.Connection.PerceptionReceived += f => frameReceived.TrySetResult(f);

            await client.ConnectAsync();
            var frame = await WaitFor(frameReceived, "first perception frame");

            Assert.That(frame.Interoception, Is.Not.Null, "live player frames carry the self-sense");
            Assert.That(frame.Interoception!.MaxHealth, Is.GreaterThan(0));
            Assert.That(frame.Interoception.Health, Is.GreaterThan(0).And.LessThanOrEqualTo(frame.Interoception.MaxHealth));
        }

        [Test]
        public async Task Tools_ListSchema_AndRoundTripThroughExecuteTool()
        {
            await using var client = NewClient();
            await client.ConnectAsync();

            var tools = await client.Tools.ListAvailableToolsAsync();
            var ids = tools.Select(t => t.ToolId).ToList();
            Assert.That(ids, Does.Contain("move").And.Contain("rotate").And.Contain("attack"),
                "the Player profile's core verbs are advertised");

            var move = tools.Single(t => t.ToolId == "move");
            Assert.That(move.ParameterSchema.Required, Does.Contain("direction"),
                "the typed wrappers' contract matches the live schema (the dev-build drift check)");

            var rotate = await client.Tools.RotateAsync(clockwise: true);
            Assert.That(rotate.Success, Is.True, $"rotate should always succeed: {rotate.Message}");
        }

        [Test]
        public async Task Anchoring_LandmarksKeepTheirClientSpaceCells_AcrossOwnMovement()
        {
            // THE anchoring assertion (docs/design/unity-sample/unity-client-library.md §PerceptionStore):
            // relative offsets are world-axis-aligned (north-up), never heading-rotated, and the
            // anchor advances exactly with the player's own movement — so terrain remembered at a
            // client-space cell must still describe the same cell after we rotate AND move.
            await using var client = NewClient();
            var firstFrame = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.Connection.PerceptionReceived += _ => firstFrame.TrySetResult(true);
            await client.ConnectAsync();
            await WaitFor(firstFrame, "first perception frame");

            // Rotate so heading != north — if offsets were heading-rotated, everything below breaks.
            var rotate = await client.Tools.RotateAsync(clockwise: true);
            Assert.That(rotate.Success, Is.True);

            // Find a direction we can actually step (the spawn is somewhere in a maze).
            var directions = new (string Name, Func<Task<ClientContracts.ToolExecutionResultDto>> Step)[]
            {
                ("forward", () => client.Tools.MoveForwardAsync()),
                ("right", () => client.Tools.MoveRightAsync()),
                ("backward", () => client.Tools.MoveBackwardAsync()),
                ("left", () => client.Tools.MoveLeftAsync()),
            };

            foreach (var (name, step) in directions)
            {
                // Fingerprint remembered terrain BEFORE the step (client-space cell → terrain name).
                var before = client.Store.Memory
                    .Where(c => c.Terrain != null)
                    .ToDictionary(c => c.Position, c => c.Terrain!.Name);
                var anchorBefore = client.Store.Anchor;

                var nextFrame = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                client.Connection.PerceptionReceived += _ => nextFrame.TrySetResult(true);

                var result = await step();
                if (!result.Success)
                    continue; // wall that way; try another direction

                await WaitFor(nextFrame, $"perception frame after moving {name}");

                // 1. The anchor advanced exactly one cardinal step.
                var anchorAfter = client.Store.Anchor;
                var delta = (X: anchorAfter.X - anchorBefore.X, Y: anchorAfter.Y - anchorBefore.Y, Z: anchorAfter.Z - anchorBefore.Z);
                Assert.That(Math.Abs(delta.X) + Math.Abs(delta.Y), Is.EqualTo(1), "one cardinal step");
                Assert.That(delta.Z, Is.EqualTo(0));

                // 2. Every remembered cell still visible now reports the SAME terrain at the
                // SAME client-space position. On a maze layout this fingerprint comparison
                // fails loudly if offsets were heading-rotated or the anchor delta was wrong.
                var after = client.Store.Memory
                    .Where(c => c.Terrain != null && c.InView)
                    .ToDictionary(c => c.Position, c => c.Terrain!.Name);
                var overlap = after.Keys.Where(before.ContainsKey).ToList();
                Assert.That(overlap, Is.Not.Empty, "the views before and after one step must overlap");
                foreach (var cell in overlap)
                    Assert.That(after[cell], Is.EqualTo(before[cell]),
                        $"terrain at client-space {cell} changed identity after our own move — anchoring drifted");
                return; // one successful step proves it
            }

            Assert.Inconclusive("No direction was walkable from this spawn — cannot exercise the anchoring assertion.");
        }

        [Test]
        public async Task CompositeMove_RotatesAndSteps_LikeAnyAgentWould()
        {
            await using var client = NewClient();
            var firstFrame = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.Connection.PerceptionReceived += _ => firstFrame.TrySetResult(true);
            await client.ConnectAsync();
            await WaitFor(firstFrame, "first perception frame");

            // WASD bridge: at least one compass direction must be walkable from spawn.
            foreach (var direction in new[]
            {
                ClientContracts.WorldDirection.North,
                ClientContracts.WorldDirection.East,
                ClientContracts.WorldDirection.South,
                ClientContracts.WorldDirection.West,
            })
            {
                var anchorBefore = client.Store.Anchor;
                var result = await client.Tools.MoveAsync(direction);
                if (!result.Success)
                    continue;

                var expected = direction switch
                {
                    ClientContracts.WorldDirection.North => anchorBefore.Offset(0, -1, 0),
                    ClientContracts.WorldDirection.East => anchorBefore.Offset(1, 0, 0),
                    ClientContracts.WorldDirection.South => anchorBefore.Offset(0, 1, 0),
                    _ => anchorBefore.Offset(-1, 0, 0),
                };
                Assert.That(client.Store.Anchor, Is.EqualTo(expected),
                    $"composite {direction} advanced the anchor along the compass axis");
                return;
            }

            Assert.Inconclusive("No compass direction was walkable from this spawn.");
        }
    }
}
