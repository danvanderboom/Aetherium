using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Server.Agents.Analysis;
using Aetherium.Server.Agents.Telemetry;
using Aetherium.Server.Narrative;
using Aetherium.Server.WorldGen.Adaptation;

namespace Aetherium.Test.WorldGen.Adaptation
{
    [TestFixture]
    public class AdaptationIntegrationTests
    {
        [Test]
        public void EndToEnd_BehaviorAnalysisToQuestGeneration_Works()
        {
            // Arrange - Create telemetry snapshots
            var snapshots = new List<PerformanceSnapshot>
            {
                new PerformanceSnapshot { AgentId = "test-agent", ActionType = "move", ActionSucceeded = false, ErrorMessage = "navigation failed", Timestamp = DateTime.UtcNow },
                new PerformanceSnapshot { AgentId = "test-agent", ActionType = "move", ActionSucceeded = false, ErrorMessage = "navigation failed", Timestamp = DateTime.UtcNow },
                new PerformanceSnapshot { AgentId = "test-agent", ActionType = "move", ActionSucceeded = false, ErrorMessage = "navigation failed", Timestamp = DateTime.UtcNow }
            };

            // Act - Analyze behavior
            var behaviorAnalysis = BehaviorAnalyzer.AnalyzeBehavior(snapshots);

            // Act - Build interest profile
            var interestProfile = BehaviorAnalyzer.BuildInterestProfile(behaviorAnalysis);

            // Act - Map to content needs
            var contentNeeds = BehaviorAnalyzer.MapWeaknessesToContentNeeds(behaviorAnalysis);

            // Act - Generate adaptive quest
            var quest = AdaptiveQuestGenerator.GenerateQuest("quest-1", behaviorAnalysis, contentNeeds, interestProfile);

            // Assert
            Assert.That(behaviorAnalysis, Is.Not.Null);
            Assert.That(interestProfile, Is.Not.Null);
            Assert.That(contentNeeds, Is.Not.Empty);
            Assert.That(quest, Is.Not.Null);
            Assert.That(quest.Objectives, Is.Not.Empty);
        }

        [Test]
        public void EndToEnd_BehaviorAnalysisToLootGeneration_Works()
        {
            // Arrange
            var snapshots = new List<PerformanceSnapshot>
            {
                new PerformanceSnapshot { AgentId = "test-agent", ActionType = "pickup", ActionSucceeded = false, ErrorMessage = "key required", Timestamp = DateTime.UtcNow }
            };

            // Act
            var behaviorAnalysis = BehaviorAnalyzer.AnalyzeBehavior(snapshots);
            var contentNeeds = BehaviorAnalyzer.MapWeaknessesToContentNeeds(behaviorAnalysis);
            var lootTable = ContextualLootGenerator.GenerateContextualLootTable("table-1", contentNeeds);

            // Assert
            Assert.That(contentNeeds, Is.Not.Empty);
            Assert.That(lootTable, Is.Not.Null);
            Assert.That(lootTable.Entries.Any(e => e.ItemType == "key"), Is.True);
        }

        [Test]
        public void QuestAdaptationRule_AppliesTo_WithMatchingConditions()
        {
            // Arrange
            var rule = new QuestAdaptationRule
            {
                Name = "Navigation Help",
                Conditions = new QuestAdaptationCondition
                {
                    MinWeaknessPriority = 0.6,
                    RequiredWeaknessTypes = new List<string> { "navigation_assistance" }
                }
            };
            var behaviorAnalysis = new BehaviorAnalysis { AgentId = "test-agent" };
            var contentNeeds = new List<ContentNeed>
            {
                new ContentNeed { NeedType = "navigation_assistance", Priority = 0.8 }
            };

            // Act
            var applies = rule.AppliesTo(behaviorAnalysis, contentNeeds);

            // Assert
            Assert.That(applies, Is.True);
        }

        [Test]
        public void QuestAdaptationRule_DoesNotApply_WithNonMatchingConditions()
        {
            // Arrange
            var rule = new QuestAdaptationRule
            {
                Name = "Combat Help",
                Conditions = new QuestAdaptationCondition
                {
                    RequiredWeaknessTypes = new List<string> { "combat_assistance" }
                }
            };
            var behaviorAnalysis = new BehaviorAnalysis { AgentId = "test-agent" };
            var contentNeeds = new List<ContentNeed>
            {
                new ContentNeed { NeedType = "navigation_assistance", Priority = 0.8 }
            };

            // Act
            var applies = rule.AppliesTo(behaviorAnalysis, contentNeeds);

            // Assert
            Assert.That(applies, Is.False);
        }
    }
}

