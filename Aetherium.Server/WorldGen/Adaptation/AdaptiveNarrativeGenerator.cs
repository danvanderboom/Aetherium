using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Server.Agents.Analysis;
using Aetherium.Model.Analysis;
using Aetherium.WorldGen;

namespace Aetherium.Server.WorldGen.Adaptation
{
    /// <summary>
    /// Generates adaptive narrative elements based on agent behavior.
    /// </summary>
    public static class AdaptiveNarrativeGenerator
    {
        /// <summary>
        /// Generates narrative tokens based on agent interests and behavior.
        /// </summary>
        public static List<NarrativeTokenRequest> GenerateNarrativeTokens(
            BehaviorAnalysis behaviorAnalysis,
            InterestProfile? interestProfile = null)
        {
            var tokens = new List<NarrativeTokenRequest>();

            // Generate tokens based on agent interests
            if (interestProfile != null)
            {
                // Add tokens for engaging interactions
                foreach (var interaction in interestProfile.EngagingInteractions.Take(3))
                {
                    var token = new NarrativeTokenRequest
                    {
                        TokenId = Guid.NewGuid().ToString("N"),
                        TokenType = $"entity_{interaction}"
                    };
                    token.Parameters["preference"] = "high";
                    token.Parameters["description"] = $"Agent prefers {interaction} interactions";
                    tokens.Add(token);
                }
            }

            // Generate tokens based on preferred content types
            if (interestProfile != null && interestProfile.PreferredContentTypes.Count > 0)
            {
                foreach (var contentType in interestProfile.PreferredContentTypes.Take(2))
                {
                    var token = new NarrativeTokenRequest
                    {
                        TokenId = Guid.NewGuid().ToString("N"),
                        TokenType = $"content_{contentType}"
                    };
                    token.Parameters["preference"] = "high";
                    token.Parameters["description"] = $"Agent prefers {contentType} content";
                    tokens.Add(token);
                }
            }

            return tokens;
        }

        /// <summary>
        /// Generates narrative points of interest based on agent behavior.
        /// </summary>
        public static List<NarrativePointOfInterest> GenerateNarrativePOIs(
            BehaviorAnalysis behaviorAnalysis,
            InterestProfile? interestProfile = null)
        {
            var pois = new List<NarrativePointOfInterest>();

            // Generate POIs based on struggle patterns (places where agent needs help)
            foreach (var struggle in behaviorAnalysis.StrugglePatterns.Take(3))
            {
                var poi = new NarrativePointOfInterest
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = $"Help_{struggle.ContextType}",
                    PreferredTerrain = InferTerrainFromStruggle(struggle),
                    Importance = WorldPoiImportance.Preferred
                };
                pois.Add(poi);
            }

            // Generate POIs based on success patterns (places agent enjoys)
            foreach (var success in behaviorAnalysis.SuccessPatterns.Take(2))
            {
                var poi = new NarrativePointOfInterest
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = $"Success_{success.ContextType}",
                    PreferredTerrain = InferTerrainFromSuccess(success),
                    Importance = WorldPoiImportance.Optional
                };
                pois.Add(poi);
            }

