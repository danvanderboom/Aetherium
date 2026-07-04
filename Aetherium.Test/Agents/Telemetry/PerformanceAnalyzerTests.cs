using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Server.Agents.Telemetry;
using Aetherium.Model.Telemetry;

namespace Aetherium.Test.Agents.Telemetry
{
    [TestFixture]
    public class PerformanceAnalyzerTests
    {
        [Test]
        public void Analyze_ZeroSnapshots_ReturnsEmptyAnalysis()
        {
            // Arrange
            var snapshots = new List<PerformanceSnapshot>();
            var agentId = "test-agent";

            // Act
            var analysis = PerformanceAnalyzer.Analyze(snapshots, agentId);

            // Assert
            Assert.That(analysis, Is.Not.Null);
            Assert.That(analysis.TotalSteps, Is.EqualTo(0));
            Assert.That(analysis.SuccessRate, Is.EqualTo(0.0));
            Assert.That(analysis.ActionTypeStats, Is.Empty);
            Assert.That(analysis.IdentifiedWeaknesses, Is.Empty);
            Assert.That(analysis.Recommendations, Is.Empty);
        }

        [Test]
        public void Analyze_SingleSuccessfulSnapshot_Returns100PercentSuccessRate()
        {
            // Arrange
            var agentId = "test-agent";
            var snapshots = new List<PerformanceSnapshot>
            {
                new PerformanceSnapshot
                {
                    AgentId = agentId,
                    ActionSucceeded = true,
                    ActionType = "move",
                    DecisionLatencyMs = 100,
                    PerceptionComplexity = 10
                }
            };

            // Act
            var analysis = PerformanceAnalyzer.Analyze(snapshots, agentId);

            // Assert
            Assert.That(analysis.TotalSteps, Is.EqualTo(1));
            Assert.That(analysis.TotalSuccessfulActions, Is.EqualTo(1));
            Assert.That(analysis.TotalFailedActions, Is.EqualTo(0));
            Assert.That(analysis.SuccessRate, Is.EqualTo(1.0));
            Assert.That(analysis.AverageDecisionLatencyMs, Is.EqualTo(100.0));
        }

        [Test]
        public void Analyze_MultipleSnapshots_CalculatesCorrectMetrics()
        {
            // Arrange
            var agentId = "test-agent";
            var snapshots = new List<PerformanceSnapshot>
            {
                new PerformanceSnapshot { AgentId = agentId, ActionSucceeded = true, ActionType = "move", DecisionLatencyMs = 100, PerceptionComplexity = 10 },
                new PerformanceSnapshot { AgentId = agentId, ActionSucceeded = true, ActionType = "pickup", DecisionLatencyMs = 150, PerceptionComplexity = 15 },
                new PerformanceSnapshot { AgentId = agentId, ActionSucceeded = false, ActionType = "move", DecisionLatencyMs = 120, PerceptionComplexity = 12 },
                new PerformanceSnapshot { AgentId = agentId, ActionSucceeded = true, ActionType = "move", DecisionLatencyMs = 110, PerceptionComplexity = 11 }
            };

            // Act
            var analysis = PerformanceAnalyzer.Analyze(snapshots, agentId);

            // Assert
            Assert.That(analysis.TotalSteps, Is.EqualTo(4));
            Assert.That(analysis.TotalSuccessfulActions, Is.EqualTo(3));
            Assert.That(analysis.TotalFailedActions, Is.EqualTo(1));
            Assert.That(analysis.SuccessRate, Is.EqualTo(0.75));
            Assert.That(analysis.AverageDecisionLatencyMs, Is.EqualTo((100 + 150 + 120 + 110) / 4.0));
            Assert.That(analysis.AveragePerceptionComplexity, Is.EqualTo((10 + 15 + 12 + 11) / 4.0));
        }

