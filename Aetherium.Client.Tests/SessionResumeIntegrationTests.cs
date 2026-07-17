using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ClientContracts = Aetherium.Client.Contracts;

namespace Aetherium.Client.Tests
{
    /// <summary>
    /// Session resume driven end-to-end against the real server in-process (the
    /// InProcServerIntegrationTests pattern: WebApplicationFactory, Orleans disabled, real
    /// SignalR over the TestServer transport). Pins the reconnect contract: a client that
    /// reconnects within the grace window and presents its resume token rebinds to the SAME
    /// server session — same PlayerId, same position (MoveSequence continuity), and no
    /// client-side discontinuity, so the PerceptionStore keeps its anchor and map memory.
    /// A client whose window lapsed falls back to a fresh join: new session, re-anchor,
    /// memory wiped — exactly what a genuinely new player gets.
    /// </summary>
    [TestFixture]
    public class SessionResumeIntegrationTests
    {
        private WebApplicationFactory<Aetherium.Server.Program> _factory = null!;
        private Aetherium.Server.GameSessionManager _sessionManager = null!;
        private TimeSpan _originalGraceWindow;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Environment.SetEnvironmentVariable("DISABLE_ORLEANS", "1");
            _factory = new WebApplicationFactory<Aetherium.Server.Program>();
            // Force server startup so CreateHandler is ready.
            _ = _factory.Server;
            _sessionManager = _factory.Services.GetRequiredService<Aetherium.Server.GameSessionManager>();
            _originalGraceWindow = _sessionManager.ResumeGraceWindow;
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await _factory.DisposeAsync();
            Environment.SetEnvironmentVariable("DISABLE_ORLEANS", null);
        }

        [SetUp]
        public void SetUp() => _sessionManager.ResumeGraceWindow = _originalGraceWindow;

        [TearDown]
        public void TearDown() => _sessionManager.ResumeGraceWindow = _originalGraceWindow;

        private AetheriumClient NewClient() => new AetheriumClient(
            _factory.Server.BaseAddress.ToString().TrimEnd('/'),
            configureHttpConnection: options => options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler());

        private static async Task<T> WaitFor<T>(TaskCompletionSource<T> source, string what, int seconds = 10)
        {
            var completed = await Task.WhenAny(source.Task, Task.Delay(TimeSpan.FromSeconds(seconds)));
            Assert.That(completed, Is.SameAs(source.Task), $"timed out waiting for {what}");
            return await source.Task;
        }

        /// <summary>Waits until a perception frame stamped with at least <paramref name="target"/>
        /// is applied to the store (the post-move push lands after the tool response, so
        /// LatestFrame can lag a just-completed move). Listens on Store.FrameReceived — not
        /// Connection.PerceptionReceived — because a frame held during a move is applied on
        /// hold-release, which only the store-level event reports.</summary>
        private static async Task WaitForSequence(AetheriumClient client, long target, int seconds = 10)
        {
            var reached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Check(ClientContracts.PerceptionDto f)
            {
                if (f.MoveSequence >= target) reached.TrySetResult(true);
            }
            client.Store.FrameReceived += Check;
            try
            {
                if (client.Store.LatestFrame?.MoveSequence >= target)
                    return;
                var completed = await Task.WhenAny(reached.Task, Task.Delay(TimeSpan.FromSeconds(seconds)));
                Assert.That(completed, Is.SameAs(reached.Task), $"timed out waiting for perception sequence {target}");
            }
            finally
            {
                client.Store.FrameReceived -= Check;
            }
        }

        /// <summary>Walks up to <paramref name="wanted"/> successful steps to build up anchor
        /// displacement and map memory; returns how many steps actually landed.</summary>
        private static async Task<int> WalkSome(AetheriumClient client, int wanted)
        {
            var stepFns = new Func<Task<ClientContracts.ToolExecutionResultDto>>[]
            {
                () => client.Tools.MoveForwardAsync(),
                () => client.Tools.MoveRightAsync(),
                () => client.Tools.MoveLeftAsync(),
                () => client.Tools.MoveBackwardAsync(),
            };
            var steps = 0;
            for (var attempt = 0; attempt < 24 && steps < wanted; attempt++)
                if ((await stepFns[attempt % stepFns.Length]()).Success)
                    steps++;
            return steps;
        }

