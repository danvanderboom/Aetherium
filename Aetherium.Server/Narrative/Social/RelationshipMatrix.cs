using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Server.Narrative;

namespace Aetherium.Server.Narrative.Social
{
    /// <summary>
    /// Represents the social relationship network between NPCs.
    /// Procedurally creates social graphs that influence dialogue and quests.
    /// </summary>
    public class RelationshipMatrix
    {
        /// <summary>
        /// NPC ID -> (NPC ID -> relationship value)
        /// Relationship values: -1.0 (enemy) to +1.0 (ally), 0.0 (neutral)
        /// </summary>
        private readonly Dictionary<string, Dictionary<string, float>> _relationships;

        /// <summary>
        /// NPC metadata: ID -> (property -> value)
        /// </summary>
        private readonly Dictionary<string, Dictionary<string, string>> _npcMetadata;

        public RelationshipMatrix()
        {
            _relationships = new Dictionary<string, Dictionary<string, float>>();
            _npcMetadata = new Dictionary<string, Dictionary<string, string>>();
        }

        /// <summary>
        /// Generates a procedural relationship network from NPC goals and types.
        /// </summary>
        public static RelationshipMatrix GenerateFromNPCGoals(
            List<NPCGoalDefinition> npcGoals,
            Random? random = null)
        {
            var rng = random ?? new Random();
            var matrix = new RelationshipMatrix();

            // Group NPCs by type
            var npcsByType = npcGoals.GroupBy(g => g.NPCType).ToList();
            var npcIds = npcGoals.Select(g => g.GoalId).ToList();

            // Initialize relationships
            foreach (var npcId in npcIds)
            {
                matrix._relationships[npcId] = new Dictionary<string, float>();
                matrix._npcMetadata[npcId] = new Dictionary<string, string>
                {
                    ["type"] = npcGoals.First(g => g.GoalId == npcId).NPCType
                };
            }

            // Generate relationships based on NPC types
            foreach (var typeGroup in npcsByType)
            {
                var npcIdsOfType = typeGroup.Select(g => g.GoalId).ToList();

                // Same type NPCs tend to be allies (0.3 to 0.7)
                foreach (var npc1 in npcIdsOfType)
                {
                    foreach (var npc2 in npcIdsOfType)
                    {
                        if (npc1 != npc2)
                        {
                            var relationship = 0.3f + (float)(rng.NextDouble() * 0.4); // 0.3 to 0.7
                            matrix.SetRelationship(npc1, npc2, relationship);
                        }
                    }
                }

                // Different types may have conflicts or alliances
                foreach (var otherTypeGroup in npcsByType)
                {
                    if (otherTypeGroup.Key == typeGroup.Key)
                        continue;

                    var relationshipType = DetermineRelationshipType(typeGroup.Key, otherTypeGroup.Key, rng);

                    foreach (var npc1 in npcIdsOfType)
                    {
                        foreach (var npc2 in otherTypeGroup.Select(g => g.GoalId))
                        {
                            float relationship = relationshipType switch
                            {
                                RelationshipType.Alliance => 0.2f + (float)(rng.NextDouble() * 0.3), // 0.2 to 0.5
                                RelationshipType.Neutral => -0.1f + (float)(rng.NextDouble() * 0.2), // -0.1 to 0.1
                                RelationshipType.Conflict => -0.5f - (float)(rng.NextDouble() * 0.3), // -0.5 to -0.8
                                _ => 0f
                            };

                            matrix.SetRelationship(npc1, npc2, relationship);
                        }
                    }
                }
            }

            // Add some random relationships for variety
            for (int i = 0; i < npcIds.Count * 2; i++)
            {
                var npc1 = npcIds[rng.Next(npcIds.Count)];
                var npc2 = npcIds[rng.Next(npcIds.Count)];

                if (npc1 != npc2 && !matrix._relationships[npc1].ContainsKey(npc2))
                {
                    var relationship = -0.3f + (float)(rng.NextDouble() * 0.6); // -0.3 to 0.3
                    matrix.SetRelationship(npc1, npc2, relationship);
                }
            }

            return matrix;
        }