        [Test]
        public void Analyze_GroupsByActionType()
        {
            // Arrange
            var agentId = "test-agent";
            var snapshots = new List<PerformanceSnapshot>
            {
                new PerformanceSnapshot { AgentId = agentId, ActionSucceeded = true, ActionType = "move", DecisionLatencyMs = 100, PerceptionComplexity = 10 },
                new PerformanceSnapshot { AgentId = agentId, ActionSucceeded = true, ActionType = "move", DecisionLatencyMs = 120, PerceptionComplexity = 12 },
                new PerformanceSnapshot { AgentId = agentId, ActionSucceeded = false, ActionType = "pickup", DecisionLatencyMs = 150, PerceptionComplexity = 15 },
                new PerformanceSnapshot { AgentId = agentId, ActionSucceeded = true, ActionType = "interact", DecisionLatencyMs = 200, PerceptionComplexity = 20 }
            };

            // Act
            var analysis = PerformanceAnalyzer.Analyze(snapshots, agentId);

            // Assert
            Assert.That(analysis.ActionTypeStats.Count, Is.EqualTo(3));
            Assert.That(analysis.ActionTypeStats.ContainsKey("move"), Is.True);
            Assert.That(analysis.ActionTypeStats.ContainsKey("pickup"), Is.True);
            Assert.That(analysis.ActionTypeStats.ContainsKey("interact"), Is.True);
            
            Assert.That(analysis.ActionTypeStats["move"].TotalCount, Is.EqualTo(2));
            Assert.That(analysis.ActionTypeStats["move"].SuccessCount, Is.EqualTo(2));
            Assert.That(analysis.ActionTypeStats["move"].SuccessRate, Is.EqualTo(1.0));
            
            Assert.That(analysis.ActionTypeStats["pickup"].TotalCount, Is.EqualTo(1));
            Assert.That(analysis.ActionTypeStats["pickup"].FailureCount, Is.EqualTo(1));
            Assert.That(analysis.ActionTypeStats["pickup"].SuccessRate, Is.EqualTo(0.0));
        }

        [Test]
        public void Analyze_IdentifiesWeakness_LowOverallSuccessRate()
        {
            // Arrange
            var agentId = "test-agent";
            var snapshots = new List<PerformanceSnapshot>();
            
            // 15 snapshots with 40% success rate (<50%)
            for (int i = 0; i < 15; i++)
            {
                snapshots.Add(new PerformanceSnapshot
                {
                    AgentId = agentId,
                    ActionSucceeded = i < 6, // 6 successful out of 15 = 40%
                    ActionType = "move",
                    DecisionLatencyMs = 100,
                    PerceptionComplexity = 10
                });
            }

            // Act
            var analysis = PerformanceAnalyzer.Analyze(snapshots, agentId);

            // Assert
            Assert.That(analysis.IdentifiedWeaknesses, Is.Not.Empty);
            Assert.That(analysis.IdentifiedWeaknesses.Any(w => w.ToLower().Contains("success rate")), Is.True);
        }

        [Test]
        public void Analyze_IdentifiesWeakness_HighLatency()
        {
            // Arrange
            var agentId = "test-agent";
            var snapshots = new List<PerformanceSnapshot>
            {
                new PerformanceSnapshot { AgentId = agentId, ActionSucceeded = true, ActionType = "move", DecisionLatencyMs = 5000, PerceptionComplexity = 10 },
                new PerformanceSnapshot { AgentId = agentId, ActionSucceeded = true, ActionType = "move", DecisionLatencyMs = 6000, PerceptionComplexity = 12 },
                new PerformanceSnapshot { AgentId = agentId, ActionSucceeded = true, ActionType = "move", DecisionLatencyMs = 5500, PerceptionComplexity = 15 }
            };

            // Act
            var analysis = PerformanceAnalyzer.Analyze(snapshots, agentId);

            // Assert
            Assert.That(analysis.AverageDecisionLatencyMs, Is.GreaterThan(4000));
            Assert.That(analysis.Recommendations, Is.Not.Empty);
            Assert.That(analysis.Recommendations.Any(r => r.ToLower().Contains("latency") || r.ToLower().Contains("decision")), Is.True);
        }