        [Test]
        public async Task ResumeWithinWindow_PreservesSessionPositionAndMemory()
        {
            await using var client = NewClient();
            var firstFrame = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.Connection.PerceptionReceived += _ => firstFrame.TrySetResult(true);

            var reanchors = new List<ReanchorReason>();
            client.Store.Reanchored += reason => { lock (reanchors) reanchors.Add(reason); };

            await client.ConnectAsync();
            await WaitFor(firstFrame, "first perception frame");

            var steps = await WalkSome(client, wanted: 3);
            if (steps < 1)
                Assert.Inconclusive("No walkable direction from this spawn — cannot displace the anchor.");
            // Server sequence counts from 1 (pre-first-move); settle on the post-walk frame.
            await WaitForSequence(client, 1 + steps);

            var playerIdBefore = client.Connection.PlayerId;
            var tokenBefore = client.Connection.ResumeToken;
            var anchorBefore = client.Store.Anchor;
            var sequenceBefore = client.Store.LatestFrame!.MoveSequence;
            var memoryBefore = client.Store.Memory
                .Where(c => c.Terrain != null)
                .ToDictionary(c => c.Position, c => c.Terrain!.Name);
            Assert.That(playerIdBefore, Is.Not.Null.And.Not.Empty);
            Assert.That(tokenBefore, Is.Not.Null.And.Not.Empty, "the server issues a resume token with the game state");
            Assert.That(anchorBefore, Is.Not.EqualTo(GridPoint.Origin), "the walk displaced the anchor");
            Assert.That(memoryBefore, Is.Not.Empty);

            int reanchorsBeforeReconnect;
            lock (reanchors) reanchorsBeforeReconnect = reanchors.Count;

            // Drop the connection and come back within the (default, generous) grace window.
            await client.DisconnectAsync();
            await client.ConnectAsync();

            // Same session on both ends: same id, same token, same move-sequence continuity.
            Assert.That(client.Connection.PlayerId, Is.EqualTo(playerIdBefore), "resume must rebind the SAME session");
            Assert.That(client.Connection.ResumeToken, Is.EqualTo(tokenBefore));
            Assert.That(client.Store.LatestFrame, Is.Not.Null, "the resume response carries the session's current frame");
            Assert.That(client.Store.LatestFrame!.MoveSequence, Is.EqualTo(sequenceBefore),
                "the resumed session continues the prior move sequence — a fresh spawn would restart at 1");

            // No discontinuity: anchor and every remembered cell survive the reconnect.
            lock (reanchors)
                Assert.That(reanchors.Count, Is.EqualTo(reanchorsBeforeReconnect),
                    $"a successful resume must not re-anchor (got: {string.Join(", ", reanchors)})");
            Assert.That(client.Store.Anchor, Is.EqualTo(anchorBefore), "the anchor survives the reconnect");
            var memoryAfter = client.Store.Memory.ToDictionary(c => c.Position, c => c.Terrain?.Name);
            foreach (var (cell, terrain) in memoryBefore)
            {
                Assert.That(memoryAfter.ContainsKey(cell), Is.True,
                    $"remembered cell {cell} ('{terrain}') was lost across the resume");
                Assert.That(memoryAfter[cell], Is.EqualTo(terrain),
                    $"remembered cell {cell} changed identity '{terrain}' → '{memoryAfter[cell]}' across the resume");
            }

            // And the session is live: the player can keep moving from where they stood.
            var moved = await WalkSome(client, wanted: 1);
            Assert.That(moved, Is.EqualTo(1), "the resumed session still executes tools");
            await WaitForSequence(client, sequenceBefore + 1);
            Assert.That(client.Store.LatestFrame!.MoveSequence, Is.EqualTo(sequenceBefore + 1),
                "post-resume movement continues the same sequence");
        }

        [Test]
        public async Task ResumeAfterExpiry_FallsBackToFreshJoin()
        {
            _sessionManager.ResumeGraceWindow = TimeSpan.FromMilliseconds(50);

            await using var client = NewClient();
            var firstFrame = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.Connection.PerceptionReceived += _ => firstFrame.TrySetResult(true);

            var reanchors = new List<ReanchorReason>();
            client.Store.Reanchored += reason => { lock (reanchors) reanchors.Add(reason); };

            await client.ConnectAsync();
            await WaitFor(firstFrame, "first perception frame");
            await WalkSome(client, wanted: 2); // build some memory to be wiped

            var playerIdBefore = client.Connection.PlayerId;
            Assert.That(playerIdBefore, Is.Not.Null.And.Not.Empty);

            int reanchorsBeforeReconnect;
            lock (reanchors) reanchorsBeforeReconnect = reanchors.Count;

            await client.DisconnectAsync();
            await Task.Delay(500); // let the 50 ms grace window lapse decisively

            await client.ConnectAsync();

            // The resume was refused; the client adopted the fresh session the server made.
            Assert.That(client.Connection.PlayerId, Is.Not.Null.And.Not.Empty);
            Assert.That(client.Connection.PlayerId, Is.Not.EqualTo(playerIdBefore),
                "an expired session must not be resumable — this is a fresh join");

            // The fallback IS a discontinuity: re-anchored at origin, old memory gone,
            // and the store repopulated from the fresh session's first frame.
            lock (reanchors)
            {
                Assert.That(reanchors.Count, Is.EqualTo(reanchorsBeforeReconnect + 1),
                    "a failed resume re-anchors exactly once");
                Assert.That(reanchors.Last(), Is.EqualTo(ReanchorReason.Joined));
            }
            Assert.That(client.Store.Anchor, Is.EqualTo(GridPoint.Origin), "fresh join re-bases the anchor");
            Assert.That(client.Store.LatestFrame, Is.Not.Null, "the buffered fresh-session frame was adopted");
            Assert.That(client.Store.LatestFrame!.MoveSequence, Is.EqualTo(1),
                "a fresh session's sequence restarts at 1");
            Assert.That(client.Store.Memory, Is.Not.Empty, "memory rebuilt from the fresh frame only");
        }
    }
}
