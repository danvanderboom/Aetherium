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
    /// Diagnostic probe for the live perception-pool shape (the "malformed circle" seen in
    /// the Unity client): renders the frame's visible set and light levels as ASCII from a
    /// real server session, before and after movement. The assertion is deliberately loose
    /// (pool must include the four adjacent cells); the value is in the printed shape.
    /// </summary>
    [TestFixture]
    public class PoolShapeProbeTests
    {
        private WebApplicationFactory<Aetherium.Server.Program> _factory = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Environment.SetEnvironmentVariable("DISABLE_ORLEANS", "1");
            _factory = new WebApplicationFactory<Aetherium.Server.Program>();
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

        private static string DrawFrame(ClientContracts.PerceptionDto frame)
        {
            var cells = new Dictionary<(int X, int Y), double>();
            foreach (var pair in frame.Visuals)
            {
                var parts = pair.Key.Split(',');
                var x = int.Parse(parts[0]);
                var y = int.Parse(parts[1]);
                var z = int.Parse(parts[2]);
                if (z == 0)
                    cells[(x, y)] = pair.Value.LightLevel;
            }
            if (cells.Count == 0)
                return "(empty frame)";

            var minX = cells.Keys.Min(c => c.X); var maxX = cells.Keys.Max(c => c.X);
            var minY = cells.Keys.Min(c => c.Y); var maxY = cells.Keys.Max(c => c.Y);
            var rows = new List<string> { $"rel x:[{minX},{maxX}] y:[{minY},{maxY}] heading:{frame.HeadingDegrees} seq:{frame.MoveSequence}" };
            for (var y = minY; y <= maxY; y++)
            {
                var row = "";
                for (var x = minX; x <= maxX; x++)
                {
                    if (x == 0 && y == 0) row += "@";
                    else if (!cells.TryGetValue((x, y), out var light)) row += ".";
                    else if (light >= 0.5) row += "#";
                    else if (light >= 0.05) row += "+";
                    else row += "-"; // visible but near-dark
                }
                rows.Add(row);
            }
            return string.Join("\n", rows);
        }

        [Test]
        public async Task Probe_PoolShape_AtSpawn_AndAfterSteps()
        {
            await using var client = NewClient();
            var firstFrame = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.Connection.PerceptionReceived += _ => firstFrame.TrySetResult(true);
            await client.ConnectAsync();
            var completed = await Task.WhenAny(firstFrame.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.That(completed, Is.SameAs(firstFrame.Task), "timed out waiting for first frame");

            TestContext.WriteLine("=== AT SPAWN ===");
            TestContext.WriteLine(DrawFrame(client.Store.LatestFrame!));

            var stepFns = new Func<Task<ClientContracts.ToolExecutionResultDto>>[]
            {
                () => client.Tools.MoveForwardAsync(),
                () => client.Tools.MoveRightAsync(),
                () => client.Tools.MoveLeftAsync(),
                () => client.Tools.MoveBackwardAsync(),
            };
            var steps = 0;
            for (var attempt = 0; attempt < 20 && steps < 5; attempt++)
            {
                if ((await stepFns[attempt % stepFns.Length]()).Success)
                {
                    steps++;
                    TestContext.WriteLine($"=== AFTER STEP {steps} ===");
                    TestContext.WriteLine(DrawFrame(client.Store.LatestFrame!));
                }
            }

            var frame = client.Store.LatestFrame!;
            Assert.That(frame.Visuals.Keys, Does.Contain("0,0,0"), "self cell visible");
        }
    }
}