        [Test]
        public void Analyze_IdentifiesWeakness_LowActionTypeSuccessRate()
        {
            // Arrange
            var agentId = "test-agent";
            var snapshots = new List<PerformanceSnapshot>();
            
            // 10 "pickup" actions with 20% success rate
            for (int i = 0; i < 10; i++)
            {
                snapshots.Add(new PerformanceSnapshot
                {
                    AgentId = agentId,
                    ActionSucceeded = i < 2, // 2 successful out of 10 = 20%
                    ActionType = "pickup",
                    DecisionLatencyMs = 100,
                    PerceptionComplexity = 10
                });
            }

            // Act
            var analysis = PerformanceAnalyzer.Analyze(snapshots, agentId);

            // Assert
            Assert.That(analysis.ActionTypeStats.ContainsKey("pickup"), Is.True);
            Assert.That(analysis.ActionTypeStats["pickup"].SuccessRate, Is.LessThan(0.5));
            Assert.That(analysis.IdentifiedWeaknesses.Any(w => w.ToLower().Contains("pickup")), Is.True);
        }

        [Test]
        public void Analyze_GeneratesRecommendations_ForWeaknesses()
        {
            // Arrange
            var agentId = "test-agent";
            var snapshots = new List<PerformanceSnapshot>();
            
            // Low success rate snapshots
            for (int i = 0; i < 15; i++)
            {
                snapshots.Add(new PerformanceSnapshot
                {
                    AgentId = agentId,
                    ActionSucceeded = i < 5, // 33% success rate
                    ActionType = "move",
                    DecisionLatencyMs = 100,
                    PerceptionComplexity = 10
                });
            }

            // Act
            var analysis = PerformanceAnalyzer.Analyze(snapshots, agentId);

            // Assert
            Assert.That(analysis.Recommendations, Is.Not.Empty);
            Assert.That(analysis.Recommendations.Any(r => r.Length > 0), Is.True);
        }

        [Test]
        public void Analyze_CalculatesTrends_WithSufficientSnapshots()
        {
            // Arrange
            var agentId = "test-agent";
            var snapshots = new List<PerformanceSnapshot>();
            
            // First 10: high success rate
            for (int i = 0; i < 10; i++)
            {
                snapshots.Add(new PerformanceSnapshot
                {
                    AgentId = agentId,
                    ActionSucceeded = true,
                    ActionType = "move",
                    DecisionLatencyMs = 100,
                    PerceptionComplexity = 10
                });
            }
            
            // Next 10: low success rate (degrading trend)
            for (int i = 0; i < 10; i++)
            {
                snapshots.Add(new PerformanceSnapshot
                {
                    AgentId = agentId,
                    ActionSucceeded = i < 3, // 30% success rate
                    ActionType = "move",
                    DecisionLatencyMs = 100,
                    PerceptionComplexity = 10
                });
            }

            // Act
            var analysis = PerformanceAnalyzer.Analyze(snapshots, agentId);

            // Assert
            Assert.That(analysis.TrendMetrics, Is.Not.Empty);
            Assert.That(analysis.TrendMetrics.ContainsKey("success_rate_trend"), Is.True);
            
            // First half: 100%, Second half: 30%, so trend should be negative
            var trend = analysis.TrendMetrics["success_rate_trend"];
            Assert.That(trend, Is.LessThan(0));
        }

        [Test]
        public void Analyze_NoTrends_WithInsufficientSnapshots()
        {
            // Arrange
            var agentId = "test-agent";
            var snapshots = new List<PerformanceSnapshot>();
            
            // Only 9 snapshots (< 10 required for trend calculation)
            for (int i = 0; i < 9; i++)
            {
                snapshots.Add(new PerformanceSnapshot
                {
                    AgentId = agentId,
                    ActionSucceeded = true,
                    ActionType = "move",
                    DecisionLatencyMs = 100,
                    PerceptionComplexity = 10
                });
            }

            // Act
            var analysis = PerformanceAnalyzer.Analyze(snapshots, agentId);

            // Assert
            Assert.That(analysis.TrendMetrics, Is.Empty);
        }
    }
}