            return pois;
        }

        /// <summary>
        /// Adapts narrative generation constraints based on agent behavior.
        /// </summary>
        public static NarrativeGenerationConstraints AdaptNarrativeConstraints(
            NarrativeGenerationConstraints baseConstraints,
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds)
        {
            var adapted = new NarrativeGenerationConstraints
            {
                NarrativeId = baseConstraints.NarrativeId
            };

            // Start with base tokens
            adapted.Tokens.AddRange(baseConstraints.Tokens);

            // Add adaptive tokens
            var adaptiveTokens = GenerateAdaptiveTokens(behaviorAnalysis, contentNeeds);
            adapted.Tokens.AddRange(adaptiveTokens);

            // Start with base POIs
            adapted.RequiredPoints.AddRange(baseConstraints.RequiredPoints);

            // Add adaptive POIs
            var adaptivePOIs = GenerateAdaptivePOIs(behaviorAnalysis, contentNeeds);
            adapted.RequiredPoints.AddRange(adaptivePOIs);

            // Adjust difficulty by depth based on behavior
            var adaptedDifficulty = AdaptDifficultyByDepth(
                baseConstraints.DifficultyByDepth,
                behaviorAnalysis,
                contentNeeds);
            foreach (var kvp in adaptedDifficulty)
            {
                adapted.DifficultyByDepth[kvp.Key] = kvp.Value;
            }

            return adapted;
        }

        /// <summary>
        /// Generates contextual descriptions based on agent context.
        /// </summary>
        public static string GenerateContextualDescription(
            BehaviorAnalysis behaviorAnalysis,
            InterestProfile? interestProfile,
            string baseDescription)
        {
            // Enhance description based on agent interests
            if (interestProfile != null && interestProfile.EngagingInteractions.Count > 0)
            {
                var preferredInteraction = interestProfile.EngagingInteractions.First();
                return $"{baseDescription} You notice several {preferredInteraction}s nearby that might interest you.";
            }

            // Add hints if agent struggles
            if (behaviorAnalysis.StrugglePatterns.Count > 0)
            {
                var primaryStruggle = behaviorAnalysis.StrugglePatterns.OrderByDescending(s => s.FailureCount).First();
                return $"{baseDescription} {GetContextualHint(primaryStruggle)}";
            }

            return baseDescription;
        }

        private static List<NarrativeTokenRequest> GenerateAdaptiveTokens(
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds)
        {
            var tokens = new List<NarrativeTokenRequest>();

            foreach (var need in contentNeeds.OrderByDescending(n => n.Priority).Take(3))
            {
                var token = new NarrativeTokenRequest
                {
                    TokenId = Guid.NewGuid().ToString("N"),
                    TokenType = $"support_{need.NeedType}"
                };
                token.Parameters["priority"] = need.Priority.ToString("F2");
                token.Parameters["description"] = need.Description;
                tokens.Add(token);
            }

            return tokens;
        }

        private static List<NarrativePointOfInterest> GenerateAdaptivePOIs(
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds)
        {
            var pois = new List<NarrativePointOfInterest>();

            foreach (var need in contentNeeds.Where(n => n.Priority >= 0.6))
            {
                var poi = new NarrativePointOfInterest
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = $"Support_{need.NeedType}",
                    PreferredTerrain = InferTerrainFromNeed(need),
                    Importance = need.Priority >= 0.8 ? WorldPoiImportance.Required : WorldPoiImportance.Preferred
                };
                pois.Add(poi);
            }

            return pois;
        }

        private static Dictionary<int, int> AdaptDifficultyByDepth(
            Dictionary<int, int> baseDifficulty,
            BehaviorAnalysis behaviorAnalysis,
            List<ContentNeed> contentNeeds)
        {
            var adapted = new Dictionary<int, int>(baseDifficulty);

            // Adjust difficulty based on agent struggles
            if (behaviorAnalysis.StrugglePatterns.Count > 0)
            {
                // Reduce difficulty at all depths
                var adjustment = -10; // Reduce by 10 points
                foreach (var depth in adapted.Keys.ToList())
                {
                    adapted[depth] = Math.Max(0, adapted[depth] + adjustment);
                }
            }

            // Add difficulty curve if not present
            if (adapted.Count == 0)
            {
                adapted[0] = 20; // Start easy
                adapted[1] = 40;
                adapted[2] = 60;
            }

            return adapted;
        }

        private static string InferTerrainFromStruggle(StrugglePattern struggle)
        {
            return struggle.ContextType switch
            {
                "navigation_failure" => "open",
                "combat_failure" => "dungeon",
                "puzzle_failure" => "indoor",
                _ => "any"
            };
        }

        private static string InferTerrainFromSuccess(SuccessPattern success)
        {
            return success.ContextType switch
            {
                var ct when ct.Contains("move") => "outdoor",
                var ct when ct.Contains("pickup") => "dungeon",
                var ct when ct.Contains("interact") => "indoor",
                _ => "any"
            };
        }

        private static string InferTerrainFromNeed(ContentNeed need)
        {
            return need.NeedType switch
            {
                "navigation_assistance" => "outdoor",
                "combat_assistance" => "dungeon",
                "puzzle_assistance" => "indoor",
                _ => "any"
            };
        }

        private static string GetContextualHint(StrugglePattern struggle)
        {
            return struggle.ContextType switch
            {
                "navigation_failure" => "Look for markers or landmarks to guide your way.",
                "key_lock_failure" => "Keys are often found near the doors they unlock.",
                "combat_failure" => "Consider using tools or items to help in combat.",
                "puzzle_failure" => "Try different interactions to solve puzzles.",
                _ => "Take your time and explore carefully."
            };
        }
    }
}

