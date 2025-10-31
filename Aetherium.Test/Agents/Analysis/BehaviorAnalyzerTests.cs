using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Server.Agents.Analysis;
using Aetherium.Server.Agents.Telemetry;

namespace Aetherium.Test.Agents.Analysis
{
    [TestFixture]
    public class BehaviorAnalyzerTests
    {
        [Test]
        public void AnalyzeBehavior_EmptySnapshots_ReturnsEmptyAnalysis()
        {
            // Arrange
            var snapshots = new List<PerformanceSnapshot>();

            // Act
            var analysis = BehaviorAnalyzer.AnalyzeBehavior(snapshots);

            // Assert
            Assert.That(analysis, Is.Not.Null);
            Assert.That(analysis.TotalSteps, Is.EqualTo(0));
            Assert.That(analysis.ActionPatterns, Is.Empty);
            Assert.That(analysis.StrugglePatterns, Is.Empty);
            Assert.That(analysis.SuccessPatterns, Is.Empty);
        }

        [Test]
        public void AnalyzeBehavior_WithSnapshots_IdentifiesActionPatterns()
        {
            // Arrange
            var snapshots = new List<PerformanceSnapshot>
            {
                new PerformanceSnapshot { AgentId = "test-agent", ActionType = "move", ActionSucceeded = true, DecisionLatencyMs = 100, Timestamp = DateTime.UtcNow },
                new PerformanceSnapshot { AgentId = "test-agent", ActionType = "move", ActionSucceeded = true, DecisionLatencyMs = 120, Timestamp = DateTime.UtcNow },
                new PerformanceSnapshot { AgentId = "test-agent", ActionType = "pickup", ActionSucceeded = false, DecisionLatencyMs = 150, Timestamp = DateTime.UtcNow }
            };

            // Act
            var analysis = BehaviorAnalyzer.AnalyzeBehavior(snapshots);

            // Assert
            Assert.That(analysis.ActionPatterns, Is.Not.Empty);
            Assert.That(analysis.ActionPatterns.Any(p => p.ActionType == "move"), Is.True);
            Assert.That(analysis.ActionPatterns.Any(p => p.ActionType == "pickup"), Is.True);
            
            var movePattern = analysis.ActionPatterns.First(p => p.ActionType == "move");
            Assert.That(movePattern.TotalCount, Is.EqualTo(2));
            Assert.That(movePattern.SuccessRate, Is.EqualTo(1.0));
        }

        [Test]
        public void AnalyzeBehavior_IdentifiesStrugglePatterns()
        {
            // Arrange
            var snapshots = new List<PerformanceSnapshot>();
            
            // Add failure sequence
            for (int i = 0; i < 5; i++)
            {
                snapshots.Add(new PerformanceSnapshot
                {
                    AgentId = "test-agent",
                    ActionType = "move",
                    ActionSucceeded = false,
                    ErrorMessage = "navigation failed",
                    Timestamp = DateTime.UtcNow.AddSeconds(i)
                });
            }

            // Add success to break sequence
            snapshots.Add(new PerformanceSnapshot
            {
                AgentId = "test-agent",
                ActionType = "move",
                ActionSucceeded = true,
                Timestamp = DateTime.UtcNow.AddSeconds(10)
            });

            // Act
            var analysis = BehaviorAnalyzer.AnalyzeBehavior(snapshots);

            // Assert
            Assert.That(analysis.StrugglePatterns, Is.Not.Empty);
            Assert.That(analysis.StrugglePatterns.Any(s => s.ContextType.Contains("navigation")), Is.True);
        }

        [Test]
        public void AnalyzeBehavior_IdentifiesSuccessPatterns()
        {
            // Arrange
            var snapshots = new List<PerformanceSnapshot>();
            
            // Add successful actions
            for (int i = 0; i < 10; i++)
            {
                snapshots.Add(new PerformanceSnapshot
                {
                    AgentId = "test-agent",
                    ActionType = "pickup",
                    ActionSucceeded = true,
                    Timestamp = DateTime.UtcNow.AddSeconds(i)
                });
            }

            // Act
            var analysis = BehaviorAnalyzer.AnalyzeBehavior(snapshots);

            // Assert
            Assert.That(analysis.SuccessPatterns, Is.Not.Empty);
            Assert.That(analysis.SuccessPatterns.Any(s => s.ContextType.Contains("pickup")), Is.True);
        }

        [Test]
        public void BuildInterestProfile_FromAnalysis_CreatesProfile()
        {
            // Arrange
            var snapshots = new List<PerformanceSnapshot>
            {
                new PerformanceSnapshot { AgentId = "test-agent", ActionType = "move", ActionSucceeded = true, Timestamp = DateTime.UtcNow },
                new PerformanceSnapshot { AgentId = "test-agent", ActionType = "pickup", ActionSucceeded = true, ActionSummary = "pickup key", Timestamp = DateTime.UtcNow }
            };
            var analysis = BehaviorAnalyzer.AnalyzeBehavior(snapshots);

            // Act
            var profile = BehaviorAnalyzer.BuildInterestProfile(analysis);

            // Assert
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.AgentId, Is.EqualTo("test-agent"));
            Assert.That(profile.ActionPreferences, Is.Not.Empty);
            Assert.That(profile.ActionPreferences.ContainsKey("move"), Is.True);
        }

        [Test]
        public void MapWeaknessesToContentNeeds_WithStruggles_GeneratesNeeds()
        {
            // Arrange
            var snapshots = new List<PerformanceSnapshot>();
            
            // Add navigation failures
            for (int i = 0; i < 10; i++)
            {
                snapshots.Add(new PerformanceSnapshot
                {
                    AgentId = "test-agent",
                    ActionType = "move",
                    ActionSucceeded = false,
                    ErrorMessage = "navigation failed",
                    Timestamp = DateTime.UtcNow.AddSeconds(i)
                });
            }

            var analysis = BehaviorAnalyzer.AnalyzeBehavior(snapshots);

            // Act
            var needs = BehaviorAnalyzer.MapWeaknessesToContentNeeds(analysis);

            // Assert
            Assert.That(needs, Is.Not.Empty);
            Assert.That(needs.Any(n => n.NeedType == "navigation_assistance"), Is.True);
        }

        [Test]
        public void MapWeaknessesToContentNeeds_KeyLockStruggles_GeneratesKeyLockNeeds()
        {
            // Arrange
            var snapshots = new List<PerformanceSnapshot>();
            
            // Add key-lock failures
            for (int i = 0; i < 5; i++)
            {
                snapshots.Add(new PerformanceSnapshot
                {
                    AgentId = "test-agent",
                    ActionType = "pickup",
                    ActionSucceeded = false,
                    ErrorMessage = "key required",
                    ActionSummary = "pickup key",
                    Timestamp = DateTime.UtcNow.AddSeconds(i)
                });
            }

            var analysis = BehaviorAnalyzer.AnalyzeBehavior(snapshots);

            // Act
            var needs = BehaviorAnalyzer.MapWeaknessesToContentNeeds(analysis);

            // Assert
            Assert.That(needs.Any(n => n.NeedType == "key_lock_assistance"), Is.True);
        }
    }
}

