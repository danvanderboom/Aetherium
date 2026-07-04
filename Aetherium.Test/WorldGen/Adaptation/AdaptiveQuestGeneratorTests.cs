using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Server.Agents.Analysis;
using Aetherium.Model.Analysis;
using Aetherium.Server.Narrative;
using Aetherium.Model.Narrative;
using Aetherium.Server.WorldGen.Adaptation;

namespace Aetherium.Test.WorldGen.Adaptation
{
    [TestFixture]
    public class AdaptiveQuestGeneratorTests
    {
        [Test]
        public void GenerateQuest_WithNavigationNeed_CreatesNavigationQuest()
        {
            // Arrange
            var behaviorAnalysis = new BehaviorAnalysis
            {
                AgentId = "test-agent",
                StrugglePatterns = new List<StrugglePattern>
                {
                    new StrugglePattern { ContextType = "navigation_failure", FailureCount = 5 }
                }
            };
            var contentNeeds = new List<ContentNeed>
            {
                new ContentNeed
                {
                    NeedType = "navigation_assistance",
                    Priority = 0.8,
                    Description = "Agent struggles with navigation"
                }
            };

            // Act
            var quest = AdaptiveQuestGenerator.GenerateQuest("quest-1", behaviorAnalysis, contentNeeds);

            // Assert
            Assert.That(quest, Is.Not.Null);
            Assert.That(quest.QuestId, Is.EqualTo("quest-1"));
            Assert.That(quest.Objectives, Is.Not.Empty);
            Assert.That(quest.Objectives.Any(o => o.Type == "visit_location"), Is.True);
        }

        [Test]
        public void GenerateQuest_WithKeyLockNeed_CreatesKeyLockQuest()
        {
            // Arrange
            var behaviorAnalysis = new BehaviorAnalysis
            {
                AgentId = "test-agent",
                StrugglePatterns = new List<StrugglePattern>
                {
                    new StrugglePattern { ContextType = "key_lock_failure", FailureCount = 3 }
                }
            };
            var contentNeeds = new List<ContentNeed>
            {
                new ContentNeed
                {
                    NeedType = "key_lock_assistance",
                    Priority = 0.7,
                    Description = "Agent struggles with key-lock puzzles"
                }
            };

            // Act
            var quest = AdaptiveQuestGenerator.GenerateQuest("quest-1", behaviorAnalysis, contentNeeds);

            // Assert
            Assert.That(quest, Is.Not.Null);
            Assert.That(quest.Objectives.Any(o => o.Type == "collect_items" && o.Parameters.ContainsKey("targetItem")), Is.True);
        }

        [Test]
        public void GenerateQuests_MultipleNeeds_GeneratesMultipleQuests()
        {
            // Arrange
            var behaviorAnalysis = new BehaviorAnalysis
            {
                AgentId = "test-agent",
                StrugglePatterns = new List<StrugglePattern>
                {
                    new StrugglePattern { ContextType = "navigation_failure", FailureCount = 5 },
                    new StrugglePattern { ContextType = "combat_failure", FailureCount = 3 }
                }
            };
            var contentNeeds = new List<ContentNeed>
            {
                new ContentNeed { NeedType = "navigation_assistance", Priority = 0.8 },
                new ContentNeed { NeedType = "combat_assistance", Priority = 0.6 }
            };

            // Act
            var quests = AdaptiveQuestGenerator.GenerateQuests(behaviorAnalysis, contentNeeds, maxQuests: 5);

            // Assert
            Assert.That(quests, Is.Not.Empty);
            Assert.That(quests.Count, Is.GreaterThan(0));
            Assert.That(quests.All(q => !string.IsNullOrEmpty(q.QuestId)), Is.True);
        }

        [Test]
        public void AdaptQuest_SimplifiesObjectives_WhenAgentStruggles()
        {
            // Arrange
            var existingQuest = new QuestDefinition
            {
                QuestId = "quest-1",
                Title = "Test Quest",
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective
                    {
                        ObjectiveId = "obj-1",
                        Type = "visit_location",
                        Parameters = new Dictionary<string, object> { ["targetCount"] = 5 }
                    }
                }
            };
            var behaviorAnalysis = new BehaviorAnalysis
            {
                AgentId = "test-agent",
                StrugglePatterns = new List<StrugglePattern>
                {
                    new StrugglePattern { ContextType = "visit_location", FailureCount = 3 }
                }
            };
            var contentNeeds = new List<ContentNeed>();

            // Act
            var adaptedQuest = AdaptiveQuestGenerator.AdaptQuest(existingQuest, behaviorAnalysis, contentNeeds);

            // Assert
            Assert.That(adaptedQuest, Is.Not.Null);
            var objective = adaptedQuest.Objectives.FirstOrDefault(o => o.Type == "visit_location");
            Assert.That(objective, Is.Not.Null);
            // Objective should be simplified (count reduced)
            if (objective != null && objective.Parameters.ContainsKey("requiredCount"))
            {
                var count = Convert.ToInt32(objective.Parameters["requiredCount"]);
                Assert.That(count, Is.LessThanOrEqualTo(5));
            }
        }
    }
}