        /// <summary>
        /// Sets the relationship between two NPCs.
        /// Relationships are symmetric by default.
        /// </summary>
        public void SetRelationship(string npc1Id, string npc2Id, float value)
        {
            // Clamp to valid range
            value = Math.Max(-1.0f, Math.Min(1.0f, value));

            if (!_relationships.ContainsKey(npc1Id))
            {
                _relationships[npc1Id] = new Dictionary<string, float>();
            }

            _relationships[npc1Id][npc2Id] = value;

            // Make symmetric
            if (!_relationships.ContainsKey(npc2Id))
            {
                _relationships[npc2Id] = new Dictionary<string, float>();
            }

            _relationships[npc2Id][npc1Id] = value;
        }

        /// <summary>
        /// Gets the relationship value between two NPCs.
        /// </summary>
        public float? GetRelationship(string npc1Id, string npc2Id)
        {
            if (_relationships.TryGetValue(npc1Id, out var relationships))
            {
                if (relationships.TryGetValue(npc2Id, out var value))
                {
                    return value;
                }
            }

            return null; // No relationship defined
        }

        /// <summary>
        /// Gets all NPCs that have relationships with the given NPC.
        /// </summary>
        public List<string> GetRelatedNPCs(string npcId)
        {
            if (_relationships.TryGetValue(npcId, out var relationships))
            {
                return relationships.Keys.ToList();
            }

            return new List<string>();
        }

        /// <summary>
        /// Gets all NPCs of a specific relationship type (ally/enemy/neutral).
        /// </summary>
        public List<string> GetNPCsByRelationship(string npcId, RelationshipCategory category)
        {
            var relatedNPCs = new List<string>();

            if (!_relationships.TryGetValue(npcId, out var relationships))
                return relatedNPCs;

            foreach (var (otherNpcId, value) in relationships)
            {
                var matches = category switch
                {
                    RelationshipCategory.Ally => value > 0.3f,
                    RelationshipCategory.Enemy => value < -0.3f,
                    RelationshipCategory.Neutral => value >= -0.3f && value <= 0.3f,
                    _ => false
                };

                if (matches)
                {
                    relatedNPCs.Add(otherNpcId);
                }
            }

            return relatedNPCs;
        }

        /// <summary>
        /// Determines the general relationship type between two NPC types.
        /// </summary>
        private static RelationshipType DetermineRelationshipType(
            string type1,
            string type2,
            Random random)
        {
            // Define relationship patterns
            var conflictPairs = new[]
            {
                ("guard", "bandit"),
                ("merchant", "thief"),
                ("citizen", "raider")
            };

            var alliancePairs = new[]
            {
                ("guard", "citizen"),
                ("merchant", "citizen"),
                ("guard", "guard")
            };

            foreach (var (t1, t2) in conflictPairs)
            {
                if ((type1.Equals(t1, StringComparison.OrdinalIgnoreCase) && type2.Equals(t2, StringComparison.OrdinalIgnoreCase)) ||
                    (type1.Equals(t2, StringComparison.OrdinalIgnoreCase) && type2.Equals(t1, StringComparison.OrdinalIgnoreCase)))
                {
                    return RelationshipType.Conflict;
                }
            }

            foreach (var (t1, t2) in alliancePairs)
            {
                if ((type1.Equals(t1, StringComparison.OrdinalIgnoreCase) && type2.Equals(t2, StringComparison.OrdinalIgnoreCase)) ||
                    (type1.Equals(t2, StringComparison.OrdinalIgnoreCase) && type2.Equals(t1, StringComparison.OrdinalIgnoreCase)))
                {
                    return RelationshipType.Alliance;
                }
            }

            // Default to neutral with some randomness
            return random.NextDouble() < 0.5 ? RelationshipType.Neutral : RelationshipType.Alliance;
        }

        /// <summary>
        /// Gets the NPC type metadata.
        /// </summary>
        public string? GetNPCType(string npcId)
        {
            if (_npcMetadata.TryGetValue(npcId, out var metadata))
            {
                return metadata.TryGetValue("type", out var type) ? type : null;
            }

            return null;
        }
    }

    /// <summary>
    /// General relationship type between NPC groups.
    /// </summary>
    public enum RelationshipType
    {
        Alliance,
        Neutral,
        Conflict
    }

    /// <summary>
    /// Relationship category for filtering NPCs.
    /// </summary>
    public enum RelationshipCategory
    {
        Ally,
        Enemy,
        Neutral
    }
}

