using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using NUnit.Framework;
using Orleans;
using Orleans.Hosting;
using Orleans.TestingHost;
using Aetherium.Server.Agents.Telemetry;

namespace Aetherium.Test.Orleans
{
    [TestFixture]
    public class AgentTelemetryGrainTests
    {
        private TestCluster? _cluster;

        [SetUp]
        public async Task SetUp()
        {
            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
            _cluster = builder.Build();
            await _cluster.DeployAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_cluster != null)
            {
                await _cluster.StopAllSilosAsync();
                _cluster.Dispose();
            }
        }

        [Test]
        public async Task AgentTelemetryGrain_RecordSnapshot_StoresSnapshot()
        {
            // Arrange
            var agentId = "test-agent-1";
            var grain = _cluster!.GrainFactory.GetGrain<IAgentTelemetryGrain>(agentId);
            var snapshot = new PerformanceSnapshot
            {
                AgentId = agentId,
                SessionId = "session-1",
                StepNumber = 1,
                ActionType = "move",
                ActionSummary = "move forward",
                ActionSucceeded = true,
                DecisionLatencyMs = 100,
                PerceptionComplexity = 10,
                Timestamp = DateTime.UtcNow
            };

            // Act
            await grain.RecordSnapshotAsync(snapshot);

            // Assert
            var snapshots = await grain.GetSnapshotsAsync();
            Assert.That(snapshots, Is.Not.Null);
            Assert.That(snapshots.Count, Is.EqualTo(1));
            Assert.That(snapshots[0].ActionType, Is.EqualTo("move"));
            Assert.That(snapshots[0].ActionSucceeded, Is.True);
        }

        [Test]
        public async Task AgentTelemetryGrain_GetSnapshots_WithLimit_ReturnsLimitedSnapshots()
        {
            // Arrange
            var agentId = "test-agent-2";
            var grain = _cluster!.GrainFactory.GetGrain<IAgentTelemetryGrain>(agentId);

            // Record multiple snapshots
            for (int i = 1; i <= 10; i++)
            {
                await grain.RecordSnapshotAsync(new PerformanceSnapshot
                {
                    AgentId = agentId,
                    SessionId = "session-1",
                    StepNumber = i,
                    ActionType = "move",
                    ActionSucceeded = true,
                    Timestamp = DateTime.UtcNow.AddSeconds(i)
                });
            }

            // Act
            var snapshots = await grain.GetSnapshotsAsync(limit: 5);

            // Assert
            Assert.That(snapshots, Is.Not.Null);
            Assert.That(snapshots.Count, Is.EqualTo(5));
            // Should return most recent 5
            Assert.That(snapshots[0].StepNumber, Is.GreaterThanOrEqualTo(6));
        }

        [Test]
        public async Task AgentTelemetryGrain_GetSnapshotsInRange_ReturnsSnapshotsInTimeRange()
        {
            // Arrange
            var agentId = "test-agent-3";
            var grain = _cluster!.GrainFactory.GetGrain<IAgentTelemetryGrain>(agentId);
            var startTime = DateTime.UtcNow;
            
            // Record snapshots at different times
            await grain.RecordSnapshotAsync(new PerformanceSnapshot
            {
                AgentId = agentId,
                StepNumber = 1,
                Timestamp = startTime.AddSeconds(-10), // Before range
                ActionSucceeded = true
            });

            await grain.RecordSnapshotAsync(new PerformanceSnapshot
            {
                AgentId = agentId,
                StepNumber = 2,
                Timestamp = startTime.AddSeconds(5), // Within range
                ActionSucceeded = true
            });

            await grain.RecordSnapshotAsync(new PerformanceSnapshot
            {
                AgentId = agentId,
                StepNumber = 3,
                Timestamp = startTime.AddSeconds(10), // Within range
                ActionSucceeded = true
            });

            var endTime = startTime.AddSeconds(15);

            await grain.RecordSnapshotAsync(new PerformanceSnapshot
            {
                AgentId = agentId,
                StepNumber = 4,
                Timestamp = endTime.AddSeconds(5), // After range
                ActionSucceeded = true
            });

            // Act
            var snapshots = await grain.GetSnapshotsInRangeAsync(startTime, endTime);

            // Assert
            Assert.That(snapshots, Is.Not.Null);
            Assert.That(snapshots.Count, Is.EqualTo(2));
            Assert.That(snapshots.All(s => s.Timestamp >= startTime && s.Timestamp <= endTime), Is.True);
        }

