using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Aetherium.Server.Agents.Telemetry;

namespace Aetherium.Test.Agents.Telemetry
{
    [TestFixture]
    public class AgentTelemetryTests
    {
        [Test]
        public void PerformanceAnalyzer_EmptySnapshots_ReturnsEmptyAnalysis()
        {
            // Arrange
            var analyzer = new PerformanceAnalyzer();
            var snapshots = new List<PerformanceSnapshot>();

            // Act
            var analysis = analyzer.Analyze(snapshots);

            // Assert
            Assert.That(analysis, Is.Not.Null);
            Assert.That(analysis.TotalSteps, Is.EqualTo(0));
            Assert.That(analysis.TotalSuccessfulActions, Is.EqualTo(0));
            Assert.That(analysis.TotalFailedActions, Is.EqualTo(0));
            Assert.That(analysis.SuccessRate, Is.EqualTo(0.0));
            Assert.That(analysis.ActionTypeStats, Is.Empty);
            Assert.That(analysis.IdentifiedWeaknesses, Is.Empty);
        }

        [Test]
        public void PerformanceAnalyzer_AllSuccessfulSnapshots_Returns100PercentSuccessRate()
        {
            // Arrange
            var analyzer = new PerformanceAnalyzer();
            var snapshots = new List<PerformanceSnapshot>
            {
                new PerformanceSnapshot { ActionSucceeded = true, ActionType = "move", DecisionLatencyMs = 100, PerceptionComplexity = 10 },
                new PerformanceSnapshot { ActionSucceeded = true, ActionType = "pickup", DecisionLatencyMs = 150, PerceptionComplexity = 15 },
                new PerformanceSnapshot { ActionSucceeded = true, ActionType = "move", DecisionLatencyMs = 120, PerceptionComplexity = 12 }
            };

            // Act
            var analysis = analyzer.Analyze(snapshots);

            // Assert
            Assert.That(analysis.TotalSteps, Is.EqualTo(3));
            Assert.That(analysis.TotalSuccessfulActions, Is.EqualTo(3));
            Assert.That(analysis.TotalFailedActions, Is.EqualTo(0));
            Assert.That(analysis.SuccessRate, Is.EqualTo(1.0));
            Assert.That(analysis.AverageDecisionLatencyMs, Is.EqualTo((100 + 150 + 120) / 3.0));
        }

        [Test]
        public void PerformanceAnalyzer_AllFailedSnapshots_Returns0PercentSuccessRate()
        {
            // Arrange
            var analyzer = new PerformanceAnalyzer();
            var snapshots = new List<PerformanceSnapshot>
            {
                new PerformanceSnapshot { ActionSucceeded = false, ActionType = "move", DecisionLatencyMs = 100, PerceptionComplexity = 10 },
                new PerformanceSnapshot { ActionSucceeded = false, ActionType = "pickup", DecisionLatencyMs = 150, PerceptionComplexity = 15 }
            };

            // Act
            var analysis = analyzer.Analyze(snapshots);

            // Assert
            Assert.That(analysis.TotalSteps, Is.EqualTo(2));
            Assert.That(analysis.TotalSuccessfulActions, Is.EqualTo(0));
            Assert.That(analysis.TotalFailedActions, Is.EqualTo(2));
            Assert.That(analysis.SuccessRate, Is.EqualTo(0.0));
        }

        [Test]
        public void PerformanceAnalyzer_GroupsByActionType()
        {
            // Arrange
            var analyzer = new PerformanceAnalyzer();
            var snapshots = new List<PerformanceSnapshot>
            {
                new PerformanceSnapshot { ActionSucceeded = true, ActionType = "move", DecisionLatencyMs = 100, PerceptionComplexity = 10 },
                new PerformanceSnapshot { ActionSucceeded = true, ActionType = "move", DecisionLatencyMs = 120, PerceptionComplexity = 12 },
                new PerformanceSnapshot { ActionSucceeded = false, ActionType = "pickup", DecisionLatencyMs = 150, PerceptionComplexity = 15 }
            };

            // Act
            var analysis = analyzer.Analyze(snapshots);

            // Assert
            Assert.That(analysis.ActionTypeStats, Contains.Key("move"));
            Assert.That(analysis.ActionTypeStats, Contains.Key("pickup"));
            Assert.That(analysis.ActionTypeStats["move"].TotalCount, Is.EqualTo(2));
            Assert.That(analysis.ActionTypeStats["move"].SuccessCount, Is.EqualTo(2));
            Assert.That(analysis.ActionTypeStats["pickup"].TotalCount, Is.EqualTo(1));
            Assert.That(analysis.ActionTypeStats["pickup"].FailureCount, Is.EqualTo(1));
        }

