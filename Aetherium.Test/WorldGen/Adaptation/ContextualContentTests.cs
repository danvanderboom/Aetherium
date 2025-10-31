using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Server.Agents.Analysis;
using Aetherium.Server.Narrative;
using Aetherium.Server.WorldGen.Adaptation;

namespace Aetherium.Test.WorldGen.Adaptation
{
    [TestFixture]
    public class ContextualContentTests
    {
        [Test]
        public void GenerateContextualLootTable_WithNeeds_IncludesRelevantItems()
        {
            // Arrange
            var contentNeeds = new List<ContentNeed>
            {
                new ContentNeed
                {
                    NeedType = "navigation_assistance",
                    Priority = 0.8,
                    SuggestedContent = new List<string> { "compass", "map" }
                },
                new ContentNeed
                {
                    NeedType = "combat_assistance",
                    Priority = 0.6,
                    SuggestedContent = new List<string> { "health_potion" }
                }
            };

            // Act
            var lootTable = ContextualLootGenerator.GenerateContextualLootTable("table-1", contentNeeds);

            // Assert
            Assert.That(lootTable, Is.Not.Null);
            Assert.That(lootTable.Entries, Is.Not.Empty);
            Assert.That(lootTable.Entries.Any(e => e.ItemType == "compass"), Is.True);
            Assert.That(lootTable.Entries.Any(e => e.ItemType == "health_potion"), Is.True);
        }

        [Test]
        public void AdjustLootTable_IncreasesWeight_ForNeededItems()
        {
            // Arrange
            var existingTable = new LootTable
            {
                TableId = "table-1",
                Entries = new List<LootEntry>
                {
                    new LootEntry { ItemType = "compass", Weight = 50 },
                    new LootEntry { ItemType = "key", Weight = 30 }
                }
            };
            var contentNeeds = new List<ContentNeed>
            {
                new ContentNeed
                {
                    NeedType = "navigation_assistance",
                    Priority = 0.8,
                    SuggestedContent = new List<string> { "compass" }
                }
            };

            // Act
            var adjustedTable = ContextualLootGenerator.AdjustLootTable(existingTable, contentNeeds);

            // Assert
            Assert.That(adjustedTable, Is.Not.Null);
            var compassEntry = adjustedTable.Entries.FirstOrDefault(e => e.ItemType == "compass");
            Assert.That(compassEntry, Is.Not.Null);
            if (compassEntry != null)
            {
                Assert.That(compassEntry.Weight, Is.GreaterThan(50)); // Weight should increase
            }
        }

        [Test]
        public void AdaptiveNarrativeGenerator_GeneratesTokens_FromInterests()
        {
            // Arrange
            var behaviorAnalysis = new BehaviorAnalysis
            {
                AgentId = "test-agent",
                InteractionPatterns = new List<InteractionPattern>
                {
                    new InteractionPattern { EntityType = "key", InteractionCount = 10, SuccessRate = 0.8 }
                }
            };
            var interestProfile = new InterestProfile
            {
                AgentId = "test-agent",
                EngagingInteractions = new List<string> { "key", "door" }
            };

            // Act
            var tokens = AdaptiveNarrativeGenerator.GenerateNarrativeTokens(behaviorAnalysis, interestProfile);

            // Assert
            Assert.That(tokens, Is.Not.Empty);
            Assert.That(tokens.Any(t => t.TokenType.Contains("key") || t.TokenType.Contains("door")), Is.True);
        }

        [Test]
        public void AdaptiveNarrativeGenerator_GeneratesPOIs_FromStruggles()
        {
            // Arrange
            var behaviorAnalysis = new BehaviorAnalysis
            {
                AgentId = "test-agent",
                StrugglePatterns = new List<StrugglePattern>
                {
                    new StrugglePattern { ContextType = "navigation_failure", FailureCount = 5 }
                },
                SuccessPatterns = new List<SuccessPattern>
                {
                    new SuccessPattern { ContextType = "pickup_success", SuccessCount = 10, SuccessRate = 1.0 }
                }
            };

            // Act
            var pois = AdaptiveNarrativeGenerator.GenerateNarrativePOIs(behaviorAnalysis);

            // Assert
            Assert.That(pois, Is.Not.Empty);
            Assert.That(pois.Any(p => p.Name.Contains("navigation") || p.Name.Contains("Help")), Is.True);
            Assert.That(pois.Any(p => p.Name.Contains("Success") || p.Name.Contains("pickup")), Is.True);
        }
    }
}