        [Test]
        public async Task AgentTelemetryGrain_GetAnalysis_CalculatesAnalysisFromSnapshots()
        {
            // Arrange
            var agentId = "test-agent-4";
            var grain = _cluster!.GrainFactory.GetGrain<IAgentTelemetryGrain>(agentId);

            // Record mix of successful and failed actions
            for (int i = 0; i < 10; i++)
            {
                await grain.RecordSnapshotAsync(new PerformanceSnapshot
                {
                    AgentId = agentId,
                    StepNumber = i + 1,
                    ActionType = "move",
                    ActionSucceeded = i < 7, // 7 successful, 3 failed = 70% success rate
                    DecisionLatencyMs = 100 + i * 10,
                    PerceptionComplexity = 10,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Act
            var analysis = await grain.GetAnalysisAsync();

            // Assert
            Assert.That(analysis, Is.Not.Null);
            Assert.That(analysis.TotalSteps, Is.EqualTo(10));
            Assert.That(analysis.TotalSuccessfulActions, Is.EqualTo(7));
            Assert.That(analysis.TotalFailedActions, Is.EqualTo(3));
            Assert.That(analysis.SuccessRate, Is.EqualTo(0.7).Within(0.001));
            Assert.That(analysis.ActionTypeStats, Contains.Key("move"));
        }

        [Test]
        public async Task AgentTelemetryGrain_RecordFailedRun_StoresReplay()
        {
            // Arrange
            var agentId = "test-agent-5";
            var grain = _cluster!.GrainFactory.GetGrain<IAgentTelemetryGrain>(agentId);
            var replayJson = @"{""agentId"":""" + agentId + @""",""failureReason"":""Test failure"",""totalSteps"":5}";

            // Act
            var replayId = await grain.RecordFailedRunAsync(replayJson);

            // Assert
            Assert.That(replayId, Is.Not.Null);
            Assert.That(replayId, Is.Not.Empty);

            var failedRunIds = await grain.GetFailedRunIdsAsync();
            Assert.That(failedRunIds, Contains.Item(replayId));
        }

        [Test]
        public async Task AgentTelemetryGrain_GetFailedRunIds_WithLimit_ReturnsLimitedIds()
        {
            // Arrange
            var agentId = "test-agent-6";
            var grain = _cluster!.GrainFactory.GetGrain<IAgentTelemetryGrain>(agentId);

            // Record multiple failed runs
            var replayIds = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                var replayJson = @"{""agentId"":""" + agentId + @""",""failureReason"":""Test " + i + @""",""totalSteps"":5}";
                var replayId = await grain.RecordFailedRunAsync(replayJson);
                replayIds.Add(replayId);
            }

            // Act
            var failedRunIds = await grain.GetFailedRunIdsAsync(limit: 5);

            // Assert
            Assert.That(failedRunIds, Is.Not.Null);
            Assert.That(failedRunIds.Count, Is.EqualTo(5));
        }

        [Test]
        public async Task AgentTelemetryGrain_ClearTelemetry_ClearsAllData()
        {
            // Arrange
            var agentId = "test-agent-7";
            var grain = _cluster!.GrainFactory.GetGrain<IAgentTelemetryGrain>(agentId);

            await grain.RecordSnapshotAsync(new PerformanceSnapshot
            {
                AgentId = agentId,
                StepNumber = 1,
                ActionSucceeded = true,
                Timestamp = DateTime.UtcNow
            });

            await grain.RecordFailedRunAsync(@"{""agentId"":""" + agentId + @""",""failureReason"":""Test"",""totalSteps"":1}");

            // Act
            await grain.ClearTelemetryAsync();

            // Assert
            var snapshots = await grain.GetSnapshotsAsync();
            Assert.That(snapshots, Is.Empty);

            var failedRunIds = await grain.GetFailedRunIdsAsync();
            Assert.That(failedRunIds, Is.Empty);

            var analysis = await grain.GetAnalysisAsync();
            Assert.That(analysis.TotalSteps, Is.EqualTo(0));
        }

        private class TestSiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                // Orleans v9 auto-discovers grain assemblies referenced by the test project.
                // No explicit application part configuration is required here.
            }
        }
    }
}