        [Test]
        public void PerformanceAnalyzer_IdentifiesWeakness_LowSuccessRate()
        {
            // Arrange
            var analyzer = new PerformanceAnalyzer();
            var snapshots = new List<PerformanceSnapshot>();
            
            // Add 15 snapshots with low success rate (<50%)
            for (int i = 0; i < 15; i++)
            {
                snapshots.Add(new PerformanceSnapshot 
                { 
                    ActionSucceeded = i < 6, // Only 6 successful out of 15 = 40%
                    ActionType = "move",
                    DecisionLatencyMs = 100,
                    PerceptionComplexity = 10
                });
            }

            // Act
            var analysis = analyzer.Analyze(snapshots);

            // Assert
            Assert.That(analysis.IdentifiedWeaknesses, Is.Not.Empty);
            Assert.That(analysis.IdentifiedWeaknesses.Any(w => w.Contains("success rate")), Is.True);
        }

        [Test]
        public void PerformanceAnalyzer_IdentifiesWeakness_HighLatency()
        {
            // Arrange
            var analyzer = new PerformanceAnalyzer();
            var snapshots = new List<PerformanceSnapshot>
            {
                new PerformanceSnapshot { ActionSucceeded = true, ActionType = "move", DecisionLatencyMs = 5000, PerceptionComplexity = 10 },
                new PerformanceSnapshot { ActionSucceeded = true, ActionType = "move", DecisionLatencyMs = 6000, PerceptionComplexity = 12 },
                new PerformanceSnapshot { ActionSucceeded = true, ActionType = "move", DecisionLatencyMs = 5500, PerceptionComplexity = 15 }
            };

            // Act
            var analysis = analyzer.Analyze(snapshots);

            // Assert
            Assert.That(analysis.AverageDecisionLatencyMs, Is.GreaterThan(4000));
            Assert.That(analysis.Recommendations.Any(r => r.ToLower().Contains("latency")), Is.True);
        }

        [Test]
        public void PerformanceAnalyzer_CalculatesTrends_WithEnoughSnapshots()
        {
            // Arrange
            var analyzer = new PerformanceAnalyzer();
            var snapshots = new List<PerformanceSnapshot>();
            
            // First 10: high success rate
            for (int i = 0; i < 10; i++)
            {
                snapshots.Add(new PerformanceSnapshot { ActionSucceeded = true, ActionType = "move", DecisionLatencyMs = 100, PerceptionComplexity = 10 });
            }
            
            // Next 10: low success rate (degrading trend)
            for (int i = 0; i < 10; i++)
            {
                snapshots.Add(new PerformanceSnapshot { ActionSucceeded = i < 3, ActionType = "move", DecisionLatencyMs = 100, PerceptionComplexity = 10 });
            }

            // Act
            var analysis = analyzer.Analyze(snapshots);

            // Assert
            Assert.That(analysis.TrendMetrics, Is.Not.Empty);
            Assert.That(analysis.TrendMetrics.ContainsKey("SuccessRateTrend"), Is.True);
            
            // First half: 100%, Second half: 30%, so trend should be negative
            Assert.That(analysis.TrendMetrics["SuccessRateTrend"], Is.LessThan(0));
        }

        [Test]
        public void ReplayData_RecordsStepsCorrectly()
        {
            // Arrange
            var replay = new ReplayData
            {
                AgentId = "test-agent",
                SessionId = "test-session",
                CreatedAt = DateTime.UtcNow,
                FailureReason = "Multiple consecutive failures",
                TotalSteps = 5,
                Steps = new List<ReplayStep>
                {
                    new ReplayStep { StepNumber = 1, ActionType = "move", ActionSummary = "move forward", Succeeded = true, Timestamp = DateTime.UtcNow },
                    new ReplayStep { StepNumber = 2, ActionType = "move", ActionSummary = "move forward", Succeeded = false, Timestamp = DateTime.UtcNow },
                    new ReplayStep { StepNumber = 3, ActionType = "pickup", ActionSummary = "pickup key", Succeeded = false, Timestamp = DateTime.UtcNow }
                }
            };

            // Assert
            Assert.That(replay.Steps.Count, Is.EqualTo(3));
            Assert.That(replay.Steps.Count(s => !s.Succeeded), Is.EqualTo(2));
            Assert.That(replay.FailureReason, Is.Not.Empty);
        }
    }
}

